using Caliburn.Micro;
using ElevenLabsStudio.Core.MVVM;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class DynamicVariablesDialogViewModel : ScreenBase
{
	private string? _error;

	public DynamicVariablesDialogViewModel(string scenarioName, IEnumerable<DynamicVariableEntry> variables)
	{
		DisplayName = $"Dynamic variables — {scenarioName}";
		ScenarioName = scenarioName;
		DynamicVariables.AddRange(variables.Select(v => new DynamicVariableEntry(v.Key, v.Value)));
	}

	public string ScenarioName { get; }
	public BindableCollection<DynamicVariableEntry> DynamicVariables { get; } = new();
	public IReadOnlyList<DynamicVariableEntry>? Result { get; private set; }
	public string? Error
	{
		get => _error;
		private set => Set(ref _error, value);
	}

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
