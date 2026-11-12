// Copyright (c) 2026 shiva9ro
// SPDX-License-Identifier: MIT

using Files.App.Views.Layouts;
using Files.App.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT;

namespace Files.App;

public sealed partial class MainWindow
{
	private DispatcherTimer? localDragTimer;
	private BaseLayoutPage? localDragSource;
	private string[] localDragPaths = [];
	private bool localSameParent;
	private NativeDragVisual? localDragVisual;

	internal void BeginLocalDrag(BaseLayoutPage source, string[] paths, ListedItem item)
	{
		if (localDragSource is not null || nativeDropRunning || nativeDropClosing || OleFileSender.IsRunning || paths.Length == 0)
			return;
		localDragSource = source;
		localDragPaths = (string[])paths.Clone();
		source.SetNativeSourceDragging(true);
		var label = paths.Length == 1 ? item.Name ?? System.IO.Path.GetFileName(paths[0]) : string.Format("GroupItemsCount_Plural".GetLocalizedResource(), paths.Length);
		localDragVisual = new NativeDragVisual(new HWND(WindowHandle), label, item.IsFolder, item.PreloadedIconData);
		localDragTimer ??= CreateLocalDragTimer();
		localDragTimer.Start();
	}

	private DispatcherTimer CreateLocalDragTimer()
	{
		var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
		timer.Tick += LocalDragTick;
		return timer;
	}

	[DynamicWindowsRuntimeCast(typeof(UIElement))]
	private void LocalDragTick(object? sender, object e)
	{
		if (localDragSource is null) return;
		try
		{
			if (Down(0x1B) || nativeDropClosing || !localDragSource.IsLoaded || Content is not UIElement { IsHitTestVisible: true })
			{
				EndLocalDrag();
				return;
			}
			if (!PInvoke.GetCursorPos(out var cursor)) { EndLocalDrag(); return; }
			localDragVisual?.Move();
			POINTL point = new() { x = cursor.X, y = cursor.Y };
			uint keys = (Down(1) ? 1u : 0) | (Down(0x11) ? 8u : 0) | (Down(0x10) ? 4u : 0);
			uint effect = NativeDropEffect(point, localDragPaths, keys, 3);
			UpdateLocalDragEffect(effect);
			localDragSource.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));
			if ((keys & 1) == 0)
			{
				localDragTimer?.Stop();
				HideLocalDragVisual();
				try { if (effect != 0) NativeDrop(point, localDragPaths, keys, effect); }
				finally { EndLocalDrag(); }
				return;
			}
			if (PInvoke.GetWindowRect(new HWND(WindowHandle), out var rect) &&
				(cursor.X < rect.left || cursor.X >= rect.right || cursor.Y < rect.top || cursor.Y >= rect.bottom))
			{
				localDragTimer?.Stop();
				localDragSource.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));
				var source = localDragSource;
				try
				{
					if (OleFileSender.Send(localDragPaths, () => localDragVisual?.Move(), HideLocalDragVisual, UpdateLocalDragEffect) == 2)
						_ = source.ParentShellPageInstance?.RefreshIfNoWatcherExistsAsync();
				}
				finally { EndLocalDrag(); }
			}
		}
		catch (Exception ex)
		{
			EndLocalDrag();
			App.Logger.LogError(ex, "Native file drag failed.");
		}
	}

	private bool UpdateLocalDragEffect(uint effect)
	{
		bool neutral = (effect & 7) == 0 && localSameParent && PInvoke.GetCursorPos(out var point) &&
			PInvoke.GetAncestor(PInvoke.WindowFromPoint(point), Windows.Win32.UI.WindowsAndMessaging.GET_ANCESTOR_FLAGS.GA_ROOT) == new HWND(WindowHandle);
		localDragVisual?.SetForbidden((effect & 7) == 0 && !neutral);
		if (!neutral) return false;
		unsafe { PInvoke.SetCursor(PInvoke.LoadCursor(default, new PCWSTR((char*)32512))); }
		return true;
	}

	private void HideLocalDragVisual()
	{
		localDragVisual?.Dispose();
		localDragVisual = null;
	}

	private void EndLocalDrag()
	{
		localDragTimer?.Stop();
		HideLocalDragVisual();
		var source = localDragSource;
		localDragSource = null;
		localDragPaths = [];
		localSameParent = false;
		if (source is not null)
		{
			source.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));
			source.SetNativeSourceDragging(false);
		}
	}

	private static bool Down(int key) => (PInvoke.GetAsyncKeyState(key) & 0x8000) != 0;
}
