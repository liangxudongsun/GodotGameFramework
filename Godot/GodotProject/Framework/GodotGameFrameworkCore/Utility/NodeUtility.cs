using Godot;
using GodotGameFramework;
using System;

public static partial class NodeUtility
{
    public static object InstantiatePack(object asset)
    {
        if (asset is PackedScene)
        {
            return (asset as PackedScene).Instantiate();
        }
        Log.Error("NodeUtility.InstantiatePack: asset is not PackedScene");
        return null;
    }

    public static void ReleaseNode(object node)
    {
        if (node is Node)
        {
            (node as Node).QueueFree();
        }
    }
}
