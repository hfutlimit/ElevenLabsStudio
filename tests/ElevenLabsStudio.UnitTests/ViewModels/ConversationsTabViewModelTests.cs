using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class ConversationsTabViewModelTests
{
	private static Agent SampleAgent() => new(
		AgentId: "agent_1",
		Name: "Agent",
		Prompt: "prompt",
		FirstMessage: "hello",
		VoiceId: "voice_1",
		Variables: Array.Empty<Variable>(),
		Workflow: WorkflowDefaults.Empty,
		UpdatedAt: DateTimeOffset.UtcNow);

	private static ConversationRecord Conversation(string id, params TranscriptTurn[] turns) => new(
		ConversationId: id,
		AgentId: "agent_1",
		StartedAt: DateTimeOffset.FromUnixTimeSeconds(1_737_000_000),
		EndedAt: DateTimeOffset.FromUnixTimeSeconds(1_737_000_010),
		DurationMs: 10_000,
		Status: "success",
		Turns: turns);

	private static ConversationsTabViewModel Build(
		IElevenLabsClient client,
		IDialogService? dialog = null) => new(
		SampleAgent(),
		client,
		dialog ?? Substitute.For<IDialogService>(),
		NullLogger<ConversationsTabViewModel>.Instance);

	[Fact]
	public void Constructor_does_not_start_network_work()
	{
		var client = Substitute.For<IElevenLabsClient>();

		_ = Build(client);

		client.DidNotReceiveWithAnyArgs().ListConversationsAsync(default!);
	}

	[Fact]
	public async Task SelectConversationAsync_ignores_a_stale_detail_response()
	{
		var first = new TaskCompletionSource<ConversationRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
		var second = new TaskCompletionSource<ConversationRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
		var client = Substitute.For<IElevenLabsClient>();
		client.GetConversationAsync("c1", Arg.Any<CancellationToken>()).Returns(first.Task);
		client.GetConversationAsync("c2", Arg.Any<CancellationToken>()).Returns(second.Task);
		var vm = Build(client);

		var loadFirst = vm.SelectConversationAsync(Conversation("c1"));
		var loadSecond = vm.SelectConversationAsync(Conversation("c2"));
		second.SetResult(Conversation("c2", new TranscriptTurn("agent", "new", DateTimeOffset.UtcNow)));
		await loadSecond;
		first.SetResult(Conversation("c1", new TranscriptTurn("agent", "stale", DateTimeOffset.UtcNow)));
		await loadFirst;

		vm.Turns.Should().ContainSingle(turn => turn.Text == "new");
	}

	[Fact]
	public async Task SelectConversationAsync_preserves_last_transcript_when_detail_fails()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.GetConversationAsync("c1", Arg.Any<CancellationToken>())
			.Returns(Conversation("c1", new TranscriptTurn("agent", "kept", DateTimeOffset.UtcNow)));
		client.GetConversationAsync("c2", Arg.Any<CancellationToken>())
			.Returns<Task<ConversationRecord>>(_ => throw new ElevenLabsException("failed", 500));
		var dialog = Substitute.For<IDialogService>();
		var vm = Build(client, dialog);
		await vm.SelectConversationAsync(Conversation("c1"));

		await vm.SelectConversationAsync(Conversation("c2"));

		vm.Turns.Should().ContainSingle(turn => turn.Text == "kept");
		await dialog.Received().ShowErrorAsync("加载失败", Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Clearing_selection_cancels_the_request_and_clears_turns()
	{
		var pending = new TaskCompletionSource<ConversationRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
		var client = Substitute.For<IElevenLabsClient>();
		CancellationToken captured = default;
		client.GetConversationAsync("c1", Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				captured = call.ArgAt<CancellationToken>(1);
				return pending.Task;
			});
		var vm = Build(client);
		vm.Turns.Add(new TranscriptTurn("agent", "old", DateTimeOffset.UtcNow));
		var load = vm.SelectConversationAsync(Conversation("c1"));

		await vm.SelectConversationAsync(null);

		captured.IsCancellationRequested.Should().BeTrue();
		vm.Turns.Should().BeEmpty();
		pending.SetResult(Conversation("c1", new TranscriptTurn("agent", "stale", DateTimeOffset.UtcNow)));
		await load;
		vm.Turns.Should().BeEmpty();
	}
}
