// Copyright (c) 2026 shiva9ro
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;
using Windows.Win32.System.SystemServices;

namespace Files.App.Utils.Shell;

// DirTrail's UI-STA OLE source, using Files' AOT-safe COM interfaces.
internal static unsafe partial class OleFileSender
{
	private static int running;
	internal static bool IsRunning => Volatile.Read(ref running) != 0;

	internal static uint Send(string[] paths, Action? feedback = null, Action? finishing = null, Func<uint, bool>? effectChanged = null)
	{
		if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
			return 0;
		try
		{
			PInvoke.OleInitialize(null).ThrowOnFailure();
			try
			{
				var data = new FileData(paths);
				var source = new Source(feedback, finishing, effectChanged);
				using var input = new OleDragInputBridge();
				var result = PInvoke.DoDragDrop(data, source, (DROPEFFECT)3, out var effect);
				GC.KeepAlive(data);
				GC.KeepAlive(source);
				result.ThrowOnFailure();
				return result.Value == 0x40100 ? data.PerformedEffect ?? (uint)effect : 0;
			}
			finally { PInvoke.OleUninitialize(); }
		}
		finally { Volatile.Write(ref running, 0); }
	}

	[GeneratedComClass]
	private sealed partial class Source(Action? feedback, Action? finishing, Func<uint, bool>? effectChanged) : IDropSource
	{
		private bool finished;
		private uint? lastEffect;
		public HRESULT QueryContinueDrag(BOOL escape, MODIFIERKEYS_FLAGS keys)
		{
			int result = escape || (PInvoke.GetAsyncKeyState(0x1B) & 0x8000) != 0 ? 0x40101 : ((uint)keys & 1) == 0 ? 0x40100 : 0;
			if (result != 0 && !finished)
			{
				finished = true;
				try { finishing?.Invoke(); }
				catch (Exception ex) { App.Logger.LogWarning(ex, "Could not hide drag visual."); }
			}
			if (result == 0 && !finished)
			{
				try { if (lastEffect is uint effect) effectChanged?.Invoke(effect); feedback?.Invoke(); }
				catch (Exception ex) { App.Logger.LogWarning(ex, "Could not update drag feedback."); }
			}
			return new(result);
		}
		public HRESULT GiveFeedback(DROPEFFECT effect)
		{
			try
			{
				lastEffect = (uint)effect;
				if (!finished)
				{
					bool handled = effectChanged?.Invoke((uint)effect) == true;
					feedback?.Invoke();
					if (handled) return new(0);
				}
			}
			catch (Exception ex) { App.Logger.LogWarning(ex, "Could not update drag visual."); }
			return new(0x40102);
		}
	}

	[GeneratedComClass]
	private sealed partial class FileData : IDataObject
	{
		private readonly byte[] payload;
		private readonly ushort performedFormat = (ushort)PInvoke.RegisterClipboardFormat("Performed DropEffect");
		private readonly ushort logicalFormat = (ushort)PInvoke.RegisterClipboardFormat("Logical Performed DropEffect");
		private uint? performed, logical;
		internal uint? PerformedEffect => logical ?? performed;
		internal FileData(string[] paths) => payload = FileDropPayload.Create(paths);
		private static FORMATETC Format => new() { cfFormat = 15, dwAspect = 1, lindex = -1, tymed = (uint)TYMED.TYMED_HGLOBAL };

		public HRESULT QueryGetData(FORMATETC* format) => new(format is not null && format->cfFormat == 15 &&
			format->dwAspect == 1 && format->lindex == -1 && (format->tymed & (uint)TYMED.TYMED_HGLOBAL) != 0
			? 0 : unchecked((int)0x80040064));

		public HRESULT GetData(FORMATETC* format, out STGMEDIUM medium)
		{
			medium = default;
			var result = QueryGetData(format);
			if (result.Failed) return result;
			var memory = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT, (nuint)payload.Length);
			if (memory.IsNull) return new(unchecked((int)0x8007000E));
			void* address = PInvoke.GlobalLock(memory);
			if (address is null)
			{
				PInvoke.GlobalFree(memory);
				return new(unchecked((int)0x8007000E));
			}
			try { payload.AsSpan().CopyTo(new Span<byte>(address, payload.Length)); }
			finally { PInvoke.GlobalUnlock(memory); }
			medium = new() { tymed = TYMED.TYMED_HGLOBAL, u = new() { hGlobal = memory } };
			return HRESULT.S_OK;
		}

		public HRESULT EnumFormatEtc(uint direction, out IEnumFORMATETC enumerator)
		{
			enumerator = null!;
			if (direction != 1) return new(unchecked((int)0x80004001));
			FORMATETC format = Format;
			return PInvoke.SHCreateStdEnumFmtEtc(1, &format, out enumerator);
		}

		// Shell owns Move; retain its result without deleting the source a second time.
		public HRESULT SetData(FORMATETC* format, in STGMEDIUM medium, BOOL release)
		{
			try
			{
				if (format is null || (format->cfFormat != performedFormat && format->cfFormat != logicalFormat) ||
					medium.tymed != TYMED.TYMED_HGLOBAL || medium.u.hGlobal.IsNull || PInvoke.GlobalSize(medium.u.hGlobal) < 4)
					return HRESULT.S_OK;
				uint* address = (uint*)PInvoke.GlobalLock(medium.u.hGlobal);
				if (address is null) return HRESULT.S_OK;
				uint value;
				try { value = *address; }
				finally { PInvoke.GlobalUnlock(medium.u.hGlobal); }
				if ((value & ~7u) == 0)
				{
					if (format->cfFormat == logicalFormat) logical = value;
					else performed = value;
				}
				return HRESULT.S_OK;
			}
			finally
			{
				if (release)
				{
					var owned = medium;
					PInvoke.ReleaseStgMedium(ref owned);
				}
			}
		}

		public HRESULT GetDataHere(FORMATETC* format, ref STGMEDIUM medium) => new(unchecked((int)0x80040069));
		public HRESULT GetCanonicalFormatEtc(FORMATETC* input, FORMATETC* output)
		{
			if (input is null || output is null) return new(unchecked((int)0x80004003));
			*output = *input;
			output->ptd = null;
			return new(0x40130);
		}
		public HRESULT DAdvise(FORMATETC* format, uint flags, IAdviseSink sink, out uint connection)
		{
			connection = 0;
			return new(unchecked((int)0x80040003));
		}
		public HRESULT DUnadvise(uint connection) => new(unchecked((int)0x80040003));
		public HRESULT EnumDAdvise(out IEnumSTATDATA enumerator)
		{
			enumerator = null!;
			return new(unchecked((int)0x80040003));
		}
	}
}
