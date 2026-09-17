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
                    Rationale: "提示词为空会导致 Agent 行为完全未定义。",
                    Severity: SuggestionSeverity.Warning));
            }
            else if (proposed.Prompt.Length > 8000)
            {
                output.Add(new Suggestion(
                    FieldName: nameof(Agent.Prompt),
                    CurrentValue: current.Prompt,
                    ProposedValue: proposed.Prompt,
                    Rationale: "提示词超过 8000 字符，ElevenLabs 将截断或拒绝。",
                    Severity: SuggestionSeverity.Warning));
            }
            else if (proposed.Prompt.Equals(current.Prompt, StringComparison.Ordinal))
            {
                output.Add(new Suggestion(
                    FieldName: nameof(Agent.Prompt),
                    CurrentValue: current.Prompt,
                    ProposedValue: proposed.Prompt,
                    Rationale: "提示词没有变化，无需推送。",
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
                Rationale: "首句为空，Agent 不会主动开场。",
                Severity: SuggestionSeverity.Suggestion));
        }

        if (proposed.VoiceId is not null && string.IsNullOrWhiteSpace(proposed.VoiceId))
        {
            output.Add(new Suggestion(
                FieldName: nameof(Agent.VoiceId),
                CurrentValue: current.VoiceId ?? string.Empty,
                ProposedValue: proposed.VoiceId,
                Rationale: "VoiceId 为空将导致 ElevenLabs 退回默认语音。",
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
                        Rationale: "变量名称不能为空。",
                        Severity: SuggestionSeverity.Warning));
                }
            }
        }

        return output;
    }
}