using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Events;
using ElevenLabsStudio.Core.Exceptions;
using ElevenLabsStudio.Infrastructure.Mock;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.ViewModels.Agents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

/// <summary>
/// Pins the AgentListViewModel behaviour: LoadAsync pulls the list
/// from the injected IElevenLabsClient, auto-selects the first agent
/// so the right pane immediately has something to show, and forwards
/// the inner AgentDetail via PropertyChanged so the ShellViewModel can
/// mirror it onto the right pane.
/// </summary>
public sealed class AgentListViewModelTests
{
	private static Agent SampleAgent(string id = "agent_test_001") => new(
		AgentId: id,
		Name: "Test",
		Prompt: "p",
		FirstMessage: "hi",
		VoiceId: "voice_x",
		Variables: Array.Empty<Variable>(),
		Workflow: WorkflowDefaults.Empty,
		UpdatedAt: DateTimeOffset.UtcNow);

	private static AgentSummary SampleSummary(string id = "agent_test_001") => new(
		AgentId: id,
		Name: "Test",
		VoiceId: "voice_x",
		CreatedAt: DateTimeOffset.UtcNow);

	private static (AgentListViewModel vm, IElevenLabsClient client, IEventAggregator events, IWindowManager windows)
		Build()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.ListAgentsAsync(Arg.Any<CancellationToken>())
			.Returns(new[] { SampleSummary("a1"), SampleSummary("a2"), SampleSummary("a3") });
		client.GetAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(call => SampleAgent(call.ArgAt<string>(0)));
		client.ListConversationsAsync(
			Arg.Any<string>(),
			Arg.Any<DateTimeOffset?>(),
			Arg.Any<DateTimeOffset?>(),
			Arg.Any<int>(),
			Arg.Any<string?>(),
			Arg.Any<CancellationToken>())
			.Returns(Array.Empty<ConversationRecord>());
		var events = Substitute.For<IEventAggregator>();
		var windows = Substitute.For<IWindowManager>();
		var dialog = Substitute.For<IDialogService>();
		var suggestions = Substitute.For<ISuggestionEngine>();
		var drafts = Substitute.For<IDraftStore>();
		var detailFactory = new AgentDetailViewModelFactory(
			client, suggestions, dialog, events, drafts,
			NullLogger<AgentDetailViewModel>.Instance);
		var vm = new AgentListViewModel(
			client, dialog, events, drafts, detailFactory,
			NullLogger<AgentListViewModel>.Instance, suggestions, windows);
		return (vm, client, events, windows);
	}

	[Fact]
	public void Constructor_does_not_start_network_work()
	{
		var (_, client, _, _) = Build();

		client.DidNotReceiveWithAnyArgs().ListAgentsAsync(default);
	}

	[Fact]
	public async Task LoadAsync_auto_selects_first_agent_so_right_pane_renders()
	{
		var (vm, _, _, _) = Build();
		await vm.LoadAsync();

		vm.SelectedAgent.Should().NotBeNull();
		vm.SelectedAgent!.AgentId.Should().Be("a1");
		vm.AgentDetail.Should().NotBeNull();
		vm.AgentDetail!.Agent.AgentId.Should().Be("a1");
	}

	[Fact]
	public async Task LoadAsync_passes_the_lifecycle_token_to_the_client()
	{
		var (vm, client, _, _) = Build();
		using var cts = new CancellationTokenSource();

		await vm.LoadAsync(cts.Token);

		await client.Received().ListAgentsAsync(cts.Token);
	}

	[Fact]
	public void Constructor_subscribes_with_the_UI_thread_marshal()
	{
		var (_, _, events, _) = Build();

		var subscribe = events.ReceivedCalls()
			.Single(call => call.GetMethodInfo().Name == nameof(IEventAggregator.Subscribe));
		var marshal = (Func<Func<Task>, Task>)subscribe.GetArguments()[1]!;
		marshal.Method.Name.Should().Contain("SubscribeOnUIThread");
	}

	[Fact]
	public async Task LoadAsync_keeps_existing_selection_when_already_set()
	{
		var (vm, _, _, _) = Build();
		await vm.LoadAsync();
		await vm.SelectAgentAsync(vm.Agents.First(a => a.AgentId == "a2"));

		// Re-load (e.g. user clicked refresh) — selection should stick
		// on a2 instead of snapping back to a1.
		await vm.LoadAsync();
		vm.SelectedAgent!.AgentId.Should().Be("a2");
	}

	[Fact]
	public async Task LoadAsync_shows_error_dialog_on_auth_failure()
	{
		var client = Substitute.For<IElevenLabsClient>();
		client.ListAgentsAsync(Arg.Any<CancellationToken>())
			.Returns<Task<IReadOnlyList<AgentSummary>>>(_ => throw new ElevenLabsAuthException("nope"));
		var dialog = Substitute.For<IDialogService>();
		var events = Substitute.For<IEventAggregator>();
		var vm = new AgentListViewModel(
			client, dialog, events, Substitute.For<IDraftStore>(),
			new AgentDetailViewModelFactory(
				client, Substitute.For<ISuggestionEngine>(), dialog, events,
				Substitute.For<IDraftStore>(),
				NullLogger<AgentDetailViewModel>.Instance),
			NullLogger<AgentListViewModel>.Instance,
			Substitute.For<ISuggestionEngine>(), Substitute.For<IWindowManager>());

		await vm.LoadAsync();

		await dialog.Received().ShowErrorAsync("Authentication failed", Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task FilterText_filter_AgentsView_keeps_matching_only()
	{
		var (vm, _, _, _) = Build();
		await vm.LoadAsync();

		vm.FilterText = "support"; // matches none of the seeded names
		// ICollectionView Filter is private; reach it via reflection.
		var view = (System.ComponentModel.ICollectionView)vm.GetType()
			.GetProperty("AgentsView")!.GetValue(vm)!;
		var matches = view.Cast<AgentSummary>().ToList();
		matches.Should().BeEmpty();
	}

	[Fact]
	public void HasNoAgents_true_before_load_completes()
	{
		// Block the client so LoadAsync never returns.
		var tcs = new TaskCompletionSource<IReadOnlyList<AgentSummary>>();
		var client = Substitute.For<IElevenLabsClient>();
		client.ListAgentsAsync(Arg.Any<CancellationToken>()).Returns(tcs.Task);
		var vm = new AgentListViewModel(
			client, Substitute.For<IDialogService>(), Substitute.For<IEventAggregator>(),
			Substitute.For<IDraftStore>(),
			Substitute.For<IAgentDetailViewModelFactory>(),
			NullLogger<AgentListViewModel>.Instance,
			Substitute.For<ISuggestionEngine>(), Substitute.For<IWindowManager>());

		vm.HasNoAgents.Should().BeTrue();
		tcs.SetResult(Array.Empty<AgentSummary>());
	}

	[Fact]
	public async Task SelectAgentAsync_ignores_a_stale_detail_response()
	{
		var first = new TaskCompletionSource<Agent>(TaskCreationOptions.RunContinuationsAsynchronously);
		var second = new TaskCompletionSource<Agent>(TaskCreationOptions.RunContinuationsAsynchronously);
		var client = Substitute.For<IElevenLabsClient>();
		client.ListAgentsAsync(Arg.Any<CancellationToken>())
			.Returns(Array.Empty<AgentSummary>());
		client.GetAgentAsync("a1", Arg.Any<CancellationToken>()).Returns(first.Task);
		client.GetAgentAsync("a2", Arg.Any<CancellationToken>()).Returns(second.Task);
		var dialog = Substitute.For<IDialogService>();
		var events = Substitute.For<IEventAggregator>();
		var drafts = Substitute.For<IDraftStore>();
		var suggestions = Substitute.For<ISuggestionEngine>();
		var factory = new AgentDetailViewModelFactory(
			client,
			suggestions,
			dialog,
			events,
			drafts,
			NullLogger<AgentDetailViewModel>.Instance);
		var vm = new AgentListViewModel(
			client,
			dialog,
			events,
			drafts,
			factory,
			NullLogger<AgentListViewModel>.Instance,
			suggestions,
			Substitute.For<IWindowManager>());

		var loadFirst = vm.SelectAgentAsync(SampleSummary("a1"));
		var loadSecond = vm.SelectAgentAsync(SampleSummary("a2"));
		second.SetResult(SampleAgent("a2"));
		await loadSecond;
		first.SetResult(SampleAgent("a1"));
		await loadFirst;

		vm.AgentDetail.Should().NotBeNull();
		vm.AgentDetail!.Agent.AgentId.Should().Be("a2");
	}
}
