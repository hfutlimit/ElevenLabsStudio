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
	string DisplayName,
	double X,
	double Y,
	bool IsSelected = false);

/// <summary>
/// A display-ready edge projection. Endpoints are calculated from the node
/// cards rather than stored in the API payload, so the line stays aligned
/// when the board layout changes. <see cref="ConditionKind"/> is the
/// human-readable classifier (LLM condition / Tool result / …).
/// </summary>
public sealed record WorkflowCanvasEdge(
	string Id,
	string Source,
	string Target,
	string SourceName,
	string TargetName,
	double X1,
	double Y1,
	double X2,
	double Y2,
	string ConditionKind,
	string? ConditionText,
	string? ConditionLabel,
	bool IsSelected = false);

/// <summary>
/// Workflow editor backing the visual board. It keeps the editable domain
/// nodes for push/draft compatibility and exposes a separate projection for
/// the WPF canvas. The API's raw workflow JSON remains the source of truth for
/// fields this editor does not own. Selecting a node or an edge opens the
/// inspector panel on the right side of the board.
/// </summary>
public sealed class WorkflowTabViewModel : ScreenBase
{
	private const double NodeWidth = 236;
	private const double NodeHeight = 64;
	private const double BoardPadding = 38;
	private const double HorizontalGap = 92;
	private const double VerticalGap = 48;
	private const double OverlapNudge = 32;

	private readonly ILogger<WorkflowTabViewModel> _logger;
	private IReadOnlyList<WorkflowEdge> _workflowEdges = Array.Empty<WorkflowEdge>();
	private bool _suppressNodeProjection;
	private string? _selectedNodeId;
	private string? _selectedEdgeId;
	private IReadOnlyList<string> _selectedNodeConnections = Array.Empty<string>();

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

	public string? SelectedNodeId => _selectedNodeId;

	public WorkflowNode? SelectedNode =>
		_selectedNodeId is null
			? null
			: Nodes.FirstOrDefault(node => string.Equals(node.Id, _selectedNodeId, StringComparison.Ordinal));

	public string SelectedNodeName
	{
		// Unlabelled nodes show the friendly type fallback here too —
		// the raw (possibly empty) domain name would read as a
		// meaningless server id.
		get => SelectedNode is null ? string.Empty : DisplayNameOf(SelectedNode);
		set => RenameSelectedNode(value);
	}

	public bool HasSelectedNode => _selectedNodeId is not null;

	public bool HasSelectedEdge => _selectedEdgeId is not null;

	public bool HasInspector => HasSelectedNode || HasSelectedEdge;

	public WorkflowCanvasEdge? SelectedEdge =>
		_selectedEdgeId is null
			? null
			: Edges.FirstOrDefault(edge => string.Equals(edge.Id, _selectedEdgeId, StringComparison.Ordinal));

	public IReadOnlyList<string> SelectedNodeConnections
	{
		get => _selectedNodeConnections;
		private set => Set(ref _selectedNodeConnections, value);
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

		if (string.Equals(_selectedNodeId, node.Id, StringComparison.Ordinal))
		{
			_selectedNodeId = null;
			OnSelectionChanged();
		}

		IsDirty = true;
		_logger.LogDebug("Removed workflow node {NodeId}", node.Id);
	}

	public IReadOnlyList<WorkflowNode> GetCurrentNodes() => Nodes.ToList();

	/// <summary>Called from the board's node cards via Caliburn.</summary>
	public void SelectNode(WorkflowCanvasNode node)
	{
		if (node is not null)
		{
			SelectNodeById(node.Id);
		}
	}

	public void SelectNodeById(string id)
	{
		if (!Nodes.Any(node => string.Equals(node.Id, id, StringComparison.Ordinal))
			|| (string.Equals(_selectedNodeId, id, StringComparison.Ordinal) && _selectedEdgeId is null))
		{
			return;
		}

		_selectedNodeId = id;
		_selectedEdgeId = null;
		OnSelectionChanged();
	}

	public void SelectEdgeById(string id)
	{
		if (!_workflowEdges.Any(edge => string.Equals(edge.Id, id, StringComparison.Ordinal))
			|| (string.Equals(_selectedEdgeId, id, StringComparison.Ordinal) && _selectedNodeId is null))
		{
			return;
		}

		_selectedEdgeId = id;
		_selectedNodeId = null;
		OnSelectionChanged();
	}

	public void ClearSelection()
	{
		if (_selectedNodeId is null && _selectedEdgeId is null) return;

		_selectedNodeId = null;
		_selectedEdgeId = null;
		OnSelectionChanged();
	}

	private void RenameSelectedNode(string? value)
	{
		var node = SelectedNode;
		var trimmed = value?.Trim() ?? string.Empty;
		if (node is null
			|| trimmed.Length == 0
			|| string.Equals(node.Name, trimmed, StringComparison.Ordinal))
		{
			return;
		}

		var index = Nodes.IndexOf(node);
		if (index < 0) return;

		// Record replacement triggers OnNodesChanged, which rebuilds the
		// canvas projections with the new name.
		Nodes[index] = node with { Name = trimmed };
		IsDirty = true;
		NotifyOfPropertyChange(nameof(SelectedNode));
		NotifyOfPropertyChange(nameof(SelectedNodeName));
		_logger.LogDebug("Renamed workflow node {NodeId} to {Name}", node.Id, trimmed);
	}

