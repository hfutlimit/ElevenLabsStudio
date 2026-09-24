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
				nodes.Add(MapToWorkflowNode(pair.Key, node));
			}
		}
		else if (dto["nodes"] is JsonArray nodeArray)
		{
			foreach (var node in nodeArray.OfType<JsonObject>())
			{
				nodes.Add(MapToWorkflowNode(
					GetString(node["id"]) ?? string.Empty,
					node));
			}
		}

		var edges = MapToWorkflowEdges(dto["edges"]);
		string? raw = null;
		try
		{
			raw = dto.ToJsonString(RawJsonOptions);
		}
		catch (JsonException)
		{
			// Fall through with raw=null; UI surfaces "no raw JSON" gracefully.
		}

		return new Workflow(nodes, raw, edges);
	}

	private static WorkflowNode MapToWorkflowNode(string id, JsonObject node)
	{
		var position = node["position"] as JsonObject;
		return new WorkflowNode(
			id,
			GetString(node["type"]) ?? string.Empty,
			GetString(node["label"])
				?? GetString(node["name"])
				?? id,
			TryGetDouble(position, "x"),
			TryGetDouble(position, "y"));
	}

	private static IReadOnlyList<WorkflowEdge> MapToWorkflowEdges(JsonNode? value)
	{
		if (value is not JsonObject edgeObject)
		{
			return Array.Empty<WorkflowEdge>();
		}

		var edges = new List<WorkflowEdge>();
		foreach (var pair in edgeObject)
		{
			if (pair.Value is not JsonObject edge)
			{
				continue;
			}

			var source = GetString(edge["source"]);
			var target = GetString(edge["target"]);
			if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
			{
				continue;
			}

			edges.Add(new WorkflowEdge(
				pair.Key,
				source,
				target,
				GetConditionText(edge["forward_condition"]) ?? GetString(edge["condition"]),
				GetConditionType(edge["forward_condition"]),
				GetConditionSuccessful(edge["forward_condition"]),
				GetConditionLabel(edge["forward_condition"])));
		}

		return edges;
	}

	private static string? GetConditionText(JsonNode? value) =>
		value is JsonObject condition
			? GetString(condition["condition"])
			: GetString(value);

	private static string? GetConditionType(JsonNode? value) =>
		value is JsonObject condition ? GetString(condition["type"]) : null;

	private static bool? GetConditionSuccessful(JsonNode? value)
	{
		if (value is not JsonObject condition
			|| condition["successful"] is not JsonNode flag)
		{
			return null;
		}

		try
		{
			return flag.GetValue<bool>();
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private static string? GetConditionLabel(JsonNode? value) =>
		value is JsonObject condition ? GetString(condition["label"]) : null;

	private static string? GetString(JsonNode? value)
	{
		try
		{
			return value?.GetValue<string>();
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private static double? TryGetDouble(JsonObject? position, string name)
	{
		if (position is null || position[name] is null)
		{
			return null;
		}

		try
		{
			return position[name]!.GetValue<double>();
		}
		catch (InvalidOperationException)
		{
			return null;
		}
		catch (FormatException)
		{
			return null;
		}
	}

	private static DateTimeOffset FromUnix(long? unixSecs) =>
		unixSecs is null
			? DateTimeOffset.UtcNow
			: DateTimeOffset.FromUnixTimeSeconds(unixSecs.Value);
}
