using Caliburn.Micro;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Editable workflow inspector. Surfaces the workflow nodes as a
/// bindable collection the DataGrid can edit in place. Add / Remove
/// buttons let the user build new node sequences; <see cref="GetCurrentNodes"/>
/// returns the in-memory edits so <see cref="AgentDetailViewModel.Push"/>
/// can hand them to <c>UpdateAgentAsync</c>.
/// </summary>
public sealed class WorkflowTabViewModel : ScreenBase
{
    private readonly ILogger<WorkflowTabViewModel> _logger;

    public BindableCollection<WorkflowNode> Nodes { get; } = new();

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

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