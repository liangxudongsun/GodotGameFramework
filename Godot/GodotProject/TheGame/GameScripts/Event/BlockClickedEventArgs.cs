//------------------------------------------------------------
// 方块点击事件参数。
// 当玩家点击任何方块时触发此事件。
//------------------------------------------------------------

using GameFramework;
using GameFramework.Event;

/// <summary>
/// 方块点击事件参数。
///
/// 当方块被点击时触发，携带方块实体 ID、方块类型 ID 和分数变化。
/// 用于游戏流程追踪方块点击行为。
/// </summary>
public class BlockClickedEventArgs : GameEventArgs
{
    /// <summary>
    /// 事件编号。
    /// </summary>
    public const int EventId = 10011;

    /// <summary>
    /// 获取类型编号。
    /// </summary>
    public override int Id => EventId;

    /// <summary>
    /// 被点击的方块实体 ID。
    /// </summary>
    public int EntityId { get; private set; }

    /// <summary>
    /// 方块类型 ID（对应 BlockTypeData 的 Id）。
    /// </summary>
    public int BlockTypeId { get; private set; }

    /// <summary>
    /// 本次点击的分数变化（正数 = 加分，负数 = 扣分）。
    /// </summary>
    public int ScoreDelta { get; private set; }

    /// <summary>
    /// 清理引用。
    /// </summary>
    public override void Clear()
    {
        EntityId = 0;
        BlockTypeId = 0;
        ScoreDelta = 0;
    }

    /// <summary>
    /// 创建事件参数实例。
    /// </summary>
    /// <param name="entityId">方块实体 ID。</param>
    /// <param name="blockTypeId">方块类型 ID。</param>
    /// <param name="scoreDelta">分数变化。</param>
    /// <returns>事件参数实例。</returns>
    public static BlockClickedEventArgs Create(int entityId, int blockTypeId, int scoreDelta)
    {
        BlockClickedEventArgs e = ReferencePool.Acquire<BlockClickedEventArgs>();
        e.EntityId = entityId;
        e.BlockTypeId = blockTypeId;
        e.ScoreDelta = scoreDelta;
        return e;
    }
}
