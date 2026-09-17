namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// Read-only summary of an Agent's workflow graph. v0.1 is intentionally
/// shallow: a list of nodes plus the raw JSON the server returned so the
/// user can inspect / diff the full shape without us committing to a
/// complete parser. Future versions can widen this as the workflow
/// editing surface matures.
/// </summary>
public sealed record Workflow(
    IReadOnlyList<WorkflowNode> Nodes,
    string? RawJson);

public sealed record WorkflowNode(
    string Id,
    string Type,
    string Name);

public static class WorkflowDefaults
{
    public static readonly Workflow Empty = new(
        Nodes: Array.Empty<WorkflowNode>(),
        RawJson: null);
}