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
    public abstract partial class EntityLogic : GodotComponent
    {
        private bool m_Available;

        private bool m_Visible;

        public Entity Entity { get; private set; }

        public bool Available => m_Available;
        private Node m_CachedNode;

        public Node CachedNode
        {
            get
            {
                if (m_CachedNode == null && Entity != null && Entity.GetChildCount() > 0)
                {
                    m_CachedNode = Entity.GetChild(0);
                }

                return m_CachedNode;
            }
        }
        public Vector2 Position2D
        {
            get
            {
                return CachedNode != null ? (Vector2)CachedNode.Get(Node2D.PropertyName.Position) : Vector2.Zero;
            }
            set
            {
                CachedNode?.Set(Node2D.PropertyName.Position, value);
            }
        }
        public Vector3 Position3D
        {
            get
            {
                return CachedNode != null ? (Vector3)CachedNode.Get(Node3D.PropertyName.Position) : Vector3.Zero;
            }
            set
            {
                CachedNode?.Set(Node3D.PropertyName.Position, value);
            }
        }

        public float Rotation2D
        {
            get
            {
                return CachedNode != null ? (float)CachedNode.Get(Node2D.PropertyName.Rotation) : 0f;
            }
            set
            {
                CachedNode?.Set(Node2D.PropertyName.Rotation, value);
            }
        }
        public Vector3 Rotation3D
        {
            get
            {
                return CachedNode != null ? (Vector3)CachedNode.Get(Node3D.PropertyName.Rotation) : Vector3.Zero;
            }
            set
            {
                CachedNode?.Set(Node3D.PropertyName.Rotation, value);
            }
        }

        public Vector2 Scale2D
        {
            get
            {
                return CachedNode != null ? (Vector2)CachedNode.Get(Node2D.PropertyName.Scale) : Vector2.One;
            }
            set
            {
                CachedNode?.Set(Node2D.PropertyName.Scale, value);
            }
        }
        public Vector3 Scale3D
        {
            get
            {
                return CachedNode != null ? (Vector3)CachedNode.Get(Node3D.PropertyName.Scale) : Vector3.One;
            }
            set
            {
                CachedNode?.Set(Node3D.PropertyName.Scale, value);
            }
        }
        public Vector2 GlobalPosition2D
        {
            get
            {
                return CachedNode != null ? (Vector2)CachedNode.Get(Node2D.PropertyName.GlobalPosition) : Vector2.Zero;
            }
            set
            {
                CachedNode?.Set(Node2D.PropertyName.GlobalPosition, value);
            }
        }
        public Vector3 GlobalPosition3D
        {
            get
            {
                return CachedNode != null ? (Vector3)CachedNode.Get(Node3D.PropertyName.GlobalPosition) : Vector3.Zero;
            }
            set
            {
                CachedNode?.Set(Node3D.PropertyName.GlobalPosition, value);
            }
        }
        public bool FlipX
        {
            get
            {
                return CachedNode != null ? (bool)CachedNode.Get(Sprite2D.PropertyName.FlipH) : false;
            }
            set
            {
                CachedNode?.Set(Sprite2D.PropertyName.FlipH, value);
            }
        }
        public bool FlipY
        {
            get
            {
                return CachedNode != null ? (bool)CachedNode.Get(Sprite2D.PropertyName.FlipH) : false;
            }
            set
            {
                CachedNode?.Set(Sprite2D.PropertyName.FlipV, value);
            }
        }

        public bool Visible
        {
            get => m_Visible;
            set => InternalSetVisible(value);
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
            CachedNode?.Set(CanvasItem.PropertyName.Visible, visible);
        }


        internal void InternalSetOwner(Entity owner)
        {
            Entity = owner;
        }
    }
}
