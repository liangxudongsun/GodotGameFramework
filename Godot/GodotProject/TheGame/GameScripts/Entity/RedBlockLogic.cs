//------------------------------------------------------------
// 红色扣分方块逻辑。
// 点击后扣分（分值从 BlockTypeData 读取），超时后自动消失。
//------------------------------------------------------------

using Godot;
using GodotGameFramework;

/// <summary>
/// 红色扣分方块逻辑。
///
/// 特性：
/// - 点击后扣除分数（从 BlockTypeData.Score 读取，默认 -5）
/// - 到达生命期后自动消失（不扣分）
/// - 颜色从 BlockTypeData.ColorR/G/B 读取
///
/// 对应 BlockTypeData 中 Id=2 (RedBlock) 的数据。
/// OnUpdate 中实现倒计时，超时后自动 HideEntity。
/// </summary>
public class RedBlockLogic : BlockLogic
{
    /// <summary>
    /// 红色方块默认颜色（仅作为首次 OnInit 时的回退值）。
    /// </summary>
    private static readonly Color DefaultPenaltyColor = new Color(0.9f, 0.2f, 0.2f);

    /// <summary>
    /// 方块生命期（秒）。
    /// </summary>
    private float m_Lifetime;

    /// <summary>
    /// 已经过的时间（秒）。
    /// </summary>
    private float m_Elapsed;

    /// <summary>
    /// 点击扣分（从 BlockSpawnData 读取，默认 -5）。
    /// </summary>
    private int m_Score = -5;

    /// <summary>
    /// 实体初始化回调。
    /// 创建或复用视觉子节点。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);
        EnsureVisuals(DefaultPenaltyColor);
    }

    /// <summary>
    /// 实体显示回调。
    /// 重置计时器，从 BlockSpawnData 读取数据驱动的分值和生命期。
    /// </summary>
    protected internal override void OnShow(object userData)
    {
        base.OnShow(userData);

        m_Elapsed = 0f;
        m_Lifetime = 3.0f; // 默认值

        // 从 userData 读取数据驱动的分值和生命期
        if (userData is BlockSpawnData spawnData)
        {
            m_Score = spawnData.Score;
            if (spawnData.Lifetime > 0f)
            {
                m_Lifetime = spawnData.Lifetime;
            }
        }
    }

    /// <summary>
    /// 每帧更新。
    /// 倒计时，超时后自动隐藏。同时检测点击。
    /// </summary>
    protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        // 先检测点击（基类的 OnUpdate 处理鼠标状态更新和点击判断）
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        if (m_Clicked) return; // 已点击则不再计时

        // 红色方块倒计时
        m_Elapsed += elapseSeconds;
        if (m_Elapsed >= m_Lifetime)
        {
            // 超时自动消失（不扣分）
            GD.Print($"  [RedBlock] 超时消失 (Entity {Owner?.Id})");

            GF.Entity?.HideEntity(Owner.Id);
        }
    }

    /// <summary>
    /// 红色方块被点击时：
    /// 1. 触发 ScoreChangedEventArgs 事件（分值从数据表读取）
    /// 2. 触发 BlockClickedEventArgs 事件
    /// 3. 隐藏实体（归还对象池）
    /// </summary>
    protected override void OnBlockClicked()
    {
        GD.Print($"  [RedBlock] 点击! {m_Score} 分 (Entity {Owner?.Id})");

        // 触发分数变化事件
        if (GF.Event != null)
        {
            GF.Event.Fire(Owner, ScoreChangedEventArgs.Create(m_Score));
            GF.Event.Fire(Owner, BlockClickedEventArgs.Create(Owner.Id, 2, m_Score));
        }

        // 隐藏实体（归还到对象池等待复用）
        GF.Entity?.HideEntity(Owner.Id);
    }
}
