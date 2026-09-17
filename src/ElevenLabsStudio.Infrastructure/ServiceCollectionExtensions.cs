using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Suggestions;
using ElevenLabsStudio.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace ElevenLabsStudio.Infrastructure;

/// <summary>
/// Single registration entry point. The UI hosts this once at startup;
/// every other project reads from the resulting <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElevenLabsStudioInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ElevenLabsOptions>()
            .Bind(configuration.GetSection(ElevenLabsOptions.SectionName))
            .ValidateOnStart();

        // Typed HttpClient with Polly retry / circuit breaker wrapped around
        // the raw ElevenLabsHttpClient. Use AddPolicyHandler from
        // Microsoft.Extensions.Http.Polly so the policies are applied
        // per-request without an explicit decorator class.
        services.AddHttpClient<IElevenLabsClient, ElevenLabsHttpClient>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ElevenLabsOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                {
                    client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
                }
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler((sp, request) =>
            {
                var opts = sp.GetRequiredService<IOptions<ElevenLabsOptions>>().Value;
                var logger = sp.GetRequiredService<ILogger<ElevenLabsHttpClient>>();

                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(msg => (int)msg.StatusCode == 429)
                    .WaitAndRetryAsync(
                        retryCount: opts.Polly.RetryCount,
                        sleepDurationProvider: attempt =>
                            TimeSpan.FromMilliseconds(opts.Polly.RetryBaseDelayMs * Math.Pow(2, attempt - 1)),
                        onRetry: (outcome, delay, _, _) =>
                        {
                            logger.LogWarning(
                                outcome.Exception,
                                "ElevenLabs HTTP retry in {Delay} (attempt outcome: {Status})",
                                delay,
                                outcome.Result?.StatusCode);
                        });
            })
            .AddPolicyHandler((sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<ElevenLabsOptions>>().Value;
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: opts.Polly.CircuitBreakerThreshold,
                        durationOfBreak: TimeSpan.FromSeconds(30));
            });

        // Local-only suggestion engines (no network). Register every
        // leaf engine as ISuggestionEngine so MS DI's IEnumerable<T>
        // resolution collects them automatically — CompositeSuggestionEngine
        // itself takes IEnumerable<ISuggestionEngine> in its ctor, so
        // registering it under the same interface would create a
        // self-referencing cycle. The composite can be wired by hand
        // later when a 2nd leaf engine shows up.
        services.AddSingleton<ISuggestionEngine, HeuristicSuggestionEngine>();

        return services;
    }
}