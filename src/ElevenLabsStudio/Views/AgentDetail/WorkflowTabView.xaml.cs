using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ElevenLabsStudio.ViewModels.AgentDetail;

namespace ElevenLabsStudio.Views.AgentDetail;

public partial class WorkflowTabView : UserControl
{
	private WorkflowTabViewModel? _viewModel;

	public WorkflowTabView()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
		Loaded += OnLoaded;
		Unloaded += (_, _) => DetachViewModel();
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (DataContext is WorkflowTabViewModel vm)
		{
			AttachViewModel(vm);
		}

		QueueEdgeDraw();
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is WorkflowTabViewModel vm)
		{
			AttachViewModel(vm);
		}
		else
		{
			DetachViewModel();
		}

		QueueEdgeDraw();
	}

	private void AttachViewModel(WorkflowTabViewModel vm)
	{
		if (ReferenceEquals(_viewModel, vm)) return;

		DetachViewModel();
		_viewModel = vm;
		_viewModel.Edges.CollectionChanged += OnGraphChanged;
		_viewModel.CanvasNodes.CollectionChanged += OnGraphChanged;
	}

	private void DetachViewModel()
	{
		if (_viewModel is null) return;

		_viewModel.Edges.CollectionChanged -= OnGraphChanged;
		_viewModel.CanvasNodes.CollectionChanged -= OnGraphChanged;
		_viewModel = null;
	}

	private void OnGraphChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueEdgeDraw();

	private void QueueEdgeDraw()
	{
		if (!IsLoaded) return;

		Dispatcher.BeginInvoke(
			DispatcherPriority.Loaded,
			new Action(DrawEdges));
	}

	private void DrawEdges()
	{
		EdgeCanvas.Children.Clear();
		if (_viewModel is null) return;

		var lineBrush = TryFindResource("App.Accent") as Brush
			?? TryFindResource("App.BorderStrong") as Brush
			?? Brushes.SlateBlue;
		var arrowBrush = TryFindResource("App.Accent") as Brush ?? Brushes.SlateBlue;

		foreach (var edge in _viewModel.Edges)
		{
			var dx = edge.X2 - edge.X1;
			var dy = edge.Y2 - edge.Y1;
			var length = Math.Sqrt((dx * dx) + (dy * dy));
			if (length < 1) continue;

			var line = new Line
			{
				X1 = edge.X1,
				Y1 = edge.Y1,
				X2 = edge.X2,
				Y2 = edge.Y2,
				Stroke = lineBrush,
				StrokeThickness = 2,
				Opacity = 1,
				SnapsToDevicePixels = true,
			};
			EdgeCanvas.Children.Add(line);

			var ux = dx / length;
			var uy = dy / length;
			var px = -uy;
			var py = ux;
			var arrowBaseX = edge.X2 - (ux * 10);
			var arrowBaseY = edge.Y2 - (uy * 10);

			var arrow = new Polygon
			{
				Points = new PointCollection
				{
					new(edge.X2, edge.Y2),
					new(arrowBaseX + (px * 4), arrowBaseY + (py * 4)),
					new(arrowBaseX - (px * 4), arrowBaseY - (py * 4)),
				},
				Fill = arrowBrush,
				Opacity = 0.9,
			};
			EdgeCanvas.Children.Add(arrow);
		}
	}

	private void RemoveCanvasNode_Click(object sender, RoutedEventArgs e)
	{
		if (sender is not Button button
			|| button.Tag is not string nodeId
			|| DataContext is not WorkflowTabViewModel vm)
		{
			return;
		}

		vm.RemoveNode(vm.Nodes.FirstOrDefault(node =>
			string.Equals(node.Id, nodeId, StringComparison.Ordinal)));
	}
}
