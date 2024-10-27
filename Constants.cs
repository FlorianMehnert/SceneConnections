using System;

public static class Constants
{
    public enum ComponentGraphDrawType
    {
        NONE = 0,
        NODES_ARE_COMPONENTS = 1,
        NODES_ARE_GAME_OBJECTS = 2
    }

    public static ComponentGraphDrawType ToCGDT(string s)
    {
        if (s == "nodes are components")
        {
            return ComponentGraphDrawType.NODES_ARE_COMPONENTS;
        }
        else if (s == "nodes are game objects")
        {
            return ComponentGraphDrawType.NODES_ARE_GAME_OBJECTS;
        }
        return ComponentGraphDrawType.NONE;
    }
}