using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.Conversations;

/// <summary>
/// Reads and paginates <see cref="ConversationRecord"/>s. The right pane
/// shows the transcript for the currently selected conversation.
/// </summary>
public sealed class ConversationListViewModel : ScreenBase
{
    private readonly IElevenLabsClient _client;
    private readonly IDialogService _dialog;
    private readonly ILogger<ConversationListViewModel> _logger;

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

    private string _agentIdFilter = string.Empty;
    public string AgentIdFilter
    {
        get => _agentIdFilter;
        set => Set(ref _agentIdFilter, value);
    }

    public ConversationListViewModel(
        IElevenLabsClient client,
        IDialogService dialog,
        ILogger<ConversationListViewModel> logger)
    {
        _client = client;
        _dialog = dialog;
        _logger = logger;
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(AgentIdFilter))
        {
            await _dialog.ShowInfoAsync("提示", "请先在筛选框输入 AgentId，再点击刷新。");
            return;
        }

        IsBusy = true;
        BusyMessage = "正在加载对话…";
        try
        {
            var items = await _client.ListConversationsAsync(
                AgentIdFilter,
                from: DateTimeOffset.UtcNow.AddDays(-7),
                to: DateTimeOffset.UtcNow,
                pageSize: 100);
            Conversations.Clear();
            Conversations.AddRange(items);
        }
        catch (ElevenLabsException ex)
        {
            _logger.LogError(ex, "ElevenLabs error while loading conversations (status={Status})", ex.HttpStatus);
            await _dialog.ShowErrorAsync("加载失败", $"HTTP {ex.HttpStatus}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading conversations");
            await _dialog.ShowErrorAsync("未知错误", ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyOfPropertyChange(nameof(BusyMessage));
        }
    }
}