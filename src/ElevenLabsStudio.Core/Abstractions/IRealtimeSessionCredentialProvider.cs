using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Abstractions;

public interface IRealtimeSessionCredentialProvider
{
	Task<string> GetSignedUrlAsync(
		RealtimeConversationOptions options,
		CancellationToken ct = default);
}
