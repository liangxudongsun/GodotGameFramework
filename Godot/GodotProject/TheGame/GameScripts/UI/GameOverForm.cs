//------------------------------------------------------------
// 游戏结束界面逻辑。
// 显示最终分数、最高分、新纪录提示。
// Phase 7: 所有文本通过 LocalizationComponent 本地化。
//------------------------------------------------------------

using GameFramework;
using GameFramework.DataNode;
using GameFramework.Localization;
using Godot;
using GodotGameFramework;

/// <summary>
/// 游戏结束界面逻辑。
///
/// 显示游戏结果：
/// - 最终分数
/// - 最高记录
/// - 新纪录标识
///
/// Phase 5+: 优先从 userData 读取数据（演示 OpenUIForm userData 传参），
/// 回退到 DataNode 读取（兼容旧方式）。
///
/// Phase 7: 标题、分数、最高分、新纪录、提示均通过 GetString 本地化。
/// </summary>
public class GameOverForm : UIFormLogic
{
    /// <summary>标题标签。</summary>
    private Label m_TitleLabel;

    /// <summary>分数标签。</summary>
    private Label m_ScoreLabel;

    /// <summary>最高分标签。</summary>
    private Label m_HighScoreLabel;

    /// <summary>新纪录标签。</summary>
    private Label m_NewRecordLabel;

    /// <summary>提示标签。</summary>
    private Label m_PromptLabel;

    /// <summary>提示闪烁计时器。</summary>
    private float m_BlinkTimer;

    /// <summary>本地化组件引用。</summary>
    private LocalizationComponent m_LocalizationComponent;

    /// <summary>
    /// 界面初始化。
    /// 获取子节点引用。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);

        Control root = CachedControl;
        if (root == null)
        {
            Log.Warning("GameOverForm: CachedControl is null.");
            return;
        }

        m_TitleLabel = root.GetNode<Label>("Panel/Margin/VBox/Title");
        m_ScoreLabel = root.GetNode<Label>("Panel/Margin/VBox/ScoreLabel");
        m_HighScoreLabel = root.GetNode<Label>("Panel/Margin/VBox/HighScoreLabel");
        m_NewRecordLabel = root.GetNode<Label>("Panel/Margin/VBox/NewRecordLabel");
        m_PromptLabel = root.GetNode<Label>("Panel/Margin/VBox/Prompt");

        m_LocalizationComponent = GF.Localization;

        Log.Info("GameOverForm OnInit - 游戏结束界面初始化完成");
    }

    /// <summary>
    /// 界面打开。
    /// 优先从 userData 读取（演示 OpenUIForm userData 传参），
    /// 回退到 DataNode 读取（兼容旧方式）。
    /// Phase 7: 所有文本本地化。
    /// </summary>
    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        m_BlinkTimer = 0f;

        // 本地化固定文本
        if (m_TitleLabel != null)
            m_TitleLabel.Text = m_LocalizationComponent?.GetString("GameOver") ?? "GAME OVER";
        if (m_NewRecordLabel != null)
            m_NewRecordLabel.Text = m_LocalizationComponent?.GetString("NewRecord") ?? "*** New Record! ***";
        if (m_PromptLabel != null)
            m_PromptLabel.Text = m_LocalizationComponent?.GetString("PressEnterToContinue") ?? "Press Enter to Restart";

        // 优先从 userData 读取（Feature 2: userData 传参测试）
        if (userData is GameOverUserData data)
        {
            if (m_ScoreLabel != null)
                m_ScoreLabel.Text = m_LocalizationComponent != null
                    ? m_LocalizationComponent.GetString("ScoreFormat", data.Score)
                    : $"Score: {data.Score}";
            if (m_HighScoreLabel != null)
                m_HighScoreLabel.Text = m_LocalizationComponent != null
                    ? m_LocalizationComponent.GetString("HighScoreMessage", data.HighScore)
                    : $"Best: {data.HighScore}";
            if (m_NewRecordLabel != null)
                m_NewRecordLabel.Visible = data.NewRecord;

            Log.Info("GameOverForm OnOpen - [userData] Score={0}, Best={1}, NewRecord={2}",
                data.Score, data.HighScore, data.NewRecord);
            return;
        }

        // 回退：从 DataNode 读取游戏结果
        DataNodeComponent dataNode = GF.DataNode;
        if (dataNode == null) return;

        VarInt32 scoreVar = dataNode.GetData<VarInt32>("Game.Score");
        int score = scoreVar?.Value ?? 0;

        VarInt32 highScoreVar = dataNode.GetData<VarInt32>("Game.HighScore");
        int highScore = highScoreVar?.Value ?? 0;

        if (m_ScoreLabel != null)
            m_ScoreLabel.Text = m_LocalizationComponent != null
                ? m_LocalizationComponent.GetString("ScoreFormat", score)
                : $"Score: {score}";
        if (m_HighScoreLabel != null)
            m_HighScoreLabel.Text = m_LocalizationComponent != null
                ? m_LocalizationComponent.GetString("HighScoreMessage", highScore)
                : $"Best: {highScore}";
        if (m_NewRecordLabel != null)
            m_NewRecordLabel.Visible = (score > 0 && score >= highScore);

        Log.Info("GameOverForm OnOpen - [DataNode] Score={0}, Best={1}", score, highScore);
    }

    /// <summary>
    /// 界面关闭。
    /// </summary>
    protected internal override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        Log.Info("GameOverForm OnClose - 游戏结束界面已关闭");
    }

    /// <summary>
    /// 每帧更新。
    /// 提示文字闪烁效果。
    /// </summary>
    protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        if (m_PromptLabel == null) return;

        m_BlinkTimer += elapseSeconds;
        m_PromptLabel.Visible = (m_BlinkTimer % 1.4f) < 1.0f;
    }
}
