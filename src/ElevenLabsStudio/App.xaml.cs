using System.Windows;
using Caliburn.Micro;
using ElevenLabsStudio.ViewModels;

namespace ElevenLabsStudio;

/// <summary>
/// WPF entry point. Builds the composition root, instantiates the
/// ShellViewModel, and asks CM to bind it to the ShellView.
/// </summary>
public partial class App : Application
{
    private Bootstrapper? _bootstrapper;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _bootstrapper = new Bootstrapper();
        _bootstrapper.Build();

        var shellVm = _bootstrapper.Resolve<ShellViewModel>();

        var shellView = new ShellView();
        ViewModelBinder.Bind(shellVm, shellView, null);
        MainWindow = shellView;
        shellView.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrapper?.Dispose();
        _bootstrapper = null;
        base.OnExit(e);
    }
}