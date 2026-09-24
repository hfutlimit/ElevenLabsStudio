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
public sealed class ConversationsTabViewModel : ScreenBase, IDisposable
{
	private readonly Agent _agent;
	private readonly IElevenLabsClient _client;
	private readonly IDialogService _dialog;
	private readonly ILogger _logger;
	private CancellationTokenSource? _selectionCts;
	private long _selectionGeneration;
	private bool _isConversationListLoading;
	private bool _isTranscriptLoading;

	public BindableCollection<ConversationRecord> Conversations { get; } = new();
	public BindableCollection<TranscriptTurn> Turns { get; } = new();

	public bool IsConversationListLoading
	{
		get => _isConversationListLoading;
		private set
		{
			if (Set(ref _isConversationListLoading, value)) UpdateBusyState();
		}
	}

	public bool IsTranscriptLoading
	{
		get => _isTranscriptLoading;
		private set
		{
			if (Set(ref _isTranscriptLoading, value)) UpdateBusyState();
		}
	}

	private ConversationRecord? _selectedConversation;
	public ConversationRecord? SelectedConversation
	{
		get => _selectedConversation;
		set
		{
			if (ReferenceEquals(_selectedConversation, value)) return;
			_ = SelectConversationGuardedAsync(value);
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

	public async Task SelectConversationAsync(
		ConversationRecord? conversation,
		CancellationToken ct = default)
	{
		_selectionCts?.Cancel();
		_selectionCts?.Dispose();
		_selectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var selectionToken = _selectionCts.Token;
		var generation = Interlocked.Increment(ref _selectionGeneration);
		Set(ref _selectedConversation, conversation, nameof(SelectedConversation));

		if (conversation is null)
		{
			IsTranscriptLoading = false;
			Turns.Clear();
			return;
		}

		IsTranscriptLoading = true;
		try
		{
			var detail = await _client.GetConversationAsync(
				conversation.ConversationId,
				selectionToken);
			if (selectionToken.IsCancellationRequested
				|| generation != _selectionGeneration
				|| _selectedConversation?.ConversationId != conversation.ConversationId)
			{
				return;
			}

			Turns.Clear();
			Turns.AddRange(detail.Turns);
		}
		catch (OperationCanceledException) when (selectionToken.IsCancellationRequested)
		{
			// A newer selection owns the transcript now.
		}
		catch (ElevenLabsException ex)
		{
			if (selectionToken.IsCancellationRequested || generation != _selectionGeneration) return;
			_logger.LogError(ex, "ElevenLabs error loading conversation {ConversationId}", conversation.ConversationId);
			await _dialog.ShowErrorAsync("Load failed", $"HTTP {ex.HttpStatus}: {ex.Message}", ct);
		}
		catch (Exception ex)
		{
			if (selectionToken.IsCancellationRequested || generation != _selectionGeneration) return;
			_logger.LogError(ex, "Unexpected error loading conversation {ConversationId}", conversation.ConversationId);
			await _dialog.ShowErrorAsync("Unexpected error", ex.Message, ct);
		}
		finally
		{
			if (generation == _selectionGeneration)
			{
				IsTranscriptLoading = false;
			}
		}
	}

	private async Task SelectConversationGuardedAsync(ConversationRecord? conversation)
	{
		try
		{
			await SelectConversationAsync(conversation);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected selection failure for {ConversationId}", conversation?.ConversationId);
			await _dialog.ShowErrorAsync("Unexpected error", ex.Message);
		}
	}

	public async Task ReloadAsync(CancellationToken ct = default)
	{
		IsConversationListLoading = true;
		try
		{
			var items = await _client.ListConversationsAsync(
				_agent.AgentId,
				from: DateTimeOffset.UtcNow.AddDays(-7),
				to: DateTimeOffset.UtcNow,
				pageSize: 100,
				ct: ct);
			Conversations.Clear();
			Conversations.AddRange(items);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (ElevenLabsException ex)
		{
			_logger.LogError(ex, "ElevenLabs error loading conversations for {AgentId}", _agent.AgentId);
			await _dialog.ShowErrorAsync("Load failed", $"HTTP {ex.HttpStatus}: {ex.Message}");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unexpected error loading conversations for {AgentId}", _agent.AgentId);
			await _dialog.ShowErrorAsync("Unexpected error", ex.Message);
		}
		finally
		{
			IsConversationListLoading = false;
		}
	}

	private void UpdateBusyState()
	{
		IsBusy = _isConversationListLoading || _isTranscriptLoading;
		BusyMessage = _isTranscriptLoading
			? "Loading transcript…"
			: _isConversationListLoading
				? "Loading conversations…"
				: null;
	}

	public void Dispose()
	{
		_selectionCts?.Cancel();
		_selectionCts?.Dispose();
	}
}
