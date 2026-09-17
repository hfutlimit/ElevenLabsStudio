using System.Windows.Threading;
using Caliburn.Micro;
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

    public ShellViewModel(
        AgentListViewModel agents,
        SettingsViewModel settings,
        IWindowManager windows)
    {
        _agents = agents;
        _settings = settings;
        _windows = windows;

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

    public async Task OpenSettingsAsync()
    {
        // WindowManager.ShowDialogAsync resolves the View via the same
        // assembly registration we wired in Build(), so SettingsView
        // (.xaml under Views/Settings/) is matched automatically.
        await _windows.ShowDialogAsync(_settings);
    }
}