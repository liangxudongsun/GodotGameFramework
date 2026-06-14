//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.ObjectPool;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 界面项实例对象。
    ///
    /// 继承自 ObjectBase，用于对象池管理 UIItem 实例。
    /// UIItem 是 UIForm 内部的子元素（如列表项、选项卡等），
    /// 使用对象池实现复用，避免频繁的实例化和销毁。
    ///
    /// 工作流程：
    /// 1. SpawnItem → 从池中 Spawn（如果有可用实例）
    /// 2. UnspawnItem → Unspawn 回池（不销毁，仅隐藏）
    /// 3. 池容量/过期触发 Release → QueueFree 真正销毁
    ///
    /// 对标 UGF 测试项目中的 UIItemObject。
    /// 与 EntityInstanceObject / UIFormInstanceObject 模式一致。
    /// </summary>
    public sealed class UIItemInstanceObject : ObjectBase
    {
        /// <summary>界面项逻辑实例。</summary>
        private UIItemBase m_ItemLogic;

        /// <summary>
        /// 获取界面项逻辑实例。
        /// </summary>
        public UIItemBase ItemLogic => m_ItemLogic;

        /// <summary>
        /// 内部方法：设置界面项逻辑实例。
        /// </summary>
        internal void InternalSetItemLogic(UIItemBase itemLogic)
        {
            m_ItemLogic = itemLogic;
        }

        /// <summary>
        /// 创建界面项实例对象。
        /// UGF 风格：使用 ReferencePool 获取实例，避免 GC。
        /// </summary>
        /// <param name="name">界面项资源名称（作为池中的键）。</param>
        /// <param name="itemInstance">界面项实例（Node）。</param>
        /// <returns>创建的界面项实例对象。</returns>
        public static UIItemInstanceObject Create(string name, Node itemInstance)
        {
            if (itemInstance == null)
            {
                throw new GameFramework.GameFrameworkException("UI item instance is invalid.");
            }

            UIItemInstanceObject itemInstanceObject = ReferencePool.Acquire<UIItemInstanceObject>();
            itemInstanceObject.Initialize(name, itemInstance);
            return itemInstanceObject;
        }

        /// <summary>
        /// 清理界面项实例对象。
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            m_ItemLogic = null;
        }

        /// <summary>
        /// 释放界面项实例对象。
        /// 当对象池决定释放此对象时调用（池满或过期）。
        /// 调用 UIItemBase.OnRecycle()，然后 QueueFree() 销毁节点。
        /// </summary>
        /// <param name="isShutdown">是否是关闭时释放。</param>
        protected internal override void Release(bool isShutdown)
        {
            if (m_ItemLogic != null)
            {
                m_ItemLogic.OnRecycle();
            }

            Node node = Target as Node;
            if (node != null)
            {
                node.QueueFree();
            }
        }

        /// <summary>
        /// 从对象池中取出时的回调。
        /// 重置位置并显示节点。
        /// </summary>
        protected internal override void OnSpawn()
        {
            base.OnSpawn();

            Node node = Target as Node;
            if (node != null)
            {
                // 重置位置到父节点的原点
                if (node is Node2D node2D)
                {
                    node2D.Position = Vector2.Zero;
                }
                // 显示节点
                if (node is CanvasItem canvasItem)
                {
                    canvasItem.Visible = true;
                }
            }
        }

        /// <summary>
        /// 归还到对象池时的回调。
        /// 隐藏节点但不销毁。
        /// </summary>
        protected internal override void OnUnspawn()
        {
            base.OnUnspawn();

            Node node = Target as Node;
            if (node != null)
            {
                if (node is CanvasItem canvasItem)
                {
                    canvasItem.Visible = false;
                }
            }
        }
    }
}
