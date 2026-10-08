using System.IO;
using System.Text.Json;
using ElevenLabsStudio.Services;
using IWindowManager = Caliburn.Micro.IWindowManager;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.ViewModels;

public sealed class LiveConversationViewModelTests
{
	[Theory]
	[InlineData(true, "updated")]
	[InlineData(false, "2601234567")]
	public async Task Parameter_dialog_applies_only_saved_values_to_the_next_call(bool save, string expected)
	{
		var windows = Substitute.For<IWindowManager>();
		var client = Substitute.For<IRealtimeConversationClient>();
		RealtimeConversationOptions? sent = null;
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(call => { sent = call.ArgAt<RealtimeConversationOptions>(0); return new FakeSession(); });
		windows.ShowDialogAsync(Arg.Any<object>()).Returns(async call =>
		{
			var editor = (DynamicVariablesDialogViewModel)call.ArgAt<object>(0);
			editor.DynamicVariables.Single(v => v.Key == "caller_id_norm").Value = "updated";
			if (save) await editor.SaveAsync();
			return (bool?)save;
		});
		using var vm = new LiveConversationViewModel(SampleAgent(), client,
			Substitute.For<IDialogService>(), new ManualClockService(),
			NullLogger<LiveConversationViewModel>.Instance, windows);
		await vm.EditDynamicVariablesAsync();
		await vm.StartAsync();
		sent!.DynamicVariables["caller_id_norm"].Should().Be(expected);
	}

