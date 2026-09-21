using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Infrastructure.Mock;
using FluentAssertions;
using Xunit;

namespace ElevenLabsStudio.UnitTests.Mock;

/// <summary>
/// Behavioural contract for the offline <see cref="MockElevenLabsClient"/>:
/// three agents, three conversations per agent, voice list, and the
/// 404-shaped exception the throw site uses for unknown ids.
/// </summary>
public sealed class MockElevenLabsClientTests
{
	private readonly MockElevenLabsClient _client = new();

	[Fact]
	public async Task ListAgentsAsync_returns_three_known_agents()
	{
		var agents = await _client.ListAgentsAsync();

		agents.Should().HaveCount(3);
		agents.Select(a => a.AgentId).Should().BeEquivalentTo(new[]
		{
			"agent_sales_001",
			"agent_support_002",
			"agent_onboarding_003",
		});
		agents.Should().AllSatisfy(a =>
		{
			a.Name.Should().NotBeNullOrWhiteSpace();
			a.VoiceId.Should().NotBeNullOrWhiteSpace();
			a.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(-48));
		});
	}

	[Fact]
	public async Task ListAgentsAsync_agents_carry_workflow_nodes()
	{
		var sales = await _client.GetAgentAsync("agent_sales_001");

		sales.Workflow.Nodes.Should().NotBeEmpty();
		sales.Workflow.Nodes.Should().Contain(n => n.Type == "greeting");
		sales.Workflow.Nodes.Should().Contain(n => n.Type == "llm");
		sales.Workflow.RawJson.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task GetAgentAsync_returns_known_agent()
	{
		var sales = await _client.GetAgentAsync("agent_sales_001");
		sales.AgentId.Should().Be("agent_sales_001");
		sales.Name.Should().Be("Sales Rep");
	}

	[Fact]
	public async Task GetAgentAsync_throws_404_for_unknown_id()
	{
		var act = () => _client.GetAgentAsync("agent_does_not_exist");

		await act.Should().ThrowAsync<ElevenLabsException>()
			.Where(ex => ex.HttpStatus == 404);
	}

	[Fact]
	public async Task ListConversationsAsync_filters_per_agent()
	{
		var salesConvs = await _client.ListConversationsAsync("agent_sales_001");
		var supportConvs = await _client.ListConversationsAsync("agent_support_002");
		var onboardingConvs = await _client.ListConversationsAsync("agent_onboarding_003");

		salesConvs.Should().HaveCount(3);
		supportConvs.Should().HaveCount(1);
		onboardingConvs.Should().HaveCount(1);
		salesConvs.Should().AllSatisfy(c =>
		{
			c.AgentId.Should().Be("agent_sales_001");
			c.Turns.Should().BeEmpty();
		});
		var detail = await _client.GetConversationAsync(salesConvs[0].ConversationId);
		detail.Turns.Should().NotBeEmpty();
		detail.Turns.First().Speaker.Should().Be("agent");
	}

	[Fact]
	public async Task ListConversationsAsync_unknown_agent_returns_empty()
	{
		var empty = await _client.ListConversationsAsync("agent_never_existed");
		empty.Should().BeEmpty();
	}

	[Fact]
	public async Task UpdateAgentAsync_merges_only_nonnull_fields()
	{
		var snapshot = await _client.UpdateAgentAsync(
			"agent_sales_001",
			new Core.Domain.AgentUpdate(Prompt: null, FirstMessage: "Hi there!"));

		snapshot.FirstMessage.Should().Be("Hi there!");
		// Prompt left null -> preserved from the original mock.
		snapshot.Prompt.Should().StartWith("You are a polite sales rep");
		// UpdatedAt should be bumped to "now".
		snapshot.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task UpdateAgentAsync_replaces_workflow_nodes_when_supplied()
	{
		var newNodes = new[]
		{
			new Core.Domain.WorkflowNode("a1", "llm", "Greeting"),
			new Core.Domain.WorkflowNode("a2", "tool", "Lookup Order"),
			new Core.Domain.WorkflowNode("a3", "end", "Wrap up"),
		};

		var snapshot = await _client.UpdateAgentAsync(
			"agent_sales_001",
			new Core.Domain.AgentUpdate(
				Workflow: new Core.Domain.Workflow(newNodes, """{"nodes":{},"edges":{}}""")));

		snapshot.Workflow.Nodes.Should().HaveCount(3);
		snapshot.Workflow.Nodes.Should().BeEquivalentTo(newNodes, opts => opts.WithStrictOrdering());
		snapshot.Workflow.Nodes[1].Type.Should().Be("tool");
	}

	[Fact]
	public async Task UpdateAgentAsync_preserves_existing_nodes_when_workflow_not_supplied()
	{
		var snapshot = await _client.UpdateAgentAsync(
			"agent_sales_001",
			new Core.Domain.AgentUpdate(FirstMessage: "kept"));

		snapshot.FirstMessage.Should().Be("kept");
		// Workflow.Nodes untouched because Workflow was left null.
		snapshot.Workflow.Nodes.Should().NotBeEmpty();
		snapshot.Workflow.Nodes.Should().Contain(n => n.Id == "n1" && n.Type == "greeting");
	}

	[Fact]
	public async Task UpdateAgentAsync_replaces_variables_when_supplied()
	{
		var newVars = new[]
		{
			new Core.Domain.Variable("region", "JP", "string"),
			new Core.Domain.Variable("industry", "fintech", "string"),
		};

		var snapshot = await _client.UpdateAgentAsync(
			"agent_sales_001",
			new Core.Domain.AgentUpdate(Variables: newVars));

		snapshot.Variables.Should().HaveCount(2);
		snapshot.Variables[0].Name.Should().Be("region");
		snapshot.Variables[0].Value.Should().Be("JP");
		snapshot.Variables[1].Name.Should().Be("industry");
	}

	[Fact]
	public async Task ListVoicesAsync_returns_three_premade_voices()
	{
		var voices = await _client.ListVoicesAsync();

		voices.Should().HaveCount(3);
		voices.Select(v => v.VoiceId).Should().BeEquivalentTo(new[]
		{
			"voice_aria",
			"voice_onyx",
			"voice_chloe",
		});
		voices.Should().AllSatisfy(v => v.Category.Should().Be("premade"));
	}
}
