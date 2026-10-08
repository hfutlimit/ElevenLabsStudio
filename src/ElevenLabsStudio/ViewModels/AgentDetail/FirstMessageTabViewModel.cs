using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class FirstMessageTabViewModel : ScreenBase
{
	public const int LengthWarnAt = 500;

	private Agent _agent;
	private readonly ISuggestionEngine _suggestions;
	private readonly ILogger _logger;

	public BindableCollection<Suggestion> Suggestions { get; } = new();

	private string _firstMessage;
	public string FirstMessage
	{
		get => _firstMessage;
		set
		{
			if (Set(ref _firstMessage, value))
			{
				RecomputeSuggestions();
				NotifyOfPropertyChange(nameof(HasLocalEdits));
				NotifyLength();
			}
		}
	}

	public string FirstMessageLength => EditorLength.Text(_firstMessage.Length);

	public string FirstMessageLengthBrushKey =>
		EditorLength.BrushKey(_firstMessage.Length, LengthWarnAt);

	/// <summary>See SystemPromptTabViewModel.HasLocalEdits.</summary>
	public bool HasLocalEdits =>
		!string.Equals(_firstMessage, _agent.FirstMessage, StringComparison.Ordinal);

	public FirstMessageTabViewModel(
		Agent agent,
		ISuggestionEngine suggestions,
		ILogger logger)
	{
		_agent = agent;
		_suggestions = suggestions;
		_logger = logger;
		_firstMessage = agent.FirstMessage;
	}

	public void RefreshFrom(Agent updated, bool preserveLocalEdits = true)
	{
		var isDirty = _firstMessage != _agent.FirstMessage;
		_agent = updated;
		if (!preserveLocalEdits || !isDirty)
		{
			FirstMessage = updated.FirstMessage;
		}
		RecomputeSuggestions();
	}

	/// <summary>See SystemPromptTabViewModel.ResetToServer.</summary>
	public void ResetToServer() => FirstMessage = _agent.FirstMessage;

	private void NotifyLength()
	{
		NotifyOfPropertyChange(nameof(FirstMessageLength));
		NotifyOfPropertyChange(nameof(FirstMessageLengthBrushKey));
	}

	private void RecomputeSuggestions()
	{
		var update = new AgentUpdate(FirstMessage: _firstMessage);
		var found = _suggestions.Analyze(_agent, update);
		Suggestions.Clear();
		Suggestions.AddRange(found);
		// The collection mutates in place, so Count-dependent bindings
		// need an explicit nudge.
		NotifyOfPropertyChange(nameof(Suggestions));
	}

	/// <summary>See SystemPromptTabViewModel.RecomputeSuggestionsPublic.</summary>
	public void RecomputeSuggestionsPublic() => RecomputeSuggestions();
}
