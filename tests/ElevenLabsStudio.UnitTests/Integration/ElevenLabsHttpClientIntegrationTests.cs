using System.Net.Http;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Infrastructure;
using ElevenLabsStudio.Infrastructure.Http;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElevenLabsStudio.UnitTests.Integration;

/// <summary>
/// Real wire-level integration tests for <see cref="ElevenLabsHttpClient"/>.
/// The client is constructed with its full DI graph and pointed at a
/// loopback <see cref="HttpTestServer"/>; no mocks or in-memory
/// seams are involved. This pins the on-the-wire JSON shape, the
/// xi-api-key header, and the Polly retries that we'd otherwise
/// only see by hitting the public ElevenLabs API.
/// </summary>
public sealed class ElevenLabsHttpClientIntegrationTests
{
	private static ElevenLabsHttpClient NewClient(HttpTestServer server, string apiKey = "test-key-123")
	{
		var opts = new StaticOptionsMonitor<ElevenLabsOptions>(new ElevenLabsOptions
		{
			BaseUrl = server.BaseUrl,
			ApiKey = apiKey,
		});
		// HttpClient created with a 5s timeout so a hung test doesn't
		// block the runner indefinitely. The handler chain is
		// real (no MockHttpMessageHandler).
		var http = new HttpClient { BaseAddress = new Uri(server.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };
		return new ElevenLabsHttpClient(http, NullLogger<ElevenLabsHttpClient>.Instance, opts);
	}

	private static string AgentsListJson() => """
		{
			"agents": [
			{
				"agent_id": "agent_wire_001",
				"name": "Wire Agent",
				"voice_id": "voice_aria",
				"tags": ["sales"],
				"created_at_unix_secs": 1789682400
			}
			]
		}
		""";

	[Fact]
	public async Task ListAgentsAsync_deserialises_payload_and_sends_xi_api_key()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, AgentsListJson());
		var client = NewClient(server, apiKey: "wire-test-key");

		var agents = await client.ListAgentsAsync();

