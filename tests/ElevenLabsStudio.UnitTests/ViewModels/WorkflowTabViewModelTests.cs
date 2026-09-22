using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class WorkflowTabViewModelTests
{
	[Fact]
	public void Exposes_positioned_nodes_and_edges_for_canvas_rendering()
	{
		var workflow = new Workflow(
			new[]
			{
				new WorkflowNode("start", "start", "Greeting", 10, 20),
				new WorkflowNode("answer", "conversation", "Answer", 180, 20),
			},
			"""{"nodes":{},"edges":{}}""",
			new[] { new WorkflowEdge("edge-1", "start", "answer", "always") });
		var agent = new Agent(
			"agent_test",
			"Test",
			"prompt",
			"hello",
			null,
			Array.Empty<Variable>(),
			workflow,
			DateTimeOffset.UtcNow);

		var vm = new WorkflowTabViewModel(agent);

		vm.Nodes.Should().HaveCount(2);
		vm.Nodes[0].X.Should().Be(10);
		vm.Nodes[1].Y.Should().Be(20);
		vm.Edges.Should().ContainSingle(edge =>
			edge.Source == "start" && edge.Target == "answer");
	}
}
