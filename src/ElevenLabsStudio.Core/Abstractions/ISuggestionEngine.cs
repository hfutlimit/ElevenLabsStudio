using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Abstractions;

/// <summary>
/// Local-only analysis that turns the current <see cref="Agent"/> + a
/// candidate <see cref="AgentUpdate"/> into a set of <see cref="Suggestion"/>
/// entries the UI can present before the user confirms the push.
/// Implementations must NOT call out to the network — keeping the
/// suggestion step synchronous and offline-friendly makes the "local
/// suggestion" workflow deterministic and cheap.
/// </summary>
public interface ISuggestionEngine
{
    IReadOnlyList<Suggestion> Analyze(Agent current, AgentUpdate proposed);
}