using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Files.App.Utils.Shell;

internal static class DropOperationPolicy
{
	// Values are the shared OLE/WinRT Copy=1, Move=2, Link=4 flags.
	internal static uint Choose(IReadOnlyList<string> sources, string destination, uint allowed, bool control, bool shift, bool alt)
	{
		if (sources.Count == 0 || string.IsNullOrEmpty(destination)) return 0;
		// Shortcut creation is not implemented; never silently copy/move on Alt.
		if (alt || (control && shift)) return 0;
		uint desired = control ? 1u : shift ? 2u :
			sources.All(path => SameRoot(path, destination)) ? 2u : 1u;
		if ((allowed & desired) != 0) return desired;
		if (control || shift) return 0;
		return (allowed & 1u) != 0 ? 1u : (allowed & 2u) != 0 ? 2u : 0;
	}
	private static bool SameRoot(string source, string destination)
	{
		try
		{
			string? root = Path.GetPathRoot(source);
			return !string.IsNullOrEmpty(root) && string.Equals(root, Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase);
		}
		catch (ArgumentException) { return false; }
	}
}