	private void OnSelectionChanged()
	{
		RebuildCanvas();
		NotifyOfPropertyChange(nameof(SelectedNodeId));
		NotifyOfPropertyChange(nameof(SelectedNode));
		NotifyOfPropertyChange(nameof(SelectedNodeName));
		NotifyOfPropertyChange(nameof(HasSelectedNode));
		NotifyOfPropertyChange(nameof(SelectedEdge));
		NotifyOfPropertyChange(nameof(HasSelectedEdge));
		NotifyOfPropertyChange(nameof(HasInspector));
	}

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
		_selectedNodeId = null;
		_selectedEdgeId = null;
		RebuildCanvas();
		NotifyOfPropertyChange(nameof(HasSelectedNode));
		NotifyOfPropertyChange(nameof(HasSelectedEdge));
		NotifyOfPropertyChange(nameof(HasInspector));
		IsDirty = false;
	}

	private void OnNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		if (!_suppressNodeProjection)
		{
			RebuildCanvas();
		}
	}

	private void RebuildCanvas()
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
			CanvasNodes.Add(new WorkflowCanvasNode(
				node.Id,
				node.Type,
				node.Name,
				DisplayNameOf(node),
				point.X,
				point.Y,
				string.Equals(node.Id, _selectedNodeId, StringComparison.Ordinal)));
		}

		var byId = CanvasNodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
		Edges.Clear();
		foreach (var edge in _workflowEdges)
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
				source.DisplayName,
				target.DisplayName,
				source.X + NodeWidth,
				source.Y + (NodeHeight / 2),
				target.X,
				target.Y + (NodeHeight / 2),
				DescribeEdge(edge),
				ConditionTextOf(edge),
				edge.ConditionLabel,
				string.Equals(edge.Id, _selectedEdgeId, StringComparison.Ordinal)));
		}

		SelectedNodeConnections = BuildConnectionSummary();

		if (CanvasNodes.Count == 0)
		{
			CanvasWidth = 720;
			CanvasHeight = 420;
			return;
		}

		CanvasWidth = Math.Max(720, CanvasNodes.Max(node => node.X) + NodeWidth + BoardPadding);
		CanvasHeight = Math.Max(420, CanvasNodes.Max(node => node.Y) + NodeHeight + BoardPadding);
	}

	private IReadOnlyList<string> BuildConnectionSummary()
	{
		if (_selectedNodeId is null)
		{
			return Array.Empty<string>();
		}

		var lines = new List<string>();
		foreach (var edge in Edges)
		{
			if (string.Equals(edge.Source, _selectedNodeId, StringComparison.Ordinal))
			{
				lines.Add($"→ {edge.TargetName} · {edge.ConditionKind}");
			}
			else if (string.Equals(edge.Target, _selectedNodeId, StringComparison.Ordinal))
			{
				lines.Add($"← {edge.SourceName} · {edge.ConditionKind}");
			}
		}

		return lines;
	}

	private static string DisplayNameOf(WorkflowNode node) =>
		string.IsNullOrWhiteSpace(node.Name) ? FriendlyType(node.Type) : node.Name;

	private static string FriendlyType(string type) => type switch
	{
		"start" => "Start",
		"tool" => "Tool",
		"override_agent" => "Agent step",
		"agent" => "Agent",
		"conversation" => "Conversation",
		_ => string.IsNullOrWhiteSpace(type) ? "Node" : type,
	};

	private static string DescribeEdge(WorkflowEdge edge) => edge.ConditionType switch
	{
		"llm" => "LLM condition",
		"result" => edge.ConditionSuccessful switch
		{
			true => "Tool result · success",
			false => "Tool result · failed",
			_ => "Tool result",
		},
		"expression" => "Expression",
		"unconditional" => "Unconditional",
		_ => edge.Condition is null ? "Unconditional" : "Condition",
	};

	private static string? ConditionTextOf(WorkflowEdge edge) =>
		string.Equals(edge.ConditionType, "llm", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(edge.ConditionType, "expression", StringComparison.OrdinalIgnoreCase)
			? edge.Condition
			: null;

	private static Dictionary<string, (double X, double Y)> LayoutServerNodes(
		IReadOnlyList<WorkflowNode> nodes)
	{
		var minX = nodes.Min(node => node.X!.Value);
		var minY = nodes.Min(node => node.Y!.Value);
		var maxX = nodes.Max(node => node.X!.Value);
		var maxY = nodes.Max(node => node.Y!.Value);
		var span = Math.Max(maxX - minX, maxY - minY);
		var scale = span > 980 ? 980 / span : 1d;

		// Server coordinates are scaled, not re-laid-out, so two nodes whose
		// server positions sit close together would render stacked. Push
		// each overlapping card straight down until its rect is clear.
		var placed = new List<(double X, double Y)>();
		var coordinates = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
		foreach (var node in nodes)
		{
			var x = BoardPadding + ((node.X!.Value - minX) * scale);
			var y = BoardPadding + ((node.Y!.Value - minY) * scale);
			while (placed.Any(p =>
				x < p.X + NodeWidth && p.X < x + NodeWidth &&
				y < p.Y + NodeHeight && p.Y < y + NodeHeight))
			{
				y += OverlapNudge;
			}

			placed.Add((x, y));
			coordinates[node.Id] = (x, y);
		}

		return coordinates;
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
