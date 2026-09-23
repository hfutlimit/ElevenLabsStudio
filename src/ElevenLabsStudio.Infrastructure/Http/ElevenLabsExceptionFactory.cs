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
    private const int MaxBodySnippetLength = 240;

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

        // Body could be a multi-KB HTML proxy intercept page or a JSON
        // envelope without a `detail.message` — either way we don't want
        // the full thing pasted into a modal. Truncate to a head + ellipsis
        // so the user still sees enough context to recognise the failure
        // without flooding the dialog.
        string Snippet(string raw) =>
            raw.Length <= MaxBodySnippetLength
                ? raw
                : raw[..MaxBodySnippetLength] + "…";

        try
        {
            var dto = JsonSerializer.Deserialize<ElevenLabsErrorDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            var message = dto?.Detail?.Message is { Length: > 0 } detail
                ? detail
                : Snippet(body);
            return (message, null);
        }
        catch (JsonException)
        {
            return (Snippet(body), null);
        }
    }
}