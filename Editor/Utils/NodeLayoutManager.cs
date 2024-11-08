using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

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
        private static EventCallback<GeometryChangedEvent> _geometryChangedCallback;

        // Dictionary to store the dimensions of each node
        private static readonly Dictionary<Node, Vector2> NodeDimensions = new();
        private static Dictionary<Node, bool> NodeReceivedDimension => new();
        private static bool _layoutUpdated;

        public static void LayoutNodes(List<Node> nodes, bool silent = false)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            NodeDimensions.Clear();
            _geometryChangedCallback = evt => OnNodeGeometryChanged(evt.target as Node, nodes, silent);

            foreach (var node in nodes)
            {
                NodeReceivedDimension[node] = false;
                node.RegisterCallback(_geometryChangedCallback);
                _layoutUpdated = false;
            }
        }

        private static void OnNodeGeometryChanged(Node node, List<Node> nodes, bool silent)
        {
            // Get the current dimensions of the node
            var rect = node.GetPosition();
            var nodeSize = new Vector2(rect.width, rect.height);

            // Only store dimensions if they are valid (non-zero)
            if (nodeSize is { x: > 0, y: > 0 })
            {
                NodeDimensions[node] = nodeSize;
                NodeReceivedDimension[node] = true;
            }

            // Check if all nodes have received valid dimensions
            if (NodeDimensions.Count != nodes.Count) return;
            // Unregister the GeometryChanged callback from all nodes
            foreach (var n in nodes)
            {
                n.UnregisterCallback(_geometryChangedCallback);
            }

            if (!NodeReceivedDimension.All(kvp => kvp.Value) || _layoutUpdated) return;
            _layoutUpdated = true;  // Make sure this is set to true to prevent re-layout
            PerformLayout(nodes, silent);
        }

        private static void PerformLayout(List<Node> nodes, bool silent)
        {
            // Calculate optimal grid dimensions
            var totalNodes = nodes.Count;
            var gridColumns = CalculateOptimalColumnCount(totalNodes);
            var gridRows = Mathf.CeilToInt((float)totalNodes / gridColumns);

            // Find maximum node dimensions to ensure consistent spacing
            var maxNodeDimensions = GetMaxNodeDimensions();

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

        private static Vector2 GetMaxNodeDimensions()
        {
            var maxWidth = DefaultNodeWidth;
            var maxHeight = DefaultNodeHeight;

            // Calculate maximum dimensions across all nodes
            foreach (var size in NodeDimensions.Values)
            {
                maxWidth = Mathf.Max(maxWidth, size.x);
                maxHeight = Mathf.Max(maxHeight, size.y);
            }

            return new Vector2(maxWidth, maxHeight);
        }

        private static void SetNodePosition(Node node, Vector2 position, Vector2 standardSize)
        {
            var width = NodeDimensions.TryGetValue(node, out var dimension) ? dimension.x : standardSize.x;
            var height = NodeDimensions.TryGetValue(node, out var nodeDimension) ? nodeDimension.y : standardSize.y;

            var newRect = new Rect(position, new Vector2(width, height));
            node.SetPosition(newRect);
        }
    }
}