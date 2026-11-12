// Copyright (c) 2026 shiva9ro
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Windows.Win32.System.Com;

namespace Windows.Win32;

public static partial class OwnedMemoryStream
{
	// CsWin32's default return marshaller does not guarantee UniqueInstance.
	// This private stream must release deterministically after GDI+ finishes decoding.
	[LibraryImport("SHLWAPI.dll", EntryPoint = "SHCreateMemStream")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	[SupportedOSPlatform("windows5.0")]
	[return: MarshalUsing(typeof(UniqueComInterfaceMarshaller<IStream>))]
	public static unsafe partial IStream? Create(byte* bytes, uint length);
}
