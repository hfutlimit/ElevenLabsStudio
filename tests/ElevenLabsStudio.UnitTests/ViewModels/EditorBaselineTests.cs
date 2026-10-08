using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class EditorBaselineTests
{
	private static FirstMessageTabViewModel FirstMessageVm(string firstMessage)
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		return new FirstMessageTabViewModel(
			Snapshot("prompt", firstMessage),
			suggestions,
			NullLogger<FirstMessageTabViewModel>.Instance);
	}

	private static SystemPromptTabViewModel PromptVm(string prompt)
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		return new SystemPromptTabViewModel(
			Snapshot(prompt, "first"),
			suggestions,
			NullLogger<SystemPromptTabViewModel>.Instance);
	}

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

	[Fact]
	public void Prompt_reset_rolls_back_to_the_latest_server_snapshot()
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		var vm = new SystemPromptTabViewModel(
			Snapshot("server-a", "first-a"),
			suggestions,
			NullLogger<SystemPromptTabViewModel>.Instance);

		vm.RefreshFrom(Snapshot("server-b", "first-b"));
		vm.Prompt.Should().Be("server-b");
		vm.HasLocalEdits.Should().BeFalse();

		vm.Prompt = "local-edit";
		vm.HasLocalEdits.Should().BeTrue("the Reset button only shows while edits exist");

		vm.ResetToServer();

		vm.Prompt.Should().Be("server-b",
			"Reset restores the latest server value, not the construction-time one");
		vm.HasLocalEdits.Should().BeFalse();
	}

	[Fact]
	public void First_message_reset_rolls_back_to_the_latest_server_snapshot()
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		var vm = new FirstMessageTabViewModel(
			Snapshot("server-a", "first-a"),
			suggestions,
			NullLogger<FirstMessageTabViewModel>.Instance);

		vm.RefreshFrom(Snapshot("server-b", "first-b"));
		vm.FirstMessage.Should().Be("first-b");
		vm.HasLocalEdits.Should().BeFalse();

		vm.FirstMessage = "local-edit";
		vm.HasLocalEdits.Should().BeTrue();

		vm.ResetToServer();

		vm.FirstMessage.Should().Be("first-b");
		vm.HasLocalEdits.Should().BeFalse();
	}

	[Theory]
	[InlineData(0, "App.TextMuted")]
	[InlineData(1, "App.TextSecondary")]
	[InlineData(450, "App.TextSecondary")]
	[InlineData(451, "App.Accent")]
	[InlineData(500, "App.Accent")]
	[InlineData(501, "App.Danger")]
	public void First_message_length_tone_follows_the_editor(int length, string brushKey)
	{
		var vm = FirstMessageVm(new string('a', length));

		vm.FirstMessageLength.Should().Be(length.ToString("N0") + " chars");
		vm.FirstMessageLengthBrushKey.Should().Be(brushKey);
	}

	[Fact]
	public void First_message_length_updates_when_the_editor_changes()
	{
		var vm = FirstMessageVm("hi");

		vm.FirstMessage = new string('a', 501);

		vm.FirstMessageLength.Should().Be(501.ToString("N0") + " chars");
		vm.FirstMessageLengthBrushKey.Should().Be("App.Danger");
	}

	[Theory]
	[InlineData(0, "App.TextMuted")]
	[InlineData(1, "App.TextSecondary")]
	[InlineData(7200, "App.TextSecondary")]
	[InlineData(7201, "App.Accent")]
	[InlineData(8000, "App.Accent")]
	[InlineData(8001, "App.Danger")]
	public void Prompt_length_tone_follows_the_editor(int length, string brushKey)
	{
		var vm = PromptVm(new string('a', length));

		vm.PromptLength.Should().Be(length.ToString("N0") + " chars");
		vm.PromptLengthBrushKey.Should().Be(brushKey);
	}

	[Fact]
	public void Variables_reset_restores_the_latest_server_snapshot()
	{
		var withVars = new Variable("customer_name", "Who?", "string");
		var original = new Agent(
			"agent_1", "Agent", "p", "f", "voice_1",
			new[] { withVars },
			WorkflowDefaults.Empty,
			DateTimeOffset.UtcNow);
		var vm = new VariablesTabViewModel(original);
		vm.IsDirty.Should().BeFalse();

		vm.AddVariable();
		vm.IsDirty.Should().BeTrue();

		// Upstream push adds a second variable.
		var updated = original with
		{
			Variables = new[] { withVars, new Variable("case_id", "Which case?", "string") },
		};
		vm.RefreshFrom(updated);
		vm.IsDirty.Should().BeFalse();

		vm.RemoveVariable(vm.Variables[0]);
		vm.IsDirty.Should().BeTrue();

		vm.ResetToServer();

		vm.Variables.Should().HaveCount(2,
			"Reset restores the latest server snapshot, not the original one");
		vm.Variables.Should().Contain(v => v.Name == "case_id");
		vm.IsDirty.Should().BeFalse();
	}
}
