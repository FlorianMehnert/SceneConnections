using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SceneConnections.Editor.Utils
{
    public class LayoutState
    {
        public struct NodeLayout
        {
            public GameObjectNode Node;
            public Rect FinalRect;
        }

        public struct GroupLayout
        {
            public Group Group;
            public Rect FinalRect;
            public List<NodeLayout> NodeLayouts;
        }

        public List<GroupLayout> GroupLayouts { get; } = new();

        public void ApplyLayout()
        {
            foreach (var groupLayout in GroupLayouts)
            {
                // Apply group rectangle
                groupLayout.Group.SetPosition(groupLayout.FinalRect);

                // Apply node rectangles
                foreach (var nodeLayout in groupLayout.NodeLayouts)
                {
                    nodeLayout.Node.SetPosition(nodeLayout.FinalRect);
                }
            }
        }

        public static void SetTotalSize()
        {
        }
    }

    public abstract class NodeUtils
    {
        public static LayoutState OptimizeGroupLayouts(Group[] groups, float padding = 500f)
        {
            LayoutState layoutState = new LayoutState();

            if (groups == null || groups.Length == 0)
                return layoutState;

            // First optimize internal layout of each group and store the results
            var groupLayouts = new List<LayoutState.GroupLayout>();

            foreach (var group in groups)
            {
                var groupLayout = new LayoutState.GroupLayout
                {
                    Group = group,
                    NodeLayouts = new List<LayoutState.NodeLayout>(),
                    FinalRect = new Rect(0, 0, 0, 0) // Initialize with zero rect
                };

                if (group.containedElements != null && group.containedElements.Any())
                {
                    List<GameObjectNode> nodes = group.containedElements.OfType<GameObjectNode>().ToList();

                    // Sort nodes by area
                    nodes.Sort((a, b) =>
                    {
                        float areaA = a.contentRect.width * a.contentRect.height;
                        float areaB = b.contentRect.width * b.contentRect.height;
                        return areaB.CompareTo(areaA);
                    });

                    // Calculate optimal node positions within group
                    float[] rowHeights = new float[Mathf.CeilToInt(Mathf.Sqrt(nodes.Count))];
                    float[] colWidths = new float[Mathf.CeilToInt((float)nodes.Count / rowHeights.Length)];
                    Vector2[] nodePositions = CalculateOptimalNodePositions(nodes, padding, rowHeights, colWidths);

                    // Store node layouts with local positions (relative to group)
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var nodeLayout = new LayoutState.NodeLayout
                        {
                            Node = nodes[i],
                            FinalRect = new Rect(
                                nodePositions[i].x,
                                nodePositions[i].y,
                                nodes[i].contentRect.width,
                                nodes[i].contentRect.height
                            )
                        };
                        groupLayout.NodeLayouts.Add(nodeLayout);
                    }

                    // Calculate group size based on node positions
                    var groupWidth = colWidths.Sum() + padding * (colWidths.Length + 1);
                    var groupHeight = rowHeights.Sum() + padding * (rowHeights.Length + 1);
                    groupLayout.FinalRect.width = groupWidth;
                    groupLayout.FinalRect.height = groupHeight;
                }

                groupLayouts.Add(groupLayout);
            }

            // Sort groups by area for better packing
            groupLayouts.Sort((a, b) =>
            {
                float areaA = a.FinalRect.width * a.FinalRect.height;
                float areaB = b.FinalRect.width * b.FinalRect.height;
                return areaB.CompareTo(areaA);
            });

            // Try different grid arrangements for groups
            float bestTotalArea = float.MaxValue;
            Vector2[] bestPositions = new Vector2[groupLayouts.Count];

            int maxRows = Mathf.CeilToInt(Mathf.Sqrt(groupLayouts.Count));

            for (int numRows = 1; numRows <= maxRows; numRows++)
            {
                int numCols = Mathf.CeilToInt((float)groupLayouts.Count / numRows);
                Vector2[] currentPositions = new Vector2[groupLayouts.Count];

                float[] rowHeights = new float[numRows];
                float[] colWidths = new float[numCols];

                // Calculate maximum dimensions for each row and column
                for (int i = 0; i < groupLayouts.Count; i++)
                {
                    int row = i / numCols;
                    int col = i % numCols;

                    Rect rect = groupLayouts[i].FinalRect;
                    rowHeights[row] = Mathf.Max(rowHeights[row], rect.height);
                    colWidths[col] = Mathf.Max(colWidths[col], rect.width);
                }

                // Calculate positions and total size
                float currentY = padding;
                float totalWidth = padding;
                float totalHeight = padding;

                for (int row = 0; row < numRows; row++)
                {
                    float currentX = padding;

                    for (int col = 0; col < numCols; col++)
                    {
                        int index = row * numCols + col;
                        if (index < groupLayouts.Count)
                        {
                            currentPositions[index] = new Vector2(currentX, currentY);
                            currentX += colWidths[col] + padding;
                        }
                    }

                    totalWidth = Mathf.Max(totalWidth, currentX);
                    currentY += rowHeights[row] + padding;
                    totalHeight = currentY;
                }

                float totalArea = totalWidth * totalHeight;

                if (totalArea < bestTotalArea)
                {
                    bestTotalArea = totalArea;
                    bestPositions = currentPositions.ToArray();
                    new Vector2(totalWidth, totalHeight);
                }
            }

            // Create final layout state with updated positions
            var finalGroupLayouts = new List<LayoutState.GroupLayout>();

            for (int i = 0; i < groupLayouts.Count; i++)
            {
                var originalGroupLayout = groupLayouts[i];
                Vector2 groupPosition = bestPositions[i];

                // Create new group layout with updated position
                var updatedGroupLayout = new LayoutState.GroupLayout
                {
                    Group = originalGroupLayout.Group,
                    FinalRect = new Rect(
                        groupPosition.x,
                        groupPosition.y,
                        originalGroupLayout.FinalRect.width,
                        originalGroupLayout.FinalRect.height
                    ),
                    NodeLayouts = new List<LayoutState.NodeLayout>()
                };

                // Update node positions relative to new group position
                foreach (var originalNodeLayout in originalGroupLayout.NodeLayouts)
                {
                    var updatedNodeLayout = new LayoutState.NodeLayout
                    {
                        Node = originalNodeLayout.Node,
                        FinalRect = new Rect(
                            groupPosition.x + originalNodeLayout.FinalRect.x,
                            groupPosition.y + originalNodeLayout.FinalRect.y,
                            originalNodeLayout.FinalRect.width,
                            originalNodeLayout.FinalRect.height
                        )
                    };
                    updatedGroupLayout.NodeLayouts.Add(updatedNodeLayout);
                }

                finalGroupLayouts.Add(updatedGroupLayout);
            }

            layoutState.GroupLayouts.AddRange(finalGroupLayouts);
            LayoutState.SetTotalSize();

            return layoutState;
        }

        private static Vector2[] CalculateOptimalNodePositions(List<GameObjectNode> nodes, float padding, float[] rowHeights, float[] colWidths)
        {
            Vector2[] positions = new Vector2[nodes.Count];

            // Calculate positions in a grid
            for (int i = 0; i < nodes.Count; i++)
            {
                int row = i / colWidths.Length;
                int col = i % colWidths.Length;

                GameObjectNode node = nodes[i];
                Rect rect = node.contentRect;

                // Update row heights and column widths
                rowHeights[row] = Mathf.Max(rowHeights[row], rect.height);
                colWidths[col] = Mathf.Max(colWidths[col], rect.width);
            }

            // Calculate final positions
            float y = padding;
            for (int row = 0; row < rowHeights.Length; row++)
            {
                float x = padding;
                for (int col = 0; col < colWidths.Length; col++)
                {
                    int index = row * colWidths.Length + col;
                    if (index < nodes.Count)
                    {
                        positions[index] = new Vector2(x, y);
                        x += colWidths[col] + padding;
                    }
                }

                y += rowHeights[row] + padding;
            }

            return positions;
        }
    }
}