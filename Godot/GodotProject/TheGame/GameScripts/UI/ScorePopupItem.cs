//--------------------------------------------------------------
// 分数弹出项逻辑。
// 使用 UIItem 对象池管理，显示在 GameHUD 左下角的分数变化提示。
//
// 框架特性展示：
// - UIItemBase：纯 C# UI 子元素基类
// - SpawnItem<TLogic>：从对象池获取 UIItem 实例
// - UnspawnItem：归还 UIItem 到对象池（隐藏但不销毁）
// - 对象池复用：多次点击时复用已回收的节点
//--------------------------------------------------------------

using Godot;
using GodotGameFramework;

/// <summary>
/// 分数弹出项逻辑。
///
/// 显示一次分数变化的信息：
/// - 绿色指示器 + "+10  Total: 30"（加分）
/// - 红色指示器 + "-5  Total: 25"（扣分）
///
/// 由 GameHUDForm 通过 SpawnItem 创建并管理生命周期。
/// 通过 SetData() 方法设置数据（因为 UIItemBase.OnInit 无参数）。
/// </summary>
public class ScorePopupItem : UIItemBase
{
    /// <summary>颜色指示器。</summary>
    private ColorRect m_Indicator;

    /// <summary>文本标签。</summary>
    private Label m_Label;

    /// <summary>该实例被 Spawn 的次数（用于观察对象池复用）。</summary>
    private int m_SpawnCount;

    /// <summary>标记是否已完成首次初始化。</summary>
    private bool m_Initialized;

    /// <summary>
    /// 界面项初始化。
    /// 获取子节点引用。仅在首次创建时调用一次。
    /// </summary>
    protected internal override void OnInit()
    {
        base.OnInit();

        Node node = CachedNode;
        if (node == null)
        {
            Log.Warning("ScorePopupItem: CachedNode is null.");
            return;
        }

        m_Indicator = node.GetNode<ColorRect>("HBox/Indicator");
        m_Label = node.GetNode<Label>("HBox/Label");
        m_Initialized = true;

        Log.Info("ScorePopupItem OnInit - 新建实例");
    }

    /// <summary>
    /// 设置分数弹出数据。
    /// 每次从对象池取出后调用，更新颜色和文本。
    /// 通过 SpawnCount 可观察对象池复用：#1=新建, #2+=复用。
    /// </summary>
    /// <param name="scoreDelta">分数变化量（正数加分，负数扣分）。</param>
    /// <param name="newTotal">变化后的总分。</param>
    public void SetData(int scoreDelta, int newTotal)
    {
        m_SpawnCount++;

        if (m_Indicator != null)
        {
            m_Indicator.Color = scoreDelta > 0
                ? new Color(0.2f, 0.8f, 0.2f)   // 绿色 = 加分
                : new Color(0.9f, 0.2f, 0.2f);   // 红色 = 扣分
        }

        if (m_Label != null)
        {
            string sign = scoreDelta > 0 ? "+" : "";
            string poolTag = m_SpawnCount > 1 ? $" [reuse#{m_SpawnCount}]" : " [new]";
            m_Label.Text = $"{sign}{scoreDelta}  Total: {newTotal}{poolTag}";
            m_Label.AddThemeColorOverride("font_color", scoreDelta > 0
                ? new Color(0.2f, 1.0f, 0.2f)
                : new Color(1.0f, 0.3f, 0.3f));
        }

        if (m_SpawnCount == 1)
        {
            Log.Info("ScorePopupItem SetData - 新建实例显示");
        }
        else
        {
            Log.Info("ScorePopupItem SetData - 对象池复用! 第 {0} 次使用此实例", m_SpawnCount);
        }
    }

    /// <summary>
    /// 界面项回收。
    /// 对象池释放此实例时调用，重置复用计数。
    /// </summary>
    protected internal override void OnRecycle()
    {
        base.OnRecycle();

        if (m_Label != null)
        {
            m_Label.RemoveThemeColorOverride("font_color");
        }

        m_SpawnCount = 0;
        m_Initialized = false;
    }
}
