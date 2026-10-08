using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElevenLabsStudio.ViewModels.AgentDetail;

namespace ElevenLabsStudio.Services;

public interface IDynamicVariableStore
{
	bool TryLoad(string scenarioKey, out IReadOnlyList<DynamicVariableEntry> variables);

	void Save(string scenarioKey, IReadOnlyList<DynamicVariableEntry> variables);
}

/// <summary>
/// Per-user JSON file of edited scenario parameters. Built-in scenario
/// defaults stay in code; this file only appears after a save.
/// </summary>
public sealed class JsonDynamicVariableStore : IDynamicVariableStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private readonly string _path;

	public JsonDynamicVariableStore(string? path = null)
	{
		_path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
	}

	public static string DefaultPath { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"ElevenLabsStudio",
		"dynamic-variables.json");

	public bool TryLoad(string scenarioKey, out IReadOnlyList<DynamicVariableEntry> variables)
	{
		variables = [];
		if (!File.Exists(_path)) return false;
		DynamicVariableFile file;
		try
		{
			file = JsonSerializer.Deserialize<DynamicVariableFile>(File.ReadAllText(_path), JsonOptions)
				?? new DynamicVariableFile();
		}
		catch (JsonException)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}

		if (file.Scenarios is null || !file.Scenarios.TryGetValue(scenarioKey, out var rows) || rows is null)
			return false;
		variables = rows
			.Where(row => !string.IsNullOrWhiteSpace(row.Key))
			.Select(row => new DynamicVariableEntry(row.Key.Trim(), row.Value ?? string.Empty))
			.ToArray();
		return variables.Count > 0;
	}

	public void Save(string scenarioKey, IReadOnlyList<DynamicVariableEntry> variables)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scenarioKey);
		var file = new DynamicVariableFile();
		if (File.Exists(_path))
		{
			try
			{
				file = JsonSerializer.Deserialize<DynamicVariableFile>(File.ReadAllText(_path), JsonOptions)
					?? new DynamicVariableFile();
			}
			catch (JsonException)
			{
				file = new DynamicVariableFile();
			}
		}

		file.Scenarios ??= new Dictionary<string, List<DynamicVariableRow>>(StringComparer.Ordinal);
		file.Scenarios[scenarioKey] = variables
			.Select(v => new DynamicVariableRow { Key = v.Key, Value = v.Value })
			.ToList();

		var directory = Path.GetDirectoryName(_path);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);
		File.WriteAllText(_path, JsonSerializer.Serialize(file, JsonOptions));
	}

	private sealed class DynamicVariableFile
	{
		public Dictionary<string, List<DynamicVariableRow>> Scenarios { get; set; } =
			new(StringComparer.Ordinal);
	}

	private sealed class DynamicVariableRow
	{
		public string Key { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
	}
}
