//------------------------------------------------------------
// 游戏 HUD 界面逻辑。
// 实时显示分数、计时器、活跃方块数。
// 通过 DataNode 读取游戏数据，实现与游戏逻辑的解耦。
//
// Phase 5+: 集成 UIItem 对象池（分数弹出）和 OnPause/OnResume 测试。
// Phase 7: 所有显示文本通过 LocalizationComponent 本地化。
//------------------------------------------------------------

using System.Collections.Generic;
using GameFramework;
using GameFramework.DataNode;
using GameFramework.Localization;
using Godot;
using GodotGameFramework;

/// <summary>
/// 游戏 HUD（抬头显示）界面逻辑。
///
/// 实时显示游戏状态信息：
/// - 当前分数（从 DataNode "Game.Score" 读取）
/// - 剩余时间（从 DataNode "Game.TimeRemaining" 读取）
/// - 活跃方块数（从 DataNode "Game.ActiveBlocks" 读取）
/// - 当前游戏状态（从 DataNode "Game.State" 读取，键名用于本地化查找）
///
/// Phase 5+ 新增功能：
/// - UIItem 对象池：左下角显示分数变化弹出提示
/// - OnPause/OnResume：被同组 TestOverlay 覆盖时暂停/恢复
///
/// Phase 7:
/// - 分数/时间/方块/状态标签均通过 GetString 本地化
/// </summary>
public class GameHUDForm : UIFormLogic
{
    // ================================================================
    //  HUD 标签
    // ================================================================

    /// <summary>分数标签。</summary>
    private Label m_ScoreLabel;

    /// <summary>计时器标签。</summary>
    private Label m_TimerLabel;

    /// <summary>活跃方块数标签。</summary>
    private Label m_BlocksLabel;

    /// <summary>游戏状态标签。</summary>
    private Label m_StateLabel;

    // ================================================================
    //  UIItem 分数弹出
    // ================================================================

    /// <summary>分数弹出场景资源。</summary>
    private PackedScene m_ScorePopupScene;

    /// <summary>分数弹出容器节点（左下角 VBox）。</summary>
    private VBoxContainer m_PopupContainer;

    /// <summary>活跃的分数弹出项列表（item, 已存在时间）。</summary>
    private readonly List<(UIItemInstanceObject item, float elapsed)> m_ActivePopups = new();

    /// <summary>分数弹出自动消失时间（秒）。</summary>
    private const float PopupLifetime = 3f;

    // ================================================================
    //  暂停遮罩
    // ================================================================

    /// <summary>暂停时的半透明遮罩（视觉反馈）。</summary>
    private ColorRect m_DimOverlay;

    // ================================================================
    //  组件引用
    // ================================================================

    /// <summary>DataNode 组件引用。</summary>
    private DataNodeComponent m_DataNodeComponent;

    /// <summary>本地化组件引用（Phase 7）。</summary>
    private LocalizationComponent m_LocalizationComponent;

    /// <summary>上次记录的分数（避免每帧更新 Label）。</summary>
    private int m_LastScore = -1;

    /// <summary>上次记录的剩余时间。</summary>
    private int m_LastTimeRemaining = -1;

    /// <summary>上次记录的方块数。</summary>
    private int m_LastBlocks = -1;

    /// <summary>上次记录的状态。</summary>
    private string m_LastState = null;

    // ================================================================
    //  生命周期
    // ================================================================

    /// <summary>
    /// 界面初始化。
    /// 获取子节点引用、组件引用、加载 UIItem 场景。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);

        Control root = CachedControl;
        if (root == null)
        {
            Log.Warning("GameHUDForm: CachedControl is null.");
            return;
        }

        // 获取 HUD 中的 Label 节点
        m_ScoreLabel = root.GetNode<Label>("TopBar/Margin/HBox/ScoreLabel");
        m_TimerLabel = root.GetNode<Label>("TopBar/Margin/HBox/TimerLabel");
        m_BlocksLabel = root.GetNode<Label>("TopBar/Margin/HBox/BlocksLabel");
        m_StateLabel = root.GetNode<Label>("TopBar/Margin/HBox/StateLabel");

        // 获取分数弹出容器（左下角）
        m_PopupContainer = root.GetNode<VBoxContainer>("PopupContainer");

        // 获取暂停遮罩
        m_DimOverlay = root.GetNodeOrNull<ColorRect>("DimOverlay");

        // 加载分数弹出场景
        m_ScorePopupScene = GD.Load<PackedScene>("res://AAAGame/UI/ScorePopup.tscn");

        // 获取组件引用
        m_DataNodeComponent = GF.DataNode;
        m_LocalizationComponent = GF.Localization;

