namespace ElevenLabsStudio.Core.Exceptions;

/// <summary>Base exception for any ElevenLabs API failure.</summary>
public class ElevenLabsException : Exception
{
    public int? HttpStatus { get; }

    public ElevenLabsException(string message, int? httpStatus = null, Exception? inner = null)
        : base(message, inner)
    {
        HttpStatus = httpStatus;
    }
}

/// <summary>401 / 403 — API key invalid or unauthorised.</summary>
public sealed class ElevenLabsAuthException(string message, Exception? inner = null)
    : ElevenLabsException(message, httpStatus: 401, inner);

/// <summary>429 — rate limited; carries Retry-After hint.</summary>
public sealed class ElevenLabsRateLimitException(
    string message,
    TimeSpan? retryAfter,
    Exception? inner = null)
    : ElevenLabsException(message, httpStatus: 429, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}