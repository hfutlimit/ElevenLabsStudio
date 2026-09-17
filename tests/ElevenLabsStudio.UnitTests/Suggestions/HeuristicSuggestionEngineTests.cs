using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Suggestions;
using FluentAssertions;
using Xunit;

namespace ElevenLabsStudio.UnitTests.Suggestions;

/// <summary>
/// Covers the deterministic rules baked into
/// <see cref="HeuristicSuggestionEngine"/>. Pure-logic — no I/O.
/// </summary>
public sealed class HeuristicSuggestionEngineTests
{
    private static Agent Sample() => new(
        AgentId: "agent_test_001",
        Name: "Sample",
        Prompt: "You are a helpful assistant.",
        FirstMessage: "Hi!",
        VoiceId: "voice-1",
        Variables: new[] { new Variable("topic", "general", "string") },
        Workflow: WorkflowDefaults.Empty,
        UpdatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public void Analyze_returns_warning_when_prompt_is_blank()
    {
        var engine = new HeuristicSuggestionEngine();
        var current = Sample();
        var update = new AgentUpdate(Prompt: "");

        var result = engine.Analyze(current, update);

        result.Should().ContainSingle(s =>
            s.FieldName == nameof(Agent.Prompt)
            && s.Severity == SuggestionSeverity.Warning);
    }

    [Fact]
    public void Analyze_returns_warning_when_prompt_exceeds_8k_chars()
    {
        var engine = new HeuristicSuggestionEngine();
        var current = Sample();
        var huge = new string('a', 8001);
        var update = new AgentUpdate(Prompt: huge);

        var result = engine.Analyze(current, update);

        result.Should().ContainSingle(s =>
            s.FieldName == nameof(Agent.Prompt)
            && s.Severity == SuggestionSeverity.Warning
            && s.ProposedValue == huge);
    }

    [Fact]
    public void Analyze_returns_info_when_prompt_unchanged()
    {
        var engine = new HeuristicSuggestionEngine();
        var current = Sample();
        var update = new AgentUpdate(Prompt: current.Prompt);

        var result = engine.Analyze(current, update);

        result.Should().ContainSingle(s =>
            s.FieldName == nameof(Agent.Prompt)
            && s.Severity == SuggestionSeverity.Info);
    }

    [Fact]
    public void Analyze_returns_suggestion_when_first_message_blank()
    {
        var engine = new HeuristicSuggestionEngine();
        var current = Sample();
        var update = new AgentUpdate(FirstMessage: "");

        var result = engine.Analyze(current, update);

        result.Should().ContainSingle(s =>
            s.FieldName == nameof(Agent.FirstMessage)
            && s.Severity == SuggestionSeverity.Suggestion);
    }

    [Fact]
    public void Analyze_returns_empty_for_empty_update()
    {
        var engine = new HeuristicSuggestionEngine();
        var current = Sample();
        var update = new AgentUpdate();

        var result = engine.Analyze(current, update);

        result.Should().BeEmpty();
    }
}