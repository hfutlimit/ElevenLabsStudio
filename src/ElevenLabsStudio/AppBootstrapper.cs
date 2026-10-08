using System.Reflection;
using System.Threading;
using System.Windows;
using ElevenLabsStudio.Infrastructure;
using Caliburn.Micro;
using ElevenLabsStudio.Configuration;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Services;
using ElevenLabsStudio.ViewModels;
using ElevenLabsStudio.ViewModels.AgentDetail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio;

/// <summary>
/// Caliburn.Micro composition root. Builds the Microsoft DI container,
/// points <see cref="IoC"/> at it, and shows <see cref="ShellViewModel"/>.
/// </summary>
public sealed class AppBootstrapper : BootstrapperBase
{
	private const string SingleInstanceMutexName = "Local\\ElevenLabsStudio.SingleInstance.v1";

	private Mutex? _singleInstanceMutex;
	private ServiceProvider? _serviceProvider;

	public AppBootstrapper()
	{
		Initialize();
	}

	protected override void Configure()
	{
		PlatformProvider.Current = new XamlPlatformProvider();
		_serviceProvider = BuildServiceProvider();
	}

	protected override IEnumerable<Assembly> SelectAssemblies() =>
	[
		typeof(Core.Domain.Agent).Assembly,
		typeof(Infrastructure.Http.ElevenLabsHttpClient).Assembly,
		typeof(ShellViewModel).Assembly,
	];

	protected override object GetInstance(Type service, string key) =>
		_serviceProvider!.GetRequiredService(service);

	protected override IEnumerable<object> GetAllInstances(Type service) =>
		_serviceProvider!.GetServices(service).OfType<object>();

	protected override void BuildUp(object instance)
	{
	}

	protected override async void OnStartup(object sender, StartupEventArgs e)
	{
		try
		{
			if (!AcquireSingleInstance())
			{
				Application.Shutdown();
				return;
			}

			await DisplayRootViewForAsync<ShellViewModel>();
			if (Application.MainWindow?.DataContext is ShellViewModel shell)
				await shell.InitializeAsync();
		}
		catch (Exception ex)
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ElevenLabsStudio-startup.txt");
			System.IO.File.WriteAllText(path, ex.ToString());
			MessageBox.Show(ex.Message, "ElevenLabs Studio failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
			Application.Shutdown();
		}
	}

	protected override void OnExit(object sender, EventArgs e)
	{
		_serviceProvider?.Dispose();
		_serviceProvider = null;
		_singleInstanceMutex?.Dispose();
		_singleInstanceMutex = null;
		base.OnExit(sender, e);
	}

	private bool AcquireSingleInstance()
	{
		_singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
		if (createdNew)
			return true;

		_singleInstanceMutex.Dispose();
		_singleInstanceMutex = null;
		MessageBox.Show(
			"ElevenLabs Studio is already running.\n\nCheck the taskbar or system tray before launching it again.",
			"Already running",
			MessageBoxButton.OK,
			MessageBoxImage.Information);
		return false;
	}

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

		services.AddSingleton<IDialogService, MaterialDialogService>();
		services.AddSingleton<WebViewRealtimeConversationClient>();
		services.AddSingleton<IRealtimeConversationClient>(sp =>
			sp.GetRequiredService<WebViewRealtimeConversationClient>());

		services.AddSingleton<IWindowManager, WindowManager>();
		services.AddSingleton<IEventAggregator, EventAggregator>();
		services.AddSingleton<IClockService, WpfClockService>();
		services.AddSingleton<IDynamicVariableStore, JsonDynamicVariableStore>();
		services.AddSingleton<IAgentDetailViewModelFactory, AgentDetailViewModelFactory>();

		services.AddSingleton<ShellViewModel>();
		services.AddSingleton<ViewModels.Agents.AgentListViewModel>();
		services.AddSingleton<SettingsViewModel>();

		return services.BuildServiceProvider();
	}
}
