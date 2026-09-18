using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace ElevenLabsStudio.Infrastructure.Http;

/// <summary>
/// Builds and holds the two HTTP resilience policies for the
/// ElevenLabs typed client as SINGLE instances.
///
/// <para>
/// <see cref="HttpClientBuilderExtensions.AddPolicyHandler"/> resolves
/// its <c>(sp, request) => policy</c> lambda once per request, so any
/// policy constructed inside that lambda is effectively stateless —
/// a circuit breaker built there never accumulates trip state past a
/// single request (review #7). Registering this holder as a singleton
/// and resolving the SAME policy instance per request keeps the
/// breaker's failure counter and open/half-open state across the
/// app's lifetime.
/// </para>
/// </summary>
internal sealed class ElevenLabsHttpPolicies
{
    public IAsyncPolicy<HttpResponseMessage> Retry { get; }

    public IAsyncPolicy<HttpResponseMessage> CircuitBreaker { get; }

    public ElevenLabsHttpPolicies(
        IOptions<ElevenLabsOptions> options,
        ILogger<ElevenLabsHttpClient> logger)
    {
        var polly = options.Value.Polly;

        Retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: polly.RetryCount,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(polly.RetryBaseDelayMs * Math.Pow(2, attempt - 1)),
                onRetry: (outcome, delay, _, _) =>
                {
                    logger.LogWarning(
                        outcome.Exception,
                        "ElevenLabs HTTP retry in {Delay} (attempt outcome: {Status})",
                        delay,
                        outcome.Result?.StatusCode);
                });

        CircuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: polly.CircuitBreakerThreshold,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
