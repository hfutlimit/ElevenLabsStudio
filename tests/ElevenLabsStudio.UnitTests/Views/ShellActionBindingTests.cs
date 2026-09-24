using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Infrastructure;
using ElevenLabsStudio.ViewModels;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.ViewModels.Agents;
using ElevenLabsStudio.Views;
using ElevenLabsStudio.Views.Agents;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class ShellActionBindingTests
{
	[Fact]
	public async Task Settings_button_invokes_bound_ViewModel_action()
	{
		await WpfTestHost.RunAsync(RunSettingsActionProbe);
	}

	private static void RunSettingsActionProbe()
	{
		IoC.BuildUp = _ => { };
		IoC.GetAllInstances = _ => Array.Empty<object>();

		var client = Substitute.For<IElevenLabsClient>();
		client.ListAgentsAsync(Arg.Any<CancellationToken>())
			.Returns(Array.Empty<AgentSummary>());
		var dialog = Substitute.For<IDialogService>();
		var events = Substitute.For<IEventAggregator>();
		var drafts = Substitute.For<IDraftStore>();
		var suggestions = Substitute.For<ISuggestionEngine>();
		var windows = Substitute.For<IWindowManager>();
		var detailFactory = new AgentDetailViewModelFactory(
			client,
			suggestions,
			dialog,
			events,
			drafts,
			NullLogger<AgentDetailViewModel>.Instance);
		var agents = new AgentListViewModel(
			client,
			dialog,
			events,
			drafts,
			detailFactory,
			NullLogger<AgentListViewModel>.Instance,
			suggestions,
			windows);

		var options = Substitute.For<IOptionsMonitor<ElevenLabsOptions>>();
		options.CurrentValue.Returns(new ElevenLabsOptions { Mock = true });
		var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
		var settings = new SettingsViewModel(
			options,
			dialog,
			NullLogger<SettingsViewModel>.Instance,
			configuration);
		var shell = new ShellViewModel(agents, settings, windows);
		var view = new ShellView();
		ViewModelBinder.Bind(shell, view, null);
		view.Show();

		var button = (Button)view.FindName("OpenSettings");
		button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		var invoked = SpinWait.SpinUntil(
			() => windows.ReceivedCalls().Any(),
			TimeSpan.FromSeconds(2));

		invoked.Should().BeTrue("the Caliburn action must reach ShellViewModel.OpenSettingsAsync");
		windows.ReceivedCalls().Should().ContainSingle();
		windows.ClearReceivedCalls();

		var agentListView = new AgentListView();
		ViewModelBinder.Bind(agents, agentListView, null);
		var agentListHost = new Window { Content = agentListView };
		agentListHost.Show();
		var importButton = (Button)agentListView.FindName("ImportAgent");
		importButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		var importInvoked = SpinWait.SpinUntil(
			() => windows.ReceivedCalls().Any(),
			TimeSpan.FromSeconds(2));
		importInvoked.Should().BeTrue("the nested AgentListView action must reach its ViewModel");
		agentListHost.Close();

		if (!AssemblySource.Instance.Contains(typeof(SettingsView).Assembly))
		{
			AssemblySource.Instance.Add(typeof(SettingsView).Assembly);
		}
		SettingsView? opened = null;
		Dispatcher.CurrentDispatcher.BeginInvoke(() =>
		{
			opened = Application.Current.Windows.OfType<SettingsView>().SingleOrDefault();
			if (opened is not null)
			{
				opened.DialogResult = false;
			}
		});
		var realWindows = new WindowManager();
		realWindows.ShowDialogAsync(settings).GetAwaiter().GetResult();
		opened.Should().NotBeNull("WindowManager must resolve and show SettingsView");
		view.Close();
	}

	private sealed class NoopDisposable : IDisposable
	{
		public void Dispose()
		{
		}
	}
}
