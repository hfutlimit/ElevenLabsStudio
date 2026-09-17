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
        MainWindow = shellView;
        shellView.Show();
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