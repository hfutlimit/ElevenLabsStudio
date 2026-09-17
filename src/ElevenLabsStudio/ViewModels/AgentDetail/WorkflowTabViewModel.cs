using Caliburn.Micro;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// v0.1 read-only workflow inspector. Surfaces the workflow nodes as a
/// flat list and the raw JSON underneath so the user can see exactly
/// what the server has without us committing to a full graph parser.
/// </summary>
public sealed class WorkflowTabViewModel : ScreenBase
{
    public BindableCollection<WorkflowNode> Nodes { get; } = new();

    private string _rawJson = string.Empty;
    public string RawJson
    {
        get => _rawJson;
        private set => Set(ref _rawJson, value);
    }

    private readonly Agent _agent;

    public Agent Agent => _agent;

    public WorkflowTabViewModel(Agent agent)
    {
        _agent = agent;
        Apply(agent);
    }

    public void RefreshFrom(Agent updated) => Apply(updated);

    private void Apply(Agent agent)
    {
        Nodes.Clear();
        Nodes.AddRange(agent.Workflow.Nodes);
        RawJson = agent.Workflow.RawJson ?? "(no workflow on this agent)";
    }
}