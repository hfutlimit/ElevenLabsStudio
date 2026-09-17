using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Abstractions;

/// <summary>
/// Contract for any ElevenLabs conversational-AI client. Real
/// implementations live in
/// <c>ElevenLabsStudio.Infrastructure.Http.ElevenLabsHttpClient</c>.
/// Every method must accept a <see cref="CancellationToken"/> so unit tests
/// and the UI can interrupt in-flight calls deterministically.
/// </summary>
public interface IElevenLabsClient
{
    Task<IReadOnlyList<Agent>> ListAgentsAsync(CancellationToken ct = default);

    Task<Agent> GetAgentAsync(string agentId, CancellationToken ct = default);

    Task<Agent> UpdateAgentAsync(
        string agentId,
        AgentUpdate update,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConversationRecord>> ListConversationsAsync(
        string agentId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int pageSize = 100,
        string? cursor = null,
        CancellationToken ct = default);

    Task<ConversationRecord> GetConversationAsync(
        string conversationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Voice>> ListVoicesAsync(CancellationToken ct = default);
}