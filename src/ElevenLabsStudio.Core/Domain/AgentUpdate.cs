namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// Partial Agent update payload. Any null field is left unchanged on
/// the server side; nullable-value fields are kept verbatim. Used as the
/// single input for <see cref="Abstractions.IElevenLabsClient.UpdateAgentAsync"/>.
/// </summary>
public sealed record AgentUpdate(
	string? Prompt = null,
	string? FirstMessage = null,
	string? VoiceId = null,
	IReadOnlyList<Variable>? Variables = null,
	Workflow? Workflow = null)
{
	public bool IsEmpty =>
		Prompt is null &&
		FirstMessage is null &&
		VoiceId is null &&
		Variables is null &&
		Workflow is null;
}
