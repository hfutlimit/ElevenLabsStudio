using System.Net;
using System.Text.Json;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Infrastructure.Http.Dto;

namespace ElevenLabsStudio.Infrastructure.Http;

/// <summary>
/// Translates raw HTTP responses into typed <see cref="ElevenLabsException"/>
/// subclasses. Lives in Infrastructure so it can read the ElevenLabs
/// error envelope; Core only consumes the typed exceptions.
/// </summary>
internal static class ElevenLabsExceptionFactory
{
    public static ElevenLabsException FromHttp(HttpStatusCode status, string body)
    {
        var (message, retryAfter) = ExtractError(body);

        return status switch
        {
            HttpStatusCode.Unauthorized => new ElevenLabsAuthException(message),
            HttpStatusCode.Forbidden => new ElevenLabsAuthException(message),
            HttpStatusCode.TooManyRequests => new ElevenLabsRateLimitException(message, retryAfter),
            _ => new ElevenLabsException(message, httpStatus: (int)status),
        };
    }

    private static (string message, TimeSpan? retryAfter) ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return ("ElevenLabs returned an empty error body.", null);
        }

        try
        {
            var dto = JsonSerializer.Deserialize<ElevenLabsErrorDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            var message = dto?.Detail?.Message ?? body;
            return (message, null);
        }
        catch (JsonException)
        {
            return (body, null);
        }
    }
}