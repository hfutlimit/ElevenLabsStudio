using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace ElevenLabsStudio;

/// <summary>
/// Composition root. Builds the MS DI service provider, registers the
/// CM5 WindowManager / EventAggregator as singletons, and exposes the
/// root <see cref="ViewModels.ShellViewModel"/> so the host App can
/// bind it to a Window.
/// <para>
/// We deliberately do NOT inherit CM5's <c>BootstrapperBase</c>: 5.0.x
/// sealed / hid the lifecycle hooks that 4.x exposed, and using a
/// minimal manual approach keeps the surface area tiny and obvious.
/// </para>
/// </summary>
public sealed class Bootstrapper : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException(
            "Bootstrapper.Build() was not called.");

    public void Build()
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

        services.AddSingleton<IDialogService, Services.MaterialDialogService>();

        // CM5 framework singletons (only the ones App touches).
        services.AddSingleton<Caliburn.Micro.IWindowManager, Caliburn.Micro.WindowManager>();
        services.AddSingleton<Caliburn.Micro.IEventAggregator, Caliburn.Micro.EventAggregator>();

        services.AddSingleton<ViewModels.ShellViewModel>();
        services.AddSingleton<ViewModels.Agents.AgentListViewModel>();

        // Per-request: a fresh detail VM per selection so the four tab
        // VMs are recreated when the user switches agents.
        services.AddTransient<ViewModels.AgentDetail.AgentDetailViewModel>();
        services.AddTransient<ViewModels.AgentDetail.SystemPromptTabViewModel>();
        services.AddTransient<ViewModels.AgentDetail.FirstMessageTabViewModel>();
        services.AddTransient<ViewModels.AgentDetail.WorkflowTabViewModel>();
        services.AddTransient<ViewModels.AgentDetail.ConversationsTabViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Caliburn.Micro 5's AssemblySource.Instance is empty by default.
        // ViewLocator.FindTypeByNames iterates only over the assemblies we
        // register here, so without this line every "cal:View.Model=..."
        // binding falls through to the "Cannot find view for {0}" fallback.
        // We add every assembly that contains View or ViewModel types —
        // Core (domain + abstractions), Infrastructure (HttpClient lives
        // there too) and the UI exe itself.
        Caliburn.Micro.AssemblySource.Instance.AddRange(new[]
        {
            typeof(Core.Domain.Agent).Assembly,
            typeof(Infrastructure.Http.ElevenLabsHttpClient).Assembly,
            typeof(ViewModels.ShellViewModel).Assembly,
        });

        // The stock ViewLocator.GetOrCreateViewType probes IoC.GetAllInstances
        // first; IoC is uninitialised in our setup (we use MS DI directly) so
        // that probe throws InvalidOperationException and breaks view
        // resolution. Replace it with an Activator-only fallback that never
        // touches IoC.
        Caliburn.Micro.ViewLocator.GetOrCreateViewType = viewType =>
        {
            if (viewType is null)
            {
                return new System.Windows.Controls.TextBlock { Text = "Null view type." };
            }

            if (viewType.IsInterface || viewType.IsAbstract
                || !typeof(System.Windows.UIElement).IsAssignableFrom(viewType))
            {
                return new System.Windows.Controls.TextBlock
                {
                    Text = string.Format("Cannot create {0}.", viewType.FullName),
                };
            }

            var view = (System.Windows.UIElement)System.Activator.CreateInstance(viewType)!;
            Caliburn.Micro.ViewLocator.InitializeComponent(view);
            return view;
        };
    }

    public T Resolve<T>() where T : notnull =>
        _serviceProvider!.GetRequiredService<T>();

    public void Dispose() => _serviceProvider?.Dispose();
}