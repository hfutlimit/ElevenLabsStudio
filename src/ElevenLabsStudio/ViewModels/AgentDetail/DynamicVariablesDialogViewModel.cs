using Caliburn.Micro;
using ElevenLabsStudio.Core.MVVM;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class DynamicVariablesDialogViewModel : ScreenBase
{
	private string? _error;
	private int _sequence;

	public DynamicVariablesDialogViewModel(string scenarioName, IEnumerable<DynamicVariableEntry> variables)
	{
		DisplayName = $"Dynamic variables — {scenarioName}";
		ScenarioName = scenarioName;
		DynamicVariables.AddRange(variables.Select(v => new DynamicVariableEntry(v.Key, v.Value)));
	}

	public string ScenarioName { get; }
	public string BranchId { get; set; } = string.Empty;
	public string Environment { get; set; } = "production";
	public BindableCollection<DynamicVariableEntry> DynamicVariables { get; } = new();
	public IReadOnlyList<DynamicVariableEntry>? Result { get; private set; }
	public string? Error
	{
		get => _error;
		private set => Set(ref _error, value);
	}

	public void AddDynamicVariable()
	{
		string key;
		do { key = $"variable_{++_sequence}"; }
		while (DynamicVariables.Any(v => v.Key.Trim() == key));
		DynamicVariables.Add(new DynamicVariableEntry(key, string.Empty));
	}

	public void RemoveDynamicVariable(DynamicVariableEntry variable) => DynamicVariables.Remove(variable);

	public async Task SaveAsync()
	{
		Error = null;
		var keys = new HashSet<string>(StringComparer.Ordinal);
		foreach (var variable in DynamicVariables)
		{
			if (string.IsNullOrWhiteSpace(variable.Key))
			{
				Error = "Variable names cannot be empty.";
				return;
			}
			if (!keys.Add(variable.Key.Trim()))
			{
				Error = $"Variable '{variable.Key.Trim()}' is duplicated.";
				return;
			}
		}
		Result = DynamicVariables.Select(v => new DynamicVariableEntry(v.Key.Trim(), v.Value)).ToArray();
		await TryCloseAsync(true);
	}

	public Task CancelAsync() => TryCloseAsync(false);
}
