using FluentAssertions;
using Xunit;

namespace ElevenLabsStudio.IntegrationTests;

/// <summary>
/// Placeholder integration test. Real tests will hit the ElevenLabs API
/// when <c>ELEVENLABS_API_KEY</c> is present. CI sets the env var from
/// GitHub Actions Secrets; locally run with
/// <c>$env:ELEVENLABS_API_KEY = "..."; dotnet test</c>.
/// </summary>
public sealed class ElevenLabsAuthSkippedTests
{
    [Fact(Skip = "No API key in current environment — set ELEVENLABS_API_KEY to enable.")]
    public void Live_api_smoke_test() =>
        true.Should().BeTrue("placeholder until live integration tests are added.");
}