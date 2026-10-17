// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Views.Layouts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT;

namespace Files.App;

public sealed partial class MainWindow
{
	private OleFileReceiver? nativeDropReceiver;
	private bool nativeDropRunning;
	private bool nativeDropClosing;

	internal bool IsNativeDropRequested => Ioc.Default.GetRequiredService<IWindowContext>().IsRunningAsAdmin;
	internal bool IsNativeDropEnabled => nativeDropReceiver is not null;

	[DynamicWindowsRuntimeCast(typeof(UIElement))]
	internal void InitializeNativeDrop()
	{
		if (!IsNativeDropRequested || nativeDropClosing || nativeDropRunning || Content is not UIElement root)
			return;

		var disabled = new List<UIElement>();
		try
		{
			DisableWinUiDrop(root, disabled);
			if (nativeDropReceiver is null)
			{
				nativeDropReceiver = new OleFileReceiver(new HWND(WindowHandle), NativeDropEffect, NativeDrop);
				Closed += NativeDrop_Closed;
			}
			else
			{
				nativeDropReceiver.Refresh();
			}
		}
		catch (Exception ex)
		{
			nativeDropReceiver?.Dispose();
			nativeDropReceiver = null;
			Closed -= NativeDrop_Closed;
			foreach (var element in disabled)
				element.AllowDrop = true;
			App.Logger.LogError(ex, "Could not initialize native OLE drag and drop.");
		}
	}

	private static void DisableWinUiDrop(DependencyObject parent, List<UIElement> disabled)
	{
		if (parent is UIElement { AllowDrop: true } element)
		{
			disabled.Add(element);
			element.AllowDrop = false;
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			DisableWinUiDrop(VisualTreeHelper.GetChild(parent, i), disabled);
	}

	[DynamicWindowsRuntimeCast(typeof(FrameworkElement))]
	private (BaseLayoutPage? Page, string? Destination) LocateNativeDrop(POINTL screen)
	{
		if (nativeDropRunning || nativeDropClosing || Content is not FrameworkElement root ||
			!root.IsHitTestVisible || root.XamlRoot is null)
			return default;

		System.Drawing.Point client = new(screen.x, screen.y);
		if (!PInvoke.ScreenToClient(new HWND(WindowHandle), ref client))
			return default;
		double scale = root.XamlRoot.RasterizationScale;
		var point = new Point(client.X / scale, client.Y / scale);
		foreach (var hit in VisualTreeHelper.FindElementsInHostCoordinates(point, root))
		{
			for (DependencyObject? element = hit; element is not null; element = VisualTreeHelper.GetParent(element))
			{
				if (element is ContentDialog)
					return default;
				if (element is BaseLayoutPage page)
				{
					string? destination = page.NativeDropDestination(point);
					if (destination is not null)
						return (page, destination);
					break;
				}
			}
		}
		return default;
	}

	private uint NativeDropEffect(POINTL point, string[] paths, uint keys, uint allowed)
	{
		var target = LocateNativeDrop(point);
		if (target.Page is null || target.Destination is null || !target.Page.CanReceiveNativeDrop(paths, target.Destination))
			return 0;
		return DropOperationPolicy.Choose(paths, target.Destination, allowed,
			(keys & 8) != 0, (keys & 4) != 0, (PInvoke.GetKeyState(0x12) & 0x8000) != 0);
	}

	private bool NativeDrop(POINTL point, string[] paths, uint keys, uint effect)
	{
		var target = LocateNativeDrop(point);
		if (target.Page is null || target.Destination is null || !target.Page.CanReceiveNativeDrop(paths, target.Destination))
			return false;
		nativeDropRunning = true;
		try
		{
			return target.Page.ReceiveNativeDrop(paths, target.Destination, effect);
		}
		finally
		{
			nativeDropRunning = false;
			if (nativeDropClosing)
			{
				nativeDropReceiver?.Dispose();
				nativeDropReceiver = null;
			}
		}
	}

	private void NativeDrop_Closed(object sender, WindowEventArgs e)
	{
		nativeDropClosing = true;
		Closed -= NativeDrop_Closed;
		if (!nativeDropRunning)
		{
			nativeDropReceiver?.Dispose();
			nativeDropReceiver = null;
		}
	}
}
