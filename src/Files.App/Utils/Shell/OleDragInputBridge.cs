// Copyright (c) 2026 shiva9ro
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Files.App.Utils.Shell;

// DirTrail's worker timer forwards physical mouse state only to our OLE capture window.
internal sealed unsafe class OleDragInputBridge : IDisposable
{
	private readonly uint uiThread = PInvoke.GetCurrentThreadId();
	private readonly uint process = (uint)Environment.ProcessId;
	private readonly Timer timer;
	private int working, disposed;

	internal OleDragInputBridge() => timer = new Timer(Tick, null, 0, 16);

	private void Tick(object? state)
	{
		if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref working, 1) != 0) return;
		try
		{
			if (Volatile.Read(ref disposed) != 0) return;
			GUITHREADINFO info = new() { cbSize = (uint)sizeof(GUITHREADINFO) };
			HWND capture = PInvoke.GetGUIThreadInfo(uiThread, ref info) ? info.hwndCapture : default;
			if (capture.IsNull) capture = FindThreadWindow();
			if (capture.IsNull || !IsOleWindow(capture)) return;
			if (!PInvoke.GetCursorPos(out var point) || !PInvoke.ScreenToClient(capture, ref point)) return;
			uint keys = (Down(1) ? 1u : 0) | (Down(2) ? 2u : 0) | (Down(4) ? 16u : 0) |
				(Down(0x10) ? 4u : 0) | (Down(0x11) ? 8u : 0);
			if (Volatile.Read(ref disposed) == 0)
				PInvoke.PostMessage(capture, 0x0200, new WPARAM(keys),
					new LPARAM(unchecked((int)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16)))));
		}
		catch (Exception ex) { App.Logger.LogWarning(ex, "OLE input bridge failed."); }
		finally { Volatile.Write(ref working, 0); }
	}

	private bool IsOleWindow(HWND window)
	{
		uint owner = 0;
		uint thread = PInvoke.GetWindowThreadProcessId(window, &owner);
		char* name = stackalloc char[128];
		int length = PInvoke.GetClassName(window, name, 128);
		return thread == uiThread && owner == process && new ReadOnlySpan<char>(name, length).SequenceEqual("CLIPBRDWNDCLASS");
	}

	private HWND FindThreadWindow()
	{
		for (HWND candidate = PInvoke.FindWindowEx(new HWND((nint)(-3)), default, "CLIPBRDWNDCLASS", null);
			!candidate.IsNull; candidate = PInvoke.FindWindowEx(new HWND((nint)(-3)), candidate, "CLIPBRDWNDCLASS", null))
			if (IsOleWindow(candidate)) return candidate;
		var context = GCHandle.Alloc(new WindowSearch(this));
		try
		{
			PInvoke.EnumThreadWindows(uiThread, &FindWindow, new LPARAM(GCHandle.ToIntPtr(context)));
			return ((WindowSearch)context.Target!).Result;
		}
		finally { context.Free(); }
	}

	private sealed class WindowSearch(OleDragInputBridge bridge)
	{
		internal readonly OleDragInputBridge Bridge = bridge;
		internal HWND Result;
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static BOOL FindWindow(HWND window, LPARAM parameter)
	{
		try
		{
			var search = (WindowSearch)GCHandle.FromIntPtr(parameter.Value).Target!;
			if (!search.Bridge.IsOleWindow(window)) return true;
			search.Result = window;
			return false;
		}
		catch { return false; }
	}

	private static bool Down(int key) => (PInvoke.GetAsyncKeyState(key) & 0x8000) != 0;
	public void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, 1) == 0) timer.Dispose();
	}
}
