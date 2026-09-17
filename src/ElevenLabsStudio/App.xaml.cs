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

        // Explicitly subscribe so we always know when the inner detail
        // VM changes — the ContentControl + cal:View.Model binding
        // would otherwise do it lazily, but here we want the new view
        // in place immediately so the 4-tab agent detail renders without
        // a round-trip through CM's attached-property callback path.
        shellVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.AgentDetail))
            {
                if (shellVm.AgentDetail is null)
                {
                    shellView.DetailContent = null;
                }
                else
                {
                    var viewType = ViewLocator.LocateTypeForModelType(
                        shellVm.AgentDetail.GetType(), null, null);
                    if (viewType is null)
                    {
                        shellView.DetailContent = new System.Windows.Controls.TextBlock
                        {
                            Text = $"Cannot find view for {shellVm.AgentDetail.GetType().FullName}",
                        };
                        return;
                    }

                    var view = (System.Windows.FrameworkElement)System.Activator.CreateInstance(viewType)!;
                    ViewModelBinder.Bind(shellVm.AgentDetail, view, null);
                    shellView.DetailContent = view;
                }
            }
        };

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