using Caliburn.Micro;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Editable workflow inspector. Mirrors the Variables tab: a
/// DataGrid-bound <see cref="BindableCollection{WorkflowNode}"/> the
/// user can add / remove / rename inline, plus an
/// <see cref="GetCurrentNodes"/> snapshot so
/// <c>AgentDetailViewModel.Push</c> can fold the local edits into a
/// single update.
/// </summary>
public sealed class WorkflowTabViewModel : ScreenBase
{
    private readonly ILogger<WorkflowTabViewModel> _logger;

    public BindableCollection<WorkflowNode> Nodes { get; } = new();

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        internal set => Set(ref _isDirty, value);
    }

    public WorkflowTabViewModel(Agent agent)
        : this(agent, NullLogger<WorkflowTabViewModel>.Instance) { }

    public WorkflowTabViewModel(Agent agent, ILogger<WorkflowTabViewModel> logger)
    {
        _logger = logger;
        Apply(agent);
    }

    public void RefreshFrom(Agent updated) => Apply(updated);

    public void AddNode()
    {
        var nextId = $"n{Nodes.Count + 1}";
        Nodes.Add(new WorkflowNode(nextId, "custom", "New Node"));
        IsDirty = true;
    }

    public void RemoveNode(WorkflowNode? node)
    {
        if (node is null) return;
        Nodes.Remove(node);
        IsDirty = true;
    }

    public IReadOnlyList<WorkflowNode> GetCurrentNodes() => Nodes.ToList();

    private void Apply(Agent agent)
    {
        Nodes.Clear();
        foreach (var n in agent.Workflow.Nodes) Nodes.Add(n);
        IsDirty = false;
    }
}