using Caliburn.Micro;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Editable variable inspector. Mirrors the Workflow tab: a
/// DataGrid-bound <see cref="BindableCollection{Variable}"/> for the
/// user to add / remove / rename agent prompt variables, plus an
/// <see cref="GetCurrentVariables"/> snapshot so
/// <c>AgentDetailViewModel.Push</c> can fold the local edits into a
/// single <c>AgentUpdate</c>.
/// </summary>
public sealed class VariablesTabViewModel : ScreenBase
{
    private readonly ILogger<VariablesTabViewModel> _logger;

    public BindableCollection<Variable> Variables { get; } = new();

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public VariablesTabViewModel(Agent agent)
        : this(agent, NullLogger<VariablesTabViewModel>.Instance) { }

    public VariablesTabViewModel(Agent agent, ILogger<VariablesTabViewModel> logger)
    {
        _logger = logger;
        Apply(agent);
    }

    public void RefreshFrom(Agent updated) => Apply(updated);

    public void AddVariable()
    {
        var nextName = $"var{Variables.Count + 1}";
        Variables.Add(new Variable(nextName, string.Empty, "string"));
        IsDirty = true;
    }

    public void RemoveVariable(Variable? variable)
    {
        if (variable is null) return;
        Variables.Remove(variable);
        IsDirty = true;
    }

    public IReadOnlyList<Variable> GetCurrentVariables() => Variables.ToList();

    private void Apply(Agent agent)
    {
        Variables.Clear();
        foreach (var v in agent.Variables) Variables.Add(v);
        IsDirty = false;
    }
}