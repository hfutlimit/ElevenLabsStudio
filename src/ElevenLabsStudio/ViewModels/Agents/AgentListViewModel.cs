using System.Windows;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Agents;

/// <summary>
/// Lists every agent the connected API key can see. Selecting one
/// instantiates a transient <see cref="UpdateAgentViewModel"/> which the
/// right-hand panel hosts via CM view-model-first binding.
/// </summary>
public sealed class AgentListViewModel : ScreenBase, IHandle<Core.Events.AgentUpdatedEvent>
{
    private readonly IElevenLabsClient _client;
    private readonly IDialogService _dialog;
    private readonly IEventAggregator _events;
    private readonly ILogger<AgentListViewModel> _logger;
    private readonly ISuggestionEngine _suggestions;

    public BindableCollection<Agent> Agents { get; } = new();

    private Agent? _selectedAgent;
    public Agent? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (Set(ref _selectedAgent, value))
            {
                UpdateAgentVm = _selectedAgent is null
                    ? null
                    : new UpdateAgentViewModel(
                        _selectedAgent,
                        _client,
                        _suggestions,
                        _dialog,
                        _events,
                        _logger);
            }
        }
    }

    public UpdateAgentViewModel? UpdateAgentVm { get; private set; }

    public AgentListViewModel(
        IElevenLabsClient client,
        IDialogService dialog,
        IEventAggregator events,
        ILogger<AgentListViewModel> logger,
        ISuggestionEngine suggestions)
    {
        _client = client;
        _dialog = dialog;
        _events = events;
        _logger = logger;
        _suggestions = suggestions;
    }

    protected override void OnViewLoaded(object view)
    {
        _events.SubscribeOnPublishedThread(this);
        base.OnViewLoaded(view);
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        BusyMessage = "正在加载 Agent…";
        try
        {
            var items = await _client.ListAgentsAsync();
            Agents.Clear();
            Agents.AddRange(items);

            await _events.PublishOnBackgroundThreadAsync(new Core.Events.AgentListRefreshedEvent(items));
        }
        catch (ElevenLabsAuthException ex)
        {
            _logger.LogError(ex, "ElevenLabs auth failed while loading agents");
            await _dialog.ShowErrorAsync("鉴权失败", "API Key 无效或缺失。请在 appsettings.json 的 ElevenLabs.ApiKey 配置后重启。");
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

    public Task HandleAsync(Core.Events.AgentUpdatedEvent message, CancellationToken ct)
    {
        var existing = Agents.FirstOrDefault(a => a.AgentId == message.AgentId);
        if (existing is not null)
        {
            var idx = Agents.IndexOf(existing);
            if (idx >= 0)
            {
                Agents[idx] = message.Snapshot;
            }
        }
        return Task.CompletedTask;
    }
}