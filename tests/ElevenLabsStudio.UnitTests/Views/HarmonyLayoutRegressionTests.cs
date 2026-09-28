using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.Views.AgentDetail;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class HarmonyLayoutRegressionTests
{
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

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public Task Editors_keep_a_stable_centered_width_when_text_changes(int tabIndex) => WpfTestHost.RunAsync(() =>
	{
		var detail = new AgentDetailView();
		WithWindow(detail, 1200, 700, host =>
		{
			var tabs = Descendants(detail).OfType<TabControl>().Single();
			var item = (TabItem)tabs.Items[tabIndex];
			var content = (ContentControl)item.Content;
			UserControl editorView = tabIndex == 0 ? new SystemPromptTabView() : new FirstMessageTabView();
			content.Content = editorView;
			tabs.SelectedIndex = tabIndex;
			var editor = (TextBox)editorView.FindName(tabIndex == 0 ? "Prompt" : "FirstMessage");
			editor.Text = "Short text";
			host.UpdateLayout();
			var left = editor.TranslatePoint(new Point(), detail).X;
			var right = detail.ActualWidth - left - editor.ActualWidth;
			editor.ActualWidth.Should().BeGreaterThan(940);
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

	[Fact]
	public Task Live_columns_fit_the_minimum_window_before_and_after_resizing() => WpfTestHost.RunAsync(() =>
	{
		var view = new LiveConversationView();
		WithWindow(view, 638, 550, host =>
		{
			var splitter = Descendants(view).OfType<GridSplitter>().Single();
			var grid = (Grid)splitter.Parent;
			void AssertFits()
			{
				var columnsRight = grid.TranslatePoint(new Point(grid.ColumnDefinitions.Sum(c => c.ActualWidth), 0), host).X;
				columnsRight.Should().BeLessOrEqualTo(host.ActualWidth - 19,
					"column minima must fit the host, not enlarge the grid beyond its layout slot");
				var stop = (Button)view.FindName("StopAsync");
				var right = stop.TranslatePoint(new Point(stop.ActualWidth, 0), host).X;
				right.Should().BeLessOrEqualTo(host.ActualWidth);
			}
			AssertFits();
			host.Width = 1100;
			host.UpdateLayout();
			var widthBeforeDrag = grid.ColumnDefinitions[0].ActualWidth;
			Drag(splitter, -60);
			host.UpdateLayout();
			grid.ColumnDefinitions[0].ActualWidth.Should().BeLessThan(widthBeforeDrag - 20,
				"the resize must change star weights before the host is narrowed again");
			host.Width = 638;
			host.UpdateLayout();
			AssertFits();
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
