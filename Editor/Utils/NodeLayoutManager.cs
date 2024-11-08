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

        // Physics parameters
        private const float AttractionStrength = 0.0001f;
        private const float RepulsionStrength = 1000.0f;
        private const float DampingFactor = 0.95f; // Reduces force over time to stabilize layout
        private const int SimulationSteps = 100000;
        
        private static readonly Dictionary<Node, List<Node>> NodeConnections = new();

        public static void LayoutNodes(List<Node> nodes, bool silent = false)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            NodeDimensions.Clear();
            foreach (var node in nodes)
            {
                NodeReceivedDimension[node] = false;
                node.RegisterCallback<GeometryChangedEvent>(_ => OnNodeGeometryChanged(node, nodes, silent));
                _layoutUpdated = false;
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
            // Default grid layout
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

        // New physics-based layout method
        public static void PhysicsBasedLayout(List<Node> nodes, List<Edge> allEdges, bool silent = false)
        {
            // Initialize positions and velocities for nodes
            var positions = nodes.ToDictionary(node => node, _ => Random.insideUnitCircle * 100);
            var velocities = nodes.ToDictionary(node => node, _ => Vector2.zero);

            var step = 0;
            for (; step < SimulationSteps; step++)
            {
                // Apply forces for each pair of nodes (repulsion)
                for (var i = 0; i < nodes.Count; i++)
                {
                    for (var j = i + 1; j < nodes.Count; j++)
                    {
                        var nodeA = nodes[i];
                        var nodeB = nodes[j];
                        var delta = positions[nodeB] - positions[nodeA];
                        var distance = delta.magnitude;
                        if (!(distance > 0)) continue;
                        var repulsionForce = RepulsionStrength / (distance * distance);
                        var repulsion = delta.normalized * repulsionForce;
                        velocities[nodeA] -= repulsion;
                        velocities[nodeB] += repulsion;
                    }
                }

                // Apply attraction for connected nodes (edges)
                foreach (var edge in allEdges)
                {
                    if (edge.input.node == null || edge.output.node == null)
                        continue;

                    var nodeA = (Node)edge.input.node;
                    var nodeB = (Node)edge.output.node;
                    var delta = positions[nodeB] - positions[nodeA];
                    var distance = delta.magnitude;

                    if (!(distance > 0)) continue;
                    var attractionForce = AttractionStrength * distance;
                    var attraction = delta.normalized * attractionForce;
                    velocities[nodeA] += attraction;
                    velocities[nodeB] -= attraction;
                }

                // Update positions and apply damping
                foreach (var node in nodes)
                {
                    velocities[node] *= DampingFactor;
                    positions[node] += velocities[node];
                }
            }

            // Set the final positions
            foreach (var node in nodes)
            {
                var finalPosition = positions[node];
                SetNodePosition(node, finalPosition, GetMaxNodeDimensions());
            }

            if (!silent)
            {
                Debug.Log("Physics-based layout applied.");
            }
        }
    }
}
