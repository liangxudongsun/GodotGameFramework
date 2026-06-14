//------------------------------------------------------------
// 方块逻辑基类。
// 处理视觉创建、鼠标点击检测和对象池安全的子节点管理。
//------------------------------------------------------------

using Godot;
using GodotGameFramework;

/// <summary>
/// 方块逻辑基类。
///
/// 提供方块的通用功能：
/// 1. 程序化创建视觉子节点（ColorRect）
/// 2. 鼠标点击检测（基于位置判断，不依赖物理拾取）
/// 3. 对象池安全的子节点管理
///
/// 对象池安全设计：
/// 当实体从对象池复用时，CachedNode（Node2D）已经带有之前创建的 ColorRect 子节点。
/// EnsureVisuals() 会检查子节点是否已存在，避免重复创建。
///
/// 点击检测设计：
/// 使用 OnUpdate 中的静态帧计数器 + 鼠标位置检测，
/// 不依赖 Area2D 的 input_event 信号（需要 physics_object_picking 开启）。
/// 所有方块共享鼠标按下状态，每帧只在第一个方块的 OnUpdate 中更新一次。
/// </summary>
public abstract class BlockLogic : EntityLogic
{
    /// <summary>
    /// 方块大小（像素）。
    /// </summary>
    protected const float BlockSize = 50f;

    /// <summary>
    /// 可视矩形。
    /// </summary>
    protected ColorRect m_ColorRect;

    /// <summary>
    /// 是否已被点击（防止重复点击）。
    /// </summary>
    protected bool m_Clicked;

    /// <summary>
    /// 从 BlockSpawnData 读取的颜色（数据驱动）。
    /// 在 OnShow 中从 userData 获取。
    /// </summary>
    protected Color m_DataColor = Colors.White;

    // ================================================================
    //  静态鼠标状态（所有方块共享）
    // ================================================================

    /// <summary>上一帧鼠标左键是否按下。</summary>
    private static bool s_PrevMouseDown = false;

    /// <summary>当前帧是否刚按下鼠标左键。</summary>
    private static bool s_JustPressed = false;

    /// <summary>上次更新鼠标状态的帧号。</summary>
    private static ulong s_LastUpdateFrame = ulong.MaxValue;

    // ================================================================
    //  视觉管理
    // ================================================================

    /// <summary>
    /// 确保视觉子节点存在。
    ///
    /// 首次创建时在 CachedNode 上添加 ColorRect。
    /// 对象池复用时 CachedNode 已有 ColorRect，只需重新获取引用并更新颜色。
    /// </summary>
    /// <param name="color">方块颜色。</param>
    protected void EnsureVisuals(Color color)
    {
        if (CachedNode == null) return;

        if (CachedNode.GetChildCount() > 0)
        {
            // 对象池复用：子节点已存在，重新获取引用
            m_ColorRect = CachedNode.GetChild<ColorRect>(0);
            if (m_ColorRect != null)
            {
                m_ColorRect.Color = color;
            }
            return;
        }

        // 首次创建 ColorRect
        m_ColorRect = new ColorRect();
        m_ColorRect.Name = "BlockVisual";
        m_ColorRect.Size = new Vector2(BlockSize, BlockSize);
        m_ColorRect.Position = new Vector2(-BlockSize / 2f, -BlockSize / 2f);
        m_ColorRect.Color = color;
        CachedNode.AddChild(m_ColorRect);
    }

    // ================================================================
    //  生命周期
    // ================================================================

    /// <summary>
    /// 实体显示回调。
    /// 重置点击状态，从 userData 读取位置和颜色。
    /// </summary>
    protected internal override void OnShow(object userData)
    {
        m_Clicked = false;

        // 从 userData 读取生成数据
        if (userData is BlockSpawnData spawnData)
        {
            if (IsNode2D)
            {
                Position2D = spawnData.Position;
            }
            m_DataColor = spawnData.Color;
            // 更新视觉颜色
            EnsureVisuals(m_DataColor);
        }
    }

    /// <summary>
    /// 每帧更新。
    /// 更新鼠标状态（每帧只更新一次），检测点击。
    /// </summary>
    protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (m_Clicked) return;

        // 每帧只在第一个方块的 OnUpdate 中更新鼠标状态
        ulong curFrame = (ulong)Engine.GetProcessFrames();
        if (curFrame != s_LastUpdateFrame)
        {
            bool curDown = Input.IsMouseButtonPressed(MouseButton.Left);
            s_JustPressed = curDown && !s_PrevMouseDown;
            s_PrevMouseDown = curDown;
            s_LastUpdateFrame = curFrame;
        }

        // 检测点击
        if (s_JustPressed)
        {
            CheckClick();
        }
    }

    /// <summary>
    /// 检测鼠标是否在方块范围内。
    /// 如果在范围内，标记为已点击并调用 OnBlockClicked()。
    /// </summary>
    private void CheckClick()
    {
        if (!IsNode2D || AsNode2D == null) return;

        Vector2 mousePos = AsNode2D.GetGlobalMousePosition();
        Vector2 blockPos = Position2D;
        float half = BlockSize / 2f;

        if (mousePos.X >= blockPos.X - half && mousePos.X <= blockPos.X + half &&
            mousePos.Y >= blockPos.Y - half && mousePos.Y <= blockPos.Y + half)
        {
            m_Clicked = true;
            OnBlockClicked();
        }
    }

    /// <summary>
    /// 方块被点击时的处理。
    /// 子类实现具体的点击行为（加分/扣分、触发事件、隐藏实体）。
    /// </summary>
    protected abstract void OnBlockClicked();
}
