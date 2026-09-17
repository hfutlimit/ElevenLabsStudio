using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Lists the most recent <see cref="ConversationRecord"/>s for the
/// agent currently displayed in <see cref="AgentDetailViewModel"/>.
/// Selecting a row shows its transcript on the right.
/// </summary>
public sealed class ConversationsTabViewModel : ScreenBase
{
    private readonly Agent _agent;
    private readonly IElevenLabsClient _client;
    private readonly IDialogService _dialog;
    private readonly ILogger _logger;

    public BindableCollection<ConversationRecord> Conversations { get; } = new();
    public BindableCollection<TranscriptTurn> Turns { get; } = new();

    private ConversationRecord? _selectedConversation;
    public ConversationRecord? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (Set(ref _selectedConversation, value))
            {
                Turns.Clear();
                if (_selectedConversation is not null)
                {
                    Turns.AddRange(_selectedConversation.Turns);
                }
            }
        }
    }

    public ConversationsTabViewModel(
        Agent agent,
        IElevenLabsClient client,
        IDialogService dialog,
        ILogger logger)
    {
        _agent = agent;
        _client = client;
        _dialog = dialog;
        _logger = logger;
    }

    public async Task ReloadAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _client.ListConversationsAsync(
                _agent.AgentId,
                from: DateTimeOffset.UtcNow.AddDays(-7),
                to: DateTimeOffset.UtcNow,
                pageSize: 100);
            Conversations.Clear();
            Conversations.AddRange(items);
        }
        catch (ElevenLabsException ex)
        {
            _logger.LogError(ex, "ElevenLabs error loading conversations for {AgentId}", _agent.AgentId);
            await _dialog.ShowErrorAsync("加载失败", $"HTTP {ex.HttpStatus}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading conversations for {AgentId}", _agent.AgentId);
            await _dialog.ShowErrorAsync("未知错误", ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyOfPropertyChange(nameof(BusyMessage));
        }
    }
}