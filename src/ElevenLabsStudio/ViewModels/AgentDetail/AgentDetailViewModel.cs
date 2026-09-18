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
    private readonly IDraftStore _drafts;
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

    /// <summary>
    /// Aggregate dirty flag — true when ANY tab still has a value
    /// that differs from the latest server snapshot. Surfaced to
    /// the parent AgentListViewModel so the sidebar can warn the user
    /// (and the next selection change can save a draft) before
    /// discarding the work.
    /// </summary>
    public bool IsDirty =>
        (SystemPromptVm?.Prompt ?? string.Empty) != (Agent?.Prompt ?? string.Empty)
        || (FirstMessageVm?.FirstMessage ?? string.Empty) != (Agent?.FirstMessage ?? string.Empty)
        || (VariablesVm?.Variables is { } vv && !vv.SequenceEqual(Agent?.Variables ?? Array.Empty<Variable>()))
        || (WorkflowVm?.Nodes is { } wn && !wn.SequenceEqual(Agent?.Workflow.Nodes ?? Array.Empty<WorkflowNode>()));

    public AgentDetailViewModel(
        Agent agent,
        IElevenLabsClient client,
        ISuggestionEngine suggestions,
        IDialogService dialog,
        IEventAggregator events,
        IDraftStore drafts,
        ILogger<AgentDetailViewModel> logger)
    {
        Agent = agent;
        _client = client;
        _dialog = dialog;
        _events = events;
        _drafts = drafts;
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

    /// <summary>Stable key under which this VM's edit drafts are stored
    /// in <see cref="IDraftStore"/>.</summary>
    public string DraftKey => $"agent:{Agent?.AgentId}";

    /// <summary>Snapshot the current tab contents into a
    /// <see cref="AgentDetailDraft"/> and persist via
    /// <see cref="IDraftStore.Save{T}"/>. No-op when there is nothing
    /// dirty to save.</summary>
    public void SaveDraft()
    {
        if (!IsDirty) return;
        var draft = new AgentDetailDraft(
            SystemPromptVm.Prompt,
            FirstMessageVm.FirstMessage,
            VariablesVm.Variables.ToList(),
            WorkflowVm.Nodes.ToList());
        _drafts.Save(DraftKey, draft);
        _logger.LogDebug("Saved draft for {AgentId}", Agent?.AgentId);
    }

    /// <summary>Try to restore a previously-saved draft. Returns true
    /// when a draft was applied, false when there was no draft (or
    /// the draft was empty).</summary>
    public bool TryRestoreDraft()
    {
        if (!_drafts.TryGet<AgentDetailDraft>(DraftKey, out var draft) || draft is null)
        {
            return false;
        }
        // Set the four tab fields so the recompute paths fire as if
        // the user had typed them.
        SystemPromptVm.Prompt = draft.Prompt;
        FirstMessageVm.FirstMessage = draft.FirstMessage;
        VariablesVm.Variables.Clear();
        foreach (var v in draft.Variables) VariablesVm.Variables.Add(v);
        VariablesVm.IsDirty = true;
        WorkflowVm.Nodes.Clear();
        foreach (var n in draft.Nodes) WorkflowVm.Nodes.Add(n);
        WorkflowVm.IsDirty = true;
        _logger.LogInformation("Restored draft for {AgentId}", Agent?.AgentId);
        return true;
    }

    /// <summary>Forget any draft for this agent. Called from the
    /// sidebar after a successful selection-change save, and from
    /// the Push handler after the server acknowledges the new state.</summary>
    public void DiscardDraft() => _drafts.Discard(DraftKey);

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
            // Successful push — the draft is now stale, drop it.
            DiscardDraft();
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
            // Server snapshot just overwrote any local edits — the
            // saved draft (if any) is now stale.
            DiscardDraft();
            // Every tab VM must reset its "edited vs. server" baseline
            // so the next Push correctly distinguishes real edits from
            // values that already match the server snapshot. Missing
            // any one of these lets the user re-push the stale value
            // and silently overwrite the server's data.
            SystemPromptVm.RefreshFrom(fresh);
            FirstMessageVm.RefreshFrom(fresh);
            VariablesVm.RefreshFrom(fresh);
            WorkflowVm.RefreshFrom(fresh);
            _logger.LogInformation(
                "Refreshed agent {Id} from server; all four tab baselines reset",
                fresh.AgentId);
            // A successful refresh is a routine background operation —
            // the refreshed content IS the feedback. Only failures get
            // a modal; success used to interrupt the user for nothing
            // (review #8).
        }
        catch (ElevenLabsException ex)
        {
            _logger.LogError(ex, "ElevenLabs error while refreshing agent {Id}", Agent.AgentId);
            await _dialog.ShowErrorAsync("刷新失败", $"HTTP {ex.HttpStatus}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while refreshing agent {Id}", Agent.AgentId);
            await _dialog.ShowErrorAsync("未知错误", ex.Message);
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
        // Calling the setters with their current value is a no-op because
        // Set<T> short-circuits on EqualityComparer<T>.Default.Equals, so
        // the previous "self-assign to trigger recompute" was a dead
        // branch — no recompute ever happened, the suggestion lists
        // never refreshed. Call the dedicated Recompute methods instead
        // so the local editor's view of the server is genuinely
        // re-evaluated.
        SystemPromptVm.RecomputeSuggestionsPublic();
        FirstMessageVm.RecomputeSuggestionsPublic();
        _logger.LogDebug("DryRun triggered RecomputeSuggestions on both edit tabs");
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

/// <summary>
/// Snapshot of the four tab values at a point in time. Stored in
/// <see cref="IDraftStore"/> when the user navigates away from an
/// agent so the next time they come back their edits are still
/// there. Lives in <see cref="ElevenLabsStudio.ViewModels.AgentDetail"/>
/// so it can be reloaded via a single <c>using</c>.
/// </summary>
public sealed record AgentDetailDraft(
    string Prompt,
    string FirstMessage,
    IReadOnlyList<Variable> Variables,
    IReadOnlyList<WorkflowNode> Nodes);