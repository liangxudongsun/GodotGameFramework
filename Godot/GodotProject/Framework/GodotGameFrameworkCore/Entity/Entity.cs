//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Entity;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 实体。
    ///
    /// 这是 GGF 中实体的核心实现，继承自 Godot 的 Node 并实现 IEntity 接口。
    /// Entity 作为包装器节点存在，实际的 Node2D/Node3D 游戏节点作为其子节点。
    ///
    /// 架构设计（UGF 风格对象池）：
    /// <code>
    /// Entity (Node, 实现 IEntity)     ← 框架管理层，池化单位
    /// └── [从 PackedScene 实例化的节点] ← 实际游戏节点（CachedNode）
    ///     ├── Node2D（2D 游戏）
    ///     └── 或 Node3D（3D 游戏）
    /// </code>
    ///
    /// 对象池复用机制（对齐 UGF）：
    /// - Entity 节点不销毁，隐藏时通过 SetEntityActive(false) 隐藏视觉
    /// - EntityLogic 不重新创建，复用时跳过 OnInit，直接调 OnShow
    /// - CachedNode 保持为 Entity 的子节点，不 RemoveChild
    /// - OnRecycle 重置 Entity 状态但保留 EntityLogic 引用
    /// - 仅当对象池释放（池满/过期）时才真正 QueueFree
    ///
    /// 生命周期：
    /// - 首次创建：OnInit(isNew=true) → OnShow → OnUpdate → OnHide → OnRecycle
    /// - 池复用：OnInit(isNew=false, 跳过EntityLogic.OnInit) → OnShow → OnUpdate → OnHide → OnRecycle
    /// </summary>
    public sealed partial class Entity : Node, IEntity
    {
        /// <summary>
        /// 关联的实体逻辑实例。
        /// 池复用时保留此引用，不重新创建。
        /// </summary>
        private EntityLogic m_EntityLogic;

        /// <summary>
        /// 获取实体编号。
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 获取实体资源名称（PackedScene 路径）。
        /// </summary>
        public string EntityAssetName { get; private set; }

        /// <summary>
        /// 获取实体实例。
        /// 返回实际的子节点（Node2D 或 Node3D），而非 Entity 自身。
        /// 如果没有子节点，返回 Entity 自身。
        /// </summary>
        public object Handle
        {
            get
            {
                if (GetChildCount() > 0)
                {
                    return GetChild(0);
                }

                return this;
            }
        }

        /// <summary>
        /// 获取实体所属的实体组。
        /// </summary>
        public IEntityGroup EntityGroup { get; private set; }

        /// <summary>
        /// 获取实体的逻辑实例。
        /// 用于外部代码获取 EntityLogic（如 ShowEntityAwait 模式）。
        /// </summary>
        public EntityLogic Logic => m_EntityLogic;

        // ================================================================
        //  IEntity 生命周期方法
        // ================================================================

        /// <summary>
        /// 实体初始化。
        ///
        /// UGF 风格生命周期：
        /// - isNewInstance=true（首次创建）：设置字段，调用 EntityLogic.OnInit
        /// - isNewInstance=false（池复用）：设置字段，跳过 EntityLogic.OnInit
        /// </summary>
        public void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            Id = entityId;
            EntityAssetName = entityAssetName;
            Name = GameFramework.Utility.Text.Format("Entity_{0}_{1}", entityId, entityAssetName);

            if (isNewInstance)
            {
                // 首次创建：设置 EntityGroup，调用 EntityLogic.OnInit
                EntityGroup = entityGroup;
                try
                {
                    m_EntityLogic?.OnInit(userData);
                }
                catch (Exception exception)
                {
                    Log.Warning("Entity '{0}' OnInit with exception '{1}'.", entityId, exception);
                }
            }
            else
            {
                // 池复用：EntityGroup 应一致，不调用 EntityLogic.OnInit
                if (EntityGroup != entityGroup)
                {
                    GameFramework.GameFrameworkLog.Warning(
                        GameFramework.Utility.Text.Format("Entity group is inconsistent for reused entity '{0}'.", entityId));
                    EntityGroup = entityGroup;
                }
            }
        }

        /// <summary>
        /// 实体回收。
        ///
        /// UGF 风格：保留 EntityLogic 引用（不设为 null），
        /// 重置实体标识字段，隐藏视觉。
        /// Entity 节点不销毁，等待对象池复用或池释放。
        /// </summary>
        public void OnRecycle()
        {
            // 通知 EntityLogic 执行回收清理（重置状态）
            try
            {
                m_EntityLogic?.OnRecycle();
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnRecycle with exception '{1}'.", Id, exception);
            }

            // 重置标识字段（但保留 EntityLogic 和 CachedNode）
            Id = 0;
            EntityAssetName = null;
            // EntityGroup 保留（同一个池的实体始终属于同一个组）
            Name = "Entity (Recycled)";

            // 隐藏视觉
            SetEntityActive(false);
        }

        /// <summary>
        /// 实体显示。
        /// </summary>
        public void OnShow(object userData)
        {
            try
            {
                m_EntityLogic?.InternalShow(userData);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnShow with exception '{1}'.", Id, exception);
            }
        }

        /// <summary>
        /// 实体隐藏。
        /// </summary>
        public void OnHide(bool isShutdown, object userData)
        {
            try
            {
                m_EntityLogic?.InternalHide(isShutdown, userData);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnHide with exception '{1}'.", Id, exception);
            }
        }

        /// <summary>
        /// 实体附加子实体。
        /// </summary>
        public void OnAttached(IEntity childEntity, object userData)
        {
            try
            {
                m_EntityLogic?.OnAttached(childEntity, userData);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnAttached with exception '{1}'.", Id, exception);
            }
        }

        /// <summary>
        /// 实体解除子实体。
        /// </summary>
        public void OnDetached(IEntity childEntity, object userData)
        {
            try
            {
                m_EntityLogic?.OnDetached(childEntity, userData);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnDetached with exception '{1}'.", Id, exception);
            }
        }

        /// <summary>
        /// 实体被附加到父实体。
        /// </summary>
        public void OnAttachTo(IEntity parentEntity, object userData)
        {
            try
            {
                m_EntityLogic?.OnAttachTo(parentEntity, userData);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnAttachTo with exception '{1}'.", Id, exception);
            }
        }

        /// <summary>
        /// 实体从父实体解除。
        /// </summary>
        public void OnDetachFrom(IEntity parentEntity, object userData)
        {
            try
            {
                m_EntityLogic?.OnDetachFrom(parentEntity, userData);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnDetachFrom with exception '{1}'.", Id, exception);
            }
        }

        /// <summary>
        /// 实体轮询。
        /// 每帧调用，转发给 EntityLogic。
        /// </summary>
        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            try
            {
                m_EntityLogic?.OnUpdate(elapseSeconds, realElapseSeconds);
            }
            catch (Exception exception)
            {
                Log.Warning("Entity '{0}' OnUpdate with exception '{1}'.", Id, exception);
            }
        }

        // ================================================================
        //  可见性控制（UGF 风格的 SetActive 等价）
        // ================================================================

        /// <summary>
        /// 设置实体的活跃状态。
        ///
        /// UGF 中使用 GameObject.SetActive，Godot 中通过控制子节点可见性实现。
        /// Entity 本身是 Node（非 CanvasItem/Node3D），没有 Visible 属性，
        /// 所以控制 CachedNode 的 Visible。
        /// 支持 CanvasItem（2D）和 Node3D（3D）两种类型的子节点。
        /// </summary>
        /// <param name="active">是否活跃（可见）。</param>
        internal void SetEntityActive(bool active)
        {
            if (GetChildCount() <= 0)
            {
                return;
            }

            var child = GetChild(0);
            if (child is CanvasItem canvasItem)
            {
                canvasItem.Visible = active;
            }
            else if (child is Node3D node3D)
            {
                node3D.Visible = active;
            }
        }

        // ================================================================
        //  内部方法
        // ================================================================

        /// <summary>
        /// 内部方法：设置实体逻辑实例。
        /// 由 DefaultEntityHelper.CreateEntity 调用（仅首次创建时）。
        /// </summary>
        internal void SetEntityLogic(EntityLogic logic)
        {
            m_EntityLogic = logic;
            if (logic != null)
            {
                logic.InternalSetOwner(this);
            }
        }
    }
}
