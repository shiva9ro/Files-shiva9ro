// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT;
using FileAttributes = System.IO.FileAttributes;

namespace Files.App.Views.Layouts;

public abstract partial class BaseLayoutPage
{
	private bool CanUseNativeDrop => InstanceViewModel is { IsPageTypeSearchResults: false,
		IsPageTypeRecycleBin: false, IsPageTypeZipFolder: false, IsPageTypeFtp: false, IsPageTypeMtpDevice: false };

	private void NativeDrop_Loaded(object sender, RoutedEventArgs e)
	{
		MainWindow.Instance.InitializeNativeDrop();
		NotifyPropertyChanged(nameof(AllowItemDrag));
		if (MainWindow.Instance.IsNativeDropEnabled && this is not ColumnsLayoutPage && !nativeDragHandlersAttached)
		{
			ItemsControl.AddHandler(UIElement.PointerPressedEvent, nativeDragPressed ??= NativeDrag_PointerPressed, true);
			ItemsControl.AddHandler(UIElement.PointerMovedEvent, nativeDragMoved ??= NativeDrag_PointerMoved, true);
			ItemsControl.AddHandler(UIElement.PointerReleasedEvent, nativeDragReleased ??= NativeDrag_PointerReleased, true);
			Unloaded += NativeDrag_Unloaded;
			nativeDragHandlersAttached = true;
		}
	}

	private bool nativeDragHandlersAttached;
	private bool nativeSourceDragging;
	private bool nativeDragTapSuppressed;
	protected bool SuppressNativeDragTap => nativeDragTapSuppressed;
	private PointerEventHandler? nativeDragPressed, nativeDragMoved, nativeDragReleased;
	private Point? nativeDragOrigin;
	private SelectorItem? nativeDragItem;

