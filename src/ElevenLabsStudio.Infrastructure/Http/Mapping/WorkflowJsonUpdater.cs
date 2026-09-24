using System.Text.Json.Nodes;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;

namespace ElevenLabsStudio.Infrastructure.Http.Mapping;

internal static class WorkflowJsonUpdater
{
	public static JsonObject Apply(Workflow edit)
	{
		try
		{
			return ApplyCore(edit);
		}
		catch (WorkflowUpdateException)
		{
			throw;
		}
		catch (Exception ex) when (ex is System.Text.Json.JsonException
			or InvalidOperationException
			or FormatException
			or KeyNotFoundException
			or ArgumentException)
		{
			// The original try/catch only wrapped the initial parse, so a
			// shape problem halfway through the mutation (wrong node type,
			// a value that will not coerce) escaped as a raw framework
			// exception and surfaced as an unmapped 500 upstream.
			throw new WorkflowUpdateException("Workflow JSON could not be applied.", ex);
		}
	}

	private static JsonObject ApplyCore(Workflow edit)
	{
		if (string.IsNullOrWhiteSpace(edit.RawJson))
		{
			throw new WorkflowUpdateException("Workflow cannot be updated without its original JSON document.");
		}

		JsonObject root;
		try
		{
			root = JsonNode.Parse(edit.RawJson)?.AsObject()
				?? throw new WorkflowUpdateException("Workflow JSON root is not an object.");
		}
		catch (WorkflowUpdateException)
		{
			throw;
		}
		catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
		{
			throw new WorkflowUpdateException("Workflow JSON is invalid.", ex);
		}

		if (root["nodes"] is not JsonObject nodes)
		{
			throw new WorkflowUpdateException("Workflow nodes must be an object keyed by node ID.");
		}
		if (root["edges"] is not null && root["edges"] is not JsonObject)
		{
			throw new WorkflowUpdateException("Workflow edges must be an object keyed by edge ID.");
		}

		var editedIds = edit.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
		var removedIds = nodes.Select(pair => pair.Key)
			.Where(id => !editedIds.Contains(id))
			.ToHashSet(StringComparer.Ordinal);
		if (root["edges"] is JsonObject edges)
		{
			foreach (var edge in edges)
			{
				if (edge.Value is not JsonObject edgeObject)
				{
					continue;
				}
				// GetValue<string>() throws InvalidOperationException on a
				// node/array/number endpoint, which took down the whole
				// update for one odd edge. Treat "not a string" as "not a
				// node reference" and leave the edge alone.
				var source = ReadNodeReference(edgeObject["source"]);
				var target = ReadNodeReference(edgeObject["target"]);
				if ((source is not null && removedIds.Contains(source))
					|| (target is not null && removedIds.Contains(target)))
				{
					throw new WorkflowUpdateException(
						$"Node removal would leave edge '{edge.Key}' referencing a missing node.");
				}
			}
		}

		foreach (var removedId in removedIds)
		{
			nodes.Remove(removedId);
		}
		foreach (var node in edit.Nodes)
		{
			if (nodes[node.Id] is not JsonObject nodeObject)
			{
				nodeObject = new JsonObject();
				nodes[node.Id] = nodeObject;
			}
			nodeObject["type"] = node.Type;
			// Unlabelled nodes keep whatever label the server has: an
			// empty local name must never fabricate or clear one.
			if (!string.IsNullOrWhiteSpace(node.Name))
			{
				if (nodeObject.ContainsKey("name") && !nodeObject.ContainsKey("label"))
				{
					nodeObject["name"] = node.Name;
				}
				else
				{
					nodeObject["label"] = node.Name;
				}
			}
			if (nodeObject.ContainsKey("id"))
			{
				nodeObject["id"] = node.Id;
			}
			ApplyPosition(nodeObject, node);
		}

		return root;
	}

	private static string? ReadNodeReference(JsonNode? node) =>
		node is JsonValue value && value.TryGetValue<string>(out var text)
			? text
			: null;

	private static void ApplyPosition(JsonObject nodeObject, WorkflowNode node)
	{
		if (node.X is null && node.Y is null)
		{
			return;
		}

		var position = nodeObject["position"] as JsonObject;
		if (position is null)
		{
			position = new JsonObject();
			nodeObject["position"] = position;
		}

		if (node.X is not null)
		{
			position["x"] = JsonValue.Create(node.X.Value);
		}
		if (node.Y is not null)
		{
			position["y"] = JsonValue.Create(node.Y.Value);
		}
	}
}