	[Theory]
	[InlineData("")]
	[InlineData(" name ")]
	public async Task Parameter_dialog_rejects_empty_or_duplicate_names(string secondKey)
	{
		var editor = new DynamicVariablesDialogViewModel("Contact found",
			new[] { new DynamicVariableEntry("name", "one"), new DynamicVariableEntry(secondKey, "two") });
		await editor.SaveAsync();
		editor.Result.Should().BeNull();
		editor.Error.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void Switching_contact_scenarios_retains_each_scenarios_edited_values()
	{
		var vm = NewViewModel(Substitute.For<IRealtimeConversationClient>());
		vm.DynamicVariables.Single(v => v.Key == "caller_id_norm").Value = "111";
		vm.SelectedVariableScenario = vm.VariableScenarios[1];
		vm.DynamicVariables.Single(v => v.Key == "caller_id_norm").Value = "222";
		vm.SelectedVariableScenario = vm.VariableScenarios[0];
		vm.DynamicVariables.Single(v => v.Key == "caller_id_norm").Value.Should().Be("111");
		vm.SelectedVariableScenario = vm.VariableScenarios[1];
		vm.DynamicVariables.Single(v => v.Key == "caller_id_norm").Value.Should().Be("222");
	}

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
		vm.DynamicVariables.Single(variable => variable.Key == "contact_name").Value.Should().Be("Joe Messia");
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
	public async Task A_session_that_lands_after_Dispose_is_released_instead_of_stranding_the_client()
	{
		var session = new FakeSession();
		var client = Substitute.For<IRealtimeConversationClient>();
		var gate = new TaskCompletionSource<IRealtimeConversationSession>();
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(_ =>
			{
				// The agent is switched while the connect is still in
				// flight — exactly the race that used to leave the
				// singleton client permanently "already active".
				return gate.Task;
			});
		var vm = NewViewModel(client);

		var start = vm.StartAsync();
		vm.Dispose();
		gate.SetResult(session);
		await start;

		// Give the fire-and-forget DisposeAsync continuation a turn.
		await Task.Yield();

		vm.CanStart.Should().BeTrue("the dead view model must not hold a session");
		session.DisposeCount.Should().Be(1);
		await client.Received(1).StartAsync(
			Arg.Any<RealtimeConversationOptions>(),
			Arg.Any<CancellationToken>());
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
		vm.StatusText.Should().Be("In call");
		vm.ConversationId.Should().Be("conv_123");
		vm.Mode.Should().Be(RealtimeConversationMode.Speaking);
		vm.Transcript.Should().ContainSingle(turn => turn.Text == "Welcome");
		vm.IsConnected.Should().BeTrue();
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

	[Fact]
	public async Task Session_end_clears_listening_volume_and_the_idle_status()
	{
		var session = new FakeSession();
		var client = Substitute.For<IRealtimeConversationClient>();
		client.StartAsync(Arg.Any<RealtimeConversationOptions>(), Arg.Any<CancellationToken>())
			.Returns(session);
		var vm = NewViewModel(client);
		await vm.StartAsync();
		session.RaiseStatus(RealtimeConversationStatus.Connected, "conv_123");
		session.RaiseMode(RealtimeConversationMode.Listening);
		session.RaiseVolume(0.42f, 0.87f);
		vm.ShowConnectionStatus.Should().BeTrue();
		vm.ShowMode.Should().BeTrue();
		vm.IsConnected.Should().BeTrue();

		session.RaiseStatus(RealtimeConversationStatus.Disconnected, "conv_123");

		vm.Mode.Should().Be(RealtimeConversationMode.Unknown);
		vm.ShowMode.Should().BeFalse();
		vm.InputVolume.Should().Be(0f);
		vm.OutputVolume.Should().Be(0f);
		vm.ShowConnectionStatus.Should().BeFalse();
		vm.IsConnected.Should().BeFalse();
		vm.Elapsed.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public async Task Saved_parameters_reload_from_the_json_file_after_restart()
	{
		var path = Path.Combine(Path.GetTempPath(), "els-vars-" + Guid.NewGuid().ToString("n") + ".json");
		var store = new JsonDynamicVariableStore(path);
		var windows = Substitute.For<IWindowManager>();
		windows.ShowDialogAsync(Arg.Any<object>()).Returns(async call =>
		{
			var editor = (DynamicVariablesDialogViewModel)call.ArgAt<object>(0);
			editor.DynamicVariables.Single(v => v.Key == "contact_name").Value = "Saved Name";
			await editor.SaveAsync();
			return (bool?)true;
		});
		var client = Substitute.For<IRealtimeConversationClient>();
		try
		{
			using (var vm = NewViewModel(client, windows, store))
			{
				await vm.EditDynamicVariablesAsync();
			}

			using var reloaded = NewViewModel(Substitute.For<IRealtimeConversationClient>(), variables: store);
			reloaded.DynamicVariables.Single(v => v.Key == "contact_name").Value.Should().Be("Saved Name");
			reloaded.DynamicVariables.Single(v => v.Key == "lookup_status").Value.Should().Be("found");
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	private static LiveConversationViewModel NewViewModel(
		IRealtimeConversationClient client,
		IWindowManager? windows = null,
		IDynamicVariableStore? variables = null) => new(
		SampleAgent(),
		client,
		Substitute.For<IDialogService>(),
		new ManualClockService(),
		NullLogger<LiveConversationViewModel>.Instance,
		windows,
		variables);

	private sealed class FakeSession : IRealtimeConversationSession
	{
		public string? ConversationId { get; private set; }
		public RealtimeConversationStatus Status { get; private set; } = RealtimeConversationStatus.Connecting;
		public int StopCount { get; private set; }
		public int DisposeCount { get; private set; }
		public event EventHandler<RealtimeConversationStatusChangedEventArgs>? StatusChanged;
		public event EventHandler<RealtimeConversationModeChangedEventArgs>? ModeChanged;
		public event EventHandler<RealtimeTranscriptEventArgs>? TranscriptReceived;
		public event EventHandler<RealtimeConversationVolumeChangedEventArgs>? VolumeChanged;
		public event EventHandler<RealtimeConversationErrorEventArgs>? Error;

		// Text is not surfaced anywhere in the app anymore (the live session is
		// voice only), so the test double just satisfies the session contract.
		public Task SendTextAsync(string text, CancellationToken ct = default) => Task.CompletedTask;

		public Task SetMutedAsync(bool muted, CancellationToken ct = default) => Task.CompletedTask;

		public Task StopAsync(CancellationToken ct = default)
		{
			StopCount++;
			RaiseStatus(RealtimeConversationStatus.Disconnected, ConversationId);
			return Task.CompletedTask;
		}

		public ValueTask DisposeAsync()
		{
			DisposeCount++;
			return ValueTask.CompletedTask;
		}

		public void RaiseStatus(RealtimeConversationStatus status, string? conversationId)
		{
			Status = status;
			ConversationId = conversationId;
			StatusChanged?.Invoke(this, new RealtimeConversationStatusChangedEventArgs(status, conversationId));
		}

		public void RaiseMode(RealtimeConversationMode mode) =>
			ModeChanged?.Invoke(this, new RealtimeConversationModeChangedEventArgs(mode));

		public void RaiseVolume(float input, float output) =>
			VolumeChanged?.Invoke(this, new RealtimeConversationVolumeChangedEventArgs(input, output));

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
