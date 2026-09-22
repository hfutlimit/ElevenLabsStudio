using System.Text.Json;
using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Services;

internal sealed record RealtimeBrowserMessage(
	string Type,
	RealtimeConversationStatus? Status = null,
	RealtimeConversationMode? Mode = null,
	string? ConversationId = null,
	RealtimeTranscriptMessage? Transcript = null,
	float? InputVolume = null,
	float? OutputVolume = null,
	string? Error = null);

internal static class RealtimeBrowserMessageParser
{
	public static RealtimeBrowserMessage Parse(string json)
	{
		using var document = JsonDocument.Parse(json);
		var root = document.RootElement;
		var type = root.GetProperty("type").GetString()
			?? throw new JsonException("Realtime browser message has no type.");

		return type switch
		{
			"connected" => new RealtimeBrowserMessage(
				type,
				Status: RealtimeConversationStatus.Connected,
				ConversationId: GetString(root, "conversationId")),
			"connecting" => new RealtimeBrowserMessage(type, RealtimeConversationStatus.Connecting),
			"disconnected" => new RealtimeBrowserMessage(type, RealtimeConversationStatus.Disconnected),
			"failed" or "error" => new RealtimeBrowserMessage(
				type,
				Status: RealtimeConversationStatus.Failed,
				Error: GetString(root, "error") ?? "Realtime client failed."),
			"modeChanged" => new RealtimeBrowserMessage(
				type,
				Mode: ParseMode(GetString(root, "mode"))),
			"message" => new RealtimeBrowserMessage(
				type,
				Transcript: new RealtimeTranscriptMessage(
					GetString(root, "speaker") ?? "agent",
					GetString(root, "text") ?? string.Empty,
					GetDateTime(root, "at"))),
			"volumeChanged" => new RealtimeBrowserMessage(
				type,
				InputVolume: GetFloat(root, "inputVolume"),
				OutputVolume: GetFloat(root, "outputVolume")),
			"ready" => new RealtimeBrowserMessage(type),
			_ => new RealtimeBrowserMessage(type),
		};
	}

	private static RealtimeConversationMode ParseMode(string? mode) =>
		Enum.TryParse<RealtimeConversationMode>(mode, ignoreCase: true, out var value)
			? value
			: RealtimeConversationMode.Unknown;

	private static string? GetString(JsonElement root, string name) =>
		root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static DateTimeOffset GetDateTime(JsonElement root, string name) =>
		DateTimeOffset.TryParse(GetString(root, name), out var value)
			? value
			: DateTimeOffset.UtcNow;

	private static float? GetFloat(JsonElement root, string name) =>
		root.TryGetProperty(name, out var value) && value.TryGetSingle(out var number)
			? number
			: null;
}
