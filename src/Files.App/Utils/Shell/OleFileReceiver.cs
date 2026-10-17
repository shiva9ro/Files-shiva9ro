// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Ole;
using Windows.Win32.System.SystemServices;
using Windows.Win32.UI.Shell;

namespace Files.App.Utils.Shell;

// Ported from ModernFileManager.Services.OleFileReceiver, using Files' generated interop.
internal sealed unsafe partial class OleFileReceiver : IDisposable
{
	private readonly List<HWND> registered = [];
	private readonly Target target;
	private readonly HWND owner;
	private bool initialized;

	internal OleFileReceiver(HWND owner, Func<POINTL, string[], uint, uint, uint> hover,
		Func<POINTL, string[], uint, uint, bool> drop)
	{
		this.owner = owner;
		target = new Target(hover, drop);
		PInvoke.OleInitialize(null).ThrowOnFailure();
		initialized = true;
		try
		{
			Refresh();
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	internal void Refresh()
	{
		var handles = new List<HWND> { owner };
		var context = GCHandle.Alloc(handles);
		try
		{
			PInvoke.EnumChildWindows(owner, &CollectInputWindows, new LPARAM(GCHandle.ToIntPtr(context)));
		}
		finally
		{
			context.Free();
		}

		foreach (var window in handles)
		{
			var result = PInvoke.RegisterDragDrop(window, target);
			if (result.Value == unchecked((int)0x80040101))
			{
				// Only take over the owner and its same-thread WinUI input hosts after disabling AllowDrop.
				PInvoke.RevokeDragDrop(window).ThrowOnFailure();
				result = PInvoke.RegisterDragDrop(window, target);
			}
			result.ThrowOnFailure();
			if (!registered.Contains(window))
				registered.Add(window);
			App.Logger.LogInformation("Native OLE registered HWND={Window}", (nint)window.Value);
		}
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static BOOL CollectInputWindows(HWND window, LPARAM parameter)
	{
		try
		{
			var handles = (List<HWND>)GCHandle.FromIntPtr(parameter.Value).Target!;
			uint ownerThread = PInvoke.GetWindowThreadProcessId(handles[0], out _);
			Span<char> name = stackalloc char[256];
			int length = PInvoke.GetClassName(window, name);
			string type = name[..length].ToString();
			if (PInvoke.GetWindowThreadProcessId(window, out _) == ownerThread &&
				(type.Contains("InputSite", StringComparison.Ordinal) || type.Contains("DesktopChildSiteBridge", StringComparison.Ordinal)))
				handles.Add(window);
		}
		catch (Exception ex)
		{
			App.Logger.LogWarning(ex, "Could not inspect OLE input window.");
		}
		return true;
	}

	public void Dispose()
	{
		foreach (var window in registered)
			PInvoke.RevokeDragDrop(window);
		registered.Clear();
		if (initialized)
		{
			PInvoke.OleUninitialize();
			initialized = false;
		}
	}

	[GeneratedComClass]
	private sealed partial class Target : IDropTarget
	{
		private readonly Func<POINTL, string[], uint, uint, uint> hover;
		private readonly Func<POINTL, string[], uint, uint, bool> drop;
		private string[] paths = [];
		private uint allowed;

		internal Target(Func<POINTL, string[], uint, uint, uint> hover, Func<POINTL, string[], uint, uint, bool> drop)
		{
			this.hover = hover;
			this.drop = drop;
		}

		public HRESULT DragEnter(IDataObject data, MODIFIERKEYS_FLAGS keys, POINTL point, DROPEFFECT* effect)
		{
			paths = [];
			allowed = (uint)*effect;
			*effect = 0;
			try
			{
				paths = ReadPaths(data);
				*effect = (DROPEFFECT)hover(point, paths, (uint)keys, allowed);
				App.Logger.LogInformation("Native OLE DragEnter count={Count}, allowed={Allowed}, accepted={Effect}", paths.Length, allowed, (uint)*effect);
			}
			catch (Exception ex)
			{
				App.Logger.LogWarning(ex, "Native OLE DragEnter failed.");
			}
			return HRESULT.S_OK;
		}

		public HRESULT DragOver(MODIFIERKEYS_FLAGS keys, POINTL point, DROPEFFECT* effect)
		{
			*effect = 0;
			try
			{
				*effect = (DROPEFFECT)hover(point, paths, (uint)keys, allowed);
			}
			catch (Exception ex)
			{
				App.Logger.LogWarning(ex, "Native OLE DragOver failed.");
			}
			return HRESULT.S_OK;
		}

		public HRESULT DragLeave()
		{
			paths = [];
			return HRESULT.S_OK;
		}

		public HRESULT Drop(IDataObject data, MODIFIERKEYS_FLAGS keys, POINTL point, DROPEFFECT* effect)
		{
			*effect = 0;
			try
			{
				var dropped = ReadPaths(data);
				uint chosen = hover(point, dropped, (uint)keys, allowed);
				if (chosen != 0 && drop(point, dropped, (uint)keys, chosen))
					*effect = (DROPEFFECT)chosen;
				App.Logger.LogInformation("Native OLE Drop count={Count}, completedEffect={Effect}", dropped.Length, (uint)*effect);
			}
			catch (Exception ex)
			{
				App.Logger.LogWarning(ex, "Native OLE Drop failed.");
			}
			finally
			{
				paths = [];
			}
			return HRESULT.S_OK;
		}
	}

	private static string[] ReadPaths(IDataObject data)
	{
		FORMATETC format = new()
		{
			cfFormat = 15, dwAspect = (uint)DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = (uint)TYMED.TYMED_HGLOBAL
		};
		if (data.QueryGetData(format).Failed)
			return [];
		data.GetData(format, out var medium).ThrowOnFailure();
		try
		{
			if (medium.tymed != TYMED.TYMED_HGLOBAL || medium.u.hGlobal.Value is null)
				return [];
			HDROP handle = new((nint)medium.u.hGlobal.Value);
			uint count = PInvoke.DragQueryFile(handle, uint.MaxValue, Span<char>.Empty);
			if (count > 100000)
				throw new InvalidDataException("Too many dropped paths.");
			var paths = new string[count];
			for (uint i = 0; i < count; i++)
			{
				uint length = PInvoke.DragQueryFile(handle, i, Span<char>.Empty);
				if (length == 0 || length > 32768)
					throw new InvalidDataException("Invalid dropped path.");
				var text = new char[length + 1];
				if (PInvoke.DragQueryFile(handle, i, text) != length)
					throw new InvalidDataException("Incomplete dropped path.");
				paths[i] = new string(text, 0, (int)length);
				if (!Path.IsPathFullyQualified(paths[i]))
					throw new InvalidDataException("Dropped path is not absolute.");
			}
			return paths;
		}
		finally
		{
			PInvoke.ReleaseStgMedium(ref medium);
		}
	}
}
