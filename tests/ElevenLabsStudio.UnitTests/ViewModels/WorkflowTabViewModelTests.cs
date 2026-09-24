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

		var vm = new WorkflowTabViewModel(BuildAgent(workflow));

		vm.Nodes.Should().HaveCount(2);
		vm.Nodes[0].X.Should().Be(10);
		vm.Nodes[1].Y.Should().Be(20);
		vm.Edges.Should().ContainSingle(edge =>
			edge.Source == "start" && edge.Target == "answer");
	}

	[Fact]
	public void Server_nodes_with_near_identical_positions_do_not_render_stacked()
	{
		var workflow = new Workflow(
			new[]
			{
				new WorkflowNode("a", "start", "First", 10, 20),
				new WorkflowNode("b", "tool", "Second", 10, 20),
			},
			null,
			Array.Empty<WorkflowEdge>());

		var vm = new WorkflowTabViewModel(BuildAgent(workflow));

		// Cards render 236x64: no pair may overlap on both axes.
		var first = vm.CanvasNodes.Single(node => node.Id == "a");
		var second = vm.CanvasNodes.Single(node => node.Id == "b");
		var overlapsHorizontally = Math.Abs(first.X - second.X) < 236;
		var overlapsVertically = Math.Abs(first.Y - second.Y) < 64;

		(overlapsHorizontally && overlapsVertically).Should().BeFalse(
			"nodes whose server coordinates are near-identical must be nudged apart on the board");
	}

	[Fact]
	public void Selecting_an_edge_classifies_its_condition_and_carries_the_prompt_text()
	{
		var workflow = new Workflow(
			new[]
			{
				new WorkflowNode("start", "start", "Greeting", 10, 20),
				new WorkflowNode("answer", "override_agent", "Answer", 180, 20),
				new WorkflowNode("fail", "override_agent", "Failure", 360, 20),
			},
			null,
			new[]
			{
				new WorkflowEdge("llm-edge", "start", "answer",
					"Transition politely.", "llm", null, "Greeting done"),
				new WorkflowEdge("result-edge", "answer", "fail",
					null, "result", false, null),
			});

		var vm = new WorkflowTabViewModel(BuildAgent(workflow));

		vm.SelectEdgeById("result-edge");
		vm.HasSelectedEdge.Should().BeTrue();
		vm.SelectedEdge!.ConditionKind.Should().Be("Tool result · failed");

		vm.SelectEdgeById("llm-edge");
		vm.SelectedEdge!.ConditionKind.Should().Be("LLM condition");
		vm.SelectedEdge.ConditionText.Should().Be("Transition politely.");
		vm.SelectedEdge.ConditionLabel.Should().Be("Greeting done");
		vm.HasSelectedNode.Should().BeFalse();

		vm.ClearSelection();
		vm.HasInspector.Should().BeFalse();
	}

	[Fact]
	public void Selecting_a_node_marks_the_projection_lists_connections_and_renames()
	{
		var workflow = new Workflow(
			new[]
			{
				new WorkflowNode("start", "start", "Greeting", 10, 20),
				new WorkflowNode("answer", "override_agent", "Answer", 180, 20),
				new WorkflowNode("fail", "override_agent", "Failure", 360, 20),
			},
			null,
			new[]
			{
				new WorkflowEdge("edge-1", "start", "answer", null, "unconditional"),
				new WorkflowEdge("edge-2", "answer", "fail", null, "result", false),
			});

		var vm = new WorkflowTabViewModel(BuildAgent(workflow));

		vm.SelectNodeById("answer");
		vm.HasSelectedNode.Should().BeTrue();
		vm.CanvasNodes.Single(node => node.Id == "answer").IsSelected.Should().BeTrue();
		vm.SelectedNodeConnections.Should().Contain("← Greeting · Unconditional");
		vm.SelectedNodeConnections.Should().Contain("→ Failure · Tool result · failed");

		vm.SelectedNodeName = "Renamed";
		vm.Nodes.Single(node => node.Id == "answer").Name.Should().Be("Renamed");
		vm.IsDirty.Should().BeTrue();
		vm.CanvasNodes.Single(node => node.Id == "answer").DisplayName.Should().Be("Renamed");

		vm.ClearSelection();
		vm.HasSelectedNode.Should().BeFalse();
		vm.HasInspector.Should().BeFalse();
	}

	[Fact]
	public void Unlabelled_nodes_fall_back_to_a_friendly_type_name()
	{
		var workflow = new Workflow(
			new[]
			{
				new WorkflowNode("tool-1", "tool", "", 10, 20),
				new WorkflowNode("start-1", "start", " ", 10, 120),
			},
			null,
			Array.Empty<WorkflowEdge>());

		var vm = new WorkflowTabViewModel(BuildAgent(workflow));

		vm.CanvasNodes.Single(node => node.Id == "tool-1").DisplayName.Should().Be("Tool");
		vm.CanvasNodes.Single(node => node.Id == "start-1").DisplayName.Should().Be("Start");
	}

	private static Agent BuildAgent(Workflow workflow) => new(
		"agent_test",
		"Test",
		"prompt",
		"hello",
		null,
		Array.Empty<Variable>(),
		workflow,
		DateTimeOffset.UtcNow);
}
