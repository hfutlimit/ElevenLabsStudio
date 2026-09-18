using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

public sealed class FirstMessageTabViewModel : ScreenBase
{
    private readonly Agent _agent;
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
            }
        }
    }

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

    public void RefreshFrom(Agent updated)
    {
        if (_firstMessage == _agent.FirstMessage)
        {
            FirstMessage = updated.FirstMessage;
        }
        else
        {
            RecomputeSuggestions();
        }
    }

    private void RecomputeSuggestions()
    {
        var update = new AgentUpdate(FirstMessage: _firstMessage);
        var found = _suggestions.Analyze(_agent, update);
        Suggestions.Clear();
        Suggestions.AddRange(found);
    }

    /// <summary>See SystemPromptTabViewModel.RecomputeSuggestionsPublic.</summary>
    public void RecomputeSuggestionsPublic() => RecomputeSuggestions();
}