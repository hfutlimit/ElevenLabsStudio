using System.Text.Json.Nodes;
using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Core.Exceptions;

namespace ElevenLabsStudio.Infrastructure.Http.Mapping;

internal static class WorkflowJsonUpdater
{
	public static JsonObject Apply(Workflow edit)
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
				var source = edgeObject["source"]?.GetValue<string>();
				var target = edgeObject["target"]?.GetValue<string>();
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
			if (nodeObject.ContainsKey("name") && !nodeObject.ContainsKey("label"))
			{
				nodeObject["name"] = node.Name;
			}
			else
			{
				nodeObject["label"] = node.Name;
			}
			if (nodeObject.ContainsKey("id"))
			{
				nodeObject["id"] = node.Id;
			}
		}

		return root;
	}
}
