using System.Runtime.CompilerServices;

// Expose internal wire DTOs + the Mapping adapter to the unit-test
// assembly so tests can construct realistic ElevenLabs payloads without
// exposing them to consumers of the public API. Tests live in a
// separate assembly so they cannot leak into the production surface.
[assembly: InternalsVisibleTo("ElevenLabsStudio.UnitTests")]
[assembly: InternalsVisibleTo("ElevenLabsStudio.IntegrationTests")]