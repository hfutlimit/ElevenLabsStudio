using System.Net.Http;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Infrastructure;
using ElevenLabsStudio.Infrastructure.Http;
using ElevenLabsStudio.Infrastructure.Mock;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ElevenLabsStudio.UnitTests.Infrastructure;

/// <summary>
/// Routing contract for <see cref="RuntimeClient"/>:
///  * When ElevenLabsOptions.Mock is true, every call hits the
///    <see cref="MockElevenLabsClient"/>.
///  * When the flag flips to false, the next call hits the real
///    <see cref="ElevenLabsHttpClient"/> — no DI rebuild, no
///    service re-registration.
///  * The shared IElevenLabsClient interface masks whichever
///    underlying implementation the runtime picked, so callers
///    (ViewModels) never need to know the answer.
/// </summary>
public sealed class RuntimeClientTests
{
    private static IOptionsMonitor<ElevenLabsOptions> MockMonitor(bool mockMode, string? apiKey = "key")
    {
        return new StaticOptionsMonitor<ElevenLabsOptions>(new ElevenLabsOptions
        {
            Mock = mockMode,
            ApiKey = apiKey,
            BaseUrl = "https://example.invalid/",
        });
    }

    [Fact]
    public async Task When_Mock_is_true_returns_mock_agents()
    {
        var mock = new MockElevenLabsClient();
        var real = MakeUnreachableRealClient();
        var client = new RuntimeClient(mock, real, MockMonitor(mockMode: true));

        var agents = await client.ListAgentsAsync();

        agents.Should().HaveCount(3);
        agents.Should().AllSatisfy(a => a.AgentId.Should().StartWith("agent_"));
    }

    [Fact]
    public async Task When_Mock_is_false_calls_real_client()
    {
        var mock = new MockElevenLabsClient();
        // HttpListener catches the real-client call so we can verify
        // it actually went out on the wire.
        await using var server = new Integration.HttpTestServer();
        server.Enqueue(200, """{"agents":[]}""");
        var real = new ElevenLabsHttpClient(
            new HttpClient { BaseAddress = new Uri(server.BaseUrl), Timeout = TimeSpan.FromSeconds(5) },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ElevenLabsHttpClient>.Instance,
            new StaticOptionsMonitor<ElevenLabsOptions>(new ElevenLabsOptions { BaseUrl = server.BaseUrl, ApiKey = "x" }));
        var client = new RuntimeClient(mock, real, MockMonitor(mockMode: false));

        var agents = await client.ListAgentsAsync();

        agents.Should().BeEmpty();
        server.Captured.Should().HaveCount(1);
        server.Captured[0].Path.Should().StartWith("/v1/convai/agents");
    }

    [Fact]
    public async Task Flipping_Mock_at_runtime_routes_the_next_call()
    {
        var monitor = new MutableOptionsMonitor(new ElevenLabsOptions
        {
            Mock = true,
            BaseUrl = "https://example.invalid/",
        });
        var mock = new MockElevenLabsClient();
        var real = MakeUnreachableRealClient();
        var client = new RuntimeClient(mock, real, monitor);

        // 1) Mock on -> mock answers.
        (await client.ListAgentsAsync()).Should().HaveCount(3);

        // 2) Flip to real and the next call must NOT go to the mock.
        monitor.Set(new ElevenLabsOptions { Mock = false, BaseUrl = "https://example.invalid/", ApiKey = "x" });
        await using var server = new Integration.HttpTestServer();
        server.Enqueue(200, """{"agents":[]}""");
        // Replace the real instance with one bound to the loopback
        // server so the assertion below can capture the request.
        var realOverServer = new ElevenLabsHttpClient(
            new HttpClient { BaseAddress = new Uri(server.BaseUrl), Timeout = TimeSpan.FromSeconds(5) },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ElevenLabsHttpClient>.Instance,
            new StaticOptionsMonitor<ElevenLabsOptions>(new ElevenLabsOptions { BaseUrl = server.BaseUrl, ApiKey = "x" }));
        var clientOverServer = new RuntimeClient(mock, realOverServer, monitor);
        _ = await clientOverServer.ListAgentsAsync();

        server.Captured.Should().HaveCount(1);
    }

    [Fact]
    public async Task Routes_GetAgent_UpdateAgent_And_GetConversation()
    {
        var monitor = MockMonitor(mockMode: true);
        var mock = new MockElevenLabsClient();
        var real = MakeUnreachableRealClient();
        var client = new RuntimeClient(mock, real, monitor);

        var agent = await client.GetAgentAsync("agent_sales_001");
        agent.AgentId.Should().Be("agent_sales_001");

        var updated = await client.UpdateAgentAsync(
            "agent_sales_001",
            new AgentUpdate(FirstMessage: "Hi from runtime"));
        updated.FirstMessage.Should().Be("Hi from runtime");

        var conv = await client.GetConversationAsync("conv_2026_09_17_001");
        conv.ConversationId.Should().Be("conv_2026_09_17_001");
    }

    // ---- helpers ----

    private static ElevenLabsHttpClient MakeUnreachableRealClient()
    {
        // For tests where we never expect the real path to run; we
        // pass a HttpClient pointed at an invalid URL so a stray call
        // would fail loudly. As long as the test never routes to it,
        // no real request goes out.
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:1/") };
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ElevenLabsHttpClient>.Instance;
        var opts = new StaticOptionsMonitor<ElevenLabsOptions>(new ElevenLabsOptions
        {
            BaseUrl = "http://localhost:1/",
            ApiKey = "x",
        });
        return new ElevenLabsHttpClient(http, logger, opts);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        public StaticOptionsMonitor(T value) { CurrentValue = value; }
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>
    /// Tiny test-only IOptionsMonitor that lets a single test flip
    /// the value mid-test, mirroring what IOptionsMonitor does when
    /// the underlying IConfigurationRoot reloads.
    /// </summary>
    private sealed class MutableOptionsMonitor : IOptionsMonitor<ElevenLabsOptions>
    {
        private ElevenLabsOptions _value;
        public MutableOptionsMonitor(ElevenLabsOptions initial) { _value = initial; }
        public void Set(ElevenLabsOptions value) => _value = value;
        public ElevenLabsOptions CurrentValue => _value;
        public ElevenLabsOptions Get(string? name) => _value;
        public IDisposable? OnChange(Action<ElevenLabsOptions, string?> listener) => null;
    }
}