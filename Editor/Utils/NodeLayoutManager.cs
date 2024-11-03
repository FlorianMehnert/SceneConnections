using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SceneConnections.Editor.Utils
{
    public abstract class NodeLayoutManager
    {
        private const float DefaultNodeWidth = 100.0f;
        private const float DefaultNodeHeight = 200.0f;
        private const float HorizontalSpacing = 50.0f;
        private const float VerticalSpacing = 50.0f;
        private const float InitialX = 100.0f;
        private const float InitialY = 100.0f;

        public static void LayoutNodes(List<Node> nodes, bool silent = false)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            // Calculate optimal grid dimensions
            var totalNodes = nodes.Count;
            var gridColumns = CalculateOptimalColumnCount(totalNodes);
            var gridRows = Mathf.CeilToInt((float)totalNodes / gridColumns);

            // Find maximum node dimensions to ensure consistent spacing
            var maxNodeDimensions = GetMaxNodeDimensions(nodes);

            if (!silent)
            {
                Debug.Log($"Grid: {gridRows}x{gridColumns}, Total Nodes: {totalNodes}");
                Debug.Log($"Max Node Dimensions: {maxNodeDimensions}");
            }


            // Position each node in the grid
            for (var i = 0; i < nodes.Count; i++)
            {
                var row = i / gridColumns;
                var col = i % gridColumns;

                var position = new Vector2(
                    InitialX + col * (maxNodeDimensions.x + HorizontalSpacing),
                    InitialY + row * (maxNodeDimensions.y + VerticalSpacing)
                );

                SetNodePosition(nodes[i], position, maxNodeDimensions);
            }
        }

        private static int CalculateOptimalColumnCount(int nodeCount)
        {
            // Aim for a golden ratio-like aspect ratio (1.618)
            const float targetAspectRatio = 1.618f;

            // Calculate columns based on desired aspect ratio
            var columns = Mathf.RoundToInt(Mathf.Sqrt(nodeCount * targetAspectRatio));

            // Ensure we have at least one column
            return Mathf.Max(1, columns);
        }

        private static Vector2 GetMaxNodeDimensions(List<Node> nodes)
        {
            var maxWidth = DefaultNodeWidth;
            var maxHeight = DefaultNodeHeight;

            foreach (var node in nodes)
            {
                var currentRect = node.GetPosition();

                // Only consider non-zero dimensions
                if (currentRect.width > 0)
                    maxWidth = Mathf.Max(maxWidth, currentRect.width);
                if (currentRect.height > 0)
                    maxHeight = Mathf.Max(maxHeight, currentRect.height);
            }

            return new Vector2(maxWidth, maxHeight);
        }

        private static void SetNodePosition(Node node, Vector2 position, Vector2 standardSize)
        {
            var currentRect = node.GetPosition();
            var width = currentRect.width > 0 ? currentRect.width : standardSize.x;
            var height = currentRect.height > 0 ? currentRect.height : standardSize.y;

            var newRect = new Rect(position, new Vector2(width, height));
            node.SetPosition(newRect);
        }
    }
}