using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Files.App.Utils.Storage;

internal static class FileDropPayload
{
	private const int HeaderSize = 20;
	public static byte[] Create(IEnumerable<string> paths)
	{
		var list = paths.ToArray();
		if (list.Length == 0 || list.Any(path => string.IsNullOrWhiteSpace(path) || path.Contains('\0')))
			throw new ArgumentException("Invalid file-drop paths.", nameof(paths));
		byte[] names = Encoding.Unicode.GetBytes(string.Join('\0', list) + "\0\0");
		byte[] bytes = new byte[checked(HeaderSize + names.Length)];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes, HeaderSize);
		BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 1); // fWide
		names.CopyTo(bytes, HeaderSize);
		return bytes;
	}

	internal static string[] Parse(byte[] bytes)
	{
		if (bytes.Length < HeaderSize) throw new InvalidDataException("Missing DROPFILES header.");
		uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
		if (offset < HeaderSize || offset > bytes.Length - 4)
			throw new InvalidDataException("Invalid DROPFILES offset.");
		// Explorer supplies Unicode; reject unsupported ANSI data explicitly.
		if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)) == 0)
			throw new InvalidDataException("ANSI FileDrop is not supported.");
		var payload = bytes.AsSpan((int)offset);
		if (payload.Length % 2 != 0) throw new InvalidDataException("Invalid Unicode FileDrop.");
		string text = new System.Text.UnicodeEncoding(false, false, true).GetString(payload);
		int end = text.IndexOf("\0\0", StringComparison.Ordinal);
		if (end < 0) throw new InvalidDataException("Unterminated FileDrop list.");
		return text[..end].Split('\0', StringSplitOptions.RemoveEmptyEntries);
	}
}
