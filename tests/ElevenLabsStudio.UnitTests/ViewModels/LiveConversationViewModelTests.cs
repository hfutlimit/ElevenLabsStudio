using System.Text.Json;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class LiveConversationViewModelTests
{
	private static Agent SampleAgent() => new(
		AgentId: "agent_live",
		Name: "Live agent",
		Prompt: "prompt",
		FirstMessage: "hello",
		VoiceId: "voice_1",
		Variables: new[]
		{
			new Variable("client_id", "supportteam", "string"),
			new Variable("attempts", "2", "number"),
		},
		Workflow: WorkflowDefaults.Empty,
		UpdatedAt: DateTimeOffset.UtcNow);

	[Fact]
	public void Default_dynamic_variables_match_the_reference_tester_found_payload()
	{
		var vm = NewViewModel(Substitute.For<IRealtimeConversationClient>());

		vm.DynamicVariables.Select(variable => variable.Key).Should().BeEquivalentTo(
			[
				"lookup_status",
				"contact_name",
				"organization_by_phone",
				"client_id_by_phone",
				"fallback_client_id",
				"email_by_phone",
				"account_id_by_phone",
				"contact_id_by_phone",
				"caller_id_norm",
			]);
		vm.DynamicVariables.Single(variable => variable.Key == "lookup_status").Value.Should().Be("found");
		vm.DynamicVariables.Single(variable => variable.Key == "contact_name").Value.Should().Be("Clinton Smith5");
		vm.DynamicVariables.Single(variable => variable.Key == "organization_by_phone").Value.Should().Be("ZYX Sample Client - tuplus01qa");
		vm.DynamicVariables.Single(variable => variable.Key == "client_id_by_phone").Value.Should().Be("tuplus01qa");
		vm.DynamicVariables.Single(variable => variable.Key == "fallback_client_id").Value.Should().Be("supportteam");
		vm.DynamicVariables.Single(variable => variable.Key == "email_by_phone").Value.Should().Be("csmith+5@transfinder.com");
		vm.DynamicVariables.Single(variable => variable.Key == "account_id_by_phone").Value.Should().Be("56f9282d-a970-f111-842b-0e9e16a6d2ed");
		vm.DynamicVariables.Single(variable => variable.Key == "contact_id_by_phone").Value.Should().Be("be1f0915-a17b-f111-842b-0e9e16a6d2ed");
		vm.DynamicVariables.Single(variable => variable.Key == "caller_id_norm").Value.Should().Be("2601234567");
	}

	[Fact]
	public void Selecting_not_found_scenario_replaces_the_editor_with_the_reference_payload()
	{
		var vm = NewViewModel(Substitute.For<IRealtimeConversationClient>());

		vm.VariableScenarios.Select(scenario => scenario.Key).Should().Equal("found", "not_found");
		vm.SelectedVariableScenario = vm.VariableScenarios.Single(scenario => scenario.Key == "not_found");

		vm.DynamicVariables.Single(variable => variable.Key == "lookup_status").Value.Should().Be("not_found");
		vm.DynamicVariables.Single(variable => variable.Key == "contact_name").Value.Should().BeEmpty();
		vm.DynamicVariables.Single(variable => variable.Key == "organization_by_phone").Value.Should().BeEmpty();
		vm.DynamicVariables.Single(variable => variable.Key == "client_id_by_phone").Value.Should().Be("tuplus01qa");
		vm.DynamicVariables.Single(variable => variable.Key == "fallback_client_id").Value.Should().Be("tuplus01qa");
		vm.DynamicVariables.Single(variable => variable.Key == "email_by_phone").Value.Should().BeEmpty();
		vm.DynamicVariables.Single(variable => variable.Key == "account_id_by_phone").Value.Should().BeEmpty();
		vm.DynamicVariables.Single(variable => variable.Key == "contact_id_by_phone").Value.Should().BeEmpty();
		vm.DynamicVariables.Single(variable => variable.Key == "caller_id_norm").Value.Should().Be("2601234567");
	}

	[Fact]
	public void Defaults_to_the_reference_tester_main_branch()
	{
		var vm = NewViewModel(Substitute.For<IRealtimeConversationClient>());

		vm.BranchId.Should().Be(LiveConversationViewModel.DefaultBranchId);
		LiveConversationViewModel.DefaultBranchId.Should().Be("agtbrch_7101m2hctwtefwtrt0jc1eaw7m9t");
	}

	[Fact]
	public void Dynamic_variables_are_editable_rows_and_follow_the_selected_scenario()
	{
		var vm = NewViewModel(Substitute.For<IRealtimeConversationClient>());

		vm.DynamicVariables.Should().HaveCount(9);
		vm.DynamicVariables.Should().ContainSingle(variable =>
			variable.Key == "caller_id_norm" && variable.Value == "2601234567");

		vm.SelectedVariableScenario = vm.VariableScenarios.Single(scenario => scenario.Key == "not_found");

		vm.DynamicVariables.Should().HaveCount(9);
		vm.DynamicVariables.Should().ContainSingle(variable =>
			variable.Key == "lookup_status" && variable.Value == "not_found");
		vm.DynamicVariables.Should().ContainSingle(variable =>
			variable.Key == "fallback_client_id" && variable.Value == "tuplus01qa");
	}

	[Fact]
	public void Dynamic_variables_can_be_added_and_removed()
	{
		var vm = NewViewModel(Substitute.For<IRealtimeConversationClient>());
		var initialCount = vm.DynamicVariables.Count;

		vm.AddDynamicVariable();

		vm.DynamicVariables.Should().HaveCount(initialCount + 1);
		vm.DynamicVariables[^1].Key.Should().Be("variable_10");

		vm.RemoveDynamicVariable(vm.DynamicVariables[^1]);

		vm.DynamicVariables.Should().HaveCount(initialCount);
	}

	[Fact]
	public async Task Transcript_empty_state_tracks_live_messages()
	{
		var session = new FakeSession();
		var client = Substitute.For<IRealtimeConversationClient>();
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(session);
		var vm = NewViewModel(client);

		vm.HasTranscript.Should().BeFalse();
		await vm.StartAsync();
		session.RaiseTranscript("agent", "Welcome");

		vm.HasTranscript.Should().BeTrue();
	}

	[Fact]
	public async Task StartAsync_builds_a_session_from_agent_and_editor_inputs()
	{
		var client = Substitute.For<IRealtimeConversationClient>();
		var session = new FakeSession();
		RealtimeConversationOptions? options = null;
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				options = call.ArgAt<RealtimeConversationOptions>(0);
				return session;
			});
		var vm = new LiveConversationViewModel(
			SampleAgent(),
			client,
			Substitute.For<IDialogService>(),
			new ManualClockService(),
			NullLogger<LiveConversationViewModel>.Instance);
		vm.BranchId = "branch_live";
		vm.Environment = "staging";
		vm.DynamicVariables.Clear();
		vm.DynamicVariables.Add(new DynamicVariableEntry("caller_id_norm", "2601234567"));
		vm.DynamicVariables.Add(new DynamicVariableEntry("attempts", "3"));

		await vm.StartAsync();

		options.Should().NotBeNull();
		options!.AgentId.Should().Be("agent_live");
		options.BranchId.Should().Be("branch_live");
		options.Environment.Should().Be("staging");
		options.DynamicVariables["caller_id_norm"].Should().Be("2601234567");
		options.DynamicVariables["attempts"].Should().Be("3");
		vm.Status.Should().Be(RealtimeConversationStatus.Connecting);
	}

	[Fact]
	public async Task Session_events_update_status_mode_and_transcript()
	{
		var session = new FakeSession();
		var client = Substitute.For<IRealtimeConversationClient>();
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(session);
		var vm = NewViewModel(client);

		await vm.StartAsync();
		session.RaiseStatus(RealtimeConversationStatus.Connected, "conv_123");
		session.RaiseMode(RealtimeConversationMode.Speaking);
		session.RaiseTranscript("agent", "Welcome");

		vm.Status.Should().Be(RealtimeConversationStatus.Connected);
		vm.ConversationId.Should().Be("conv_123");
		vm.Mode.Should().Be(RealtimeConversationMode.Speaking);
		vm.Transcript.Should().ContainSingle(turn => turn.Text == "Welcome");
		vm.IsConnected.Should().BeTrue();
	}

	[Fact]
	public async Task SendTextAsync_forwards_non_empty_message_and_clears_editor()
	{
		var session = new FakeSession();
		var client = Substitute.For<IRealtimeConversationClient>();
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(session);
		var vm = NewViewModel(client);
		await vm.StartAsync();
		vm.MessageText = "  hello agent  ";

		await vm.SendTextAsync();

		session.SentText.Should().ContainSingle().Which.Should().Be("hello agent");
		vm.MessageText.Should().BeEmpty();
	}

	[Fact]
	public async Task StopAsync_ends_the_session_and_returns_to_disconnected()
	{
		var session = new FakeSession();
		var client = Substitute.For<IRealtimeConversationClient>();
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(session);
		var vm = NewViewModel(client);
		await vm.StartAsync();

		await vm.StopAsync();

		session.StopCount.Should().Be(1);
		vm.Status.Should().Be(RealtimeConversationStatus.Disconnected);
		vm.IsConnected.Should().BeFalse();
	}

	private static LiveConversationViewModel NewViewModel(IRealtimeConversationClient client) => new(
		SampleAgent(),
		client,
		Substitute.For<IDialogService>(),
		new ManualClockService(),
		NullLogger<LiveConversationViewModel>.Instance);

	private sealed class FakeSession : IRealtimeConversationSession
	{
		public string? ConversationId { get; private set; }
		public RealtimeConversationStatus Status { get; private set; } = RealtimeConversationStatus.Connecting;
		public List<string> SentText { get; } = new();
		public int StopCount { get; private set; }
		public event EventHandler<RealtimeConversationStatusChangedEventArgs>? StatusChanged;
		public event EventHandler<RealtimeConversationModeChangedEventArgs>? ModeChanged;
		public event EventHandler<RealtimeTranscriptEventArgs>? TranscriptReceived;
		public event EventHandler<RealtimeConversationVolumeChangedEventArgs>? VolumeChanged;
		public event EventHandler<RealtimeConversationErrorEventArgs>? Error;

		public Task SendTextAsync(string text, CancellationToken ct = default)
		{
			SentText.Add(text);
			return Task.CompletedTask;
		}

		public Task SetMutedAsync(bool muted, CancellationToken ct = default) => Task.CompletedTask;

		public Task StopAsync(CancellationToken ct = default)
		{
			StopCount++;
			RaiseStatus(RealtimeConversationStatus.Disconnected, ConversationId);
			return Task.CompletedTask;
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;

		public void RaiseStatus(RealtimeConversationStatus status, string? conversationId)
		{
			Status = status;
			ConversationId = conversationId;
			StatusChanged?.Invoke(this, new RealtimeConversationStatusChangedEventArgs(status, conversationId));
		}

		public void RaiseMode(RealtimeConversationMode mode) =>
			ModeChanged?.Invoke(this, new RealtimeConversationModeChangedEventArgs(mode));

		public void RaiseTranscript(string speaker, string text) =>
			TranscriptReceived?.Invoke(this, new RealtimeTranscriptEventArgs(
				new RealtimeTranscriptMessage(speaker, text, DateTimeOffset.UtcNow)));
	}

	private sealed class ManualClockService : IClockService
	{
		public DateTime Now => DateTime.UtcNow;
		public IDisposable Start(TimeSpan interval, Action tick) => new NoopTimer();
		private sealed class NoopTimer : IDisposable
		{
			public void Dispose() { }
		}
	}
}
