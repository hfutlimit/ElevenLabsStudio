using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Infrastructure.Http.Dto;

namespace ElevenLabsStudio.Infrastructure.Http.Mapping;

/// <summary>
/// Internal-only adapter from ElevenLabs wire DTOs into Core Domain
/// records. Kept static so the dependency is one-way (Infrastructure →
/// Domain) and easy to unit-test without any I/O.
/// </summary>
internal static class Mapping
{
    public static Agent MapToAgent(ElevenLabsAgentDto dto) => new(
        AgentId: dto.AgentId,
        Name: dto.Name,
        Prompt: dto.ConversationConfig.Agent.Prompt.Text,
        FirstMessage: dto.ConversationConfig.Agent.FirstMessage ?? string.Empty,
        VoiceId: dto.ConversationConfig.Tts.VoiceId,
        Variables: (dto.ConversationConfig.Agent.Prompt.Variables ?? new())
            .Select(MapToVariable)
            .ToList(),
        UpdatedAt: dto.Metadata.UpdatedAt ?? DateTimeOffset.UtcNow);

    public static Variable MapToVariable(ElevenLabsVariableDto dto) =>
        new(dto.Name, dto.Value, string.IsNullOrEmpty(dto.Type) ? "string" : dto.Type);

    public static ConversationRecord MapToConversation(ElevenLabsConversationDto dto) => new(
        ConversationId: dto.ConversationId,
        AgentId: dto.AgentId,
        StartedAt: FromUnix(dto.StartUnix),
        EndedAt: FromUnix((dto.StartUnix ?? 0) + (dto.CallDurationSecs ?? 0)),
        DurationMs: (dto.CallDurationSecs ?? 0) * 1000,
        Status: dto.Status,
        Turns: dto.Transcript.Select(MapToTurn).ToList());

    public static TranscriptTurn MapToTurn(ElevenLabsTranscriptTurnDto dto) => new(
        Speaker: dto.Role,
        Text: dto.Message,
        At: FromRelative(dto.TimeInCallSecs));

    public static Voice MapToVoice(ElevenLabsVoiceDto dto) =>
        new(dto.VoiceId, dto.Name, dto.Category, dto.PreviewUrl);

    private static DateTimeOffset FromUnix(long? unixSecs) =>
        unixSecs is null
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.FromUnixTimeSeconds(unixSecs.Value);

    private static DateTimeOffset FromRelative(double? secs) =>
        DateTimeOffset.UtcNow.AddSeconds(-(secs ?? 0));
}