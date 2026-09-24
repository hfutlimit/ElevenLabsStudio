namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// A workflow graph together with the original wire document used for
/// lossless updates. The typed members contain only the graph data needed by
/// the editor; unknown server fields remain in <see cref="RawJson"/>.
/// </summary>
public sealed record Workflow
{
	public IReadOnlyList<WorkflowNode> Nodes { get; init; }
	public string? RawJson { get; init; }
	public IReadOnlyList<WorkflowEdge> Edges { get; init; }

	public Workflow(
		IReadOnlyList<WorkflowNode> Nodes,
		string? RawJson,
		IReadOnlyList<WorkflowEdge>? Edges = null)
	{
		this.Nodes = Nodes ?? Array.Empty<WorkflowNode>();
		this.RawJson = RawJson;
		this.Edges = Edges ?? Array.Empty<WorkflowEdge>();
	}
}

/// <summary>
/// A workflow node's server-provided canvas position is optional because
/// older or partially configured workflows may not include a position.
/// </summary>
public sealed record WorkflowNode(
	string Id,
	string Type,
	string Name,
	double? X = null,
	double? Y = null);

/// <summary>
/// A workflow edge. <see cref="Condition"/> carries the decision text
/// (the LLM prompt or expression body); <see cref="ConditionType"/> is
/// the server's discriminator ("llm", "result", "unconditional",
/// "expression") and <see cref="ConditionSuccessful"/> applies to
/// "result" branches.
/// </summary>
public sealed record WorkflowEdge(
	string Id,
	string Source,
	string Target,
	string? Condition = null,
	string? ConditionType = null,
	bool? ConditionSuccessful = null,
	string? ConditionLabel = null);

public static class WorkflowDefaults
{
	public static readonly Workflow Empty = new(
		Nodes: Array.Empty<WorkflowNode>(),
		RawJson: null,
		Edges: Array.Empty<WorkflowEdge>());
}
