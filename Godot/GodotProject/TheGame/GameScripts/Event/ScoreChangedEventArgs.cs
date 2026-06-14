//------------------------------------------------------------
// 分数变化事件参数。
// 当玩家点击方块导致分数变化时触发此事件。
//------------------------------------------------------------

using GameFramework;
using GameFramework.Event;

/// <summary>
/// 分数变化事件参数。
///
/// 当方块被点击导致分数变化时触发。
/// ScoreDelta 为本次变化的分数值（正数加分，负数扣分）。
///
/// 游戏流程（TestGameProcedure）订阅此事件来更新总分。
/// </summary>
public class ScoreChangedEventArgs : GameEventArgs
{
    /// <summary>
    /// 事件编号。
    /// </summary>
    public const int EventId = 10010;

    /// <summary>
    /// 获取类型编号。
    /// </summary>
    public override int Id => EventId;

    /// <summary>
    /// 本次分数变化量（正数 = 加分，负数 = 扣分）。
    /// </summary>
    public int ScoreDelta { get; private set; }

    /// <summary>
    /// 清理引用。
    /// </summary>
    public override void Clear()
    {
        ScoreDelta = 0;
    }

    /// <summary>
    /// 创建事件参数实例。
    /// </summary>
    /// <param name="scoreDelta">分数变化量。</param>
    /// <returns>事件参数实例。</returns>
    public static ScoreChangedEventArgs Create(int scoreDelta)
    {
        ScoreChangedEventArgs e = ReferencePool.Acquire<ScoreChangedEventArgs>();
        e.ScoreDelta = scoreDelta;
        return e;
    }
}
