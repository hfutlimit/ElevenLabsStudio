using System.Windows;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Infrastructure;
using ElevenLabsStudio.Services;
using ElevenLabsStudio.ViewModels;
using ElevenLabsStudio.ViewModels.Agents;
using ElevenLabsStudio.ViewModels.Conversations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio;

/// <summary>
/// Caliburn.Micro 5 BootstrapperBase. Owns the composition root: every
/// type the UI asks for (VM, dialog, client) comes from
/// <see cref="Configure"/>. Keep this class thin — no business logic, no
/// "do X on startup" code, that lives in VMs.
/// </summary>
public sealed class Bootstrapper : BootstrapperBase
{
    private SimpleContainer? _container;

    public Bootstrapper()
    {
        Initialize();
    }

    /// <summary>
    /// Build the Microsoft.Extensions.DependencyInjection container with
    /// real services (Infrastructure, Core, etc.). We then lift the
    /// singleton instances the UI actually uses (IWindowManager,
    /// IEventAggregator, IDialogService, IElevenLabsClient) and register
    /// them inside the Caliburn.Micro SimpleContainer so the framework's
    /// view-model resolution continues to work.
    /// </summary>
    protected override void Configure()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "ELEVENLABS_")
            .Build();

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

        var sp = services.BuildServiceProvider();

        _container = new SimpleContainer();
        _container.Singleton<IWindowManager, WindowManager>();
        _container.Singleton<IEventAggregator, EventAggregator>();
        _container.RegisterInstance(typeof(IDialogService), null, sp.GetRequiredService<IDialogService>());
        _container.RegisterInstance(typeof(IElevenLabsClient), null, sp.GetRequiredService<IElevenLabsClient>());
        _container.RegisterInstance(typeof(ISuggestionEngine), null, sp.GetRequiredService<ISuggestionEngine>());

        _container.Singleton<ShellViewModel>();
        _container.Singleton<AgentListViewModel>();
        _container.PerRequest<UpdateAgentViewModel>();
        _container.Singleton<ConversationListViewModel>();
    }

    protected override object GetInstance(Type service, string key) =>
        _container is null
            ? throw new InvalidOperationException("Bootstrapper.Configure() was not called.")
            : _container.GetInstance(service, key);

    protected override IEnumerable<object> GetAllInstances(Type service) =>
        _container?.GetAllInstances(service) ?? Array.Empty<object>();

    protected override void BuildUp(object instance) => _container?.BuildUp(instance);
}