		agents.Should().HaveCount(1);
		agents[0].AgentId.Should().Be("agent_wire_001");
		agents[0].Name.Should().Be("Wire Agent");
		agents[0].VoiceId.Should().Be("voice_aria");
		// The header the server received should include our API key.
		server.Captured.Should().HaveCount(1);
		server.Captured[0].Method.Should().Be("GET");
		server.Captured[0].Path.Should().Be("/v1/convai/agents");
		server.Captured[0].Headers.Should().ContainKey("xi-api-key");
		server.Captured[0].Headers["xi-api-key"].Should().Be("wire-test-key");
	}

	[Fact]
	public async Task GetAgentAsync_maps_single_agent_payload()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, """
			{
				"agent_id": "agent_wire_002",
				"name": "Single",
				"conversation_config": {
				"agent": { "prompt": {"prompt": "p"}, "first_message": "f" },
				"tts": {}
				},
				"workflow": { "nodes": [] },
				"metadata": {}
			}
			""");
		var client = NewClient(server);

		var agent = await client.GetAgentAsync("agent_wire_002");
		agent.AgentId.Should().Be("agent_wire_002");
		agent.Prompt.Should().Be("p");
		agent.FirstMessage.Should().Be("f");
	}

	[Fact]
	public async Task UpdateAgentAsync_sends_patch_with_xi_api_key_and_payload()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, """
			{
				"agent_id": "agent_wire_003",
				"name": "Updated",
				"conversation_config": {
				"agent": { "prompt": {"prompt": "new"}, "first_message": "new-fm" },
				"tts": { "voice_id": "voice_b" }
				},
				"workflow": { "nodes": [{"id":"n9","type":"llm","name":"Greeting"}] },
				"metadata": { "updated_at": "2026-09-17T23:00:00Z" }
			}
			""");
		var client = NewClient(server, apiKey: "patch-key");

		var snapshot = await client.UpdateAgentAsync(
			"agent_wire_003",
			new AgentUpdate(
				Prompt: "new",
				FirstMessage: "new-fm",
				Variables: new[] { new Variable("topic", "billing", "string") },
				Workflow: new Workflow(
					new[] { new WorkflowNode("n9", "llm", "Greeting") },
					"""{"nodes":{"old":{"type":"llm","label":"Old"}},"edges":{}}""")));

		snapshot.Prompt.Should().Be("new");
		snapshot.FirstMessage.Should().Be("new-fm");
		snapshot.Workflow.Nodes.Should().HaveCount(1);
		snapshot.Workflow.Nodes[0].Id.Should().Be("n9");

		// The PATCH request must include the api key and the merged
		// payload we sent. Variables nest INSIDE the prompt object
		// (mirroring the GET model) and Domain records serialize with
		// snake_case property names — the wire contract of the
		// ElevenLabs API (review #7).
		server.Captured.Should().HaveCount(1);
		var sent = server.Captured[0];
		sent.Method.Should().Be("PATCH");
		sent.Path.Should().Be("/v1/convai/agents/agent_wire_003");
		sent.Headers["xi-api-key"].Should().Be("patch-key");
		sent.RequestBody.Should().Contain("\"prompt\":{\"prompt\":\"new\",\"variables\":[{\"name\":\"topic\",\"value\":\"billing\",\"type\":\"string\"}]}");
		sent.RequestBody.Should().Contain("\"first_message\":\"new-fm\"");
		sent.RequestBody.Should().Contain("\"workflow\":{\"nodes\":{");
		sent.RequestBody.Should().Contain("\"n9\":{\"type\":\"llm\",\"label\":\"Greeting\"}");
	}

	[Fact]
	public async Task UpdateAgentAsync_partial_update_omits_unchanged_fields()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, """
			{
				"agent_id": "agent_wire_004",
				"name": "Partial",
				"conversation_config": {
				"agent": { "prompt": {"prompt": "only"}, "first_message": "kept" },
				"tts": {}
				},
				"workflow": { "nodes": [] },
				"metadata": {}
			}
			""");
		var client = NewClient(server);

		// Only the prompt changed — the wire body must not mention
		// first_message / tts / workflow / variables at all. Serializing
		// them as JSON null could make the server clear those fields.
		_ = await client.UpdateAgentAsync(
			"agent_wire_004", new AgentUpdate(Prompt: "only"));

		var body = server.Captured[0].RequestBody;
		body.Should().Contain("\"prompt\":{\"prompt\":\"only\"}");
		body.Should().NotContain("first_message");
		body.Should().NotContain("\"tts\"");
		body.Should().NotContain("workflow");
		body.Should().NotContain("variables");
	}

	[Fact]
	public async Task ListAgentsAsync_retries_transient_500_through_DI_polly_pipeline()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(500, "transient boom");
		server.Enqueue(200, AgentsListJson());

		// Build the client through the SAME registration the app uses
		// (AddElevenLabsStudioInfrastructure + AddPolicyHandler chain),
		// pointed at the loopback test server. The previous tests
		// hand-new the HttpClient and so never exercised the Polly
		// policies at all (review #7).
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElevenLabs:BaseUrl"] = server.BaseUrl,
				["ElevenLabs:ApiKey"] = "di-key",
				["ElevenLabs:Polly:RetryCount"] = "3",
				["ElevenLabs:Polly:RetryBaseDelayMs"] = "1",
				["ElevenLabs:Polly:CircuitBreakerThreshold"] = "5",
			})
			.Build();
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddElevenLabsStudioInfrastructure(config);
		await using var sp = services.BuildServiceProvider();
		var client = sp.GetRequiredService<ElevenLabsHttpClient>();

		var agents = await client.ListAgentsAsync();

		agents.Should().HaveCount(1);
		agents[0].AgentId.Should().Be("agent_wire_001");
		// One initial attempt + one retry, both carrying the per-request
		// auth header.
		server.Captured.Should().HaveCount(2);
		server.Captured.Should().OnlyContain(c => c.Headers["xi-api-key"] == "di-key");
	}

	[Fact]
	public async Task ListConversationsAsync_maps_summary_and_uses_current_query_names()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, """
			{
				"conversations": [
				{
					"conversation_id": "conv_wire_001",
					"agent_id": "agent_wire_001",
					"start_time_unix_secs": 1737000000,
					"call_duration_secs": 12,
					"status": "success"
				}
				],
				"next_cursor": null
			}
			""");
		var client = NewClient(server);

		var from = DateTimeOffset.FromUnixTimeSeconds(1736990000);
		var to = DateTimeOffset.FromUnixTimeSeconds(1737010000);
		var convs = await client.ListConversationsAsync("agent_wire_001", from, to, pageSize: 500);

		convs.Should().HaveCount(1);
		convs[0].Turns.Should().BeEmpty();
		convs[0].DurationMs.Should().Be(12_000);
		// The agent id is sent as a query string; HttpListener exposes
		// the path separately from the query.
		var sent = server.Captured[0];
		sent.Path.Should().StartWith("/v1/convai/conversations");
		sent.FullPath.Should().Contain("agent_id=agent_wire_001");
		sent.FullPath.Should().Contain("call_start_after_unix=1736990000");
		sent.FullPath.Should().Contain("call_start_before_unix=1737010000");
		sent.FullPath.Should().Contain("page_size=100");
	}

	[Fact]
	public async Task GetConversationAsync_maps_turn_times_relative_to_start()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, """
			{
				"conversation_id": "conv_wire_002",
				"agent_id": "agent_wire_001",
				"start_time_unix_secs": 1737000000,
				"call_duration_secs": 12,
				"status": "success",
				"transcript": [
				{"role": "agent", "message": "hi", "time_in_call_secs": 0.5},
				{"role": "user", "message": "hello", "time_in_call_secs": 1.5}
				]
			}
			""");
		var client = NewClient(server);

		var conversation = await client.GetConversationAsync("conv_wire_002");

		conversation.Turns[0].At.Should().Be(
			DateTimeOffset.FromUnixTimeSeconds(1737000000).AddSeconds(0.5));
		conversation.Turns[1].At.Should().Be(
			DateTimeOffset.FromUnixTimeSeconds(1737000000).AddSeconds(1.5));
	}

	[Fact]
	public async Task GetAgentAsync_maps_404_to_ElevenLabsException_with_status()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(404, """{"detail":{"message":"agent not found","status":"not_found"}}""");
		var client = NewClient(server);

		var act = () => client.GetAgentAsync("agent_missing");

		var ex = (await act.Should().ThrowAsync<Core.Exceptions.ElevenLabsException>()).Which;
		ex.HttpStatus.Should().Be(404);
		ex.Message.Should().Contain("agent not found");
	}

	[Fact]
	public async Task UpdateAgentAsync_maps_429_to_RateLimitException()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(429, """{"detail":{"message":"slow down","status":"too_many_requests"}}""");
		var client = NewClient(server);

		var act = () => client.UpdateAgentAsync(
			"agent_wire_001",
			new AgentUpdate(Prompt: "p"));

		var ex = (await act.Should().ThrowAsync<Core.Exceptions.ElevenLabsRateLimitException>()).Which;
		ex.HttpStatus.Should().Be(429);
		ex.Message.Should().Contain("slow down");
	}

	[Fact]
	public async Task ListAgentsAsync_sends_accept_json_header()
	{
		await using var server = new HttpTestServer();
		server.Enqueue(200, """{"agents":[]}""");
		var client = NewClient(server);

		_ = await client.ListAgentsAsync();

		server.Captured[0].Headers.Should().ContainKey("Accept");
		server.Captured[0].Headers["Accept"].Should().Contain("application/json");
	}
}

/// <summary>
/// Minimal <see cref="IOptionsMonitor{T}"/> wrapper for tests that
/// only need a static snapshot. Matches the shape of the real
/// Microsoft.Extensions.Options.OptionsMonitor without its
/// change-tracking machinery.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
	public StaticOptionsMonitor(T value) { CurrentValue = value; }
	public T CurrentValue { get; }
	public T Get(string? name) => CurrentValue;
	public IDisposable? OnChange(Action<T, string?> listener) => null;
}
