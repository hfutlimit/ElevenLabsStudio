using System.Text.Json;
using System.Text.Json.Nodes;
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
	private static readonly JsonSerializerOptions RawJsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
	};

	public static AgentSummary MapToAgentSummary(ElevenLabsAgentSummaryDto dto) => new(
		AgentId: dto.AgentId,
		Name: dto.Name,
		VoiceId: dto.VoiceId,
		CreatedAt: dto.CreatedAtUnixSecs is null
			? null
			: DateTimeOffset.FromUnixTimeSeconds(dto.CreatedAtUnixSecs.Value));

	public static Agent MapToAgent(ElevenLabsAgentDto dto) => new(
		AgentId: dto.AgentId,
		Name: dto.Name,
		Prompt: dto.ConversationConfig.Agent.Prompt.Text,
		FirstMessage: dto.ConversationConfig.Agent.FirstMessage ?? string.Empty,
		VoiceId: dto.ConversationConfig.Tts.VoiceId,
		Variables: (dto.ConversationConfig.Agent.Prompt.Variables ?? new())
			.Select(MapToVariable)
			.ToList(),
		Workflow: MapToWorkflow(dto.Workflow),
		UpdatedAt: dto.Metadata.UpdatedAt ?? DateTimeOffset.UtcNow);

	public static Variable MapToVariable(ElevenLabsVariableDto dto) =>
		new(dto.Name, dto.Value, string.IsNullOrEmpty(dto.Type) ? "string" : dto.Type);

	public static ConversationRecord MapToConversationSummary(ElevenLabsConversationSummaryDto dto) => new(
		ConversationId: dto.ConversationId,
		AgentId: dto.AgentId,
		StartedAt: FromUnix(dto.StartUnix),
		EndedAt: FromUnix((dto.StartUnix ?? 0) + (dto.CallDurationSecs ?? 0)),
		DurationMs: (dto.CallDurationSecs ?? 0) * 1000,
		Status: dto.Status,
		Turns: Array.Empty<TranscriptTurn>());

	public static ConversationRecord MapToConversationDetail(ElevenLabsConversationDetailDto dto)
	{
		var startedAt = FromUnix(dto.StartUnix);
		return new ConversationRecord(
			ConversationId: dto.ConversationId,
			AgentId: dto.AgentId,
			StartedAt: startedAt,
			EndedAt: startedAt.AddSeconds(dto.CallDurationSecs ?? 0),
			DurationMs: (dto.CallDurationSecs ?? 0) * 1000,
			Status: dto.Status,
			Turns: dto.Transcript.Select(turn => MapToTurn(turn, startedAt)).ToList());
	}

	public static TranscriptTurn MapToTurn(
		ElevenLabsTranscriptTurnDto dto,
		DateTimeOffset startedAt) => new(
		Speaker: dto.Role,
		Text: dto.Message,
		At: startedAt.AddSeconds(dto.TimeInCallSecs ?? 0));

	public static Voice MapToVoice(ElevenLabsVoiceDto dto) =>
		new(dto.VoiceId, dto.Name, dto.Category, dto.PreviewUrl);

	public static Workflow MapToWorkflow(JsonObject? dto)
	{
		if (dto is null)
		{
			return WorkflowDefaults.Empty;
		}

		var nodes = new List<WorkflowNode>();
		if (dto["nodes"] is JsonObject nodeObject)
		{
			foreach (var pair in nodeObject)
			{
				if (pair.Value is not JsonObject node)
				{
					continue;
				}
				nodes.Add(new WorkflowNode(
					pair.Key,
					node["type"]?.GetValue<string>() ?? string.Empty,
					node["label"]?.GetValue<string>()
						?? node["name"]?.GetValue<string>()
						?? pair.Key));
			}
		}
		else if (dto["nodes"] is JsonArray nodeArray)
		{
			foreach (var node in nodeArray.OfType<JsonObject>())
			{
				nodes.Add(new WorkflowNode(
					node["id"]?.GetValue<string>() ?? string.Empty,
					node["type"]?.GetValue<string>() ?? string.Empty,
					node["label"]?.GetValue<string>()
						?? node["name"]?.GetValue<string>()
						?? string.Empty));
			}
		}
		string? raw = null;
		try
		{
			raw = dto.ToJsonString(RawJsonOptions);
		}
		catch (JsonException)
		{
			// Fall through with raw=null; UI surfaces "no raw JSON" gracefully.
		}

		return new Workflow(nodes, raw);
	}

	private static DateTimeOffset FromUnix(long? unixSecs) =>
		unixSecs is null
			? DateTimeOffset.UtcNow
			: DateTimeOffset.FromUnixTimeSeconds(unixSecs.Value);
}
