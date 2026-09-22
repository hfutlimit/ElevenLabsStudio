namespace ElevenLabsStudio.Infrastructure;

/// <summary>
/// Bound from <c>appsettings.json</c> section <c>ElevenLabs</c>. Decouples
/// configuration shape from the HttpClient registration code so unit
/// tests can substitute minimal POCO instances.
/// </summary>
public sealed class ElevenLabsOptions
{
	public const string SectionName = "ElevenLabs";

	/// <summary>
	/// Use the offline <see cref="Mock.MockElevenLabsClient"/> instead of
	/// the real HTTP client. Useful for UI iteration and E2E tests
	/// without an API key. Defaults to true so first-time launches show
	/// content immediately.
	/// </summary>
	public bool Mock { get; set; } = true;

	/// <summary>
	/// ElevenLabs API key. Read from <c>ELEVENLABS_API_KEY</c> environment
	/// variable if blank in config. NEVER hard-code; never commit.
	/// </summary>
	public string ApiKey { get; set; } = string.Empty;

	public string BaseUrl { get; set; } = "https://api.elevenlabs.io/";

	/// <summary>
	/// Optional local-development scope. When set, the runtime client loads
	/// this agent through the detail endpoint and exposes it as the only list
	/// result. This avoids depending on the account-wide agent list while
	/// developing against one permitted agent.
	/// </summary>
	public string? TestAgentId { get; set; }

	public PollyOptions Polly { get; set; } = new();

	public sealed class PollyOptions
	{
		public int RetryCount { get; set; } = 3;
		public int RetryBaseDelayMs { get; set; } = 500;
		public int CircuitBreakerThreshold { get; set; } = 5;
	}
}
