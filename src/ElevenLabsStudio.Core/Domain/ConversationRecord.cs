namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// One stored conversation between an end-user and an ElevenLabs agent,
/// including the ordered transcript turns.
/// </summary>
public sealed record ConversationRecord(
    string ConversationId,
    string AgentId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int DurationMs,
    string Status,
    IReadOnlyList<TranscriptTurn> Turns);

public sealed record TranscriptTurn(
    string Speaker,
    string Text,
    DateTimeOffset At);