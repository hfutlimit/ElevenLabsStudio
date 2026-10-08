using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.ViewModels.AgentDetail;
using ElevenLabsStudio.Views.AgentDetail;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElevenLabsStudio.UnitTests.Views;

/// <summary>
/// Row actions are declared with Caliburn bindings instead of being handled
/// in code-behind. A compile-clean XAML proves nothing about that wiring —
/// the binding only resolves when the button is actually clicked — so these
/// tests raise a real Click and assert the view model reacted.
/// </summary>
public sealed class CaliburnRowActionTests
{
	[Fact]
	public Task Variables_delete_button_calls_the_view_model() => WpfTestHost.RunAsync(() =>
	{
		var vm = NewVariablesViewModel();
		vm.Variables.Add(new Variable("tone", "formal", "string"));
		var view = new VariablesTabView { DataContext = vm };

		WithWindow(view, 900, 500, host =>
		{
			host.UpdateLayout();
			var remove = Descendants(view).OfType<Button>()
				.Single(button => button.ToolTip?.ToString() == "Delete variable");

			remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			vm.Variables.Should().BeEmpty(
				"the Caliburn action must reach RemoveVariable with the row's variable");
		});
	});

	[Fact]
	public Task Workflow_remove_node_button_calls_the_view_model() => WpfTestHost.RunAsync(() =>
	{
		var node = new WorkflowNode("tool-1", "tool", "Do the thing", 40, 60);
		var vm = new WorkflowTabViewModel(
			Agent.Empty("agent_actions") with { Workflow = new Workflow([node], null, []) });
		var view = new WorkflowTabView { DataContext = vm };

		WithWindow(view, 1100, 640, host =>
		{
			host.UpdateLayout();
			var remove = Descendants(view).OfType<Button>()
				.Single(button => button.ToolTip?.ToString() == "Remove node");

			remove.DataContext.Should().BeOfType<WorkflowCanvasNode>();
			remove.Tag.Should().Be("tool-1");

			remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			vm.Nodes.Should().BeEmpty(
				"the Caliburn action must reach RemoveNodeById with the id from $dataContext.Id");
		});
	});

	private static VariablesTabViewModel NewVariablesViewModel() =>
		new(Agent.Empty("agent_actions"), NullLogger<VariablesTabViewModel>.Instance);

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

	private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
	{
		for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			yield return child;
			foreach (var nested in Descendants(child)) yield return nested;
		}
	}
}