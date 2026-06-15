//------------------------------------------------------------
// 暂停菜单界面逻辑。
// 半透明遮罩 + 暂停提示。
// Phase 7: 所有文本通过 LocalizationComponent 本地化。
//------------------------------------------------------------

using GameFramework.Localization;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.UI;

/// <summary>
/// 暂停菜单界面逻辑。
///
/// 显示半透明遮罩和"已暂停"提示。
/// 打开在 Popup 组中，覆盖在 GameHUD 之上。
///
/// Phase 7: 标题和提示文本通过 GetString 本地化。
/// </summary>
public class PauseMenuForm : UIFormLogic
{
    /// <summary>标题标签。</summary>
    private Label m_TitleLabel;

    /// <summary>提示标签。</summary>
    private Label m_PromptLabel;

    /// <summary>本地化组件引用。</summary>
    private LocalizationComponent m_LocalizationComponent;

    /// <summary>
    /// 界面初始化。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);

        Control root = CachedControl;
        if (root == null)
        {
            Log.Warning("PauseMenuForm: CachedControl is null.");
            return;
        }

        m_TitleLabel = root.GetNode<Label>("Panel/Margin/VBox/Title");
        m_PromptLabel = root.GetNode<Label>("Panel/Margin/VBox/Prompt");

        m_LocalizationComponent = GF.Localization;

        Log.Info("PauseMenuForm OnInit - 暂停菜单初始化完成");
    }

    /// <summary>
    /// 界面打开。
    /// 暂停游戏速度，本地化文本。
    /// </summary>
    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        // Phase 7: 本地化文本
        if (m_TitleLabel != null)
            m_TitleLabel.Text = m_LocalizationComponent?.GetString("Paused") ?? "PAUSED";
        if (m_PromptLabel != null)
            m_PromptLabel.Text = m_LocalizationComponent?.GetString("PressEscToResume") ?? "Press Esc to Resume";

        // 暂停游戏（设置 Engine.TimeScale = 0）
        BaseComponent baseComp = GF.Base;
        baseComp?.PauseGame();

        Log.Info("PauseMenuForm OnOpen - 游戏已暂停");
    }

    /// <summary>
    /// 界面关闭。
    /// 恢复游戏速度。
    /// </summary>
    protected internal override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);

        // 恢复游戏
        BaseComponent baseComp = GF.Base;
        baseComp?.ResumeGame();

        Log.Info("PauseMenuForm OnClose - 游戏已恢复");
    }
}
