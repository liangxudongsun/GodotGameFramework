//--------------------------------------------------------------
// 测试覆盖层界面逻辑。
// 同时演示 Feature 3 (OnPause/OnResume) 和 Feature 4 (Depth Sorting)。
//
// 框架特性展示：
// - PauseCoveredUIForm：暂停同组被覆盖窗体
// - OnDepthChanged：深度变化回调
// - RefocusUIForm：重新聚焦窗体
//--------------------------------------------------------------

using Godot;
using GodotGameFramework;

/// <summary>
/// 测试覆盖层界面逻辑。
///
/// 打开在 "Normal" 组中，PauseCoveredUIForm=true。
/// 当此窗体打开时，同组的 GameHUD 会收到 OnPause（隐藏）。
/// 当此窗体关闭时，GameHUD 收到 OnResume（恢复显示）。
///
/// 深度排序演示：
/// - 可多次按 T 打开多个实例，观察 depth 值变化
/// - 按 R 将第一个实例 Refocus 到顶部，观察深度重新计算
/// - OnDepthChanged 回调实时显示当前深度值
///
/// 按键说明：
/// - T: 打开/关闭覆盖层（由 TestGameProcedure 管理）
/// - R: Refocus 第一个覆盖层实例（由 TestGameProcedure 管理）
/// </summary>
public class TestOverlayForm : UIFormLogic
{
    /// <summary>深度标签。</summary>
    private Label m_DepthLabel;

    /// <summary>标题标签。</summary>
    private Label m_TitleLabel;

    /// <summary>提示标签。</summary>
    private Label m_PromptLabel;

    /// <summary>当前深度值。</summary>
    private int m_CurrentDepth = -1;

    /// <summary>
    /// 界面初始化。
    /// </summary>
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);

        Control root = CachedControl;
        if (root == null)
        {
            Log.Warning("TestOverlayForm: CachedControl is null.");
            return;
        }

        m_TitleLabel = root.GetNode<Label>("VBox/Title");
        m_DepthLabel = root.GetNode<Label>("VBox/DepthLabel");
        m_PromptLabel = root.GetNode<Label>("VBox/Prompt");

        Log.Info("TestOverlayForm OnInit - 测试覆盖层初始化完成");
    }

    /// <summary>
    /// 界面打开。
    /// </summary>
    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        Log.Info("TestOverlayForm OnOpen - 测试覆盖层已打开");
    }

    /// <summary>
    /// 界面关闭。
    /// </summary>
    protected internal override void OnClose(bool isShutdown, object userData)
    {
        base.OnClose(isShutdown, userData);
        m_CurrentDepth = -1;
        Log.Info("TestOverlayForm OnClose - 测试覆盖层已关闭");
    }

    /// <summary>
    /// 深度变化回调。
    /// 当同组中有窗体打开/关闭/Refocus 时由框架调用。
    /// </summary>
    protected internal override void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        base.OnDepthChanged(uiGroupDepth, depthInUIGroup);
        m_CurrentDepth = depthInUIGroup;

        if (m_DepthLabel != null)
        {
            m_DepthLabel.Text = $"Depth: {depthInUIGroup} (Group: {uiGroupDepth})";
        }

        Log.Info("TestOverlayForm OnDepthChanged - uiGroupDepth={0}, depthInUIGroup={1}",
            uiGroupDepth, depthInUIGroup);
    }

    /// <summary>
    /// 重新获得焦点回调。
    /// 当窗体被 RefocusUIForm 移动到组的最顶部时调用。
    /// </summary>
    protected internal override void OnRefocus(object userData)
    {
        base.OnRefocus(userData);
        Log.Info("TestOverlayForm OnRefocus - 窗体已重新聚焦到顶部");
    }

    /// <summary>
    /// 获取当前深度值（供外部查询）。
    /// </summary>
    public int CurrentDepth => m_CurrentDepth;
}
