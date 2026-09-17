using System.ComponentModel;
using System.Windows.Data;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Events;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using ElevenLabsStudio.ViewModels.AgentDetail;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Agents;

/// <summary>
/// Left-hand agent menu. Holds the list of pulled agents, the filter
/// text, and the currently selected <see cref="AgentDetailViewModel"/>
/// that the right pane renders. Agents are pulled by ID one at a time
/// via <see cref="PullAgentByIdAsync"/>; the user can refresh an
/// already-pulled agent from its detail view.
/// </summary>
public sealed class AgentListViewModel : ScreenBase, IHandle<AgentUpdatedEvent>
{
    private readonly IElevenLabsClient _client;
    private readonly IDialogService _dialog;
    private readonly IEventAggregator _events;
    private readonly ILogger<AgentListViewModel> _logger;
    private readonly ISuggestionEngine _suggestions;
    private readonly IWindowManager _windowManager;

    public BindableCollection<Agent> Agents { get; } = new();

    /// <summary>Filtered / sorted view of <see cref="Agents"/> for the ListBox.</summary>
    public ICollectionView AgentsView { get; }

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (Set(ref _filterText, value))
            {
                AgentsView.Refresh();
                NotifyOfPropertyChange(nameof(HasNoAgents));
            }
        }
    }

    private Agent? _selectedAgent;
    public Agent? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (Set(ref _selectedAgent, value))
            {
                AgentDetail = _selectedAgent is null
                    ? null
                    : new AgentDetailViewModel(
                        _selectedAgent,
                        _client,
                        _suggestions,
                        _dialog,
                        _events,
                        _logger);
            }
        }
    }

    public AgentDetailViewModel? AgentDetail { get; private set; }

    public bool HasNoAgents => Agents.Count == 0;

    public AgentListViewModel(
        IElevenLabsClient client,
        IDialogService dialog,
        IEventAggregator events,
        ILogger<AgentListViewModel> logger,
        ISuggestionEngine suggestions,
        IWindowManager windowManager)
    {
        _client = client;
        _dialog = dialog;
        _events = events;
        _logger = logger;
        _suggestions = suggestions;
        _windowManager = windowManager;

        AgentsView = CollectionViewSource.GetDefaultView(Agents);

        // Fire LoadAsync from ctor instead of OnViewLoaded. CM5's
        // ContentControl + cal:View.Model does not reliably trigger
        // OnViewLoaded on the VM when the ContentControl is nested
        // inside another ContentControl (ShellView), so the load
        // would silently never happen. Kicking off here means the list
        // is populated as soon as the AgentListViewModel exists in the
        // DI graph.
        _ = LoadAsync();
        AgentsView.Filter = FilterAgent;
        Agents.CollectionChanged += (_, _) =>
            NotifyOfPropertyChange(nameof(HasNoAgents));
    }

    private bool FilterAgent(object obj)
    {
        if (obj is not Agent a) return false;
        if (string.IsNullOrWhiteSpace(_filterText)) return true;
        var f = _filterText.Trim();
        return a.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
            || a.AgentId.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Open the pull-by-id dialog. On success, append the agent to the
    /// local list and select it so the right pane renders its detail.
    /// </summary>
    public async Task PullAgentByIdAsync()
    {
        var dialogVm = new PullAgentDialogViewModel(_client, _dialog, _logger);
        var ok = await _windowManager.ShowDialogAsync(dialogVm);
        if (ok == true && dialogVm.Result is { } pulled)
        {
            // Replace if already present (re-pull).
            var idx = Agents.IndexOf(Agents.FirstOrDefault(a => a.AgentId == pulled.AgentId)!);
            if (idx >= 0)
            {
                Agents[idx] = pulled;
            }
            else
            {
                Agents.Add(pulled);
            }
            SelectedAgent = pulled;
            await _events.PublishOnBackgroundThreadAsync(
                new AgentListRefreshedEvent(Agents.ToList()));
        }
    }

    public Task HandleAsync(AgentUpdatedEvent message, CancellationToken ct)
    {
        var existing = Agents.FirstOrDefault(a => a.AgentId == message.AgentId);
        if (existing is not null)
        {
            var idx = Agents.IndexOf(existing);
            if (idx >= 0)
            {
                Agents[idx] = message.Snapshot;
                if (_selectedAgent?.AgentId == message.AgentId)
                {
                    SelectedAgent = message.Snapshot;
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pulls the agent list from the (mock or real) client and populates
    /// the bound collection. Already wired up to run from the ctor so
    /// the UI never sits empty.
    /// </summary>
    public async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "正在加载 Agent…";
        try
        {
            var items = await _client.ListAgentsAsync();
            Agents.Clear();
            Agents.AddRange(items);

            await _events.PublishOnBackgroundThreadAsync(
                new AgentListRefreshedEvent(items));
        }
        catch (ElevenLabsAuthException ex)
        {
            _logger.LogError(ex, "ElevenLabs auth failed while loading agents");
            await _dialog.ShowErrorAsync(
                "鉴权失败",
                "API Key 无效或缺失。请在 appsettings.json 的 ElevenLabs.ApiKey 配置后重启。");
        }
        catch (ElevenLabsException ex)
        {
            _logger.LogError(ex, "ElevenLabs error while loading agents (status={Status})", ex.HttpStatus);
            await _dialog.ShowErrorAsync("加载失败", $"无法加载 Agent（HTTP {ex.HttpStatus}）。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading agents");
            await _dialog.ShowErrorAsync("未知错误", ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyOfPropertyChange(nameof(BusyMessage));
        }
    }
}