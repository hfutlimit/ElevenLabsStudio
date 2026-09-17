namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// User-defined template variable bound into an Agent prompt.
/// <para>
/// <c>Type</c> is the ElevenLabs variable type token
/// (e.g. <c>string</c>, <c>number</c>, <c>boolean</c>); the runtime value
/// flows in via conversation initiation.
/// </para>
/// </summary>
public sealed record Variable(string Name, string? Value, string Type);