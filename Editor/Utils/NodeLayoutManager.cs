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

        private static readonly Dictionary<Node, Vector2> NodeDimensions = new();
        private static Dictionary<Node, bool> NodeReceivedDimension => new();
        private static bool _layoutUpdated;

        // Dictionary to track connections between nodes
        private static readonly Dictionary<Node, List<Node>> NodeConnections = new();

        public static void LayoutNodes(List<Node> nodes, List<Edge> allEdges=null, bool silent = false)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            NodeDimensions.Clear();
            NodeConnections.Clear();

            foreach (var node in nodes)
            {
                NodeReceivedDimension[node] = false;
                node.RegisterCallback<GeometryChangedEvent>(_ => OnNodeGeometryChanged(node, nodes, silent));
                _layoutUpdated = false;

                // Populate NodeConnections based on edges
                if (allEdges != null)
                {
                    NodeConnections[node] = GetConnectedNodes(node, allEdges);
                }
            }
        }

        private static void OnNodeGeometryChanged(Node node, List<Node> nodes, bool silent)
        {
            var rect = node.GetPosition();
            var nodeSize = new Vector2(rect.width, rect.height);

            if (nodeSize is { x: > 0, y: > 0 })
            {
                NodeDimensions[node] = nodeSize;
                NodeReceivedDimension[node] = true;
            }

            if (NodeDimensions.Count != nodes.Count) return;

            foreach (var n in nodes)
            {
                n.UnregisterCallback<GeometryChangedEvent>(_ => OnNodeGeometryChanged(n, nodes, silent));
            }

            if (!NodeReceivedDimension.All(kvp => kvp.Value) || _layoutUpdated) return;
            _layoutUpdated = true;
            PerformLayout(nodes, silent);
        }

        private static void PerformLayout(List<Node> nodes, bool silent)
        {
            var totalNodes = nodes.Count;
            var gridColumns = CalculateOptimalColumnCount(totalNodes);
            var gridRows = Mathf.CeilToInt((float)totalNodes / gridColumns);
            var maxNodeDimensions = GetMaxNodeDimensions();

            if (!silent)
            {
                Debug.Log($"Grid: {gridRows}x{gridColumns}, Total Nodes: {totalNodes}");
                Debug.Log($"Max Node Dimensions: {maxNodeDimensions}");
            }

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
            const float targetAspectRatio = 1.618f;
            var columns = Mathf.RoundToInt(Mathf.Sqrt(nodeCount * targetAspectRatio));
            return Mathf.Max(1, columns);
        }

        private static Vector2 GetMaxNodeDimensions()
        {
            var maxWidth = DefaultNodeWidth;
            var maxHeight = DefaultNodeHeight;

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

        // Method to get nodes connected to a given node based on edges
        private static List<Node> GetConnectedNodes(Node node, List<Edge> allEdges)
        {
            var connectedNodes = new List<Node>();

            // Find all edges that involve the current node
            foreach (var edge in allEdges)
            {
                if (edge.input.node == node && edge.output.node != null)
                    connectedNodes.Add(edge.output.node);
                else if (edge.output.node == node && edge.input.node != null)
                    connectedNodes.Add(edge.input.node);
            }

            return connectedNodes;
        }

        // Method to disable nodes without connections and recalculate layout
        public static void DisableDisconnectedNodes(List<Node> nodes, List<Edge> allEdges, bool silent = false)
        {
            var connectedNodes = nodes
                .Where(node => GetConnectedNodes(node, allEdges).Count > 0)
                .ToList();

            // Disable all nodes without connections
            foreach (var node in nodes)
            {
                node.visible = connectedNodes.Contains(node);
            }

            // Recalculate layout with only connected nodes
            PerformLayout(connectedNodes, silent);
        }
    }
}
