using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Suggestions;
using ElevenLabsStudio.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

		// Both the offline mock client and the typed HTTP client are always
		// registered. A thin <see cref="RuntimeClient"/> decorator
		// picks one or the other on every call, reading the current
		// ElevenLabsOptions.Mock flag via IOptionsMonitor so the user
		// can flip Mock ↔ Real in Settings without restarting the app.
		services.AddSingleton<Mock.MockElevenLabsClient>();
		services.AddSingleton<Http.ElevenLabsHttpPolicies>();
		services.AddHttpClient<Http.ElevenLabsHttpClient>((sp, client) =>
		{
			var opts = sp.GetRequiredService<IOptions<ElevenLabsOptions>>().Value;
			if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
			{
				client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
			}
			client.Timeout = TimeSpan.FromSeconds(30);
		})
		// Resolve the SAME singleton policy instance on every request
		// so the circuit breaker accumulates trip state (review #7).
		// Building policies inside the (sp, request) lambda would hand
		// each request a fresh breaker that never trips.
		.AddPolicyHandler((sp, _) =>
			sp.GetRequiredService<Http.ElevenLabsHttpPolicies>().Retry)
		.AddPolicyHandler((sp, _) =>
			sp.GetRequiredService<Http.ElevenLabsHttpPolicies>().CircuitBreaker);

		services.AddHttpClient<ElevenLabsRealtimeCredentialProvider>((sp, client) =>
		{
			var opts = sp.GetRequiredService<IOptions<ElevenLabsOptions>>().Value;
			if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
			{
				client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
			}
			client.Timeout = TimeSpan.FromSeconds(15);
		})
		// Signed URL is a one-shot idempotent GET — add the same retry
		// policy the main client uses, but skip the shared circuit
		// breaker: a transient signed-url failure shouldn't trip the
		// breaker's view of the API as a whole (and vice versa).
		.AddPolicyHandler((sp, _) =>
			sp.GetRequiredService<Http.ElevenLabsHttpPolicies>().Retry);
		services.AddSingleton<IRealtimeSessionCredentialProvider>(sp =>
			sp.GetRequiredService<ElevenLabsRealtimeCredentialProvider>());

		// Single IElevenLabsClient facade — delegates to mock or real
		// per call, based on the live ElevenLabsOptions value.
		services.AddSingleton<IElevenLabsClient, RuntimeClient>();

		// Local-only suggestion engines (no network). Register every
		// leaf engine as ISuggestionEngine so MS DI's IEnumerable<T>
		// resolution collects them automatically — CompositeSuggestionEngine
		// itself takes IEnumerable<ISuggestionEngine> in its ctor, so
		// registering it under the same interface would create a
		// self-referencing cycle. The composite can be wired by hand
		// later when a 2nd leaf engine shows up.
		services.AddSingleton<ISuggestionEngine, HeuristicSuggestionEngine>();

		// In-memory draft store for per-agent edit drafts. Survives
		// until ClearAll() or process exit — drafts are never persisted
		// to disk, so a restart is the same as a deliberate "discard
		// everything" policy.
		services.AddSingleton<IDraftStore, Memory.MemoryDraftStore>();

		return services;
	}
}
