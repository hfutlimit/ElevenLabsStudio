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
    /// ElevenLabs API key. Read from <c>ELEVENLABS_API_KEY</c> environment
    /// variable if blank in config. NEVER hard-code; never commit.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.elevenlabs.io/";

    public PollyOptions Polly { get; set; } = new();

    public sealed class PollyOptions
    {
        public int RetryCount { get; set; } = 3;
        public int RetryBaseDelayMs { get; set; } = 500;
        public int CircuitBreakerThreshold { get; set; } = 5;
    }
}