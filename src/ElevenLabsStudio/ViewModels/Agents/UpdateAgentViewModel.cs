using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Agents;

/// <summary>
/// Holds the editable copy of a single <see cref="Agent"/> + a local
/// <see cref="Suggestion"/> list produced by
/// <see cref="ISuggestionEngine"/>. Pressing "Push" materialises the
/// edits into an <see cref="AgentUpdate"/> and dispatches it through
/// <see cref="IElevenLabsClient"/>.
/// </summary>
public sealed class UpdateAgentViewModel : ScreenBase
{
    private readonly IElevenLabsClient _client;
    private readonly ISuggestionEngine _suggestions;
    private readonly IDialogService _dialog;
    private readonly IEventAggregator _events;
    private readonly ILogger _logger;

    public Agent Agent { get; }

    private string _prompt;
    public string Prompt
    {
        get => _prompt;
        set { if (Set(ref _prompt, value)) RefreshSuggestions(); }
    }

    private string _firstMessage;
    public string FirstMessage
    {
        get => _firstMessage;
        set { if (Set(ref _firstMessage, value)) RefreshSuggestions(); }
    }

    private string? _voiceId;
    public string? VoiceId
    {
        get => _voiceId;
        set { if (Set(ref _voiceId, value)) RefreshSuggestions(); }
    }

    public BindableCollection<Suggestion> Suggestions { get; } = new();

    public UpdateAgentViewModel(
        Agent agent,
        IElevenLabsClient client,
        ISuggestionEngine suggestions,
        IDialogService dialog,
        IEventAggregator events,
        ILogger logger)
    {
        Agent = agent;
        _client = client;
        _suggestions = suggestions;
        _dialog = dialog;
        _events = events;
        _logger = logger;

        _prompt = agent.Prompt;
        _firstMessage = agent.FirstMessage;
        _voiceId = agent.VoiceId;

        RefreshSuggestions();
    }

    public void Reanalyse() => RefreshSuggestions();

    private void RefreshSuggestions()
    {
        var update = new AgentUpdate(Prompt, FirstMessage, VoiceId, Agent.Variables);
        var found = _suggestions.Analyze(Agent, update);
        Suggestions.Clear();
        Suggestions.AddRange(found);
    }

    public async Task Save()
    {
        var update = new AgentUpdate(Prompt, FirstMessage, VoiceId, Agent.Variables);
        if (update.IsEmpty)
        {
            await _dialog.ShowInfoAsync("无变更", "没有任何字段需要推送。");
            return;
        }

        IsBusy = true;
        BusyMessage = "正在推送更新…";
        try
        {
            var snapshot = await _client.UpdateAgentAsync(Agent.AgentId, update);
            await _events.PublishOnBackgroundThreadAsync(new Core.Events.AgentUpdatedEvent(snapshot.AgentId, snapshot));
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
        }
    }
}