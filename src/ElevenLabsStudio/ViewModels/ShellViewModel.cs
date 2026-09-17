using System.Windows.Threading;
using Caliburn.Micro;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.ViewModels.Agents;

namespace ElevenLabsStudio.ViewModels;

/// <summary>
/// Top-level shell. Wires the left agent menu and the right detail
/// pane. Owns a 1-second clock so the status bar always shows a live
/// timestamp. The Conductor pattern from Caliburn is intentionally
/// bypassed — there is exactly one detail slot, bound to the
/// currently-selected <see cref="AgentDetailViewModel"/>.
/// </summary>
public sealed class ShellViewModel : Screen
{
    private readonly AgentListViewModel _agents;
    private readonly DispatcherTimer _clockTimer;

    public AgentListViewModel AgentsVm => _agents;

    private AgentDetailViewModel? _agentDetail;
    public AgentDetailViewModel? AgentDetail
    {
        get => _agentDetail;
        set => Set(ref _agentDetail, value);
    }

    private DateTimeOffset _currentTime;
    public DateTimeOffset CurrentTime
    {
        get => _currentTime;
        private set => Set(ref _currentTime, value);
    }

    public bool IsBusy => _agents.IsBusy
        || (_agentDetail?.IsBusy ?? false);

    public string BusyMessage =>
        _agentDetail?.BusyMessage
        ?? _agents.BusyMessage
        ?? string.Empty;

    public ShellViewModel(AgentListViewModel agents)
    {
        _agents = agents;

        // Forward selection changes to the right pane.
        _agents.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentListViewModel.AgentDetail))
            {
                AgentDetail = _agents.AgentDetail;
                NotifyOfPropertyChange(nameof(IsBusy));
                NotifyOfPropertyChange(nameof(BusyMessage));
            }
        };

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTimeOffset.Now;
        _clockTimer.Start();
        CurrentTime = DateTimeOffset.Now;
    }

    /// <summary>
/// CM5 5.0.x seals / hides <c>OnDeactivate</c> + <c>Deactivate</c> from
/// subclasses, so we can't override the lifecycle hook directly. The
/// DispatcherTimer holds a weak-style reference and will be GC'd when
/// the ShellViewModel is collected; for explicit teardown the user can
/// close the window which tears the WPF tree down anyway.
/// </summary>

    public void OpenSettings()
    {
        // Settings tab is intentionally out of scope for v0.1; hook is in
        // place so the top-bar button is wired and behaviour stays
        // explicit. Future iterations open a SettingsView dialog here.
    }

    public void OpenHelp()
    {
        // Same — hook only.
    }
}