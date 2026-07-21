using Godot;
using GodotGameFrameworkCore.SingletonSystem;
using System;
using System.Collections.Generic;
/// <summary>
/// 方便获取Layer的类
/// </summary> 
public partial class LayerMask : SingletonNode<LayerMask>
{
    /// <summary>2D physics layer count. Godot uses layers 1–32 (bits 0–31).</summary>
    public const int LayerCount = 32;

    private static readonly Dictionary<string, int> m__2DLayerNameToIndex = new Dictionary<string, int>();
    private static readonly Dictionary<int, string> m__2DLayerIndexToName = new Dictionary<int, string>();
    private static readonly Dictionary<string, int> m__3DLayerNameToIndex = new Dictionary<string, int>();
    private static readonly Dictionary<int, string> m__3DLayerIndexToName = new Dictionary<int, string>();
    private static bool m__Initialized = false;

    protected override void OnLoad()
    {
        InitializeLayerMaps();
    }

    private static void InitializeLayerMaps()
    {
        if (m__Initialized) return;

        for (int i = 1; i <= LayerCount; i++)
        {
            string layerName2D = ProjectSettings.GetSetting($"layer_names/2d_physics/layer_{i}").AsString();
            if (!string.IsNullOrEmpty(layerName2D))
            {
                m__2DLayerNameToIndex[layerName2D] = i;
                m__2DLayerIndexToName[i] = layerName2D;
            }

            string layerName3D = ProjectSettings.GetSetting($"layer_names/3d_physics/layer_{i}").AsString();
            if (!string.IsNullOrEmpty(layerName3D))
            {
                m__3DLayerNameToIndex[layerName3D] = i;
                m__3DLayerIndexToName[i] = layerName3D;
            }
        }
        m__Initialized = true;
    }

    // ─── 2D Layer ──────────────────────────────────────────
    /// <summary>
    /// 将layerName转换为layerIndex
    /// </summary>
    /// <param name="layerName"></param>
    /// <returns></returns>
    public static int NameToLayer2D(string layerName)
    {
        if (m__2DLayerNameToIndex.TryGetValue(layerName, out int layerIndex))
        {
            return layerIndex;
        }
        return 0;
    }
    /// <summary>
    /// 将layerIndex转换为layerName
    /// </summary>
    /// <param name="layerIndex"></param>
    /// <returns></returns> 
    public static string LayerToName2D(int layerIndex)
    {
        if (m__2DLayerIndexToName.TryGetValue(layerIndex, out string layerName))
        {
            return layerName;
        }
        return string.Empty;
    }
    /// <summary>
    /// 将layerIndex转换为layerMask
    /// </summary>
    /// <param name="layerIndex"></param>
    /// <returns></returns>
    public static uint LayerToMask2D(int layerIndex)
    {
        if (layerIndex < 1 || layerIndex > LayerCount)
        {
            return 0;
        }
        return (uint)(1 << (layerIndex - 1));
    }
    /// <summary>
    /// 将layerName转换为layerMask
    /// </summary>
    /// <param name="layerName"></param>
    /// <returns></returns>
    public static uint LayerToMask2D(string layerName)
    {
        int layerIndex = NameToLayer2D(layerName);
        if (layerIndex == 0)
        {
            return 0;
        }
        return LayerToMask2D(layerIndex);
    }

    /// <summary>Combine multiple layer names into a single 2D mask (e.g., "Player", "Enemy").</summary>
    public static uint LayerToMask2D(params string[] layerNames)
    {
        uint mask = 0;
        foreach (string name in layerNames)
        {
            mask |= LayerToMask2D(name);
        }
        return mask;
    }

    // ─── 3D Layer ──────────────────────────────────────────

    public static int NameToLayer3D(string layerName)
    {
        if (m__3DLayerNameToIndex.TryGetValue(layerName, out int layerIndex))
        {
            return layerIndex;
        }
        return 0;
    }

    public static string LayerToName3D(int layerIndex)
    {
        if (m__3DLayerIndexToName.TryGetValue(layerIndex, out string layerName))
        {
            return layerName;
        }
        return string.Empty;
    }

    public static uint LayerToMask3D(int layerIndex)
    {
        if (layerIndex < 1 || layerIndex > LayerCount)
        {
            return 0;
        }
        return (uint)(1 << (layerIndex - 1));
    }

    public static uint LayerToMask3D(string layerName)
    {
        int layerIndex = NameToLayer3D(layerName);
        if (layerIndex == 0)
        {
            return 0;
        }
        return LayerToMask3D(layerIndex);
    }

    /// <summary>Combine multiple layer names into a single 3D mask (e.g., "Player", "Enemy").</summary>
    public static uint LayerToMask3D(params string[] layerNames)
    {
        uint mask = 0;
        foreach (string name in layerNames)
        {
            mask |= LayerToMask3D(name);
        }
        return mask;
    }
}
