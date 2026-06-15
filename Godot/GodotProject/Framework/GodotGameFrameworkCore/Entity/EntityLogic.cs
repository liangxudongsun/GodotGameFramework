//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Entity;
using Godot;

namespace GodotGameFramework.Entity
{
    /// <summary>
    /// 实体逻辑基类。
    /// 这是用户编写实体游戏逻辑的基类。用户通过继承此类并重写生命周期方法，
    /// 来实现实体的初始化、显示、隐藏、更新等逻辑。
    /// </summary>
    public abstract class EntityLogic
    {
        private bool m_Available;

        private bool m_Visible;

        public Entity Owner { get; private set; }

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

        public bool Available => m_Available;

        public bool Visible
        {
            get => m_Visible;
            set => InternalSetVisible(value);
        }

        public bool IsNode2D => CachedNode is Node2D;

        public bool IsNode3D => CachedNode is Node3D;

        public Node2D AsNode2D => CachedNode as Node2D;

        public Node3D AsNode3D => CachedNode as Node3D;

        public Vector2 Position2D
        {
            get => AsNode2D?.Position ?? Vector2.Zero;
            set { if (AsNode2D != null) AsNode2D.Position = value; }
        }

        public float Rotation2D
        {
            get => AsNode2D?.Rotation ?? 0f;
            set { if (AsNode2D != null) AsNode2D.Rotation = value; }
        }

        public Vector2 Scale2D
        {
            get => AsNode2D?.Scale ?? Vector2.One;
            set { if (AsNode2D != null) AsNode2D.Scale = value; }
        }

        public Vector3 Position3D
        {
            get => AsNode3D?.Position ?? Vector3.Zero;
            set { if (AsNode3D != null) AsNode3D.Position = value; }
        }

        public Vector3 Rotation3D
        {
            get => AsNode3D?.Rotation ?? Vector3.Zero;
            set { if (AsNode3D != null) AsNode3D.Rotation = value; }
        }
        public Vector3 Scale3D
        {
            get => AsNode3D?.Scale ?? Vector3.One;
            set { if (AsNode3D != null) AsNode3D.Scale = value; }
        }

        protected internal virtual void OnInit(object userData)
        {
        }


        protected internal virtual void OnShow(object userData)
        {
        }


        protected internal virtual void OnHide(bool isShutdown, object userData)
        {
        }


        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 实体附加子实体回调。
        /// 当有子实体附加到此实体时调用（在父实体上触发）。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnAttached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 实体解除子实体回调。
        /// 当子实体从此实体解除时调用（在父实体上触发）。
        /// </summary>
        /// <param name="childEntity">被解除的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnDetached(IEntity childEntity, object userData)
        {
        }

        /// <summary>
        /// 实体附加到父实体回调。
        /// 当此实体被附加到父实体时调用（在子实体上触发）。
        /// </summary>
        /// <param name="parentEntity">被附加到的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnAttachTo(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 实体从父实体解除回调。
        /// 当此实体从父实体解除时调用（在子实体上触发）。
        /// </summary>
        /// <param name="parentEntity">被解除的父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {
        }

        /// <summary>
        /// 实体回收回调。
        /// 所以所有需要在每次显示时重置的状态都应该在此方法中清理。
        /// </summary>
        protected internal virtual void OnRecycle()
        {
        }



        internal void InternalShow(object userData)
        {
            m_Available = true;
            InternalSetVisible(true);
            OnShow(userData);
        }


        internal void InternalHide(bool isShutdown, object userData)
        {
            OnHide(isShutdown, userData);
            InternalSetVisible(false);
            m_Available = false;
        }


        protected virtual void InternalSetVisible(bool visible)
        {
            m_Visible = visible;
            if (Owner != null && Owner.GetChildCount() > 0 && Owner.GetChild(0) is CanvasItem cachedNode)
            {
                cachedNode.Visible = visible;
            }
        }


        internal void InternalSetOwner(Entity owner)
        {
            Owner = owner;
        }


    }
}
