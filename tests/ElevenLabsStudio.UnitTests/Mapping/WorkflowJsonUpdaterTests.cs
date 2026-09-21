using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Infrastructure.Http.Mapping;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.Mapping;

public sealed class WorkflowJsonUpdaterTests
{
	private const string RawWorkflow = """
		{
			"nodes": {
			"start": {
				"type": "start",
				"label": "Greeting",
				"position": { "x": 10, "y": 20 },
				"future": true
			},
			"answer": { "type": "conversation", "label": "Answer" }
			},
			"edges": {
			"edge-1": { "source": "start", "target": "answer", "condition": "always" }
			},
			"future_graph_field": { "keep": 1 }
		}
		""";

	[Fact]
	public void Apply_changes_owned_node_fields_and_preserves_graph_data()
	{
		var edit = new Workflow(
			new[]
			{
				new WorkflowNode("start", "start", "Welcome"),
				new WorkflowNode("answer", "conversation", "Answer"),
			},
			RawWorkflow);

		var result = WorkflowJsonUpdater.Apply(edit);

		result["nodes"]!["start"]!["label"]!.GetValue<string>().Should().Be("Welcome");
		result["nodes"]!["start"]!["position"]!["x"]!.GetValue<int>().Should().Be(10);
		result["nodes"]!["start"]!["future"]!.GetValue<bool>().Should().BeTrue();
		result["edges"]!["edge-1"]!["condition"]!.GetValue<string>().Should().Be("always");
		result["future_graph_field"]!["keep"]!.GetValue<int>().Should().Be(1);
	}

	[Fact]
	public void Apply_rejects_removing_a_node_that_an_edge_references()
	{
		var edit = new Workflow(
			new[] { new WorkflowNode("start", "start", "Greeting") },
			RawWorkflow);

		var act = () => WorkflowJsonUpdater.Apply(edit);

		act.Should().Throw<WorkflowUpdateException>();
	}

	[Fact]
	public void Apply_rejects_invalid_raw_json()
	{
		var edit = new Workflow(Array.Empty<WorkflowNode>(), "not-json");

		var act = () => WorkflowJsonUpdater.Apply(edit);

		act.Should().Throw<WorkflowUpdateException>();
	}
}
