using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Caliburn.Micro;
using ElevenLabsStudio.Core.Abstractions;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.Views.AgentDetail;
using ElevenLabsStudio.Views.Agents;
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

	[Fact]
	public async Task Agent_list_is_bound_to_the_view_model_collection()
	{
		await RunOnStaAsync(() =>
		{
			var stub = new AgentListStub(hasNoAgents: false, items:
			[
				new AgentSummary("agent_layout", "Layout agent", "voice_1", DateTimeOffset.UnixEpoch),
			]);
			var view = new AgentListView { DataContext = stub, Width = 310 };
			var host = new Window { Content = view };
			host.Show();

			var list = view.FindName("AgentsView").Should().BeOfType<ListBox>().Subject;

			list.ItemsSource.Should().BeSameAs(stub.AgentsView,
				"the sidebar list must render the view model's collection");
			list.Items.Count.Should().Be(1);
			host.Close();
		});
	}

	[Fact]
	public async Task Conversations_list_is_bound_to_the_view_model_collection()
	{
		await RunOnStaAsync(() =>
		{
			var stub = new ConversationsStub();
			var view = new ConversationsTabView { DataContext = stub, Width = 700 };
			var host = new Window { Content = view };
			host.Show();

			var list = view.FindName("Conversations").Should().BeOfType<ListBox>().Subject;

			list.ItemsSource.Should().BeSameAs(stub.Conversations,
				"the conversations list must render the view model's collection");
			host.Close();
		});
	}

	[Fact]
	public async Task Agent_list_row_template_keeps_icon_and_text_vertically_aligned()
	{
		await RunOnStaAsync(() =>
		{
			var stub = new AgentListStub(hasNoAgents: false, items:
			[
				new AgentSummary("agent_layout", "Layout agent", "voice_1", DateTimeOffset.UnixEpoch),
			]);
			var view = new AgentListView { DataContext = stub, Width = 310 };
			var host = new Window { Content = view };
			host.Show();

			var item = view.FindName("AgentsView").Should().BeOfType<ListBox>().Subject
				.ItemContainerGenerator.ContainerFromIndex(0)
				.Should().BeOfType<ListBoxItem>().Subject;
			var presenter = VisualDescendants(item).OfType<ContentPresenter>().Single();
			presenter.VerticalAlignment.Should().Be(VerticalAlignment.Stretch,
				"the row template must stretch the ContentPresenter so the icon and text align");
			presenter.HorizontalAlignment.Should().Be(HorizontalAlignment.Stretch);
			host.Close();
		});
	}

	[Fact]
	public async Task First_message_meta_row_sits_below_the_editor_not_at_the_bottom()
	{
		await RunOnStaAsync(() =>
		{
			var view = new FirstMessageTabView();
			var host = new Window { Content = view, Width = 900, Height = 600 };
			host.Show();

			var grid = VisualDescendants(host)
				.OfType<Grid>()
				.First(g => g.RowDefinitions.Count == 2
					&& VisualDescendants(g).OfType<TextBox>().Any());
			grid.RowDefinitions[0].Height.Should().Be(new GridLength(1, GridUnitType.Star),
				"the editor takes the remaining space");
			grid.RowDefinitions[1].Height.Should().Be(GridLength.Auto,
				"the meta row must be Auto-sized so it hugs the editor instead of sliding to the bottom");

			// Sanity-check the meta row actually exists: the char counter lives
			// inside a Border docked to the right of a DockPanel in row 1.
			var counter = view.FindName("FirstMessageLength").Should().BeOfType<TextBlock>().Subject;
			var dock = FindAncestor<DockPanel>(counter);
			dock.Should().NotBeNull();
			dock!.LastChildFill.Should().BeTrue();
			host.Close();
		});
	}

	[Fact]
	public async Task Empty_agent_state_text_wraps_inside_the_sidebar_card()
	{
		await RunOnStaAsync(() =>
		{
			var view = new AgentListView
			{
				DataContext = new AgentListStub(hasNoAgents: true, items: []),
				Width = 310,
			};
			var host = new Window { Content = view };
			host.Show();

			var message = VisualDescendants(view)
				.OfType<TextBlock>()
				.Single(text => text.Text.StartsWith("No agents yet", StringComparison.Ordinal));

			// The message sits inside a card with 16px side padding, so it
			// must wrap instead of running past the sidebar column.
			message.ActualWidth.Should().BeLessOrEqualTo(310 - 32 - 24);
			host.Close();
		});
	}

	[Fact]
	public async Task Workflow_board_has_an_inspector_panel_beside_the_canvas()
	{
		await RunOnStaAsync(() =>
		{
			var view = new WorkflowTabView();
			var host = new Window { Content = view };
			host.Show();
			var frame = view.FindName("WorkflowCanvasFrame")
				.Should().BeOfType<Border>().Subject;
			var splitter = view.FindName("InspectorSplitter")
				.Should().BeOfType<GridSplitter>().Subject;
			var inspector = view.FindName("WorkflowInspector")
				.Should().BeOfType<Border>().Subject;

			Grid.GetColumn(frame).Should().Be(0);
			Grid.GetColumn(splitter).Should().Be(1);
			Grid.GetColumn(inspector).Should().Be(2);
			splitter.ResizeDirection.Should().Be(GridResizeDirection.Columns);
			host.Close();
		});
	}

	[Fact]
	public async Task Workflow_inspector_width_follows_a_drag_on_the_splitter()
	{
		await RunOnStaAsync(() =>
		{
			var workflow = new Workflow(
				new[] { new WorkflowNode("tool-1", "tool", "", 10, 20) },
				null,
				Array.Empty<WorkflowEdge>());
			var agent = new Agent(
				"agent_layout", "Layout", "prompt", "hello", null,
				Array.Empty<Variable>(), workflow, DateTimeOffset.UtcNow);
			var vm = new WorkflowTabViewModel(agent);
			vm.SelectNodeById("tool-1");

			var view = new WorkflowTabView { DataContext = vm };
			var host = new Window { Content = view, Width = 1240, Height = 720 };
			host.Show();

			var splitter = view.FindName("InspectorSplitter")
				.Should().BeOfType<GridSplitter>().Subject;
			splitter.Visibility.Should().Be(Visibility.Visible);
			var column = ((Grid)splitter.Parent).ColumnDefinitions[2];
			var startWidth = column.ActualWidth;

			// A real pointer drag arrives as a burst of Thumb drag events;
			// GridSplitter only re-sizes the columns on that pipeline.
			// The test host loads the app dictionaries but not the WPF theme
			// that normally supplies the splitter's Thumb, so install the
			// template its control contract requires.
			splitter.Template = (ControlTemplate)XamlReader.Parse(
				"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'"
				+ " TargetType='GridSplitter'><Thumb Name='Thumb' /></ControlTemplate>");
			splitter.ApplyTemplate();

			// The shipped splitter previews the drag through an adorner.
			// A unit-test window exposes no adorner layer, so drive the
			// direct-resize branch instead; both branches end in the same
			// ResizeColumns + Min/Max clamping this test asserts.
			splitter.ShowsPreview = false;

			var thumb = VisualDescendants(splitter).OfType<Thumb>().Single();
			var canvas = ((Grid)splitter.Parent).ColumnDefinitions[0];
			var beforeCanvas = canvas.ActualWidth;
			var beforeInspector = column.ActualWidth;
			Drag(thumb, +140);
			host.UpdateLayout();
			var afterCanvas = canvas.ActualWidth;
			var afterInspector = column.ActualWidth;

			// Dragging right shrinks the inspector and grows the canvas.
			afterInspector.Should().BeLessThan(beforeInspector,
				"dragging the right-hand splitter right must narrow the inspector column");
			afterCanvas.Should().BeGreaterThan(beforeCanvas,
				"the freed space must flow into the canvas column to its left");
			(beforeCanvas + beforeInspector).Should().BeApproximately(afterCanvas + afterInspector, 1,
				"the columns share the available width, so the total must stay constant");

			Drag(thumb, -140);
			host.UpdateLayout();

			column.ActualWidth.Should().BeGreaterOrEqualTo(240,
				"the inspector must not collapse below its declared minimum");
			column.ActualWidth.Should().BeLessOrEqualTo(560,
				"the inspector must not grow past its declared maximum");
			host.Close();
		});
	}

	private static void Drag(Thumb thumb, double horizontal)
	{
		const int steps = 7;
		thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
		for (var step = 0; step < steps; step++)
		{
			// (horizontalChange, verticalChange). The splitter only consumes
			// the axis that matches its ResizeDirection, but the contract is
			// two doubles in this order regardless of orientation.
			thumb.RaiseEvent(new DragDeltaEventArgs(horizontal / steps, 0));
		}

		thumb.RaiseEvent(new DragCompletedEventArgs(horizontal, 0, false));
	}

	private sealed class AgentListStub
	{
		public bool HasNoAgents { get; }
		public ICollectionView AgentsView { get; }
		public AgentSummary? SelectedAgent { get; set; }

		public AgentListStub(bool hasNoAgents, IEnumerable<AgentSummary> items)
		{
			HasNoAgents = hasNoAgents;
			AgentsView = CollectionViewSource.GetDefaultView(items.ToList());
		}
	}

	private sealed class ConversationsStub
	{
		public BindableCollection<object> Conversations { get; } = new();
		public object? SelectedConversation { get; set; }
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

	private static T? FindAncestor<T>(DependencyObject? descendant) where T : DependencyObject
	{
		for (var current = VisualTreeHelper.GetParent(descendant ?? throw new InvalidOperationException()); current is not null; current = VisualTreeHelper.GetParent(current))
		{
			if (current is T match)
			{
				return match;
			}
		}

		return null;
	}
}
