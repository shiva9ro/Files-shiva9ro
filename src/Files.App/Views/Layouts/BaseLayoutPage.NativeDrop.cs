// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System.IO;
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

	internal bool CanReceiveNativeDrop(string[] paths, string destination)
	{
		if (paths.Length == 0)
			return false;
		var items = paths.Select(path => StorageHelpers.FromPathAndType(path,
			Win32Helper.HasFileAttribute(path, FileAttributes.Directory) ? FilesystemItemType.Directory : FilesystemItemType.File)).ToList();
		return !items.AreItemsAlreadyInFolder(destination) && !items.ContainsDestinationOrAncestor(destination);
	}

	internal bool ReceiveNativeDrop(string[] paths, string destination, uint effect)
	{
		if (paths.Length == 0 || effect is not (1 or 2) || ParentShellPageInstance is not { } shellPage)
			return false;
		try
		{
			var operation = PerformNativeDropAsync(shellPage, paths, destination, effect);
			// Match ModernFileManager: return the completed effect to OLE, pumping this STA while waiting.
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
