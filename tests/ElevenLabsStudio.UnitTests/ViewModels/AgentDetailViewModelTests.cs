using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class AgentDetailViewModelTests
{
	private static Agent AgentSnapshot(string prompt, string firstMessage = "hello") => new(
		AgentId: "agent_1",
		Name: "Agent",
		Prompt: prompt,
		FirstMessage: firstMessage,
		VoiceId: "voice_1",
		Variables: new[] { new Variable("name", "value", "string") },
		Workflow: new Workflow(new[] { new WorkflowNode("n1", "start", "Start") }, null),
		UpdatedAt: DateTimeOffset.UtcNow);

	private static AgentDetailViewModel Build(
		Agent initial,
		IElevenLabsClient client,
		IDialogService? dialog = null,
		IEventAggregator? events = null)
	{
		var suggestions = Substitute.For<ISuggestionEngine>();
		suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
			.Returns(Array.Empty<Suggestion>());
		return new AgentDetailViewModel(
			initial,
			client,
			suggestions,
			dialog ?? Substitute.For<IDialogService>(),
			events ?? Substitute.For<IEventAggregator>(),
			Substitute.For<IDraftStore>(),
			NullLogger<AgentDetailViewModel>.Instance);
	}

	[Fact]
	public void View_load_subscribes_with_the_UI_thread_marshal()
	{
		var events = Substitute.For<IEventAggregator>();
		var vm = Build(AgentSnapshot("initial"), Substitute.For<IElevenLabsClient>(), events: events);

		typeof(AgentDetailViewModel)
			.GetMethod("OnViewLoaded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.Invoke(vm, new object[] { new object() });

		var subscribe = events.ReceivedCalls()
			.Single(call => call.GetMethodInfo().Name == nameof(IEventAggregator.Subscribe));
		var marshal = (Func<Func<Task>, Task>)subscribe.GetArguments()[1]!;
		marshal.Method.Name.Should().Contain("SubscribeOnUIThread");
	}

	[Fact]
	public async Task InitializeAsync_passes_the_lifecycle_token_to_conversation_loading()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.ListConversationsAsync(
			Arg.Any<string>(),
			Arg.Any<DateTimeOffset?>(),
			Arg.Any<DateTimeOffset?>(),
			Arg.Any<int>(),
			Arg.Any<string?>(),
			Arg.Any<CancellationToken>())
			.Returns(Array.Empty<ConversationRecord>());
		var vm = Build(AgentSnapshot("initial"), client);
		using var cts = new CancellationTokenSource();

		await vm.InitializeAsync(cts.Token);

		await client.Received().ListConversationsAsync(
			"agent_1",
			Arg.Any<DateTimeOffset?>(),
			Arg.Any<DateTimeOffset?>(),
			100,
			null,
			cts.Token);
	}

	[Fact]
	public async Task ReloadAsync_updates_prompt_baseline_on_every_refresh()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.GetAgentAsync("agent_1", Arg.Any<CancellationToken>())
			.Returns(AgentSnapshot("server-one"), AgentSnapshot("server-two"));
		var vm = Build(AgentSnapshot("initial"), client);

		await vm.ReloadAsync();
		await vm.ReloadAsync();

		vm.SystemPromptVm.Prompt.Should().Be("server-two");
		vm.IsDirty.Should().BeFalse();
	}

	[Fact]
	public async Task Push_applies_the_acknowledged_server_snapshot_to_all_tabs()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.UpdateAgentAsync("agent_1", Arg.Any<AgentUpdate>(), Arg.Any<CancellationToken>())
			.Returns(AgentSnapshot("server-normalized", "server-first-message"));
		var dialog = ConfirmedDialog();
		var vm = Build(AgentSnapshot("initial"), client, dialog);
		vm.SystemPromptVm.Prompt = "local-edit";

		await vm.Push();

		vm.Agent.Prompt.Should().Be("server-normalized");
		vm.SystemPromptVm.Prompt.Should().Be("server-normalized");
		vm.FirstMessageVm.FirstMessage.Should().Be("server-first-message");
		vm.IsDirty.Should().BeFalse();
	}

	[Fact]
	public async Task Push_does_nothing_when_the_user_declines_the_confirmation()
	{
		var client = Substitute.For<IElevenLabsClient>();
		var dialog = Substitute.For<IDialogService>();
		dialog.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var vm = Build(AgentSnapshot("initial"), client, dialog);
		vm.SystemPromptVm.Prompt = "local-edit";

		await vm.Push();

		await client.DidNotReceive().UpdateAgentAsync(
			Arg.Any<string>(),
			Arg.Any<AgentUpdate>(),
			Arg.Any<CancellationToken>());
		// The edit is still on screen and still pending — declining
		// must not silently throw the user's work away.
		vm.Agent.Prompt.Should().Be("initial");
		vm.SystemPromptVm.Prompt.Should().Be("local-edit");
		vm.IsDirty.Should().BeTrue();
	}

	[Fact]
	public async Task Push_asks_before_overwriting_the_server_copy()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.UpdateAgentAsync("agent_1", Arg.Any<AgentUpdate>(), Arg.Any<CancellationToken>())
			.Returns(AgentSnapshot("server-normalized"));
		var dialog = ConfirmedDialog();
		var vm = Build(AgentSnapshot("initial"), client, dialog);
		vm.SystemPromptVm.Prompt = "local-edit";

		await vm.Push();

		await dialog.Received(1).ConfirmAsync(
			Arg.Is<string>(title => title.Contains("Push")),
			Arg.Is<string>(message => message.Contains("Agent")),
			Arg.Any<CancellationToken>());
	}

	private static IDialogService ConfirmedDialog()
	{
		var dialog = Substitute.For<IDialogService>();
		dialog.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(true);
		return dialog;
	}
}
