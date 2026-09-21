using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Events;

/// <summary>
/// Published after <see cref="Abstractions.IElevenLabsClient.UpdateAgentAsync"/>
/// succeeds. Subscribers (e.g. <c>AgentListViewModel</c>) refresh their
/// local cached copy.
/// </summary>
public sealed record AgentUpdatedEvent(string AgentId, Agent Snapshot);

/// <summary>
/// Published after an Agent list refresh finishes, signalling other VMs to
/// drop stale references and rebind any open detail panes.
/// </summary>
public sealed record AgentListRefreshedEvent(IReadOnlyList<AgentSummary> Agents);
