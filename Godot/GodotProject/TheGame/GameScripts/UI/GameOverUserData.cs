//--------------------------------------------------------------
// 游戏结束界面用户数据。
// 通过 OpenUIForm 的 userData 参数传递给 GameOverForm。
//
// 框架特性展示：
// - OpenUIForm<TLogic>(..., object userData)：泛型传参
// - OnOpen(object userData)：在 UIFormLogic 中接收 userData
//--------------------------------------------------------------

/// <summary>
/// 游戏结束界面用户数据。
///
/// 通过 OpenUIForm 的 userData 参数传递给 GameOverForm，
/// 替代直接从 DataNode 读取数据的方式。
///
/// 演示框架的 userData 传参机制：
/// - 调用方：OpenUIForm&lt;GameOverForm&gt;(..., userData)
/// - 接收方：OnOpen(object userData) 中 userData as GameOverUserData
/// </summary>
public class GameOverUserData
{
    /// <summary>最终分数。</summary>
    public int Score { get; set; }

    /// <summary>最高分。</summary>
    public int HighScore { get; set; }

    /// <summary>是否新纪录。</summary>
    public bool NewRecord { get; set; }
}
