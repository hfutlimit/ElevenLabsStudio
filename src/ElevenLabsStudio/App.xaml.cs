using System.Windows;

namespace ElevenLabsStudio;

/// <summary>
/// WPF entry point. The real wiring lives in <see cref="Bootstrapper"/>
/// which configures IoC + configuration + logging. <c>App.xaml.cs</c>
/// only owns the WPF lifecycle: <c>OnStartup</c> hands control to the
/// Bootstrapper, <c>OnExit</c> releases the host.
/// </summary>
public partial class App : Application
{
    private Bootstrapper? _bootstrapper;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _bootstrapper = new Bootstrapper();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrapper = null;
        base.OnExit(e);
    }
}