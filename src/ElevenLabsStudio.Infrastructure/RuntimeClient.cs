using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using Microsoft.Extensions.Options;

namespace ElevenLabsStudio.Infrastructure;

/// <summary>
/// Decorator over <see cref="IElevenLabsClient"/> that picks between
/// the offline <see cref="Mock.MockElevenLabsClient"/> and the real
/// <see cref="Http.ElevenLabsHttpClient"/> on every call, based on
/// the current value of <see cref="ElevenLabsOptions.Mock"/>.
///
/// <para>
/// When the user flips Mock=true in Settings (which writes to
/// appsettings.json and calls <c>IConfigurationRoot.Reload()</c>),
/// the next <c>RuntimeClient.ListAgentsAsync</c> immediately picks up
/// the new value through <see cref="IOptionsMonitor{T}.CurrentValue"/>
/// — no app restart, no DI container rebuild required.
/// </para>
///
/// <para>
/// The Mock instance is always registered (cheap, in-process) and the
/// real HTTP client is registered as a typed HttpClient; this class
/// just routes the call to one or the other on every invocation.
/// </para>
/// </summary>
public sealed class RuntimeClient : IElevenLabsClient
{
	private readonly Mock.MockElevenLabsClient _mock;
	private readonly Http.ElevenLabsHttpClient _real;
	private readonly IOptionsMonitor<ElevenLabsOptions> _options;

	public RuntimeClient(
		Mock.MockElevenLabsClient mock,
		Http.ElevenLabsHttpClient real,
		IOptionsMonitor<ElevenLabsOptions> options)
	{
		_mock = mock;
		_real = real;
		_options = options;
	}

	private IElevenLabsClient Route
	{
		get
		{
			var mock = _options.CurrentValue.Mock;
			return mock ? _mock : _real;
		}
	}

	public Task<IReadOnlyList<AgentSummary>> ListAgentsAsync(CancellationToken ct = default) =>
		Route.ListAgentsAsync(ct);

	public Task<Agent> GetAgentAsync(string agentId, CancellationToken ct = default) =>
		Route.GetAgentAsync(agentId, ct);

	public Task<Agent> UpdateAgentAsync(string agentId, AgentUpdate update, CancellationToken ct = default) =>
		Route.UpdateAgentAsync(agentId, update, ct);

	public Task<IReadOnlyList<ConversationRecord>> ListConversationsAsync(
		string agentId,
		DateTimeOffset? from = null,
		DateTimeOffset? to = null,
		int pageSize = 100,
		string? cursor = null,
		CancellationToken ct = default) =>
		Route.ListConversationsAsync(agentId, from, to, pageSize, cursor, ct);

	public Task<ConversationRecord> GetConversationAsync(string conversationId, CancellationToken ct = default) =>
		Route.GetConversationAsync(conversationId, ct);

	public Task<IReadOnlyList<Voice>> ListVoicesAsync(CancellationToken ct = default) =>
		Route.ListVoicesAsync(ct);
}
