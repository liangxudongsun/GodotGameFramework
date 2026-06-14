//------------------------------------------------------------
// 测试用实体逻辑类
// 验证 EntityLogic 基类的 2D 兼容性、生命周期回调和对象池复用
//------------------------------------------------------------

using Godot;
using GodotGameFramework;

/// <summary>
/// 测试用实体逻辑。
///
/// 验证以下功能：
/// 1. EntityLogic 生命周期回调（OnInit/OnShow/OnHide/OnUpdate）
/// 2. CachedNode / IsNode2D / Position2D 等 2D 便捷属性
/// 3. Attach/Detach 回调
/// 4. 对象池复用时生命周期回调的调用次数
/// </summary>
public class TestEntityLogic : EntityLogic
{
    /// <summary>
    /// OnInit 被调用的次数。
    /// 对象池复用时每次 Show 都会重新触发 OnInit。
    /// </summary>
    public int InitCount { get; private set; }

    /// <summary>
    /// OnShow 被调用的次数。
    /// </summary>
    public int ShowCount { get; private set; }

    /// <summary>
    /// OnHide 被调用的次数。
    /// </summary>
    public int HideCount { get; private set; }

    /// <summary>
    /// OnUpdate 是否已被调用。
    /// </summary>
    public bool UpdateCalled { get; private set; }

    /// <summary>
    /// OnAttachTo 被调用的次数。
    /// </summary>
    public int AttachToCount { get; private set; }

    /// <summary>
    /// OnDetachFrom 被调用的次数。
    /// </summary>
    public int DetachFromCount { get; private set; }

    protected internal override void OnInit(object userData)
    {
        InitCount++;
        GD.Print($"    [TestEntityLogic] OnInit(#{InitCount}) - CachedNode={CachedNode?.Name}, IsNode2D={IsNode2D}");
    }

    protected internal override void OnShow(object userData)
    {
        ShowCount++;
        GD.Print($"    [TestEntityLogic] OnShow(#{ShowCount}) - Position2D={Position2D}");

        // 设置初始位置
        if (IsNode2D)
        {
            Position2D = new Vector2(100, 200);
            GD.Print($"    [TestEntityLogic] 设置 Position2D={Position2D}");
        }
    }

    protected internal override void OnHide(bool isShutdown, object userData)
    {
        HideCount++;
        GD.Print($"    [TestEntityLogic] OnHide(#{HideCount}) - isShutdown={isShutdown}");
    }

    protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (!UpdateCalled)
        {
            UpdateCalled = true;
            GD.Print("    [TestEntityLogic] OnUpdate - 首帧更新");
        }
    }

    protected internal override void OnAttachTo(GameFramework.Entity.IEntity parentEntity, object userData)
    {
        AttachToCount++;
        GD.Print($"    [TestEntityLogic] OnAttachTo(#{AttachToCount}) - parentId={parentEntity?.Id}");
    }

    protected internal override void OnDetachFrom(GameFramework.Entity.IEntity parentEntity, object userData)
    {
        DetachFromCount++;
        GD.Print($"    [TestEntityLogic] OnDetachFrom(#{DetachFromCount}) - parentId={parentEntity?.Id}");
    }
}
