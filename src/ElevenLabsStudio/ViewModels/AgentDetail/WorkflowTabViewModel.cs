using Caliburn.Micro;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.MVVM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// A display-ready node projection for the workflow board. The domain keeps
/// server coordinates optional; the board always receives finite coordinates
/// so it can render both real and newly-added nodes.
/// </summary>
public sealed record WorkflowCanvasNode(
	string Id,
	string Type,
	string Name,
	double X,
	double Y);

/// <summary>
/// A display-ready edge projection. Endpoints are calculated from the node
/// cards rather than stored in the API payload, so the line stays aligned
/// when the board layout changes.
/// </summary>
public sealed record WorkflowCanvasEdge(
	string Id,
	string Source,
	string Target,
	double X1,
	double Y1,
	double X2,
	double Y2,
	string? Condition);

/// <summary>
/// Workflow editor backing the visual board. It keeps the editable domain
/// nodes for push/draft compatibility and exposes a separate projection for
/// the WPF canvas. The API's raw workflow JSON remains the source of truth for
/// fields this editor does not own.
/// </summary>
public sealed class WorkflowTabViewModel : ScreenBase
{
	private const double NodeWidth = 236;
	private const double NodeHeight = 108;
	private const double BoardPadding = 38;
	private const double HorizontalGap = 92;
	private const double VerticalGap = 68;

	private readonly ILogger<WorkflowTabViewModel> _logger;
	private IReadOnlyList<WorkflowEdge> _workflowEdges = Array.Empty<WorkflowEdge>();
	private bool _suppressNodeProjection;

	public BindableCollection<WorkflowNode> Nodes { get; } = new();

	/// <summary>Nodes with finite coordinates for ItemsControl/Canvas.</summary>
	public BindableCollection<WorkflowCanvasNode> CanvasNodes { get; } = new();

	/// <summary>Edges with calculated line endpoints for the board.</summary>
	public BindableCollection<WorkflowCanvasEdge> Edges { get; } = new();

	private string _rawJson = string.Empty;
	public string RawJson
	{
		get => _rawJson;
		private set => Set(ref _rawJson, value);
	}

	private double _canvasWidth = 720;
	public double CanvasWidth
	{
		get => _canvasWidth;
		private set => Set(ref _canvasWidth, value);
	}

	private double _canvasHeight = 420;
	public double CanvasHeight
	{
		get => _canvasHeight;
		private set => Set(ref _canvasHeight, value);
	}

	private bool _isDirty;
	public bool IsDirty
	{
		get => _isDirty;
		internal set => Set(ref _isDirty, value);
	}

	public WorkflowTabViewModel(Agent agent)
		: this(agent, NullLogger<WorkflowTabViewModel>.Instance) { }

	public WorkflowTabViewModel(Agent agent, ILogger<WorkflowTabViewModel> logger)
	{
		_logger = logger;
		Nodes.CollectionChanged += OnNodesChanged;
		Apply(agent);
	}

	public void RefreshFrom(Agent updated) => Apply(updated);

	public void AddNode()
	{
		var nextNumber = 1;
		string nextId;
		do
		{
			nextId = $"n{nextNumber++}";
		}
		while (Nodes.Any(node => string.Equals(node.Id, nextId, StringComparison.Ordinal)));

		Nodes.Add(new WorkflowNode(nextId, "custom", "New Node"));
		IsDirty = true;
		_logger.LogDebug("Added workflow node {NodeId}", nextId);
	}

	public void RemoveNode(WorkflowNode? node)
	{
		if (node is null || !Nodes.Remove(node)) return;

		IsDirty = true;
		_logger.LogDebug("Removed workflow node {NodeId}", node.Id);
	}

	public IReadOnlyList<WorkflowNode> GetCurrentNodes() => Nodes.ToList();

	private void Apply(Agent agent)
	{
		_suppressNodeProjection = true;
		try
		{
			Nodes.Clear();
			foreach (var node in agent.Workflow.Nodes)
			{
				Nodes.Add(node);
			}
		}
		finally
		{
			_suppressNodeProjection = false;
		}

		RawJson = agent.Workflow.RawJson ?? string.Empty;
		_workflowEdges = agent.Workflow.Edges.ToList();
		RebuildCanvas();
		IsDirty = false;
	}

	private void OnNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		if (!_suppressNodeProjection)
		{
			RebuildCanvas();
		}
	}

	private void RebuildCanvas(IReadOnlyList<WorkflowEdge>? sourceEdges = null)
	{
		var nodes = Nodes.ToList();
		var allHaveServerPositions = nodes.Count > 0
			&& nodes.All(node => node.X is not null && node.Y is not null);
		var coordinates = allHaveServerPositions
			? LayoutServerNodes(nodes)
			: LayoutFallbackNodes(nodes);

		CanvasNodes.Clear();
		foreach (var node in nodes)
		{
			var point = coordinates[node.Id];
			CanvasNodes.Add(new WorkflowCanvasNode(node.Id, node.Type, node.Name, point.X, point.Y));
		}

		var byId = CanvasNodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
		var edges = sourceEdges ?? _workflowEdges;
		Edges.Clear();
		foreach (var edge in edges)
		{
			if (!byId.TryGetValue(edge.Source, out var source)
				|| !byId.TryGetValue(edge.Target, out var target))
			{
				continue;
			}

			Edges.Add(new WorkflowCanvasEdge(
				edge.Id,
				edge.Source,
				edge.Target,
				source.X + NodeWidth,
				source.Y + (NodeHeight / 2),
				target.X,
				target.Y + (NodeHeight / 2),
				edge.Condition));
		}

		if (CanvasNodes.Count == 0)
		{
			CanvasWidth = 720;
			CanvasHeight = 420;
			return;
		}

		CanvasWidth = Math.Max(720, CanvasNodes.Max(node => node.X) + NodeWidth + BoardPadding);
		CanvasHeight = Math.Max(420, CanvasNodes.Max(node => node.Y) + NodeHeight + BoardPadding);
	}

	private static Dictionary<string, (double X, double Y)> LayoutServerNodes(
		IReadOnlyList<WorkflowNode> nodes)
	{
		var minX = nodes.Min(node => node.X!.Value);
		var minY = nodes.Min(node => node.Y!.Value);
		var maxX = nodes.Max(node => node.X!.Value);
		var maxY = nodes.Max(node => node.Y!.Value);
		var span = Math.Max(maxX - minX, maxY - minY);
		var scale = span > 980 ? 980 / span : 1d;

		return nodes.ToDictionary(
			node => node.Id,
			node => (
				X: BoardPadding + ((node.X!.Value - minX) * scale),
				Y: BoardPadding + ((node.Y!.Value - minY) * scale)),
			StringComparer.Ordinal);
	}

	private static Dictionary<string, (double X, double Y)> LayoutFallbackNodes(
		IReadOnlyList<WorkflowNode> nodes)
	{
		var columns = Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(nodes.Count, 1))), 1, 3);
		return nodes.Select((node, index) => new
			{
				node.Id,
				X = BoardPadding + (index % columns) * (NodeWidth + HorizontalGap),
				Y = BoardPadding + (index / columns) * (NodeHeight + VerticalGap),
			})
			.ToDictionary(item => item.Id, item => (item.X, item.Y), StringComparer.Ordinal);
	}
}
