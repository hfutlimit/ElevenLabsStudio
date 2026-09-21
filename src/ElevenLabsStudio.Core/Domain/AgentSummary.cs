namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// Read-only row returned by List Agents. It deliberately excludes editable
/// configuration, which must be loaded through GetAgentAsync.
/// </summary>
public sealed record AgentSummary(
	string AgentId,
	string Name,
	string? VoiceId,
	DateTimeOffset? CreatedAt);
