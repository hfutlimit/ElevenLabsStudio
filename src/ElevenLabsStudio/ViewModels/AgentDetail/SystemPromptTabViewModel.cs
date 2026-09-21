using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Owns the editable copy of <see cref="Agent.Prompt"/> and the local
/// suggestion list produced by <see cref="ISuggestionEngine"/>. The actual
/// push happens at the parent <see cref="AgentDetailViewModel"/> level so
/// the four tab VMs can be merged into a single update.
/// </summary>
public sealed class SystemPromptTabViewModel : ScreenBase
{
	private Agent _agent;
	private readonly ISuggestionEngine _suggestions;
	private readonly ILogger _logger;

	public BindableCollection<Suggestion> Suggestions { get; } = new();

	private string _prompt;
	public string Prompt
	{
		get => _prompt;
		set
		{
			if (Set(ref _prompt, value))
			{
				RecomputeSuggestions();
			}
		}
	}

	public Agent Agent => _agent;

	public SystemPromptTabViewModel(
		Agent agent,
		ISuggestionEngine suggestions,
		ILogger logger)
	{
		_agent = agent;
		_suggestions = suggestions;
		_logger = logger;
		_prompt = agent.Prompt;
	}

	public void RefreshFrom(Agent updated, bool preserveLocalEdits = true)
	{
		var isDirty = _prompt != _agent.Prompt;
		_agent = updated;
		if (!preserveLocalEdits || !isDirty)
		{
			Prompt = updated.Prompt;
		}
		RecomputeSuggestions();
	}

	private void RecomputeSuggestions()
	{
		var update = new AgentUpdate(Prompt: _prompt);
		var found = _suggestions.Analyze(_agent, update);
		Suggestions.Clear();
		Suggestions.AddRange(found);
	}

	/// <summary>Public entry point so the parent VM can force a
	/// re-evaluation (e.g. the Dry-run button) without going through
	/// the property setter, which would no-op when the local text
	/// already matches the field.</summary>
	public void RecomputeSuggestionsPublic() => RecomputeSuggestions();
}
