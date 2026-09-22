using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Creates a fully-wired <see cref="AgentDetailViewModel"/> for a
/// given <see cref="Agent"/>. Hides the four-tab construction
/// (SystemPrompt / FirstMessage / Variables / Workflow /
/// Conversations) plus the <c>NullLogger</c> fallbacks from the
/// call site so AgentListViewModel can stay clean.
/// </summary>
public interface IAgentDetailViewModelFactory
{
	AgentDetailViewModel Create(Agent agent);
}

/// <summary>
/// Production implementation of
/// <see cref="IAgentDetailViewModelFactory"/>. Constructs the four
/// edit tabs with <see cref="NullLogger{T}"/> fallbacks so the test
/// surface that constructs detail VMs by hand can stay simple. Uses
/// the registered <c>ILogger&lt;T&gt;</c> for the parent
/// AgentDetailViewModel.
/// </summary>
public sealed class AgentDetailViewModelFactory : IAgentDetailViewModelFactory
{
	private readonly IElevenLabsClient _client;
	private readonly ISuggestionEngine _suggestions;
	private readonly IDialogService _dialog;
	private readonly IEventAggregator _events;
	private readonly IDraftStore _drafts;
	private readonly ILogger<AgentDetailViewModel> _detailLogger;
	private readonly IRealtimeConversationClient? _realtime;
	private readonly IClockService? _clock;
	private readonly ILogger<LiveConversationViewModel>? _liveLogger;

	public AgentDetailViewModelFactory(
		IElevenLabsClient client,
		ISuggestionEngine suggestions,
		IDialogService dialog,
		IEventAggregator events,
		IDraftStore drafts,
		ILogger<AgentDetailViewModel> detailLogger,
		IRealtimeConversationClient? realtime = null,
		IClockService? clock = null,
		ILogger<LiveConversationViewModel>? liveLogger = null)
	{
		_client = client;
		_suggestions = suggestions;
		_dialog = dialog;
		_events = events;
		_drafts = drafts;
		_detailLogger = detailLogger;
		_realtime = realtime;
		_clock = clock;
		_liveLogger = liveLogger;
	}

	public AgentDetailViewModel Create(Agent agent) => new(
		agent,
		_client,
		_suggestions,
		_dialog,
		_events,
		_drafts,
		_detailLogger,
		_realtime,
		_clock,
		_liveLogger);
}
