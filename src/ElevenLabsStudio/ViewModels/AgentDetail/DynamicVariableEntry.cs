using Caliburn.Micro;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class DynamicVariableEntry : PropertyChangedBase
{
	private string _key;
	private string _value;

	public DynamicVariableEntry(string key, string value)
	{
		_key = key;
		_value = value;
	}

	public string Key
	{
		get => _key;
		set => Set(ref _key, value);
	}

	public string Value
	{
		get => _value;
		set => Set(ref _value, value);
	}
}
