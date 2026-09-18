using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.ViewModels.Agents;

namespace ElevenLabsStudio.ViewModels;

/// <summary>
/// Top-level shell. Wires the left agent menu and the right detail
/// pane. Owns a 1-second clock so the status bar always shows a live
/// timestamp. Hosts the Settings dialog action.
/// </summary>
public sealed class ShellViewModel : Screen
{
    private readonly AgentListViewModel _agents;
    private readonly SettingsViewModel _settings;
    private readonly IWindowManager _windows;
    private readonly IClockService _clock;
    private IDisposable? _clockSubscription;

    public AgentListViewModel AgentsVm => _agents;

    private AgentDetailViewModel? _agentDetail;
    public AgentDetailViewModel? AgentDetail
    {
        get => _agentDetail;
        set
        {
            _agentDetail = value;
            // Forward the inner AgentListViewModel.AgentDetail into
            // our own property so the right-pane ContentControl
            // (bound via XAML) re-renders through its DataContextChanged
            // event.
            NotifyOfPropertyChange(nameof(AgentDetail));
        }
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

    public ShellViewModel(
        AgentListViewModel agents,
        SettingsViewModel settings,
        IWindowManager windows,
        IClockService clock)
    {
        _agents = agents;
        _settings = settings;
        _windows = windows;
        _clock = clock;

        // Forward AgentListViewModel.AgentDetail to our own AgentDetail
        // so the right-pane XAML binding (DataContext="{Binding
        // AgentDetail}") gets a fresh value and ShellView's
        // DataContextChanged handler rebuilds the view.
        _agents.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgentListViewModel.AgentDetail))
            {
                AgentDetail = _agents.AgentDetail;
            }
        };

        // 1Hz tick via the IClockService boundary so the unit tests
        // can substitute a deterministic clock.
        _clockSubscription = _clock.Start(TimeSpan.FromSeconds(1),
            () => CurrentTime = _clock.Now);
        CurrentTime = _clock.Now;
    }

    public async Task OpenSettingsAsync()
    {
        // WindowManager.ShowDialogAsync resolves the View via the same
        // assembly registration we wired in Build(), so SettingsView
        // (.xaml under Views/Settings/) is matched automatically.
        await _windows.ShowDialogAsync(_settings);
    }
}
