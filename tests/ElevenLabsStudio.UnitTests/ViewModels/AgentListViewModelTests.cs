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

    private static (AgentListViewModel vm, IElevenLabsClient client, IEventAggregator events, IWindowManager windows)
        Build()
    {
        var client = Substitute.For<IElevenLabsClient>();
        client.ListAgentsAsync(Arg.Any<CancellationToken>())
              .Returns(new[] { SampleAgent("a1"), SampleAgent("a2"), SampleAgent("a3") });
        var events = Substitute.For<IEventAggregator>();
        var windows = Substitute.For<IWindowManager>();
        var dialog = Substitute.For<IDialogService>();
        var suggestions = Substitute.For<ISuggestionEngine>();
        var vm = new AgentListViewModel(
            client, dialog, events, NullLogger<AgentListViewModel>.Instance, suggestions, windows);
        return (vm, client, events, windows);
    }

    [Fact]
    public void Ctor_kicks_off_LoadAsync_immediately()
    {
        var (vm, _, _, _) = Build();

        // ctor fires `_ = LoadAsync()`; the awaited ListAgentsAsync
        // returns the mock list within a tick or two. Yield once to
        // let the continuation run.
        SpinWait.SpinUntil(() => vm.Agents.Count == 3, TimeSpan.FromSeconds(2));

        vm.Agents.Should().HaveCount(3);
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
    public async Task LoadAsync_keeps_existing_selection_when_already_set()
    {
        var (vm, _, _, _) = Build();
        await vm.LoadAsync();
        vm.SelectedAgent = vm.Agents.First(a => a.AgentId == "a2");

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
              .Returns<Task<IReadOnlyList<Agent>>>(_ => throw new ElevenLabsAuthException("nope"));
        var dialog = Substitute.For<IDialogService>();
        var events = Substitute.For<IEventAggregator>();
        var vm = new AgentListViewModel(
            client, dialog, events, NullLogger<AgentListViewModel>.Instance,
            Substitute.For<ISuggestionEngine>(), Substitute.For<IWindowManager>());

        await vm.LoadAsync();

        await dialog.Received().ShowErrorAsync("鉴权失败", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void FilterText_filter_AgentsView_keeps_matching_only()
    {
        var (vm, _, _, _) = Build();
        SpinWait.SpinUntil(() => vm.Agents.Count == 3, TimeSpan.FromSeconds(2));

        vm.FilterText = "support"; // matches none of the seeded names
        // ICollectionView Filter is private; reach it via reflection.
        var view = (System.ComponentModel.ICollectionView)vm.GetType()
            .GetProperty("AgentsView")!.GetValue(vm)!;
        var matches = view.Cast<Agent>().ToList();
        matches.Should().BeEmpty();
    }

    [Fact]
    public void HasNoAgents_true_before_load_completes()
    {
        // Block the client so LoadAsync never returns.
        var tcs = new TaskCompletionSource<IReadOnlyList<Agent>>();
        var client = Substitute.For<IElevenLabsClient>();
        client.ListAgentsAsync(Arg.Any<CancellationToken>()).Returns(tcs.Task);
        var vm = new AgentListViewModel(
            client, Substitute.For<IDialogService>(), Substitute.For<IEventAggregator>(),
            NullLogger<AgentListViewModel>.Instance,
            Substitute.For<ISuggestionEngine>(), Substitute.For<IWindowManager>());

        vm.HasNoAgents.Should().BeTrue();
        tcs.SetResult(Array.Empty<Agent>());
    }
}