using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class NodeUtils
{
    /// get
    static Vector2[] calculatedSizes(Group[] groups)
    {
        {
            Vector2[] idealSizes = new Vector2[groups.Length];

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].containedElements == null || groups[i].containedElements.Count() == 0)
                {
                    idealSizes[i] = Vector2.zero;
                    continue;
                }

                // Calculate bounds for all nodes in the group
                float minX = float.MaxValue;
                float minY = float.MaxValue;
                float maxX = float.MinValue;
                float maxY = float.MinValue;

                foreach (Node node in groups[i].containedElements.Cast<Node>())
                {
                    // Assuming each node has a position and size
                    Rect nodeRect = node.contentRect;

                    minX = Mathf.Min(minX, nodeRect.x);
                    minY = Mathf.Min(minY, nodeRect.y);
                    maxX = Mathf.Max(maxX, nodeRect.x + nodeRect.width);
                    maxY = Mathf.Max(maxY, nodeRect.y + nodeRect.height);
                }

                // Calculate ideal size with some padding
                const float padding = 10f; // Adjust padding as needed
                idealSizes[i] = new Vector2(
                    maxX - minX + (padding * 2),
                    maxY - minY + (padding * 2)
                );
            }

            return idealSizes;
        }
    }

    public static void OptimizeGroupLayout(Group group, float padding = 10f)
    {
        if (group.containedElements == null || group.containedElements.Count() == 0)
            return;

        List<Node> nodes = group.containedElements.OfType<Node>().ToList();
        
        // Sort nodes by area in descending order for better packing
        nodes.Sort((a, b) => {
            float areaA = a.contentRect.width * a.contentRect.height;
            float areaB = b.contentRect.width * b.contentRect.height;
            return areaB.CompareTo(areaA);
        });

        // Try different arrangements to find the most compact one
        float bestAspectRatio = float.MaxValue;
        Vector2 bestSize = Vector2.zero;
        List<Vector2> bestPositions = new List<Vector2>();
        
        // Try different numbers of rows
        int maxRows = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count));
        
        for (int numRows = 1; numRows <= maxRows; numRows++)
        {
            int numCols = Mathf.CeilToInt((float)nodes.Count / numRows);
            List<Vector2> currentPositions = new List<Vector2>();
            
            float currentWidth = 0;
            float currentHeight = 0;
            float[] rowHeights = new float[numRows];
            float[] colWidths = new float[numCols];

            // Calculate positions in a grid
            for (int i = 0; i < nodes.Count; i++)
            {
                int row = i / numCols;
                int col = i % numCols;
                
                Node node = nodes[i];
                Rect rect = node.contentRect;
                
                // Update row heights and column widths
                rowHeights[row] = Mathf.Max(rowHeights[row], rect.height);
                colWidths[col] = Mathf.Max(colWidths[col], rect.width);
            }

            // Calculate total size and positions
            float y = padding;
            for (int row = 0; row < numRows; row++)
            {
                float x = padding;
                for (int col = 0; col < numCols; col++)
                {
                    int index = row * numCols + col;
                    if (index < nodes.Count)
                    {
                        currentPositions.Add(new Vector2(x, y));
                        x += colWidths[col] + padding;
                    }
                }
                currentWidth = Mathf.Max(currentWidth, x);
                y += rowHeights[row] + padding;
                currentHeight = y;
            }

            // Calculate aspect ratio
            float aspectRatio = Mathf.Abs((currentWidth / currentHeight) - 1f);
            
            // Update best arrangement if this one is better
            if (aspectRatio < bestAspectRatio)
            {
                bestAspectRatio = aspectRatio;
                bestSize = new Vector2(currentWidth, currentHeight);
                bestPositions = currentPositions;
            }
        }

        // Apply the best arrangement
        for (int i = 0; i < nodes.Count; i++)
        {
            Node node = nodes[i];
            Vector2 newPosition = bestPositions[i];
            
            // Update node position
            Rect newRect = node.contentRect;
            newRect.x = newPosition.x;
            newRect.y = newPosition.y;
            node.SetPosition(newRect);
        }

        // Update group size
        Rect groupRect = group.contentRect;
        groupRect.width = bestSize.x;
        groupRect.height = bestSize.y;
        group.SetPosition(groupRect);
    }

    // Helper method to optimize multiple groups
    public static void OptimizeGroupLayouts(Group[] groups, float padding = 10f)
    {
        foreach (Group group in groups)
        {
            OptimizeGroupLayout(group, padding);
        }
    }
}