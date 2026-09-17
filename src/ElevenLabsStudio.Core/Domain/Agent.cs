namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// Server-side Agent definition. Mirrors the ElevenLabs Conversational AI
/// Agent model. Immutable; mutations go through <see cref="AgentUpdate"/>.
/// </summary>
public sealed record Agent(
    string AgentId,
    string Name,
    string Prompt,
    string FirstMessage,
    string? VoiceId,
    IReadOnlyList<Variable> Variables,
    Workflow Workflow,
    DateTimeOffset UpdatedAt)
{
    public static Agent Empty(string agentId) => new(
        agentId,
        Name: string.Empty,
        Prompt: string.Empty,
        FirstMessage: string.Empty,
        VoiceId: null,
        Variables: Array.Empty<Variable>(),
        Workflow: WorkflowDefaults.Empty,
        UpdatedAt: DateTimeOffset.UtcNow);
}