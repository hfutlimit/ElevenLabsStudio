namespace ElevenLabsStudio.Core.Domain;

/// <summary>
/// Read-only summary of an ElevenLabs voice. Full voice configuration is
/// surfaced by the API but not required for Agent update flows.
/// </summary>
public sealed record Voice(string VoiceId, string Name, string? Category, string? PreviewUrl);