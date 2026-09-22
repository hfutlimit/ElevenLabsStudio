using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;

namespace ElevenLabsStudio.Core.Suggestions;

/// <summary>
/// Pure, deterministic rule-based suggestion engine. Runs locally only —
/// no network calls. Each rule examines the candidate <see cref="AgentUpdate"/>
/// against the current <see cref="Agent"/> and emits 0..N <see cref="Suggestion"/>s.
///
/// Why rules first (and not LLM): the user explicitly wants "local
/// suggestions before push"; deterministic rules make the workflow
/// reproducible, unit-testable, and free of API costs. A future
/// <c>LlmSuggestionEngine</c> can be plugged in alongside this one via
/// <see cref="CompositeSuggestionEngine"/>.
/// </summary>
public sealed class HeuristicSuggestionEngine : ISuggestionEngine
{
    public IReadOnlyList<Suggestion> Analyze(Agent current, AgentUpdate proposed)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);

        var output = new List<Suggestion>();

        if (proposed.Prompt is not null)
        {
            if (string.IsNullOrWhiteSpace(proposed.Prompt))
            {
                output.Add(new Suggestion(
                    FieldName: nameof(Agent.Prompt),
                    CurrentValue: current.Prompt,
                    ProposedValue: proposed.Prompt,
                    Rationale: "An empty prompt leaves the Agent's behavior undefined.",
                    Severity: SuggestionSeverity.Warning));
            }
            else if (proposed.Prompt.Length > 8000)
            {
                output.Add(new Suggestion(
                    FieldName: nameof(Agent.Prompt),
                    CurrentValue: current.Prompt,
                    ProposedValue: proposed.Prompt,
                    Rationale: "Prompts over 8,000 characters may be truncated or rejected by ElevenLabs.",
                    Severity: SuggestionSeverity.Warning));
            }
            else if (proposed.Prompt.Equals(current.Prompt, StringComparison.Ordinal))
            {
                output.Add(new Suggestion(
                    FieldName: nameof(Agent.Prompt),
                    CurrentValue: current.Prompt,
                    ProposedValue: proposed.Prompt,
                    Rationale: "The prompt is unchanged; there is nothing to push.",
                    Severity: SuggestionSeverity.Info));
            }
        }

        if (proposed.FirstMessage is not null
            && string.IsNullOrWhiteSpace(proposed.FirstMessage))
        {
            output.Add(new Suggestion(
                FieldName: nameof(Agent.FirstMessage),
                CurrentValue: current.FirstMessage,
                ProposedValue: proposed.FirstMessage,
                Rationale: "An empty first message means the Agent will not open the conversation proactively.",
                Severity: SuggestionSeverity.Suggestion));
        }

        if (proposed.VoiceId is not null && string.IsNullOrWhiteSpace(proposed.VoiceId))
        {
            output.Add(new Suggestion(
                FieldName: nameof(Agent.VoiceId),
                CurrentValue: current.VoiceId ?? string.Empty,
                ProposedValue: proposed.VoiceId,
                Rationale: "An empty VoiceId causes ElevenLabs to fall back to its default voice.",
                Severity: SuggestionSeverity.Suggestion));
        }

        if (proposed.Variables is not null)
        {
            foreach (var variable in proposed.Variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Name))
                {
                    output.Add(new Suggestion(
                        FieldName: $"Variable:{variable.Name}",
                        CurrentValue: variable.Value ?? string.Empty,
                        ProposedValue: variable.Value ?? string.Empty,
                        Rationale: "Variable names cannot be empty.",
                        Severity: SuggestionSeverity.Warning));
                }
            }
        }

        return output;
    }
}
