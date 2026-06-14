//------------------------------------------------------------
// 方块生成数据。
// 作为 ShowEntity 的 userData 传递给 EntityLogic。
//------------------------------------------------------------

using Godot;

/// <summary>
/// 方块生成数据。
///
/// 在 TestGameProcedure.TrySpawnBlock() 中创建，
/// 通过 ShowEntity 的 userData 参数传递到方块的 EntityLogic.OnShow()。
///
/// 包含方块的位置、类型 ID、分值、颜色和生命期信息。
/// 所有属性均从 BlockTypeData DataTable 读取，实现数据驱动。
/// </summary>
public class BlockSpawnData
{
    /// <summary>
    /// 方块在游戏区域中的位置。
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// 方块类型 ID（对应 BlockTypeData 的 Id）。
    /// </summary>
    public int BlockTypeId { get; set; }

    /// <summary>
    /// 点击后获得的分数（正数为加分，负数为扣分）。
    /// 从 BlockTypeData.Score 读取。
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// 方块颜色。
    /// 从 BlockTypeData.ColorR/G/B 构建。
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// 方块生命期（秒）。
    /// 仅对红色方块有意义，绿色方块此值为 0。
    /// </summary>
    public float Lifetime { get; set; }

    /// <summary>
    /// 创建方块生成数据。
    /// </summary>
    /// <param name="position">生成位置。</param>
    /// <param name="blockTypeId">方块类型 ID。</param>
    /// <param name="score">点击得分。</param>
    /// <param name="color">方块颜色。</param>
    /// <param name="lifetime">生命期（秒）。</param>
    public BlockSpawnData(Vector2 position, int blockTypeId, int score, Color color, float lifetime = 0f)
    {
        Position = position;
        BlockTypeId = blockTypeId;
        Score = score;
        Color = color;
        Lifetime = lifetime;
    }
}
