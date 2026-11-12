// Copyright (c) 2026 shiva9ro
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Graphics.GdiPlus;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Files.App.Utils.Shell;

// An image only: never captures input, activates a window, or changes the drop operation.
internal sealed unsafe class NativeDragVisual : IDisposable
{
	private HWND window;
	private bool forbidden;
	private readonly string label;
	private readonly bool folder;
	private readonly byte[]? image;
	private uint dpi;
	private int width, height;
	private COLORREF background, foreground, outline;

	internal NativeDragVisual(HWND owner, string label, bool folder, byte[]? image)
	{
		this.label = label;
		this.folder = folder;
		this.image = image;
		try
		{
			background = ThemeColor("SolidBackgroundFillColorBaseBrush");
			foreground = ThemeColor("TextFillColorPrimaryBrush");
			outline = ThemeColor("AccentFillColorDefaultBrush");
			window = PInvoke.CreateWindowEx((WINDOW_EX_STYLE)0x080800A0, "STATIC", "",
				(WINDOW_STYLE)0x8000000E, 0, 0, 1, 1, owner, null, null, null);
			if (window.IsNull)
				throw new Win32Exception();
			dpi = PInvoke.GetDpiForWindow(owner);
			Render();
			Move();
		}
		catch (Exception ex) { Dispose(); App.Logger.LogWarning(ex, "Drag visual unavailable; retaining standard cursor feedback."); }
	}

	internal void SetForbidden(bool value)
	{
		if (window.IsNull || forbidden == value) return;
		forbidden = value;
		try { Render(); Move(); }
		catch (Exception ex) { Dispose(); App.Logger.LogWarning(ex, "Drag feedback rendering failed."); }
	}

