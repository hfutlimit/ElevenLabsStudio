using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Events;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Composite view-model for the right pane. Owns the four tab VMs and
/// acts as the single push / refresh target so the user only needs one
/// "📤 Push" button at the top, not one per tab.
/// </summary>
public sealed class AgentDetailViewModel : ScreenBase, IHandle<AgentUpdatedEvent>
{
    private readonly IElevenLabsClient _client;
    private readonly IDialogService _dialog;
    private readonly IEventAggregator _events;
    private readonly ILogger _logger;

    public Agent Agent { get; private set; }

    public SystemPromptTabViewModel SystemPromptVm { get; }
    public FirstMessageTabViewModel FirstMessageVm { get; }
    public VariablesTabViewModel VariablesVm { get; }
    public WorkflowTabViewModel WorkflowVm { get; }
    public ConversationsTabViewModel ConversationsVm { get; }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => Set(ref _selectedTabIndex, value);
    }

    public AgentDetailViewModel(
        Agent agent,
        IElevenLabsClient client,
        ISuggestionEngine suggestions,
        IDialogService dialog,
        IEventAggregator events,
        ILogger<AgentDetailViewModel> logger)
    {
        Agent = agent;
        _client = client;
        _dialog = dialog;
        _events = events;
        _logger = logger;

        SystemPromptVm = new SystemPromptTabViewModel(agent, suggestions, NullLogger<SystemPromptTabViewModel>.Instance);
        FirstMessageVm = new FirstMessageTabViewModel(agent, suggestions, NullLogger<FirstMessageTabViewModel>.Instance);
        VariablesVm = new VariablesTabViewModel(agent, NullLogger<VariablesTabViewModel>.Instance);
        WorkflowVm = new WorkflowTabViewModel(agent, NullLogger<WorkflowTabViewModel>.Instance);
        ConversationsVm = new ConversationsTabViewModel(agent, client, dialog, NullLogger<ConversationsTabViewModel>.Instance);
    }

    protected override void OnViewLoaded(object view)
    {
        _events.SubscribeOnPublishedThread(this);
        base.OnViewLoaded(view);
        // Auto-load conversations when the user first opens an agent.
        _ = ConversationsVm.ReloadAsync();
    }

    /// <summary>Push: merge tab edits into a single AgentUpdate.</summary>
    public async Task Push()
    {
        var prompt = SystemPromptVm.Prompt;
        var firstMsg = FirstMessageVm.FirstMessage;
        var variables = VariablesVm.GetCurrentVariables();
        var workflowNodes = WorkflowVm.GetCurrentNodes();

        var hasPromptChange = prompt != Agent.Prompt;
        var hasFirstMsgChange = firstMsg != Agent.FirstMessage;
        var hasVariablesChange = !variables.SequenceEqual(Agent.Variables);
        var hasWorkflowChange = workflowNodes is not null
            && !workflowNodes.SequenceEqual(Agent.Workflow.Nodes);

        if (!hasPromptChange && !hasFirstMsgChange && !hasVariablesChange && !hasWorkflowChange)
        {
            await _dialog.ShowInfoAsync("无变更", "Prompt、First Message、Variables、Workflow 都没改动，无需推送。");
            return;
        }

        var update = new AgentUpdate(
            Prompt: hasPromptChange ? prompt : null,
            FirstMessage: hasFirstMsgChange ? firstMsg : null,
            Variables: hasVariablesChange ? variables : null,
            WorkflowNodes: hasWorkflowChange ? workflowNodes : null);

        IsBusy = true;
        try
        {
            var snapshot = await _client.UpdateAgentAsync(Agent.AgentId, update);
            Agent = snapshot;
            await _events.PublishOnBackgroundThreadAsync(
                new AgentUpdatedEvent(snapshot.AgentId, snapshot));
            await _dialog.ShowInfoAsync("推送成功", $"Agent {snapshot.Name} 已更新。");
        }
        catch (ElevenLabsException ex)
        {
            _logger.LogError(ex, "ElevenLabs error while updating agent {Id}", Agent.AgentId);
            await _dialog.ShowErrorAsync("推送失败", $"HTTP {ex.HttpStatus}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating agent {Id}", Agent.AgentId);
            await _dialog.ShowErrorAsync("未知错误", ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyOfPropertyChange(nameof(BusyMessage));
            NotifyOfPropertyChange(nameof(Agent));
        }
    }

    /// <summary>Refresh: re-pull the agent from the server and overwrite tab state.</summary>
    public async Task ReloadAsync()
    {
        IsBusy = true;
        try
        {
            var fresh = await _client.GetAgentAsync(Agent.AgentId);
            Agent = fresh;
            SystemPromptVm.RefreshFrom(fresh);
            FirstMessageVm.RefreshFrom(fresh);
            WorkflowVm.RefreshFrom(fresh);
            await _dialog.ShowInfoAsync("已刷新", $"Agent {fresh.Name} 重新拉取成功。");
        }
        catch (ElevenLabsException ex)
        {
            _logger.LogError(ex, "ElevenLabs error while refreshing agent {Id}", Agent.AgentId);
            await _dialog.ShowErrorAsync("刷新失败", $"HTTP {ex.HttpStatus}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyOfPropertyChange(nameof(BusyMessage));
            NotifyOfPropertyChange(nameof(Agent));
        }
    }

    /// <summary>Dry-run: re-run suggestion engine only, no network call.</summary>
    public Task DryRun()
    {
        // Re-fire each tab's recompute so the suggestions list reflects the
        // current local edits. Both tab VMs only recompute when their
        // setter is called, so we toggle the bound properties to trigger
        // the change notification.
        SystemPromptVm.Prompt = SystemPromptVm.Prompt;
        FirstMessageVm.FirstMessage = FirstMessageVm.FirstMessage;
        return Task.CompletedTask;
    }

    public Task HandleAsync(AgentUpdatedEvent message, CancellationToken ct)
    {
        if (message.AgentId == Agent.AgentId)
        {
            Agent = message.Snapshot;
            NotifyOfPropertyChange(nameof(Agent));
        }
        return Task.CompletedTask;
    }
}