using System.Net.Http;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ElevenLabsStudio.UnitTests.Infrastructure;

public sealed class ElevenLabsRealtimeCredentialProviderTests
{
	[Fact]
	public async Task Requests_a_signed_url_without_exposing_the_key_in_the_query()
	{
		await using var server = new Integration.HttpTestServer();
		server.Enqueue(200, """{"signed_url":"wss://signed.example/session?token=one"}""");
		var options = new StaticOptionsMonitor<ElevenLabsOptions>(new ElevenLabsOptions
		{
			ApiKey = "secret-key",
			BaseUrl = server.BaseUrl,
		});
		var provider = new ElevenLabsRealtimeCredentialProvider(
			new HttpClient { BaseAddress = new Uri(server.BaseUrl) },
			options);

		var signedUrl = await provider.GetSignedUrlAsync(new RealtimeConversationOptions(
			"agent_live",
			"branch_live",
			"staging",
			new Dictionary<string, object?>()));

		await server.DrainAsync();
		signedUrl.Should().Be("wss://signed.example/session?token=one");
		server.Captured.Should().ContainSingle();
		var request = server.Captured[0];
		request.Path.Should().Be("/v1/convai/conversation/get-signed-url");
		request.Query.Should().Contain("agent_id=agent_live");
		request.Query.Should().Contain("branch_id=branch_live");
		request.Query.Should().Contain("environment=staging");
		request.Query.Should().NotContain("secret-key");
		request.Headers["xi-api-key"].Should().Be("secret-key");
	}

	private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T> where T : class
	{
		public T CurrentValue { get; } = value;
		public T Get(string? name) => CurrentValue;
		public IDisposable? OnChange(Action<T, string?> listener) => null;
	}
}
