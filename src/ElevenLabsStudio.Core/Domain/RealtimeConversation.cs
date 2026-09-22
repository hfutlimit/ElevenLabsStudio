namespace ElevenLabsStudio.Core.Domain;

public enum RealtimeConversationStatus
{
	Disconnected,
	Connecting,
	Connected,
	Disconnecting,
	Failed,
}

public enum RealtimeConversationMode
{
	Unknown,
	Listening,
	Speaking,
}

public sealed record RealtimeConversationOptions(
	string AgentId,
	string? BranchId,
	string Environment,
	IReadOnlyDictionary<string, object?> DynamicVariables);

public sealed record RealtimeTranscriptMessage(
	string Speaker,
	string Text,
	DateTimeOffset At);

public sealed class RealtimeConversationStatusChangedEventArgs(
	RealtimeConversationStatus status,
	string? conversationId) : EventArgs
{
	public RealtimeConversationStatus Status { get; } = status;
	public string? ConversationId { get; } = conversationId;
}

public sealed class RealtimeConversationModeChangedEventArgs(
	RealtimeConversationMode mode) : EventArgs
{
	public RealtimeConversationMode Mode { get; } = mode;
}

public sealed class RealtimeTranscriptEventArgs(RealtimeTranscriptMessage message) : EventArgs
{
	public RealtimeTranscriptMessage Message { get; } = message;
}

public sealed class RealtimeConversationVolumeChangedEventArgs(
	float input,
	float output) : EventArgs
{
	public float Input { get; } = input;
	public float Output { get; } = output;
}

public sealed class RealtimeConversationErrorEventArgs(Exception exception) : EventArgs
{
	public Exception Exception { get; } = exception;
}