	[DynamicWindowsRuntimeCast(typeof(SelectorItem))]
	private void NativeDrag_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		nativeDragTapSuppressed = false;
		nativeDragOrigin = null;
		nativeDragItem = null;
		if (!CanUseNativeDrop || IsRenamingItem || e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse ||
			!e.GetCurrentPoint(ItemsControl).Properties.IsLeftButtonPressed)
			return;
		for (var element = e.OriginalSource as DependencyObject; element is not null && element != ItemsControl; element = VisualTreeHelper.GetParent(element))
		{
			if (element is SelectorItem { Content: ListedItem } item)
			{
				nativeDragOrigin = e.GetCurrentPoint(ItemsControl).Position;
				nativeDragItem = item;
				return;
			}
			if (element is ScrollBar or TextBox or ButtonBase) return;
		}
	}

	private void NativeDrag_PointerMoved(object sender, PointerRoutedEventArgs e)
	{
		if (nativeDragOrigin is not { } origin || nativeDragItem is not { Content: ListedItem item } container || itemDragging)
			return;
		if ((PInvoke.GetAsyncKeyState(1) & 0x8000) == 0 || !CanUseNativeDrop || IsRenamingItem)
		{
			nativeDragOrigin = null;
			nativeDragItem = null;
			return;
		}
		var point = e.GetCurrentPoint(ItemsControl).Position;
		if (Math.Abs(point.X - origin.X) < 5 && Math.Abs(point.Y - origin.Y) < 5) return;
		nativeDragOrigin = null;
		nativeDragItem = null;
		if (ParentShellPageInstance?.ShellViewModel is not { IsLoadingItems: false }) return;
		try
		{
			var items = container.IsSelected && SelectedItems.Contains(item) ? SelectedItems : [item];
			var paths = items.Select(entry => entry.GetRequiredPath()).ToArray();
			if (paths.Length == 0 || items.Any(entry => entry.IsRecycleBinItem) ||
				paths.Any(path => !Path.IsPathFullyQualified(path) || ZipStorageFolder.IsZipPath(path, includeRoot: false))) return;
			container.ReleasePointerCaptures();
			ItemsControl.ReleasePointerCaptures();
			e.Handled = true;
			MainWindow.Instance.BeginLocalDrag(this, paths, item);
		}
		catch (Exception ex) { App.Logger.LogWarning(ex, "Could not start native file drag."); }
	}

	private void NativeDrag_PointerReleased(object sender, PointerRoutedEventArgs e)
	{
		nativeDragOrigin = null;
		nativeDragItem = null;
	}

	private void NativeDrag_Unloaded(object sender, RoutedEventArgs e)
	{
		ItemsControl.RemoveHandler(UIElement.PointerPressedEvent, nativeDragPressed!);
		ItemsControl.RemoveHandler(UIElement.PointerMovedEvent, nativeDragMoved!);
		ItemsControl.RemoveHandler(UIElement.PointerReleasedEvent, nativeDragReleased!);
		Unloaded -= NativeDrag_Unloaded;
		nativeDragHandlersAttached = false;
		nativeDragOrigin = null;
		nativeDragItem = null;
	}

	internal void SetNativeSourceDragging(bool value)
	{
		if (value) nativeDragTapSuppressed = true;
		nativeSourceDragging = value;
		itemDragging = value;
		MainWindow.Instance.SetCanWindowToFront(!value);
	}

	protected void FileList_DragStarting(UIElement sender, DragStartingEventArgs e)
	{
		if (MainWindow.Instance.IsNativeDropEnabled)
			e.AllowedOperations = DataPackageOperation.Copy | DataPackageOperation.Move;
	}

	[DynamicWindowsRuntimeCast(typeof(SelectorItem))]
	internal string? NativeDropDestination(Point rootPoint)
	{
		if (!CanUseNativeDrop || !IsLoaded || !IsEnabled || !IsHitTestVisible || XamlRoot is null ||
			ParentShellPageInstance?.ShellViewModel is not { IsLoadingItems: false } shell ||
			shell.WorkingDirectory is not string destination || !Path.IsPathFullyQualified(destination))
			return null;

		var origin = ItemsControl.TransformToVisual(XamlRoot.Content).TransformPoint(new Point());
		if (rootPoint.X < origin.X || rootPoint.Y < origin.Y || rootPoint.X >= origin.X + ItemsControl.ActualWidth ||
			rootPoint.Y >= origin.Y + ItemsControl.ActualHeight)
			return null;

		foreach (var hit in VisualTreeHelper.FindElementsInHostCoordinates(rootPoint, ItemsControl))
		{
			for (DependencyObject? element = hit; element is not null && element != ItemsControl; element = VisualTreeHelper.GetParent(element))
			{
				if (element is SelectorItem { Content: ListedItem item } && item.IsFolder && !item.IsRecycleBinItem)
				{
					string path = item.GetRequiredPath();
					return Path.IsPathFullyQualified(path) && !ZipStorageFolder.IsZipPath(path) ? path : null;
				}
			}
		}
		return destination;
	}

	internal bool CanReceiveNativeDrop(string[] paths, string destination, uint effect)
	{
		if (paths.Length == 0)
			return false;
		var sources = paths.Select(Path.TrimEndingDirectorySeparator).ToArray();
		return (effect != 2 || !sources.AreItemsAlreadyInFolder(destination)) && !sources.ContainsDestinationOrAncestor(destination);
	}

	internal bool ReceiveNativeDrop(string[] paths, string destination, uint effect)
	{
		if (paths.Length == 0 || effect is not (1 or 2) || ParentShellPageInstance is not { } shellPage)
			return false;
		try
		{
			var operation = PerformNativeDropAsync(shellPage, paths, destination, effect);
			// Match DirTrail: return the completed effect to OLE, pumping this STA while waiting.
			using var wait = ((IAsyncResult)operation).AsyncWaitHandle;
			unsafe
			{
				HANDLE handle = new(wait.SafeWaitHandle.DangerousGetHandle());
				while (!operation.IsCompleted)
				{
					uint index;
					var result = PInvoke.CoWaitForMultipleHandles(0x18, 100, 1, &handle, &index);
					if (result.Failed && result.Value != unchecked((int)0x80010115))
						result.ThrowOnFailure();
				}
			}
			return operation.GetAwaiter().GetResult() == ReturnResult.Success;
		}
		catch (Exception ex)
		{
			App.Logger.LogError(ex, "Native OLE file operation failed.");
			return false;
		}
	}

	private static async Task<ReturnResult> PerformNativeDropAsync(IShellPage shellPage, string[] paths, string destination, uint effect)
	{
		var items = paths.Select(path => StorageHelpers.FromPathAndType(path,
			Win32Helper.HasFileAttribute(path, FileAttributes.Directory) ? FilesystemItemType.Directory : FilesystemItemType.File)).ToList();
		var destinations = items.Select(item => PathNormalization.Combine(destination, item.Name)).ToList();
		try
		{
			return effect == 2
				? await shellPage.FilesystemHelpers.MoveItemsAsync(items, destinations, false, true)
				: await shellPage.FilesystemHelpers.CopyItemsAsync(items, destinations, false, true);
		}
		finally
		{
			await shellPage.RefreshIfNoWatcherExistsAsync();
		}
	}
}
