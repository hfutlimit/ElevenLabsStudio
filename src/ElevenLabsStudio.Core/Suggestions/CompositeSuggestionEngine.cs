using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Suggestions;

/// <summary>
/// Fans <see cref="Analyze"/> out to every inner engine and returns the
/// concatenated result. Plug in any number of <see cref="ISuggestionEngine"/>
/// implementations to layer local rules with LLM-driven ideas.
/// </summary>
public sealed class CompositeSuggestionEngine(
    IEnumerable<ISuggestionEngine> engines) : ISuggestionEngine
{
    private readonly IReadOnlyList<ISuggestionEngine> _engines = engines.ToList();

    public IReadOnlyList<Suggestion> Analyze(Agent current, AgentUpdate proposed)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);

        var result = new List<Suggestion>();
        foreach (var engine in _engines)
        {
            result.AddRange(engine.Analyze(current, proposed));
        }
        return result;
    }
}