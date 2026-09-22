using ElevenLabsStudio.Infrastructure.Http.Dto;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

// 'Mapping' here means DTO→Domain mapping, NOT the Caliburn.Micro class.
// We use a fully-qualified call to the Infrastructure mapping adapter
// because this namespace also defines a folder-level class 'Mapping'.
using static ElevenLabsStudio.Infrastructure.Http.Mapping.Mapping;

namespace ElevenLabsStudio.UnitTests.Mapping;

/// <summary>
/// Locks down the DTO → Domain mapping so wire format changes do not
/// silently break the UI.
/// </summary>
public sealed class AgentMappingTests
{
	[Fact]
	public void Maps_full_agent_dto_into_domain()
	{
		var dto = new ElevenLabsAgentDto
		{
			AgentId = "agent_abc",
			Name = "Sales Rep",
			ConversationConfig = new ElevenLabsConversationConfigDto
			{
				Agent = new ElevenLabsConversationAgentDto
				{
					Prompt = new ElevenLabsPromptDto
					{
						Text = "Act as a polite sales rep.",
						Variables = new()
						{
							new ElevenLabsVariableDto { Name = "region", Value = "CN", Type = "string" },
						},
					},
					FirstMessage = "Hello, how can I help?",
				},
				Tts = new ElevenLabsTtsDto { VoiceId = "voice_xyz" },
			},
			Workflow = JsonNode.Parse("""
				{
					"nodes": {
					"n1": { "type": "start", "label": "Greeting", "position": { "x": 10, "y": 20 } },
					"n2": { "type": "llm", "label": "Answer", "position": { "x": 120, "y": 20 } }
					},
					"edges": {
						"e1": { "source": "n1", "target": "n2", "forward_condition": { "type": "llm", "condition": "always" } }
					}
				}
				""")!.AsObject(),
			Metadata = new ElevenLabsMetadataDto
			{
				UpdatedAt = DateTimeOffset.UtcNow,
			},
		};

		var agent = MapToAgent(dto);

		agent.AgentId.Should().Be("agent_abc");
		agent.Name.Should().Be("Sales Rep");
		agent.Prompt.Should().Be("Act as a polite sales rep.");
		agent.FirstMessage.Should().Be("Hello, how can I help?");
		agent.VoiceId.Should().Be("voice_xyz");
		agent.Variables.Should().HaveCount(1);
		agent.Variables[0].Name.Should().Be("region");
		agent.Variables[0].Value.Should().Be("CN");
		agent.Workflow.Nodes.Should().HaveCount(2);
		agent.Workflow.Nodes[0].Id.Should().Be("n1");
		agent.Workflow.Nodes[0].X.Should().Be(10);
		agent.Workflow.Nodes[0].Y.Should().Be(20);
		agent.Workflow.Edges.Should().ContainSingle();
		agent.Workflow.Edges[0].Source.Should().Be("n1");
		agent.Workflow.Edges[0].Target.Should().Be("n2");
		agent.Workflow.Edges[0].Condition.Should().Be("always");
		agent.Workflow.RawJson.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Fills_missing_first_message_with_empty_string()
	{
		var dto = new ElevenLabsAgentDto
		{
			AgentId = "agent_1",
			Name = "X",
			ConversationConfig = new ElevenLabsConversationConfigDto
			{
				Agent = new ElevenLabsConversationAgentDto
				{
					Prompt = new ElevenLabsPromptDto { Text = "p" },
					FirstMessage = null,
				},
			},
		};

		var agent = MapToAgent(dto);

		agent.FirstMessage.Should().BeEmpty();
		agent.Workflow.Nodes.Should().BeEmpty();
	}

	[Fact]
	public void Empty_workflow_when_dto_is_null()
	{
		var dto = new ElevenLabsAgentDto
		{
			AgentId = "agent_nowf",
			Name = "No Workflow",
			ConversationConfig = new ElevenLabsConversationConfigDto(),
			Workflow = null,
		};

		var agent = MapToAgent(dto);

		agent.Workflow.Nodes.Should().BeEmpty();
		agent.Workflow.RawJson.Should().BeNull();
	}
}
