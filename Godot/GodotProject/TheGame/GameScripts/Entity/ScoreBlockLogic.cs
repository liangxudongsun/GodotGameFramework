//------------------------------------------------------------
// 绿色加分方块逻辑。
// 点击后加分（分值从 BlockTypeData 读取），不自动消失，只能通过点击消除。
//------------------------------------------------------------

using Godot;
using GodotGameFramework;

/// <summary>
/// 绿色加分方块逻辑。
///
/// 特性：
/// - 点击后获得分数（从 BlockTypeData.Score 读取，默认 +10）
/// - 不会自动消失，只能通过点击消除
/// - 颜色从 BlockTypeData.ColorR/G/B 读取
///
/// 对应 BlockTypeData 中 Id=1 (ScoreBlock) 的数据。
/// </summary>
public class ScoreBlockLogic : BlockLogic
{
    /// <summary>
    /// 绿色方块默认颜色（仅作为首次 OnInit 时 BlockSpawnData 尚未传入的回退值）。
    /// </summary>
    private static readonly Color DefaultScoreColor = new Color(0.2f, 0.8f, 0.2f);

    /// <summary>
    /// 点击得分（从 BlockSpawnData 读取，默认 10）。
    /// </summary>
    private int m_Score = 10;

    /// <summary>
    /// 实体初始化回调。
    /// 创建或复用视觉子节点。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);
        EnsureVisuals(DefaultScoreColor);
    }

    /// <summary>
    /// 实体显示回调。
    /// 从 BlockSpawnData 读取数据驱动的分数。
    /// </summary>
    protected internal override void OnShow(object userData)
    {
        base.OnShow(userData);

        // 从 userData 读取数据驱动的分值
        if (userData is BlockSpawnData spawnData)
        {
            m_Score = spawnData.Score;
        }
    }

    /// <summary>
    /// 绿色方块被点击时：
    /// 1. 触发 ScoreChangedEventArgs 事件（分值从数据表读取）
    /// 2. 触发 BlockClickedEventArgs 事件
    /// 3. 隐藏实体（归还对象池）
    /// </summary>
    protected override void OnBlockClicked()
    {
        GD.Print($"  [ScoreBlock] 点击! +{m_Score} 分 (Entity {Owner?.Id})");

        // 触发分数变化事件
        if (GF.Event != null)
        {
            GF.Event.Fire(Owner, ScoreChangedEventArgs.Create(m_Score));
            GF.Event.Fire(Owner, BlockClickedEventArgs.Create(Owner.Id, 1, m_Score));
        }

        // 隐藏实体（归还到对象池等待复用）
        GF.Entity?.HideEntity(Owner.Id);
    }
}
