using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Infrastructure.Mock;

/// <summary>
/// Offline stand-in for <see cref="IElevenLabsClient"/>. Returns a fixed
/// cast of agents + conversations so the UI can be exercised end-to-end
/// without an API key. Registered only when
/// <c>ElevenLabsOptions.Mock == true</c>; the real
/// <c>ElevenLabsHttpClient</c> is wired otherwise.
/// </summary>
public sealed class MockElevenLabsClient : IElevenLabsClient
{
	private static readonly Agent FocusedTestAgent = new(
		AgentId: "agent_3001m2hctwtcfeqvwb5nk5bxbg89",
		Name: "Staging Single Case",
		Prompt: "You are the staging single-case support agent. " +
				"Use the initial webhook account variables to identify the caller " +
				"and keep the conversation focused on one support case.",
		FirstMessage: "Hello, how can I help with your support case today?",
		VoiceId: "voice_aria",
		Variables: new[]
		{
			new Variable("client_id", "tuplus01qa", "string"),
			new Variable("caller_id_norm", "2601234567", "string"),
		},
		Workflow: new Workflow(
			Nodes: new[]
			{
				new WorkflowNode("n1", "greeting", "Greeting"),
				new WorkflowNode("n2", "llm", "Account Lookup"),
				new WorkflowNode("n3", "tool", "Case Search"),
				new WorkflowNode("n4", "end", "Wrap Up"),
			},
			RawJson: "{\"nodes\":[\"greeting\",\"llm\",\"tool\",\"end\"]}"),
		UpdatedAt: DateTimeOffset.UtcNow.AddMinutes(-12));

	private static readonly IReadOnlyList<Agent> _agents = new[]
	{
		FocusedTestAgent,
	};

	private static readonly IReadOnlyList<ConversationRecord> _focusedTestConversations = new[]
	{
		new ConversationRecord(
			ConversationId: "conv_2026_09_17_001",
			AgentId: "agent_3001m2hctwtcfeqvwb5nk5bxbg89",
			StartedAt: DateTimeOffset.UtcNow.AddMinutes(-22),
			EndedAt: DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(184),
			DurationMs: 184_000,
			Status: "success",
			Turns: new[]
			{
				new TranscriptTurn("agent", "Hello, how can I help with your support case today?", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(2)),
				new TranscriptTurn("user", "I need help with a case for my account.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(15)),
				new TranscriptTurn("agent", "I can look that up. Let me confirm the account details first.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(40)),
				new TranscriptTurn("user", "Sure.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(58)),
				new TranscriptTurn("agent", "Thanks. I found the account and will continue with the case.", DateTimeOffset.UtcNow.AddMinutes(-22).AddSeconds(75)),
			}),
		new ConversationRecord(
			ConversationId: "conv_2026_09_16_009",
			AgentId: "agent_3001m2hctwtcfeqvwb5nk5bxbg89",
			StartedAt: DateTimeOffset.UtcNow.AddHours(-2),
			EndedAt: DateTimeOffset.UtcNow.AddHours(-2).AddMinutes(3),
			DurationMs: 180_000,
			Status: "success",
			Turns: new[]
			{
				new TranscriptTurn("agent", "Hello, how can I help with your support case today?", DateTimeOffset.UtcNow.AddHours(-2)),
				new TranscriptTurn("user", "I need a status update.", DateTimeOffset.UtcNow.AddHours(-2).AddSeconds(20)),
				new TranscriptTurn("agent", "I will check the current case status for you.", DateTimeOffset.UtcNow.AddHours(-2).AddSeconds(45)),
			}),
		new ConversationRecord(
			ConversationId: "conv_2026_09_15_004",
			AgentId: "agent_3001m2hctwtcfeqvwb5nk5bxbg89",
			StartedAt: DateTimeOffset.UtcNow.AddHours(-26),
			EndedAt: DateTimeOffset.UtcNow.AddHours(-26).AddMinutes(5),
			DurationMs: 300_000,
			Status: "failure",
			Turns: new[]
			{
				new TranscriptTurn("agent", "Hello, how can I help with your support case today?", DateTimeOffset.UtcNow.AddHours(-26)),
				new TranscriptTurn("user", "I will call back later.", DateTimeOffset.UtcNow.AddHours(-26).AddSeconds(15)),
				new TranscriptTurn("agent", "No problem. We will be ready when you are.", DateTimeOffset.UtcNow.AddHours(-26).AddSeconds(40)),
			}),
	};

	public Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(CancellationToken ct = default) =>
		Task.FromResult<IReadOnlyList<AgentSummary>>(_agents
			.Select(agent => new AgentSummary(
				agent.AgentId,
				agent.Name,
				agent.VoiceId,
				agent.UpdatedAt))
			.ToList());

	public Task<Agent> GetAgentAsync(string agentId, CancellationToken ct = default)
	{
		var agent = _agents.FirstOrDefault(a => a.AgentId == agentId)
			?? throw new ElevenLabsStudio.Core.Exceptions.ElevenLabsException(
				$"Mock agent '{agentId}' does not exist.",
				httpStatus: 404);
		return Task.FromResult(agent);
	}

	public Task<Agent> UpdateAgentAsync(string agentId, AgentUpdate update, CancellationToken ct = default)
	{
		var current = _agents.FirstOrDefault(a => a.AgentId == agentId)
			?? throw new ElevenLabsStudio.Core.Exceptions.ElevenLabsException(
				$"Mock agent '{agentId}' does not exist.",
				httpStatus: 404);

		// Echo the merged state back without mutation. The VM treats
		// the response as the canonical post-update snapshot.
		var newWorkflow = update.Workflow ?? current.Workflow;
		var snapshot = current with
		{
			Prompt = update.Prompt ?? current.Prompt,
			FirstMessage = update.FirstMessage ?? current.FirstMessage,
			VoiceId = update.VoiceId ?? current.VoiceId,
			Variables = update.Variables ?? current.Variables,
			Workflow = newWorkflow,
			UpdatedAt = DateTimeOffset.UtcNow,
		};
		return Task.FromResult(snapshot);
	}

	public Task<IReadOnlyList<ConversationRecord>> ListConversationsAsync(
		string agentId,
		DateTimeOffset? from = null,
		DateTimeOffset? to = null,
		int pageSize = 100,
		string? cursor = null,
		CancellationToken ct = default)
	{
		IReadOnlyList<ConversationRecord> source = agentId == "agent_3001m2hctwtcfeqvwb5nk5bxbg89"
			? _focusedTestConversations
			: Array.Empty<ConversationRecord>();
		return Task.FromResult<IReadOnlyList<ConversationRecord>>(
			source.Select(record => record with
			{
				Turns = Array.Empty<TranscriptTurn>(),
			}).ToList());
	}

	public Task<ConversationRecord> GetConversationAsync(string conversationId, CancellationToken ct = default)
	{
		var all = _focusedTestConversations;
		var match = all.FirstOrDefault(c => c.ConversationId == conversationId)
			?? throw new ElevenLabsStudio.Core.Exceptions.ElevenLabsException(
				$"Mock conversation '{conversationId}' does not exist.",
				httpStatus: 404);
		return Task.FromResult(match);
	}

	public Task<IReadOnlyList<Voice>> ListVoicesAsync(CancellationToken ct = default) =>
		Task.FromResult<IReadOnlyList<Voice>>(new[]
		{
			new Voice("voice_aria", "Aria", "premade", null),
			new Voice("voice_onyx", "Onyx", "premade", null),
			new Voice("voice_chloe", "Chloe", "premade", null),
		});
}
