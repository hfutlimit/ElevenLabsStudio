using System;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using ElevenLabsStudio.Configuration;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Infrastructure;
using ElevenLabsStudio.ViewModels;
using ElevenLabsStudio.ViewModels.AgentDetail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio;

/// <summary>
/// WPF entry point. Holds the single-instance mutex, builds the MS DI
/// service provider, wires CM5 framework singletons (WindowManager,
/// EventAggregator), and asks CM to bind the ShellViewModel to a
/// Window.
///
/// <para>
/// CM5's lifecycle hooks (OnStartup, OnExit) are sealed in 5.0.x, so
/// we don't inherit <c>BootstrapperBase</c> — every step happens in
/// line below and is easy to follow.
/// </para>
/// </summary>
public partial class App : Application
{
	// Per-user (Local\) mutex name; "Global\" would clash with RDP
	// sessions, so stick to Local\ which still rejects a second start
	// in the same logon session.
	private const string SingleInstanceMutexName = "Local\\ElevenLabsStudio.SingleInstance.v1";

	private Mutex? _singleInstanceMutex;
	private ServiceProvider? _serviceProvider;

	protected override async void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		PlatformProvider.Current = new XamlPlatformProvider();

		_singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
		if (!createdNew)
		{
			MessageBox.Show(
				"ElevenLabs Studio is already running.\n\nCheck the taskbar or system tray before launching it again.",
				"Already running",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
			// Release the would-be mutex (it was never ours) and bail.
			_singleInstanceMutex.Dispose();
			_singleInstanceMutex = null;
			Shutdown();
			return;
		}

		_serviceProvider = BuildServiceProvider();
		IoC.GetInstance = (service, _) => _serviceProvider.GetRequiredService(service);
		IoC.GetAllInstances = _ => Array.Empty<object>();
		IoC.BuildUp = _ => { };
		var shellVm = _serviceProvider.GetRequiredService<ShellViewModel>();

		var shellView = new ShellView();
		ViewModelBinder.Bind(shellVm, shellView, null);

		// DPI-aware sizing: cap the window to 80% of the working area
		// of the monitor where the cursor is, so on a 1080p secondary
		// screen the window never opens off-screen.
		ClampToDpi(shellView);

		MainWindow = shellView;
		shellView.Show();
		await shellVm.InitializeAsync();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_serviceProvider?.Dispose();
		_serviceProvider = null;
		if (_singleInstanceMutex is not null)
		{
			// Release the mutex we acquired on start; SafeWaitHandle
			// disposal is enough because we used initiallyOwned=true.
			_singleInstanceMutex.Dispose();
			_singleInstanceMutex = null;
		}
		base.OnExit(e);
	}

	/// <summary>
	/// Build the MS DI graph. Split out so OnStartup stays readable
	/// and so the registration order is obvious: configuration,
	/// logging, infrastructure, CM5 singletons, view-models.
	/// </summary>
	private static ServiceProvider BuildServiceProvider()
	{
		var services = new ServiceCollection();

		var config = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
			.AddJsonFile("appsettings.elevenlabs.local.json", optional: true, reloadOnChange: true)
			.AddEnvironmentVariables()
			.AddElevenLabsApiKeyAlias(Environment.GetEnvironmentVariable)
			.Build();
		// Hold the IConfigurationRoot so SettingsViewModel can write
		// back to appsettings.json + call Reload() at runtime.
		services.AddSingleton<IConfiguration>(config);
		services.AddSingleton<IConfigurationRoot>(config);

		services.AddLogging(b =>
		{
			b.AddDebug();
			b.AddSimpleConsole(o =>
			{
				o.SingleLine = true;
				o.TimestampFormat = "HH:mm:ss ";
			});
			b.SetMinimumLevel(LogLevel.Information);
		});

		services.AddElevenLabsStudioInfrastructure(config);

		services.AddSingleton<IDialogService, Services.MaterialDialogService>();
		services.AddSingleton<Services.WebViewRealtimeConversationClient>();
		services.AddSingleton<IRealtimeConversationClient>(sp =>
			sp.GetRequiredService<Services.WebViewRealtimeConversationClient>());

		// CM5 framework singletons (only the ones App touches).
		services.AddSingleton<IWindowManager, WindowManager>();
		services.AddSingleton<IEventAggregator, EventAggregator>();

		// Boundary services that hide WPF / file-system details from
		// the VMs (review #6). The clock keeps DispatcherTimer out of
		// the VMs; the factory hides the four-tab detail construction
		// (and stays in the UI layer because it returns a UI type).
		services.AddSingleton<IClockService, Services.WpfClockService>();
		services.AddSingleton<IAgentDetailViewModelFactory,
			AgentDetailViewModelFactory>();

		services.AddSingleton<ShellViewModel>();
		services.AddSingleton<ViewModels.Agents.AgentListViewModel>();
		services.AddSingleton<SettingsViewModel>();

		var sp = services.BuildServiceProvider();

		// Caliburn.Micro 5's AssemblySource.Instance is empty by default.
		// ViewLocator.FindTypeByNames iterates only over the assemblies we
		// register here, so without this line every "cal:View.Model=..."
		// binding falls through to the "Cannot find view for {0}" fallback.
		// We add every assembly that contains View or ViewModel types —
		// Core (domain + abstractions), Infrastructure (HttpClient lives
		// there too) and the UI exe itself.
		AssemblySource.Instance.AddRange(new[]
		{
			typeof(Core.Domain.Agent).Assembly,
			typeof(Infrastructure.Http.ElevenLabsHttpClient).Assembly,
			typeof(ShellViewModel).Assembly,
		});
		return sp;
	}

	private static void ClampToDpi(Window window)
	{
		// Pick the monitor the cursor is currently on via P/Invoke and
		// use its working area (physical pixels, excluding the taskbar)
		// as the ceiling for the design-time Width/Height so the window
		// never opens off-screen on a smaller secondary monitor.
		//
		// Window.Left/Top are in DIPs but GetMonitorInfo returns physical
		// pixels, so we must read the monitor's effective DPI before
		// Show() — at that point PresentationSource.FromVisual still
		// returns null and the dpiX/dpiY fallback stays at 1.0, which
		// would mis-place the window on any non-100% scaled display.
		// Asking the OS directly via GetDpiForMonitor (shcore.dll) gets
		// the same DPI the runtime will use once the window is shown.
		var (workX, workY, workW, workH, dpiX, dpiY) = GetCursorMonitor();

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

	[System.Runtime.InteropServices.DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

	private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
	private const int MDT_EFFECTIVE_DPI = 0;

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
		{
			return fallback;
		}
		var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
		if (hMon == IntPtr.Zero)
		{
			return fallback;
		}

		var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
		if (!GetMonitorInfo(hMon, ref info))
		{
			return fallback;
		}
		var rc = info.rcWork;

		// 96 DPI = 1.0 scale factor. shcore returns 0..N where 96 is the
		// baseline; normalise so the maths downstream reads in the same
		// units as PresentationSource.TransformToDevice.M11/M22.
		var (dpiX, dpiY) = GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out var rawX, out var rawY) == 0
			? ((double)rawX / 96.0, (double)rawY / 96.0)
			: (1.0, 1.0);

		return (rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top, dpiX, dpiY);
	}
}
