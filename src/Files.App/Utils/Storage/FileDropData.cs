// Copyright (c) 2026 shiva9ro
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Files.App.Utils.Storage;

internal static class FileDropData
{
	public const string Format = "FileDrop";
	private const int HeaderSize = 20; // DROPFILES contains DWORD/LONG/BOOL, even on ARM64.
	private const int MaximumPayload = 16 * 1024 * 1024;

	public static IRandomAccessStream CreateStream(IEnumerable<string> paths)
	{
		return new MemoryStream(FileDropPayload.Create(paths), writable: false).AsRandomAccessStream();
	}

	public static async Task<IReadOnlyList<string>> ReadAsync(DataPackageView view)
	{
		if (view.Contains(Format) && await view.GetDataAsync(Format) is IRandomAccessStream stream)
		{
			return await ReadStreamAsync(stream);
		}
		if (view.Contains(StandardDataFormats.StorageItems))
			return (await view.GetStorageItemsAsync()).Select(item => item.Path)
				.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
		return Array.Empty<string>();
	}

	internal static async Task<IReadOnlyList<string>> ReadStreamAsync(IRandomAccessStream stream)
	{
		// In-process DataPackage transfers retain the MemoryStream adapter. It cannot
		// clone, so GetInputStreamAt fails even though the same payload works via Explorer.
		// Do not dispose this borrowed stream; its DataPackage owns its lifetime.
		ulong position = stream.Position;
		using var reader = new DataReader(stream);
		try
		{
			ulong length = stream.Size;
			if (length < HeaderSize || length > MaximumPayload)
				throw new InvalidDataException("Invalid FileDrop size.");
			stream.Seek(0);
			byte[] bytes = new byte[checked((int)length)];
			if (await reader.LoadAsync((uint)length) != length)
				throw new InvalidDataException("Truncated FileDrop data.");
			reader.ReadBytes(bytes);
			return FileDropPayload.Parse(bytes);
		}
		finally
		{
			reader.DetachStream();
			stream.Seek(position);
		}
	}

}
