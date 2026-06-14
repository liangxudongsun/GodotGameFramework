//------------------------------------------------------------
// 主菜单界面逻辑。
// 通过 UIComponent.OpenUIForm<MainMenuForm>() 打开。
// 演示 UIFormLogic 生命周期：OnInit → OnOpen → OnClose → OnRecycle
// Phase 7: 本地化文本 + 语言切换按钮
//------------------------------------------------------------

using GameFramework.Localization;
using Godot;
using GodotGameFramework;

/// <summary>
/// 主菜单界面逻辑。
///
/// 显示游戏标题、操作说明和开始按钮。
/// Phase 7: 所有文本通过 LocalizationComponent.GetString() 获取，
/// 支持运行时语言切换。
///
/// 框架特性展示：
/// - UIFormLogic.OnInit：获取子节点引用（仅在首次创建时调用）
/// - UIFormLogic.OnOpen：每次打开时重置状态并刷新本地化文本
/// - LocalizationComponent.GetString：获取翻译文本
/// - 运行时语言切换：点击按钮即时切换中文/英文
/// </summary>
public class MainMenuForm : UIFormLogic
{
    /// <summary>标题标签。</summary>
    private Label m_TitleLabel;

    /// <summary>副标题标签。</summary>
    private Label m_SubtitleLabel;

    /// <summary>阶段信息标签。</summary>
    private Label m_PhaseLabel;

    /// <summary>规则标签。</summary>
    private Label m_RulesLabel;

    /// <summary>开始游戏按钮。</summary>
    private Button m_StartButton;

    /// <summary>语言切换按钮。</summary>
    private Button m_LanguageButton;

    /// <summary>本地化组件引用。</summary>
    private LocalizationComponent m_LocalizationComponent;

    /// <summary>
    /// 是否请求开始游戏。
    /// 由 StartButton 点击时设置为 true，Procedure 每帧检查此标志。
    /// </summary>
    public bool StartRequested { get; private set; }

    /// <summary>
    /// 界面初始化。
    /// 从场景树中获取子节点引用。仅在首次创建时调用一次。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);

        Control root = CachedControl;
        if (root == null)
        {
            Log.Warning("MainMenuForm: CachedControl is null.");
            return;
        }

        m_TitleLabel = root.GetNode<Label>("VBox/Title");
        m_SubtitleLabel = root.GetNode<Label>("VBox/Subtitle");
        m_PhaseLabel = root.GetNode<Label>("VBox/PhaseLabel");
        m_RulesLabel = root.GetNode<Label>("VBox/Rules");
        m_StartButton = root.GetNode<Button>("VBox/StartButton");
        m_LanguageButton = root.GetNode<Button>("VBox/LanguageButton");

        m_LocalizationComponent = GF.Localization;

        // 连接按钮事件
        if (m_StartButton != null)
        {
            m_StartButton.Pressed += OnStartButtonPressed;
        }
        if (m_LanguageButton != null)
        {
            m_LanguageButton.Pressed += OnLanguageButtonPressed;
        }

        Log.Info("MainMenuForm OnInit - 主菜单界面初始化完成");
    }

    /// <summary>
    /// 界面打开。
    /// 重置开始标志，刷新本地化文本。
    /// </summary>
    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        StartRequested = false;
        RefreshLocalizedText();

        Log.Info("MainMenuForm OnOpen - 主菜单界面已打开");
    }

    /// <summary>
    /// 界面关闭。
    /// </summary>
    protected internal override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        Log.Info("MainMenuForm OnClose - 主菜单界面已关闭");
    }

    /// <summary>
    /// 界面回收。
    /// 对象池复用前的清理（不释放子节点引用，保留供下次复用）。
    /// </summary>
    protected internal override void OnRecycle()
    {
        base.OnRecycle();
        Log.Info("MainMenuForm OnRecycle - 主菜单界面已回收");
    }

    /// <summary>
    /// 刷新所有本地化文本。
    /// 通过 LocalizationComponent.GetString() 获取翻译。
    /// </summary>
    private void RefreshLocalizedText()
    {
        if (m_LocalizationComponent == null) return;

        if (m_TitleLabel != null)
            m_TitleLabel.Text = m_LocalizationComponent.GetString("GameTitle");

        if (m_SubtitleLabel != null)
            m_SubtitleLabel.Text = m_LocalizationComponent.GetString("DemoSubtitle");

        if (m_PhaseLabel != null)
            m_PhaseLabel.Text = m_LocalizationComponent.GetString("PhaseInfo");

        if (m_RulesLabel != null)
            m_RulesLabel.Text = m_LocalizationComponent.GetString("GameRules");

        if (m_StartButton != null)
            m_StartButton.Text = m_LocalizationComponent.GetString("StartGame");

        // 语言切换按钮显示当前可切换到的语言
        if (m_LanguageButton != null)
        {
            m_LanguageButton.Text = m_LocalizationComponent.GetString("SwitchLanguage");
        }
    }

    /// <summary>
    /// 开始游戏按钮点击处理。
    /// 设置 StartRequested 标志，由 Procedure 检测并执行流程切换。
    /// </summary>
    private void OnStartButtonPressed()
    {
        StartRequested = true;
    }

    /// <summary>
    /// 语言切换按钮点击处理。
    /// 在中文和英文之间切换，重新加载字典并刷新所有文本。
    /// </summary>
    private void OnLanguageButtonPressed()
    {
        if (m_LocalizationComponent == null) return;

        Language currentLang = m_LocalizationComponent.Language;
        Language targetLang;
        string dictFile;

        if (currentLang == Language.ChineseSimplified)
        {
            targetLang = Language.English;
            dictFile = "res://Data/Localization/English.txt";
        }
        else
        {
            targetLang = Language.ChineseSimplified;
            dictFile = "res://Data/Localization/ChineseSimplified.txt";
        }

        // 切换语言：清除旧字典 → 加载新字典 → 设置语言
        m_LocalizationComponent.RemoveAllRawStrings();
        bool loaded = m_LocalizationComponent.ReadData(dictFile);
        if (loaded)
        {
            m_LocalizationComponent.Language = targetLang;
            GD.Print($"  [Phase 7] 语言已切换: {currentLang} → {targetLang}");
        }

        // 刷新所有 UI 文本
        RefreshLocalizedText();
    }
}
