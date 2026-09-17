namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// Local heuristic suggestion for an Agent field. Not remote — produced
/// by <see cref="Abstractions.ISuggestionEngine"/> before pushing to
/// ElevenLabs. The user must explicitly accept each Suggestion before it
/// becomes part of the outgoing <see cref="AgentUpdate"/>.
/// </summary>
public sealed record Suggestion(
    string FieldName,
    string CurrentValue,
    string ProposedValue,
    string Rationale,
    SuggestionSeverity Severity);

public enum SuggestionSeverity
{
    Info = 0,
    Suggestion = 1,
    Warning = 2,
}