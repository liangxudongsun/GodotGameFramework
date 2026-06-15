//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Entity;
using Godot;
using System;

namespace GodotGameFramework.Entity
{
    /// <summary>
    /// 实体。
    /// 生命周期：
    /// - 首次创建：OnInit(isNew=true) → OnShow → OnUpdate → OnHide → OnRecycle
    /// - 池复用：OnInit(isNew=false, 跳过EntityLogic.OnInit) → OnShow → OnUpdate → OnHide → OnRecycle
    /// </summary>
    public sealed partial class Entity : GodotComponent, IEntity
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


        /// <summary>
        /// 实体初始化。
        /// 如果 userData 是 ShowEntityInfo，会自动解包取出内部 UserData
        /// 再传递给 EntityLogic.OnInit
        /// </summary>
        public void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            Id = entityId;
            EntityAssetName = entityAssetName;
            Name = GameFramework.Utility.Text.Format("Entity_{0}_{1}", entityId, entityAssetName);

            // 解包 ShowEntityInfo，提取内部 UserData
            object actualUserData = userData;
            if (userData is ShowEntityInfo showInfo)
            {
                actualUserData = showInfo.UserData;
            }

            if (isNewInstance)
            {
                // 首次创建：设置 EntityGroup，调用 EntityLogic.OnInit
                EntityGroup = entityGroup;
                try
                {
                    m_EntityLogic?.OnInit(actualUserData);
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
        /// 保留 EntityLogic 引用（不设为 null），
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

        /// <summary>
        /// 设置实体的活跃状态。
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