	internal void Move()
	{
		if (window.IsNull) return;
		try
		{
			if (!PInvoke.GetCursorPos(out var point)) throw new Win32Exception();
			var monitor = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
			if (PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint currentDpi, out _).Succeeded && currentDpi != dpi)
			{ dpi = currentDpi; Render(); }
			int offset = Math.Max(12, (int)(16 * dpi / 96));
			int x = point.X + offset, y = point.Y + offset;
			var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
			if (PInvoke.GetMonitorInfo(monitor, ref info))
			{
				var work = info.rcWork;
				if (x + width > work.right) x = point.X - offset - width;
				if (y + height > work.bottom) y = point.Y - offset - height;
				x = Math.Clamp(x, work.left, Math.Max(work.left, work.right - width));
				y = Math.Clamp(y, work.top, Math.Max(work.top, work.bottom - height));
			}
			if (!PInvoke.SetWindowPos(window, new HWND(-1), x, y, width, height, (SET_WINDOW_POS_FLAGS)0x50))
				throw new Win32Exception();
		}
		catch (Exception ex) { Dispose(); App.Logger.LogWarning(ex, "Drag visual tracking stopped."); }
	}

	private void Render()
	{
		int Px(int value) => Math.Max(1, (int)Math.Round(value * Math.Max(96, dpi) / 96.0));
		width = Px(forbidden ? 32 : 240); height = Px(forbidden ? 32 : 40);
		var screen = PInvoke.GetDC(default(HWND));
		HDC dc = default;
		HBITMAP next = default;
		HGDIOBJ previous = default, oldFont = default;
		HBRUSH brush = default, border = default;
		HFONT font = default;
		try
		{
			dc = PInvoke.CreateCompatibleDC(screen);
			int scale = forbidden ? 4 : 1;
			int renderWidth = width * scale, renderHeight = height * scale;
			BITMAPINFO info = new() { bmiHeader = new() { biSize = (uint)sizeof(BITMAPINFOHEADER),
				biWidth = renderWidth, biHeight = -renderHeight, biPlanes = 1, biBitCount = 32 } };
			void* bits = null;
			next = PInvoke.CreateDIBSection(screen, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, default, 0);
			if (dc.IsNull || next.IsNull) throw new Win32Exception();
			previous = PInvoke.SelectObject(dc, next);
			if (forbidden)
			{
				var handle = PInvoke.LoadImage(default, new PCWSTR((char*)32648), GDI_IMAGE_TYPE.IMAGE_CURSOR,
					renderWidth, renderHeight, IMAGE_FLAGS.LR_SHARED);
				if (handle.IsNull) throw new Win32Exception(Marshal.GetLastWin32Error(), "Load prohibition cursor");
				HICON cursor = new(handle.Value);
				var black = new int[renderWidth * renderHeight];
				var white = new int[black.Length];
				Marshal.Copy(black, 0, (nint)bits, black.Length);
				if (!PInvoke.DrawIconEx(dc, 0, 0, cursor, renderWidth, renderHeight, 0, default, DI_FLAGS.DI_NORMAL)) throw new Win32Exception();
				PInvoke.GdiFlush();
				Marshal.Copy((nint)bits, black, 0, black.Length);
				Array.Fill(white, 0x00FFFFFF);
				Marshal.Copy(white, 0, (nint)bits, white.Length);
				if (!PInvoke.DrawIconEx(dc, 0, 0, cursor, renderWidth, renderHeight, 0, default, DI_FLAGS.DI_NORMAL)) throw new Win32Exception();
				PInvoke.GdiFlush();
				Marshal.Copy((nint)bits, white, 0, white.Length);
				for (int i = 0; i < black.Length; i++) black[i] = RestoreCursorAlpha(black[i], white[i]);
				var reduced = DownsampleCursor(black, width, height, scale);
				for (int y = 0; y < height; y++) Marshal.Copy(reduced, y * width, (nint)bits + y * renderWidth * 4, width);
				Present(screen, dc, 255);
				return;
			}
			RECT rect = new() { right = width, bottom = height };
			brush = PInvoke.CreateSolidBrush(background);
			border = PInvoke.CreateSolidBrush(outline);
			PInvoke.FillRect(dc, &rect, brush);
			PInvoke.FrameRect(dc, &rect, border);
			PInvoke.SetBkMode(dc, BACKGROUND_MODE.TRANSPARENT);
			if (!DrawImage(dc, Px(10), Px(8), Px(24)))
			{
				font = CreateFont(Px(23), "Segoe Fluent Icons");
				oldFont = PInvoke.SelectObject(dc, font);
				PInvoke.SetTextColor(dc, foreground);
				RECT iconRect = new() { left = Px(10), top = Px(7), right = Px(36), bottom = Px(34) };
				DrawText(dc, folder ? "\uE8B7" : "\uE7C3", ref iconRect, (DRAW_TEXT_FORMAT)0x24);
				PInvoke.SelectObject(dc, oldFont); oldFont = default;
				PInvoke.DeleteObject(font); font = default;
			}
			font = CreateFont(Px(13), "Segoe UI");
			oldFont = PInvoke.SelectObject(dc, font);
			PInvoke.SetTextColor(dc, foreground);
			RECT textRect = new() { left = Px(44), right = width - Px(10), bottom = height };
			DrawText(dc, label, ref textRect, (DRAW_TEXT_FORMAT)0x8824);
			PInvoke.SelectObject(dc, oldFont); oldFont = default;
			PInvoke.GdiFlush();
			var pixels = new int[width * height];
			Marshal.Copy((nint)bits, pixels, 0, pixels.Length);
			for (int i = 0; i < pixels.Length; i++) pixels[i] |= unchecked((int)0xFF000000);
			Marshal.Copy(pixels, 0, (nint)bits, pixels.Length);
			Present(screen, dc, 240);
		}
		finally
		{
			if (!previous.IsNull) PInvoke.SelectObject(dc, previous);
			if (!oldFont.IsNull) PInvoke.SelectObject(dc, oldFont);
			if (!font.IsNull) PInvoke.DeleteObject(font);
			if (!brush.IsNull) PInvoke.DeleteObject(brush);
			if (!border.IsNull) PInvoke.DeleteObject(border);
			if (!next.IsNull) PInvoke.DeleteObject(next);
			if (!dc.IsNull) PInvoke.DeleteDC(dc);
			if (!screen.IsNull) PInvoke.ReleaseDC(default(HWND), screen);
		}
	}

	private static COLORREF ThemeColor(string key)
	{
		var color = ((SolidColorBrush)Application.Current.Resources[key]).Color;
		return new COLORREF((uint)(color.R | color.G << 8 | color.B << 16));
	}

	private static void DrawText(HDC dc, string text, ref RECT rect, DRAW_TEXT_FORMAT format)
	{
		fixed (char* characters = text)
			PInvoke.DrawText(dc, characters, text.Length, ref rect, format);
	}

	private static HFONT CreateFont(int height, string face)
	{
		fixed (char* characters = face)
			return PInvoke.CreateFont(-height, 0, 0, 0, 400, 0, 0, 0, FONT_CHARSET.DEFAULT_CHARSET,
				FONT_OUTPUT_PRECISION.OUT_DEFAULT_PRECIS, FONT_CLIP_PRECISION.CLIP_DEFAULT_PRECIS,
				FONT_QUALITY.CLEARTYPE_QUALITY, 0, characters);
	}

	private bool DrawImage(HDC dc, int x, int y, int size)
	{
		if (image is not { Length: > 0 and <= 4194304 }) return false;
		var startup = new GdiplusStartupInput { GdiplusVersion = 1 };
		nuint token = 0;
		GpImage* decoded = null;
		GpGraphics* graphics = null;
		try
		{
			if (PInvoke.GdiplusStartup(&token, &startup, null) != Status.Ok) return false;
			// Decode the already loaded icon bytes, never request the file or Shell again.
			fixed (byte* bytes = image)
			{
				var nativeStream = OwnedMemoryStream.Create(bytes, (uint)image.Length);
				if (nativeStream is null) return false;
				try
				{
					uint w = 0, h = 0;
					if (PInvoke.GdipLoadImageFromStream(nativeStream, &decoded) != Status.Ok ||
						PInvoke.GdipCreateFromHDC(dc, &graphics) != Status.Ok ||
						PInvoke.GdipGetImageWidth(decoded, &w) != Status.Ok ||
						PInvoke.GdipGetImageHeight(decoded, &h) != Status.Ok || w == 0 || h == 0) return false;
					double scale = (double)size / Math.Max(w, h);
					int iw = Math.Max(1, (int)(w * scale)), ih = Math.Max(1, (int)(h * scale));
					return PInvoke.GdipDrawImageRectI(graphics, decoded, x + (size - iw) / 2, y + (size - ih) / 2, iw, ih) == Status.Ok;
				}
				finally
				{
					if (decoded is not null) { PInvoke.GdipDisposeImage(decoded); decoded = null; }
					if ((object)nativeStream is ComObject comObject) comObject.FinalRelease();
				}
			}
		}
		finally
		{
			if (graphics is not null) PInvoke.GdipDeleteGraphics(graphics);
			if (token != 0) PInvoke.GdiplusShutdown(token);
		}
	}

    internal static int RestoreCursorAlpha(int black, int white)
    {
        int alpha = 255 - Math.Clamp((white & 255) - (black & 255), 0, 255);
        return (alpha << 24) | (black & 0x00FFFFFF);
    }

    internal static int[] DownsampleCursor(int[] source, int width, int height, int scale)
    {
        var result = new int[width * height];
        int stride = width * scale, count = scale * scale;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int a = 0, r = 0, g = 0, b = 0;
                for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                    {
                        uint pixel = unchecked((uint)source[(y * scale + sy) * stride + x * scale + sx]);
                        a += (int)(pixel >> 24); r += (int)((pixel >> 16) & 255);
                        g += (int)((pixel >> 8) & 255); b += (int)(pixel & 255);
                    }
                // Average premultiplied channels and coverage together to avoid colored fringes.
                result[y * width + x] = (((a + count / 2) / count) << 24) |
                    (((r + count / 2) / count) << 16) | (((g + count / 2) / count) << 8) | ((b + count / 2) / count);
            }
        return result;
    }

	private void Present(HDC screen, HDC dc, byte opacity)
	{
		SIZE size = new() { cx = width, cy = height };
		System.Drawing.Point origin = default;
		BLENDFUNCTION blend = new() { SourceConstantAlpha = opacity, AlphaFormat = 1 };
		if (!PInvoke.UpdateLayeredWindow(window, screen, null, size, dc, origin, default, blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA))
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Present drag surface");
	}

	public void Dispose()
	{
		if (!window.IsNull) { PInvoke.DestroyWindow(window); window = default; }
	}
}