        Log.Info("GameHUDForm OnInit - 游戏 HUD 初始化完成");
    }

    /// <summary>
    /// 界面打开。
    /// 重置上次记录值，强制刷新所有显示。
    /// </summary>
    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        // 重置缓存值，强制在第一次 OnUpdate 中刷新
        m_LastScore = -1;
        m_LastTimeRemaining = -1;
        m_LastBlocks = -1;
        m_LastState = null;

        Log.Info("GameHUDForm OnOpen - 游戏 HUD 已打开");
    }

    /// <summary>
    /// 界面关闭。
    /// 清理所有 UIItem 弹出项。
    /// </summary>
    protected internal override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);

        // 清理所有活跃弹出项
        if (m_ScorePopupScene != null)
        {
            UnspawnAllItems(m_ScorePopupScene);
        }
        m_ActivePopups.Clear();

        Log.Info("GameHUDForm OnClose - 游戏 HUD 已关闭");
    }

    /// <summary>
    /// 每帧更新。
    /// 从 DataNode 读取最新数据并更新 Label（仅在数据变化时更新）。
    /// Phase 7: 使用 GetString 本地化格式。
    /// </summary>
    protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        if (m_DataNodeComponent == null) return;

        // 读取分数
        VarInt32 scoreVar = m_DataNodeComponent.GetData<VarInt32>("Game.Score");
        int score = scoreVar?.Value ?? 0;
        if (score != m_LastScore)
        {
            m_LastScore = score;
            if (m_ScoreLabel != null)
            {
                m_ScoreLabel.Text = m_LocalizationComponent != null
                    ? m_LocalizationComponent.GetString("ScoreFormat", score)
                    : $"Score: {score}";
            }
        }

        // 读取剩余时间
        VarInt32 timeVar = m_DataNodeComponent.GetData<VarInt32>("Game.TimeRemaining");
        int timeRemaining = timeVar?.Value ?? 0;
        if (timeRemaining != m_LastTimeRemaining)
        {
            m_LastTimeRemaining = timeRemaining;
            if (m_TimerLabel != null)
            {
                // 时间紧迫时变红
                if (timeRemaining <= 5 && timeRemaining > 0)
                {
                    m_TimerLabel.AddThemeColorOverride("font_color", new Color(1, 0.3f, 0.3f));
                }
                else
                {
                    m_TimerLabel.RemoveThemeColorOverride("font_color");
                }
                m_TimerLabel.Text = m_LocalizationComponent != null
                    ? m_LocalizationComponent.GetString("TimeFormat", timeRemaining)
                    : $"Time: {timeRemaining}s";
            }
        }

        // 读取活跃方块数
        VarInt32 blocksVar = m_DataNodeComponent.GetData<VarInt32>("Game.ActiveBlocks");
        int blocks = blocksVar?.Value ?? 0;
        if (blocks != m_LastBlocks)
        {
            m_LastBlocks = blocks;
            if (m_BlocksLabel != null)
            {
                m_BlocksLabel.Text = m_LocalizationComponent != null
                    ? m_LocalizationComponent.GetString("HUDBlocks", blocks)
                    : $"Blocks: {blocks}";
            }
        }

        // 读取游戏状态（键名作为本地化 key）
        VarString stateVar = m_DataNodeComponent.GetData<VarString>("Game.State");
        string state = stateVar?.Value ?? "";
        if (state != m_LastState)
        {
            m_LastState = state;
            if (m_StateLabel != null)
            {
                m_StateLabel.Text = m_LocalizationComponent != null
                    ? m_LocalizationComponent.GetString(state)
                    : state;
            }
        }

        // 更新分数弹出项生命周期（超时则归还到对象池）
        for (int i = m_ActivePopups.Count - 1; i >= 0; i--)
        {
            var (item, elapsed) = m_ActivePopups[i];
            float newElapsed = elapsed + elapseSeconds;
            if (newElapsed >= PopupLifetime)
            {
                UnspawnItem(m_ScorePopupScene, item);
                m_ActivePopups.RemoveAt(i);
            }
            else
            {
                m_ActivePopups[i] = (item, newElapsed);
            }
        }
    }

    // ================================================================
    //  暂停/恢复（Feature 3: OnPause/OnResume 测试）
    // ================================================================

    /// <summary>
    /// 界面暂停。
    /// 被同组 PauseCoveredUIForm=true 的窗体覆盖时由框架调用。
    /// 显示半透明遮罩作为视觉反馈。
    /// </summary>
    protected internal override void OnPause()
    {
        base.OnPause();

        if (m_DimOverlay != null)
        {
            m_DimOverlay.Visible = true;
        }

        Log.Info("GameHUDForm OnPause - HUD 已暂停（被同组 TestOverlay 覆盖）");
    }

    /// <summary>
    /// 界面暂停恢复。
    /// 覆盖窗体关闭后由框架调用。
    /// </summary>
    protected internal override void OnResume()
    {
        base.OnResume();

        if (m_DimOverlay != null)
        {
            m_DimOverlay.Visible = false;
        }

        Log.Info("GameHUDForm OnResume - HUD 已恢复");
    }

    // ================================================================
    //  UIItem 分数弹出（Feature 1: UIItem 对象池测试）
    // ================================================================

    /// <summary>
    /// 显示分数变化弹出。
    /// 从对象池获取 ScorePopupItem，设置数据后添加到活跃列表。
    /// 3 秒后由 OnUpdate 自动归还到对象池。
    /// </summary>
    /// <param name="scoreDelta">分数变化量（正数加分，负数扣分）。</param>
    /// <param name="newTotal">变化后的总分。</param>
    public void ShowScorePopup(int scoreDelta, int newTotal)
    {
        if (m_ScorePopupScene == null || m_PopupContainer == null) return;

        UIItemInstanceObject popup = SpawnItem<ScorePopupItem>(
            m_ScorePopupScene, m_PopupContainer,
            autoReleaseInterval: 10f, capacity: 20, expireTime: 60f);

        if (popup?.ItemLogic is ScorePopupItem logic)
        {
            logic.SetData(scoreDelta, newTotal);
            m_ActivePopups.Add((popup, 0f));
        }
    }
}
