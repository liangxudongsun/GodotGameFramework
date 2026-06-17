//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using Godot;

namespace GodotGameFramework.UI
{
    /// <summary>
    /// 界面项逻辑基类。
    /// </summary>
    public abstract class UIItemBase
    {
        /// <summary>所属的 UIItemInstanceObject。</summary>
        private UIItemInstanceObject m_Owner;

        /// <summary>
        /// 获取所属的 UIItemInstanceObject。
        /// </summary>
        public UIItemInstanceObject Owner
        {
            get { return m_Owner; }
        }

        /// <summary>
        /// 获取已缓存的节点。
        ///
        /// 返回 UIItemInstanceObject.Target（实际的 Godot 节点）。
        /// </summary>
        public Node CachedNode
        {
            get { return m_Owner?.Target as Node; }
        }

        /// <summary>
        /// 内部方法：设置所属的 UIItemInstanceObject。
        /// </summary>
        /// <param name="owner">所属的 UIItemInstanceObject。</param>
        internal void InternalSetOwner(UIItemInstanceObject owner)
        {
            m_Owner = owner;
        }

        /// <summary>
        /// 界面项初始化。
        ///
        /// 在 UIItem 首次被创建并注册到对象池时调用。
        /// 用户应在此方法中获取子节点引用。
        /// </summary>
        protected internal virtual void OnInit()
        {
        }

        /// <summary>
        /// 界面项回收。
        ///
        /// 在 UIItem 从对象池中被驱逐（真正销毁）前调用。
        /// 用于清理运行时状态。
        /// </summary>
        protected internal virtual void OnRecycle()
        {
        }
    }
}
