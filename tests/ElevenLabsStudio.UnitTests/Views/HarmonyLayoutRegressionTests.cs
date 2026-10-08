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
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class HarmonyLayoutRegressionTests
{
	[Theory]
	[InlineData(698, 544)]
	[InlineData(1056, 720)]
	public Task Switching_tabs_keeps_the_tab_strip_and_content_bounds_stable(int width, int height) => WpfTestHost.RunAsync(() =>
	{
		using var vm = new AgentDetailViewModel(
			Agent.Empty("phone_agent"), Substitute.For<IElevenLabsClient>(),
			Substitute.For<ISuggestionEngine>(), Substitute.For<IDialogService>(),
			Substitute.For<IEventAggregator>(), Substitute.For<IDraftStore>(),
			NullLogger<AgentDetailViewModel>.Instance,
			Substitute.For<IRealtimeConversationClient>(), Substitute.For<IClockService>());
		var detail = new AgentDetailView { DataContext = vm };
		WithWindow(detail, width, height, host =>
		{
			var tabs = Descendants(detail).OfType<TabControl>().Single();
			var content = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
			var tabTop = tabs.TranslatePoint(new Point(), detail).Y;
			var contentTop = content.TranslatePoint(new Point(), detail).Y;
			var contentHeight = content.ActualHeight;
			foreach (var item in tabs.Items.OfType<TabItem>())
			{
				tabs.SelectedItem = item;
				host.UpdateLayout();
				tabs.TranslatePoint(new Point(), detail).Y.Should().BeApproximately(tabTop, 1);
				content.TranslatePoint(new Point(), detail).Y.Should().BeApproximately(contentTop, 1);
				content.ActualHeight.Should().BeApproximately(contentHeight, 1);
			}
		});
	});

	[Theory]
	[InlineData(1000, 600)]
	[InlineData(1360, 840)]
	public Task Every_tab_keeps_its_content_inside_the_fixed_tab_region(int width, int height) => WpfTestHost.RunAsync(() =>
	{
		using var vm = new AgentDetailViewModel(
			Agent.Empty("phone_agent"), Substitute.For<IElevenLabsClient>(),
			Substitute.For<ISuggestionEngine>(), Substitute.For<IDialogService>(),
			Substitute.For<IEventAggregator>(), Substitute.For<IDraftStore>(),
			NullLogger<AgentDetailViewModel>.Instance,
			Substitute.For<IRealtimeConversationClient>(), Substitute.For<IClockService>());
		var detail = new AgentDetailView();
		WithWindow(detail, width, height, host =>
		{
			var tabs = Descendants(detail).OfType<TabControl>().Single();
			var content = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
			var footer = Descendants(detail).OfType<Border>()
				.Single(border => Math.Abs(border.ActualHeight - 56) < 0.5 && border.ActualHeight > 0);
			var footerTop = footer.TranslatePoint(new Point(), detail).Y;

			foreach (var item in tabs.Items.OfType<TabItem>())
			{
				// Build each tab's real view by hand: going through Caliburn
				// would depend on ViewLocator static state left behind by
				// whichever test ran before this one.
				((ContentControl)item.Content).Content = ViewFor((string)item.Header, vm);
				tabs.SelectedItem = item;
				host.UpdateLayout();

				var label = (string)item.Header;
				content.ClipToBounds.Should().BeTrue(
					$"{label}: the tab region must clip, or an unbounded view paints over the footer");

				// Nothing may render below the region: that spill is exactly
				// what the user saw as "the tab changed height".
				foreach (var element in Descendants(content).OfType<FrameworkElement>())
				{
					if (element.ActualHeight <= 0 || !element.IsVisible) continue;
					if (IsClippedByAncestor(element, content)) continue;
					var bottom = element.TranslatePoint(new Point(0, element.ActualHeight), detail).Y;
					bottom.Should().BeLessThanOrEqualTo(
						footerTop + 1,
						$"{label}: {element.GetType().Name} renders past the tab region into the footer");
				}
			}
		});
	});

	private static bool IsClippedByAncestor(FrameworkElement element, DependencyObject stopAt)
	{
		for (var parent = VisualTreeHelper.GetParent(element); parent is FrameworkElement fe; parent = VisualTreeHelper.GetParent(fe))
		{
			if (fe is ScrollViewer or Border { ClipToBounds: true } or Grid { ClipToBounds: true }) return true;
			if (ReferenceEquals(fe, stopAt)) break;
		}
		return false;
	}

	private static FrameworkElement ViewFor(string header, AgentDetailViewModel vm) => header switch
	{
		"New Conversation" => new LiveConversationView { DataContext = vm.LiveConversationVm },
		"System Prompt" => new SystemPromptTabView { DataContext = vm.SystemPromptVm },
		"First Message" => new FirstMessageTabView { DataContext = vm.FirstMessageVm },
		"Workflow" => new WorkflowTabView { DataContext = vm.WorkflowVm },
		"Variables" => new VariablesTabView { DataContext = vm.VariablesVm },
		"Conversations" => new ConversationsTabView { DataContext = vm.ConversationsVm },
		_ => throw new InvalidOperationException($"unknown tab {header}"),
	};

	[Theory]
	[InlineData(1000, 700)]
	[InlineData(698, 544)]
	public Task Selecting_an_agent_opens_the_new_conversation_workspace(int width, int height) => WpfTestHost.RunAsync(() =>
	{
		using var vm = new AgentDetailViewModel(
			Agent.Empty("phone_agent"), Substitute.For<IElevenLabsClient>(),
			Substitute.For<ISuggestionEngine>(), Substitute.For<IDialogService>(),
			Substitute.For<IEventAggregator>(), Substitute.For<IDraftStore>(),
			NullLogger<AgentDetailViewModel>.Instance,
			Substitute.For<IRealtimeConversationClient>(), Substitute.For<IClockService>());
		var detail = new AgentDetailView { DataContext = vm };
		WithWindow(detail, width, height, host =>
		{
			var tabs = Descendants(detail).OfType<TabControl>().Single();
			var selected = (TabItem)tabs.SelectedItem;
			selected.Header.Should().Be("New Conversation");
			Caliburn.Micro.View.GetModel((ContentControl)selected.Content)
				.Should().BeSameAs(vm.LiveConversationVm);
			var live = Descendants(detail).OfType<LiveConversationView>().Single();
			var scenario = Descendants(live).OfType<ComboBox>().Single();
			scenario.TranslatePoint(new Point(0, scenario.ActualHeight), host).Y.Should().BeLessThan(host.ActualHeight);
			((Button)live.FindName("EditDynamicVariablesAsync")).IsVisible.Should().BeTrue();
			var transcript = Descendants(live).OfType<ScrollViewer>().Single(viewer =>
				BindingOperations.GetBinding(viewer, UIElement.VisibilityProperty)?.Path.Path == "HasTranscript");
			((FrameworkElement)transcript.Parent).ActualHeight.Should().BeGreaterThan(80,
				"audio controls must leave room for the live transcript at minimum window height");
		});
	});

	[Fact]
	public Task Tab_headers_have_a_full_height_click_target() => WpfTestHost.RunAsync(() =>
	{
		var tab = new TabItem
		{
			Header = "System Prompt",
			Style = (Style)Application.Current.FindResource("App.TabItem"),
			VerticalAlignment = VerticalAlignment.Top,
		};
		WithWindow(tab, 500, 200, host =>
		{
			tab.ActualHeight.Should().BeGreaterOrEqualTo(44,
				"the custom template must retain the tab strip's vertical breathing room");
		});
	});

	[Fact]
	public Task Parameter_dialog_edits_values_without_changing_the_source_until_saved() => WpfTestHost.RunAsync(() =>
	{
		var source = new DynamicVariableEntry("caller_id_norm", "original");
		var vm = new DynamicVariablesDialogViewModel("Contact found", new[] { source });
		var view = new DynamicVariablesDialogView { DataContext = vm };
		WithWindow(view, 800, 760, host =>
		{
			var value = Descendants(view).OfType<TextBox>().First(box =>
				BindingOperations.GetBinding(box, TextBox.TextProperty)?.Path.Path == "Value");
			value.SetCurrentValue(TextBox.TextProperty, "edited-for-call");
			vm.DynamicVariables[0].Value.Should().Be("edited-for-call");
			source.Value.Should().Be("original");
			value.ActualWidth.Should().BeGreaterThan(400);
			value.TranslatePoint(new Point(0, value.ActualHeight), host).Y.Should().BeLessThan(host.ActualHeight);
		});
	});

	[Fact]
	public Task Parameter_dialog_shows_most_of_the_scenario_without_scrolling() => WpfTestHost.RunAsync(() =>
	{
		// Contact found ships nine variables at roughly 88px each.
		var vm = new DynamicVariablesDialogViewModel(
			"Contact found",
			Services.ReferenceTesterInitialWebhookVariables.Scenarios[0].Variables
				.Select(pair => new DynamicVariableEntry(pair.Key, pair.Value?.ToString() ?? string.Empty)));
		var view = new DynamicVariablesDialogView { DataContext = vm };
		WithWindow(view, 800, 760, host =>
		{
			host.UpdateLayout();
			var scroll = Descendants(view).OfType<ScrollViewer>()
				.Single(viewer => viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);
			scroll.ViewportHeight.Should().BeGreaterThan(400,
				"the dialog must be tall enough to show most of a nine-variable scenario at once");

			var save = (Button)view.FindName("SaveAsync");
			save.TranslatePoint(new Point(0, save.ActualHeight), host).Y.Should().BeLessThan(host.ActualHeight,
				"the action row must stay inside the dialog, not below its edge");
		});
	});

	[Theory]
	[InlineData("System Prompt")]
	[InlineData("First Message")]
	public Task Editors_fill_the_tab_region_and_ignore_their_own_text(string header) => WpfTestHost.RunAsync(() =>
	{
		var detail = new AgentDetailView();
		WithWindow(detail, 1800, 700, host =>
		{
			var tabs = Descendants(detail).OfType<TabControl>().Single();
			var item = tabs.Items.OfType<TabItem>().Single(tab => Equals(tab.Header, header));
			var content = (ContentControl)item.Content;
			UserControl editorView = header == "System Prompt" ? new SystemPromptTabView() : new FirstMessageTabView();
			content.Content = editorView;
			tabs.SelectedItem = item;
			var editor = (TextBox)editorView.FindName(header == "System Prompt" ? "Prompt" : "FirstMessage");
			editor.Text = "Short text";
			host.UpdateLayout();

			// No measure cap any more: the editor must span the whole tab
			// region, not sit as a 980px column in the middle of a wide pane.
			// A ContentPresenter's ActualWidth already excludes its own
			// Margin, so the only slack is the editor frame's 1px border.
			var region = (ContentPresenter)tabs.Template.FindName("PART_SelectedContentHost", tabs);
			editor.ActualWidth.Should().BeApproximately(region.ActualWidth - 2, 2,
				"the editor must fill the tab region instead of being capped");

			var left = editor.TranslatePoint(new Point(), detail).X;
			var right = detail.ActualWidth - left - editor.ActualWidth;
			Math.Abs(left - right).Should().BeLessThan(5);
			var width = editor.ActualWidth;
			editor.Text = new string('W', 500);
			host.UpdateLayout();
			editor.ActualWidth.Should().BeApproximately(width, 1);
		});
	});

	[Fact]
	public Task Closing_a_resized_workflow_inspector_releases_and_restores_its_column() => WpfTestHost.RunAsync(() =>
	{
		var workflow = new Workflow(
			new[] { new WorkflowNode("tool-1", "tool", "", 10, 20) }, null, Array.Empty<WorkflowEdge>());
		var vm = new WorkflowTabViewModel(Agent.Empty("agent_layout") with { Workflow = workflow });
		vm.SelectNodeById("tool-1");
		var view = new WorkflowTabView { DataContext = vm };
		WithWindow(view, 1100, 600, host =>
		{
			var splitter = (GridSplitter)view.FindName("InspectorSplitter");
			var grid = (Grid)splitter.Parent;
			var column = grid.ColumnDefinitions[2];
			Drag(splitter, -60);
			host.UpdateLayout();
			var resizedWidth = column.ActualWidth;
			resizedWidth.Should().BeGreaterThan(320);
			vm.ClearSelection();
			host.UpdateLayout();
			column.ActualWidth.Should().Be(0, "closing the inspector must also collapse a pixel width left by dragging");
			vm.SelectNodeById("tool-1");
			host.UpdateLayout();
			column.ActualWidth.Should().BeApproximately(resizedWidth, 1);
		});
	});

	private static void WithWindow(FrameworkElement content, double width, double height, Action<Window> check)
	{
		var host = new Window
		{
			Content = content,
			Width = width,
			Height = height,
			WindowStyle = WindowStyle.None,
			ResizeMode = ResizeMode.NoResize,
			ShowActivated = false,
			Left = -10000,
			Top = -10000,
		};
		try
		{
			host.Show();
			host.UpdateLayout();
			check(host);
		}
		finally
		{
			host.Close();
		}
	}

	private static void Drag(GridSplitter splitter, double delta)
	{
		splitter.Template = (ControlTemplate)XamlReader.Parse(
			"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'"
			+ " TargetType='GridSplitter'><Thumb Name='Thumb' /></ControlTemplate>");
		splitter.ApplyTemplate();
		splitter.ShowsPreview = false;
		var thumb = Descendants(splitter).OfType<Thumb>().Single();
		thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
		thumb.RaiseEvent(new DragDeltaEventArgs(delta, 0));
		thumb.RaiseEvent(new DragCompletedEventArgs(delta, 0, false));
	}

	private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			yield return child;
			foreach (var descendant in Descendants(child)) yield return descendant;
		}
	}
}
