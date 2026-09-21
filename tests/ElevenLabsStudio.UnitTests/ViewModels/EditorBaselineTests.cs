using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class EditorBaselineTests
{
	private static Agent Snapshot(string prompt, string firstMessage) => new(
		"agent_1",
		"Agent",
		prompt,
		firstMessage,
		"voice_1",
		Array.Empty<Variable>(),
		WorkflowDefaults.Empty,
		DateTimeOffset.UtcNow);

	[Fact]
	public void Prompt_refresh_advances_baseline_but_preserves_a_dirty_local_edit()
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		var vm = new SystemPromptTabViewModel(
			Snapshot("server-a", "first-a"),
			suggestions,
			NullLogger<SystemPromptTabViewModel>.Instance);
		var serverB = Snapshot("server-b", "first-b");
		var serverC = Snapshot("server-c", "first-c");

		vm.RefreshFrom(serverB);
		vm.Prompt.Should().Be("server-b");
		vm.Prompt = "local-edit";
		vm.RefreshFrom(serverC);

		vm.Prompt.Should().Be("local-edit");
		suggestions.Received().Analyze(serverC, Arg.Any<AgentUpdate>());
	}

	[Fact]
	public void First_message_refresh_advances_baseline_but_preserves_a_dirty_local_edit()
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		var vm = new FirstMessageTabViewModel(
			Snapshot("server-a", "first-a"),
			suggestions,
			NullLogger<FirstMessageTabViewModel>.Instance);

		vm.RefreshFrom(Snapshot("server-b", "first-b"));
		vm.FirstMessage = "local-edit";
		var serverC = Snapshot("server-c", "first-c");
		vm.RefreshFrom(serverC);

		vm.FirstMessage.Should().Be("local-edit");
		suggestions.Received().Analyze(serverC, Arg.Any<AgentUpdate>());
	}
}
