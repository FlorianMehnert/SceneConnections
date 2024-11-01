using System;

namespace SceneConnections
{
    public static class Constants
    {
        public enum ComponentGraphDrawType
        {
            NodesAreComponents = 1,
            NodesAreGameObjects = 2,
            NodesAreScripts = 3
        }

        public static ComponentGraphDrawType ToCgdt(string s)
        {
            switch (s)
            {
                case "nodes are components":
                    return ComponentGraphDrawType.NodesAreComponents;
                case "nodes are game objects":
                    return ComponentGraphDrawType.NodesAreGameObjects;
                case "nodes are scripts":
                    return ComponentGraphDrawType.NodesAreScripts;
                default:
                    throw new ArgumentOutOfRangeException(nameof(s), s, null);
            }
        }
    }
}