using System.Runtime.InteropServices;
using System.Windows;

namespace ElevenLabsStudio;

/// <summary>
/// Caps a window to the work area of the monitor under the cursor.
/// Called from the shell constructor so the size is set before the
/// bootstrapper shows the window.
/// </summary>
internal static class WindowWorkArea
{
	public static void FitToCursorMonitor(Window window)
	{
		var (workX, workY, workW, workH, dpiX, dpiY) = GetCursorMonitor();

		var maxW = (int)(workW * 0.8);
		var maxH = (int)(workH * 0.8);
		var widthDip = Math.Min(window.Width, maxW / dpiX);
		var heightDip = Math.Min(window.Height, maxH / dpiY);
		window.Width = widthDip;
		window.Height = heightDip;

		window.WindowStartupLocation = WindowStartupLocation.Manual;
		window.Left = (workX + (workW - widthDip * dpiX) / 2.0) / dpiX;
		window.Top = (workY + (workH - heightDip * dpiY) / 2.0) / dpiY;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT { public int X; public int Y; }

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT { public int Left, Top, Right, Bottom; }

	[StructLayout(LayoutKind.Sequential)]
	private struct MONITORINFO
	{
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

	private const uint MonitorDefaultToNearest = 0x00000002;
	private const int EffectiveDpi = 0;

	private static (int X, int Y, int W, int H, double DpiX, double DpiY) GetCursorMonitor()
	{
		var fallback = (
			X: 0,
			Y: 0,
			W: (int)SystemParameters.PrimaryScreenWidth,
			H: (int)SystemParameters.PrimaryScreenHeight,
			DpiX: 1.0,
			DpiY: 1.0);

		if (!GetCursorPos(out var pt))
			return fallback;
		var monitor = MonitorFromPoint(pt, MonitorDefaultToNearest);
		if (monitor == IntPtr.Zero)
			return fallback;

		var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
		if (!GetMonitorInfo(monitor, ref info))
			return fallback;

		var work = info.rcWork;
		var (dpiX, dpiY) = GetDpiForMonitor(monitor, EffectiveDpi, out var rawX, out var rawY) == 0
			? ((double)rawX / 96.0, (double)rawY / 96.0)
			: (1.0, 1.0);
		return (work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top, dpiX, dpiY);
	}
}
