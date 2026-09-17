using System.Text.Json.Serialization;

namespace ElevenLabsStudio.Infrastructure.Http.Dto;

// These DTOs mirror the ElevenLabs Conversational AI v1 responses. They
// are intentionally internal: only the typed HttpClient and the Mapping
// module may touch them. Domain code never sees an "ElevenLabsAgentDto"
// — it always gets a Core.Domain.Agent.

// ---- Agent ----

internal sealed class ElevenLabsAgentListResponseDto
{
    [JsonPropertyName("agents")]
    public List<ElevenLabsAgentDto> Agents { get; set; } = new();
}

internal sealed class ElevenLabsAgentDto
{
    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("conversation_config")]
    public ElevenLabsConversationConfigDto ConversationConfig { get; set; } = new();

    [JsonPropertyName("workflow")]
    public ElevenLabsWorkflowDto? Workflow { get; set; }

    [JsonPropertyName("metadata")]
    public ElevenLabsMetadataDto Metadata { get; set; } = new();
}

internal sealed class ElevenLabsWorkflowDto
{
    [JsonPropertyName("nodes")]
    public List<ElevenLabsWorkflowNodeDto> Nodes { get; set; } = new();
}

internal sealed class ElevenLabsWorkflowNodeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class ElevenLabsConversationConfigDto
{
    [JsonPropertyName("agent")]
    public ElevenLabsConversationAgentDto Agent { get; set; } = new();

    [JsonPropertyName("tts")]
    public ElevenLabsTtsDto Tts { get; set; } = new();
}

internal sealed class ElevenLabsConversationAgentDto
{
    [JsonPropertyName("prompt")]
    public ElevenLabsPromptDto Prompt { get; set; } = new();

    [JsonPropertyName("first_message")]
    public string? FirstMessage { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}

internal sealed class ElevenLabsPromptDto
{
    [JsonPropertyName("prompt")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("variables")]
    public List<ElevenLabsVariableDto>? Variables { get; set; }
}

internal sealed class ElevenLabsVariableDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";
}

internal sealed class ElevenLabsTtsDto
{
    [JsonPropertyName("voice_id")]
    public string? VoiceId { get; set; }
}

internal sealed class ElevenLabsMetadataDto
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

// ---- Conversation ----

internal sealed class ElevenLabsConversationListResponseDto
{
    [JsonPropertyName("conversations")]
    public List<ElevenLabsConversationDto> Conversations { get; set; } = new();

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}

internal sealed class ElevenLabsConversationDto
{
    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonPropertyName("agent_id")]
    public string AgentId { get; set; } = string.Empty;

    [JsonPropertyName("start_time_unix_secs")]
    public long? StartUnix { get; set; }

    [JsonPropertyName("call_duration_secs")]
    public int? CallDurationSecs { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    [JsonPropertyName("transcript")]
    public List<ElevenLabsTranscriptTurnDto> Transcript { get; set; } = new();
}

internal sealed class ElevenLabsTranscriptTurnDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("time_in_call_secs")]
    public double? TimeInCallSecs { get; set; }
}

// ---- Voice ----

internal sealed class ElevenLabsVoiceListResponseDto
{
    [JsonPropertyName("voices")]
    public List<ElevenLabsVoiceDto> Voices { get; set; } = new();
}

internal sealed class ElevenLabsVoiceDto
{
    [JsonPropertyName("voice_id")]
    public string VoiceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("preview_url")]
    public string? PreviewUrl { get; set; }
}

// ---- Error envelope ----

internal sealed class ElevenLabsErrorDto
{
    [JsonPropertyName("detail")]
    public ElevenLabsErrorDetailDto? Detail { get; set; }
}

internal sealed class ElevenLabsErrorDetailDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}