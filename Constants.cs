using System;

namespace SceneConnections
{
    public static class Constants
    {
        public enum ComponentGraphDrawType
        {
            NodesAreComponents = 1,
            NodesAreGameObjects = 2
        }

        public static ComponentGraphDrawType ToCgdt(string s)
        {
            switch (s)
            {
                case "nodes are components":
                    return ComponentGraphDrawType.NodesAreComponents;
                case "nodes are game objects":
                    return ComponentGraphDrawType.NodesAreGameObjects;
                default:
                    throw new ArgumentOutOfRangeException(nameof(s), s, null);
            }
        }
    }
}