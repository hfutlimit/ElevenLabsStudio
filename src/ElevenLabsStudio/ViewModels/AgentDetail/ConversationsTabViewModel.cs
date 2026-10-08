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
	private string? _transcriptError;

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

	/// <summary>
	/// Why the current conversation's transcript is not showing. Kept
	/// separate from <see cref="IsTranscriptLoading"/> so the right pane
	/// can say "this failed" instead of presenting an empty box that is
	/// indistinguishable from "this conversation had no turns".
	/// </summary>
	public string? TranscriptError
	{
		get => _transcriptError;
		private set
		{
			if (Set(ref _transcriptError, value))
			{
				NotifyOfPropertyChange(nameof(HasTranscriptError));
			}
		}
	}

	/// <summary>
	/// String-to-visibility is not available in this app (only a boolean
	/// converter is registered), so the view binds this instead of
	/// <see cref="TranscriptError"/>.
	/// </summary>
	public bool HasTranscriptError => !string.IsNullOrEmpty(_transcriptError);

	/// <summary>
	/// True when a transcript was asked for and came back with no turns
	/// and no error. Drives the "nothing recorded" empty state.
	/// </summary>
	public bool HasNoTranscript =>
		SelectedConversation is not null && !IsTranscriptLoading && TranscriptError is null && Turns.Count == 0;

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
			TranscriptError = null;
			Turns.Clear();
			NotifyOfPropertyChange(nameof(HasNoTranscript));
			return;
		}

		IsTranscriptLoading = true;
		TranscriptError = null;
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
			TranscriptError = $"HTTP {ex.HttpStatus}: {ex.Message}";
			await _dialog.ShowErrorAsync("Load failed", TranscriptError, ct);
		}
		catch (Exception ex)
		{
			if (selectionToken.IsCancellationRequested || generation != _selectionGeneration) return;
			_logger.LogError(ex, "Unexpected error loading conversation {ConversationId}", conversation.ConversationId);
			TranscriptError = ex.Message;
			await _dialog.ShowErrorAsync("Unexpected error", TranscriptError, ct);
		}
		finally
		{
			if (generation == _selectionGeneration)
			{
				IsTranscriptLoading = false;
				// Both the error and the empty state depend on the settled
				// loading flag and on Turns, neither of which raise their
				// own change for the view to recompute this from.
				NotifyOfPropertyChange(nameof(HasNoTranscript));
			}
		}
	}

	/// <summary>
	/// Re-run the transcript request for the current conversation. The
	/// network in front of this box drops TLS handshakes often enough
	/// that a failed transcript is usually worth one more try.
	/// </summary>
	public Task RetryTranscriptAsync(CancellationToken ct = default)
	{
		var current = _selectedConversation;
		return current is null
			? Task.CompletedTask
			: SelectConversationAsync(current, ct);
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

			// Remember the open conversation across the refresh. The
			// Clear() below makes the bound ListBox push null back into
			// SelectedConversation, which used to cascade into a
			// transcript wipe — the user hit Reload and the whole right
			// pane went blank even though the conversation was still
			// right there in the new list.
			var selectedId = _selectedConversation?.ConversationId;

			Conversations.Clear();
			Conversations.AddRange(items);

			if (selectedId is not null)
			{
				var reopened = Conversations.FirstOrDefault(
					c => c.ConversationId == selectedId);
				// A null result means the conversation really is gone
				// from the server, so clearing the transcript is correct.
				await SelectConversationAsync(reopened, ct);
			}
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
