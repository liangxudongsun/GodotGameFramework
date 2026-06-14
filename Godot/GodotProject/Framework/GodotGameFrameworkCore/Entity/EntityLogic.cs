//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Entity;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 实体逻辑基类。
    ///
    /// 这是用户编写实体游戏逻辑的基类。用户通过继承此类并重写生命周期方法，
    /// 来实现实体的初始化、显示、隐藏、更新等逻辑。
    ///
    /// EntityLogic 不继承 Node，它是一个纯 C# 类，
    /// 通过 Owner 属性持有对 Entity（Node）的引用，
    /// 并通过 CachedNode 属性访问实际的 Node2D 或 Node3D 子节点。
    ///
    /// 对齐 UGF: 包含 m_Available/m_Visible 双状态管理和 InternalSetVisible 扩展点。
    ///
    /// 2D/3D 兼容设计：
    /// - CachedNode 返回 Entity 的第一个子节点（实际的 Node2D 或 Node3D）
    /// - IsNode2D / IsNode3D 判断子节点类型
    /// - AsNode2D / AsNode3D 获取类型化的子节点引用
    /// - Position2D / Position3D 等便捷属性直接操作子节点的位置/旋转
    ///
    /// 使用方式：
    /// <code>
    /// public class EnemyLogic : EntityLogic
    /// {
    ///     private int m_HP;
    ///
    ///     protected internal override void OnInit(object userData)
    ///     {
    ///         m_HP = 100;
    ///     }
    ///
    ///     protected internal override void OnShow(object userData)
    ///     {
    ///         // 设置初始位置（自动兼容 2D/3D）
    ///         if (IsNode2D)
    ///             Position2D = new Vector2(100, 200);
    ///         else if (IsNode3D)
    ///             Position3D = new Vector3(100, 0, 200);
    ///     }
    ///
    ///     protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    ///     {
    ///         // 每帧更新逻辑
    ///     }
    /// }
    ///
    /// // 显示实体时指定逻辑类型
    /// entityComponent.ShowEntity&lt;EnemyLogic&gt;(1, "res://Scenes/Enemy.tscn", "EnemyGroup");
    /// </code>
    ///
    /// 对应 Unity 版本中的 EntityLogic（MonoBehaviour）。
    /// </summary>
    public abstract class EntityLogic
    {
        /// <summary>
        /// 实体逻辑是否可用（已显示且未隐藏）。
        /// UGF 风格：OnShow 后为 true，OnHide 后为 false。
        /// </summary>
        private bool m_Available;

        /// <summary>
        /// 实体逻辑是否可见。
        /// UGF 风格：通过 Visible 属性或 InternalSetVisible 控制。
        /// </summary>
        private bool m_Visible;

        /// <summary>
        /// 关联的 Entity 节点。
        /// Entity 是一个 Node，实现了 IEntity 接口，
        /// 作为实际游戏节点的包装器存在。
        /// </summary>
        public Entity Owner { get; private set; }

        /// <summary>
        /// 获取实际的子节点。
        ///
        /// 这是 Entity 的第一个子节点，即从 PackedScene 实例化的实际游戏节点。
        /// 可能是 Node2D（2D 游戏）或 Node3D（3D 游戏）。
        /// </summary>
        public Node CachedNode
        {
            get
            {
                if (Owner != null && Owner.GetChildCount() > 0)
                {
                    return Owner.GetChild(0);
                }

                return null;
            }
        }

        /// <summary>
        /// 获取实体逻辑是否可用。
        /// UGF 风格：Available = true 表示实体已显示且未隐藏。
        /// </summary>
        public bool Available => m_Available;

        /// <summary>
        /// 获取或设置实体逻辑是否可见。
        /// UGF 风格：设置时调用 InternalSetVisible，子类可重写自定义可见性行为。
        /// </summary>
        public bool Visible
        {
            get => m_Visible;
            set => InternalSetVisible(value);
        }

        /// <summary>
        /// 实际的子节点是否为 Node2D。
        /// </summary>
        public bool IsNode2D => CachedNode is Node2D;

        /// <summary>
        /// 实际的子节点是否为 Node3D。
        /// </summary>
        public bool IsNode3D => CachedNode is Node3D;

        /// <summary>
        /// 获取 Node2D 类型的子节点引用。
        /// 如果子节点不是 Node2D，返回 null。
        /// </summary>
        public Node2D AsNode2D => CachedNode as Node2D;

        /// <summary>
        /// 获取 Node3D 类型的子节点引用。
        /// 如果子节点不是 Node3D，返回 null。
        /// </summary>
        public Node3D AsNode3D => CachedNode as Node3D;

        /// <summary>
        /// 获取或设置 2D 位置。
        /// 仅当子节点为 Node2D 时有效。
        /// </summary>
        public Vector2 Position2D
        {
            get => AsNode2D?.Position ?? Vector2.Zero;
            set { if (AsNode2D != null) AsNode2D.Position = value; }
        }

        /// <summary>
        /// 获取或设置 2D 旋转（弧度）。
        /// 仅当子节点为 Node2D 时有效。
        /// </summary>
        public float Rotation2D
        {
            get => AsNode2D?.Rotation ?? 0f;
            set { if (AsNode2D != null) AsNode2D.Rotation = value; }
        }

        /// <summary>
        /// 获取或设置 2D 缩放。
        /// 仅当子节点为 Node2D 时有效。
        /// </summary>
        public Vector2 Scale2D
        {
            get => AsNode2D?.Scale ?? Vector2.One;
            set { if (AsNode2D != null) AsNode2D.Scale = value; }
        }

        /// <summary>
        /// 获取或设置 3D 位置。
        /// 仅当子节点为 Node3D 时有效。
        /// </summary>
        public Vector3 Position3D
        {
            get => AsNode3D?.Position ?? Vector3.Zero;
            set { if (AsNode3D != null) AsNode3D.Position = value; }
        }

        /// <summary>
        /// 获取或设置 3D 旋转（弧度，欧拉角）。
        /// 仅当子节点为 Node3D 时有效。
        /// </summary>
        public Vector3 Rotation3D
        {
            get => AsNode3D?.Rotation ?? Vector3.Zero;
            set { if (AsNode3D != null) AsNode3D.Rotation = value; }
        }

        /// <summary>
        /// 获取或设置 3D 缩放。
        /// 仅当子节点为 Node3D 时有效。
        /// </summary>
        public Vector3 Scale3D
        {
            get => AsNode3D?.Scale ?? Vector3.One;
            set { if (AsNode3D != null) AsNode3D.Scale = value; }
        }

        // ================================================================
        //  生命周期虚方法
        // ================================================================

        /// <summary>
        /// 实体初始化回调。
        ///
        /// 在实体首次创建时调用一次。
        /// 用于初始化实体逻辑状态（如 HP、速度等）。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnInit(object userData)
        {
        }

        /// <summary>
        /// 实体显示回调。
        ///
        /// 每次实体从隐藏状态变为显示状态时调用。
        /// 用于设置实体的初始位置、外观等。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnShow(object userData)
        {
        }

        /// <summary>
        /// 实体隐藏回调。
        ///
        /// 实体从显示状态变为隐藏状态时调用。
        /// 用于清理显示相关的状态。
        /// </summary>
        /// <param name="isShutdown">是否是因为关闭实体管理器而隐藏。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnHide(bool isShutdown, object userData)
        {
        }

        /// <summary>
        /// 实体轮询回调。
        ///
        /// 每帧调用，用于更新实体逻辑（如移动、AI 等）。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（秒）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（秒）。</param>
        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 实体附加子实体回调。
        ///
        /// 当有子实体附加到此实体时调用（在父实体上触发）。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnAttached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 实体解除子实体回调。
        ///
        /// 当子实体从此实体解除时调用（在父实体上触发）。
        /// </summary>
        /// <param name="childEntity">被解除的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnDetached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 实体附加到父实体回调。
        ///
        /// 当此实体被附加到父实体时调用（在子实体上触发）。
        /// </summary>
        /// <param name="parentEntity">被附加到的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnAttachTo(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 实体从父实体解除回调。
        ///
        /// 当此实体从父实体解除时调用（在子实体上触发）。
        /// </summary>
        /// <param name="parentEntity">被解除的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 实体回收回调。
        ///
        /// 当实体被隐藏并归还到对象池时调用。
        /// 子类应重写此方法来重置状态，以便下次从池中复用时状态是干净的。
        ///
        /// UGF 风格：EntityLogic 不销毁，只是"休眠"。
        /// OnInit 不会再次调用，OnShow 会重新调用。
        /// 所以所有需要在每次显示时重置的状态都应该在此方法中清理。
        /// </summary>
        protected internal virtual void OnRecycle()
        {
        }

        // ================================================================
        //  UGF 风格内部方法（状态管理）
        // ================================================================

        /// <summary>
        /// 内部方法：实体显示。
        /// UGF 风格：设置 Available=true，调用 InternalSetVisible(true)，再调用 OnShow。
        /// 由 Entity.OnShow 调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        internal void InternalShow(object userData)
        {
            m_Available = true;
            InternalSetVisible(true);
            OnShow(userData);
        }

        /// <summary>
        /// 内部方法：实体隐藏。
        /// UGF 风格：先调用 OnHide，再设置 InternalSetVisible(false)，最后设置 Available=false。
        /// 由 Entity.OnHide 调用。
        /// </summary>
        /// <param name="isShutdown">是否是因为关闭实体管理器而隐藏。</param>
        /// <param name="userData">用户自定义数据。</param>
        internal void InternalHide(bool isShutdown, object userData)
        {
            OnHide(isShutdown, userData);
            InternalSetVisible(false);
            m_Available = false;
        }

        /// <summary>
        /// 内部方法：设置可见性。
        /// UGF 风格：virtual 方法，子类可重写自定义可见性行为。
        /// 默认实现控制 CachedNode（CanvasItem）的 Visible 属性。
        /// </summary>
        /// <param name="visible">是否可见。</param>
        protected virtual void InternalSetVisible(bool visible)
        {
            m_Visible = visible;
            if (Owner != null && Owner.GetChildCount() > 0 && Owner.GetChild(0) is CanvasItem cachedNode)
            {
                cachedNode.Visible = visible;
            }
        }

        /// <summary>
        /// 内部方法：设置关联的 Entity 节点。
        /// 由 DefaultEntityHelper.CreateEntity 调用。
        /// </summary>
        /// <param name="owner">关联的 Entity 节点。</param>
        internal void InternalSetOwner(Entity owner)
        {
            Owner = owner;
        }
    }
}
