using System;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using ElevenLabsStudio.ViewModels;

namespace ElevenLabsStudio;

/// <summary>
/// WPF entry point. Holds the single-instance mutex, builds the
/// composition root, instantiates the ShellViewModel, and asks CM to
/// bind it to the ShellView.
/// </summary>
public partial class App : Application
{
    // Per-user (Local\) mutex name; "Global\" would clash with RDP
    // sessions, so stick to Local\ which still rejects a second start
    // in the same logon session.
    private const string SingleInstanceMutexName = "Local\\ElevenLabsStudio.SingleInstance.v1";
    private Mutex? _singleInstanceMutex;

    private Bootstrapper? _bootstrapper;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "ElevenLabs Studio 已经在运行。\n\n请检查任务栏 / 系统托盘后再次启动。",
                "Already running",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            // Release the would-be mutex (it was never ours) and bail.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _bootstrapper = new Bootstrapper();
        _bootstrapper.Build();

        var shellVm = _bootstrapper.Resolve<ShellViewModel>();

        var shellView = new ShellView();
        ViewModelBinder.Bind(shellVm, shellView, null);

        // DPI-aware sizing: cap the window to 80% of the working area of
        // the monitor where the cursor is, so on a 1080p secondary
        // screen the window never opens off-screen.
        ClampToDpi(shellView);

        MainWindow = shellView;
        shellView.Show();
    }

    private static void ClampToDpi(Window window)
    {
        // Pick the monitor the cursor is currently on via P/Invoke and
        // use its working area (physical pixels, excluding the taskbar)
        // as the ceiling for the design-time Width/Height so the window
        // never opens off-screen on a smaller secondary monitor.
        var (workX, workY, workW, workH) = GetCursorMonitorWorkArea();

        double dpiX = 1.0, dpiY = 1.0;
        var source = System.Windows.PresentationSource.FromVisual(window);
        if (source?.CompositionTarget is { } ct)
        {
            var m = ct.TransformToDevice;
            dpiX = m.M11;
            dpiY = m.M22;
        }

        var maxW = (int)(workW * 0.8);
        var maxH = (int)(workH * 0.8);
        var widthDip = Math.Min(window.Width, maxW / dpiX);
        var heightDip = Math.Min(window.Height, maxH / dpiY);
        window.Width = widthDip;
        window.Height = heightDip;

        // Centre within the working area.
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = (workX + (workW - widthDip * dpiX) / 2.0) / dpiX;
        window.Top = (workY + (workH - heightDip * dpiY) / 2.0) / dpiY;
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    private static (int X, int Y, int W, int H) GetCursorMonitorWorkArea()
    {
        if (!GetCursorPos(out var pt))
        {
            return (0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        }
        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hMon == IntPtr.Zero)
        {
            return (0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        }
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMon, ref info))
        {
            return (0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        }
        var rc = info.rcWork;
        return (rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrapper?.Dispose();
        _bootstrapper = null;
        if (_singleInstanceMutex is not null)
        {
            // Release the mutex we acquired on start; SafeWaitHandle
            // disposal is enough because we used initiallyOwned=true.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
        base.OnExit(e);
    }
}