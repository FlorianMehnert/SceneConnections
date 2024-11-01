using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public abstract class NodeLayoutManager
{
    private const float DefaultNodeWidth = 100.0f;
    private const float DefaultNodeHeight = 200.0f;
    private const float HorizontalSpacing = 50.0f;
    private const float VerticalSpacing = 50.0f;

    public static void LayoutNodes(List<Node> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return;

        // Calculate grid dimensions based on the number of nodes
        var gridSize = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count));

        // Starting position (top-left of the layout area)
        var startPosition = new Vector2(100.0f, 100.0f);

        // Position each node in a grid
        for (var i = 0; i < nodes.Count; i++)
        {
            var row = i / gridSize;
            var col = i % gridSize;

            var nodeSize = GetNodeSize(nodes[i]);
            var position = new Vector2(
                startPosition.x + col * (nodeSize.x + HorizontalSpacing),
                startPosition.y + row * (nodeSize.y + VerticalSpacing)
            );

            SetNodePosition(nodes[i], position);
        }
    }

    private static Vector2 GetNodeSize(Node node)
    {
        // Try to get actual node size, fallback to defaults if not available
        var currentRect = node.GetPosition();
        var width = currentRect.width > 0 ? currentRect.width : DefaultNodeWidth;
        var height = currentRect.height > 0 ? currentRect.height : DefaultNodeHeight;

        return new Vector2(width, height);
    }

    private static void SetNodePosition(Node node, Vector2 position)
    {
        var size = GetNodeSize(node);
        var newRect = new Rect(position, size);
        node.SetPosition(newRect);
    }
}