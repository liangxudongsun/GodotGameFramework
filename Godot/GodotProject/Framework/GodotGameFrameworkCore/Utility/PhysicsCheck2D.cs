using System;
using System.Collections.Generic;
using GameFramework;
using Godot;
using Godot.Collections;


/// <summary>
/// 2D 物理检测辅助类。封装 PhysicsDirectSpaceState2D.IntersectShape 的常用检测逻辑，
/// 支持对象池复用，自动排除自身节点。
/// </summary>
public partial class PhysicsCheck2D : IReference
{
    /// <summary>
    /// 被检测的目标节点（自动从 IntersectShape 结果中排除）。
    /// </summary>
    public Node2D TargetNode { get; private set; }
    /// <summary>
    /// 物理形状查询参数。通过 <see cref="QueryParameters.Shape"/> 访问形状实例。
    /// </summary>
    public PhysicsShapeQueryParameters2D QueryParameters { get; private set; }

    /// <summary>
    /// 最近一次检测命中的碰撞节点列表（已过滤、已去重）。
    /// </summary>
    public List<Node2D> CollidingNodes { get; private set; }
    private int m_MaxResults;

    public PhysicsCheck2D() { }

    /// <summary>
    /// 创建 PhysicsCheck2D 实例（从对象池获取）。
    /// </summary>
    /// <param name="targetNode">检测中心的目标节点（自动设 QueryParameters.Transform）。</param>
    /// <param name="shape">检测用的碰撞形状。</param>
    /// <param name="collisionMask">碰撞掩码，决定检测哪些层。0 表示不限制（默认 0）。</param>
    /// <param name="maxResults">最大返回结果数。默认 32（Godot IntersectShape 默认值）。</param>
    /// <param name="collideWithBodies">是否检测刚体碰撞（默认 true）。</param>
    /// <param name="collideWithAreas">是否检测 Area2D（默认 false）。</param>
    /// <param name="margin">碰撞检测的额外容差。配合 <see cref="CircleShape2D"/> 等形状做范围检测时很方便。</param>
    public static PhysicsCheck2D Create(
        Node2D targetNode,
        Shape2D shape,
        uint collisionMask = 0,
        int maxResults = 32,
        bool collideWithBodies = true,
        bool collideWithAreas = false,
        float margin = 0.0f)
    {
        var pc2 = ReferencePool.Acquire<PhysicsCheck2D>();
        pc2.TargetNode = targetNode;
        pc2.QueryParameters = new PhysicsShapeQueryParameters2D
        {
            Shape = shape,
            Transform = targetNode.GlobalTransform,
            CollisionMask = collisionMask,
            CollideWithBodies = collideWithBodies,
            CollideWithAreas = collideWithAreas,
            Margin = margin,
        };
        pc2.m_MaxResults = maxResults;
        pc2.CollidingNodes = new List<Node2D>();
        return pc2;
    }

    /// <summary>
    /// 执行碰撞检测，收集范围内有效的碰撞节点。
    /// 自动用 <see cref="TargetNode"/> 的当前位置更新查询变换，确保结果始终正确。
    /// </summary>
    /// <returns>是否存在有效碰撞节点。</returns>
    public bool IsColliding()
    {
        // 每次检测前用目标节点的当前变换更新查询位置，支持持久化字段用法
        QueryParameters.Transform = TargetNode.GlobalTransform;
        PhysicsDirectSpaceState2D spaceState = TargetNode.GetWorld2D().DirectSpaceState;
        Array<Dictionary> result = m_MaxResults > 0 ? spaceState.IntersectShape(QueryParameters, m_MaxResults) : spaceState.IntersectShape(QueryParameters);

        CollidingNodes.Clear();
        foreach (var r in result)
        {
            var collider = r["collider"].Obj;
            if (collider is Node2D node && node != TargetNode && node.IsInsideTree() && node.Visible)
            {
                CollidingNodes.Add(node);
            }
        }

        return CollidingNodes.Count > 0;
    }

    /// <summary>
    /// 执行碰撞检测并返回按距离升序排列的碰撞节点列表。
    /// </summary>
    public List<Node2D> GetCollidingNodesSorted()
    {
        IsColliding();
        CollidingNodes.Sort((a, b) => TargetNode.GlobalPosition.DistanceSquaredTo(a.GlobalPosition).CompareTo(TargetNode.GlobalPosition.DistanceSquaredTo(b.GlobalPosition)));
        return CollidingNodes;
    }

    /// <summary>
    /// 最近一次检测命中的节点数量。
    /// </summary>
    public int CollidingCount => CollidingNodes?.Count ?? 0;

    /// <summary>
    /// 重置状态以返回对象池。
    /// </summary>
    public void Clear()
    {
        TargetNode = null;
        QueryParameters = null;
        CollidingNodes?.Clear();
    }

    /// <summary>
    /// 必须从 <see cref="CanvasItem._Draw()"/> 内部调用，否则不生效。
    /// </summary>
    /// <param name="canvasNode">绘制目标节点（通常是 this，即节点自身）。</param>
    /// <param name="color">连线颜色，默认绿色。</param>
    /// <param name="lineWidth">线宽，默认 2。</param>
    public void DrawDebugLines(Color? color = null, float lineWidth = 2f)
    {
        Color drawColor = color ?? Colors.Green;
        foreach (var node in CollidingNodes)
        {
            TargetNode.DrawLine(Vector2.Zero, TargetNode.ToLocal(node.GlobalPosition), drawColor, lineWidth);
        }
    }
}

