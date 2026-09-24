using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.Views.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class WorkspaceLayoutTests
{
	[Fact]
	public async Task Conversations_list_and_transcript_use_a_resizable_horizontal_split()
	{
		await RunOnStaAsync(() =>
		{
			var view = new ConversationsTabView();
			var host = new Window { Content = view };
			host.Show();
			var listPanel = view.FindName("ConversationListPanel").Should().BeOfType<Border>().Subject;
			var transcriptPanel = view.FindName("TranscriptPanel").Should().BeOfType<Border>().Subject;
			var splitter = view.FindName("ConversationSplitter").Should().BeOfType<GridSplitter>().Subject;

			Grid.GetColumn(listPanel).Should().Be(0);
			Grid.GetColumn(splitter).Should().Be(1);
			Grid.GetColumn(transcriptPanel).Should().Be(2);
			splitter.ResizeDirection.Should().Be(GridResizeDirection.Columns);
			view.FindName("ConversationListLoadingIndicator").Should().BeAssignableTo<FrameworkElement>();
			view.FindName("TranscriptLoadingIndicator").Should().BeAssignableTo<FrameworkElement>();
			host.Close();
		});
	}

	[Fact]
	public async Task Workflow_add_node_action_is_part_of_the_canvas()
	{
		await RunOnStaAsync(() =>
		{
			var view = new WorkflowTabView();
			var host = new Window { Content = view };
			host.Show();
			var canvasFrame = view.FindName("WorkflowCanvasFrame")
				.Should().BeOfType<Border>().Subject;
			var addNode = view.FindName("AddNode").Should().BeOfType<Button>().Subject;

			VisualDescendants(canvasFrame).Should().Contain(addNode,
				"the primary workflow action should sit directly on the canvas");
			host.Close();
		});
	}

	[Fact]
	public async Task Sidebar_toggle_switches_between_expanded_and_compact_layouts()
	{
		await RunOnStaAsync(() =>
		{
			var view = new ShellView();
			view.Show();
			var column = view.FindName("SidebarColumn").Should().BeOfType<ColumnDefinition>().Subject;
			var expandedHost = view.FindName("SidebarHost").Should().BeOfType<ContentControl>().Subject;
			var compactRail = view.FindName("CollapsedSidebarRail").Should().BeAssignableTo<FrameworkElement>().Subject;
			var toggle = view.FindName("SidebarToggle").Should().BeOfType<Button>().Subject;

			toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			column.Width.Value.Should().Be(48);
			expandedHost.Visibility.Should().Be(Visibility.Collapsed);
			compactRail.Visibility.Should().Be(Visibility.Visible);

			toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			column.Width.Value.Should().Be(310);
			expandedHost.Visibility.Should().Be(Visibility.Visible);
			compactRail.Visibility.Should().Be(Visibility.Collapsed);
			view.Close();
		});
	}

	[Fact]
	public async Task First_message_editor_is_the_only_visible_message_copy()
	{
		await RunOnStaAsync(() =>
		{
			const string message = "A unique first-message value";
			var agent = Agent.Empty("agent_layout") with { FirstMessage = message };
			var suggestions = Substitute.For<ISuggestionEngine>();
			suggestions.Analyze(Arg.Any<Agent>(), Arg.Any<AgentUpdate>())
				.Returns(Array.Empty<Suggestion>());
			var vm = new FirstMessageTabViewModel(
				agent,
				suggestions,
				NullLogger<FirstMessageTabViewModel>.Instance);
			var view = new FirstMessageTabView { DataContext = vm };
			var editor = view.FindName("FirstMessage").Should().BeOfType<TextBox>().Subject;
			editor.Text = message;
			var host = new Window { Content = view };
			host.Show();

			var visibleCopies = VisualDescendants(view)
				.Count(element => element switch
				{
					TextBox textBox => textBox.Text == message,
					TextBlock textBlock => textBlock.Text == message,
					_ => false,
				});

			visibleCopies.Should().Be(1,
				"the greeting should not be repeated in a separate preview");
			host.Close();
		});
	}

	private static Task RunOnStaAsync(System.Action action) => WpfTestHost.RunAsync(action);

	private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
	{
		for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
		{
			var child = VisualTreeHelper.GetChild(parent, index);
			yield return child;
			foreach (var descendant in VisualDescendants(child))
			{
				yield return descendant;
			}
		}
	}
}
