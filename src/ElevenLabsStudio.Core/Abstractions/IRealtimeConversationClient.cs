using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Abstractions;

public interface IRealtimeConversationClient
{
	Task<IRealtimeConversationSession> StartAsync(
		RealtimeConversationOptions options,
		CancellationToken ct = default);
}

public interface IRealtimeConversationSession : IAsyncDisposable
{
	string? ConversationId { get; }
	RealtimeConversationStatus Status { get; }

	event EventHandler<RealtimeConversationStatusChangedEventArgs>? StatusChanged;
	event EventHandler<RealtimeConversationModeChangedEventArgs>? ModeChanged;
	event EventHandler<RealtimeTranscriptEventArgs>? TranscriptReceived;
	event EventHandler<RealtimeConversationVolumeChangedEventArgs>? VolumeChanged;
	event EventHandler<RealtimeConversationErrorEventArgs>? Error;

	Task SendTextAsync(string text, CancellationToken ct = default);
	Task SetMutedAsync(bool muted, CancellationToken ct = default);
	Task StopAsync(CancellationToken ct = default);
}
