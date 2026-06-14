//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Entity;
using GameFramework.ObjectPool;

namespace GodotGameFramework
{
    /// <summary>
    /// 实体实例对象。
    ///
    /// 继承自 ObjectBase，用于对象池管理实体实例。
    /// 当实体被隐藏时，实例对象会被归还到池中等待复用，
    /// 而不是直接销毁。当池满或实例过期时，才真正释放节点。
    ///
    /// 工作流程：
    /// 1. ShowEntity → 从池中 Spawn（如果有可用实例）
    /// 2. HideEntity → Unspawn 回池（不销毁）
    /// 3. 池容量/过期触发 Release → QueueFree 真正销毁
    ///
    /// 对应核心框架中 EntityManager.EntityInstanceObject（internal 类）。
    /// </summary>
    public sealed class EntityInstanceObject : ObjectBase
    {
        /// <summary>
        /// 实体资源（PackedScene）。
        /// 用于在池释放时传递给 EntityHelper.ReleaseEntity。
        /// </summary>
        private object m_EntityAsset;

        /// <summary>
        /// 实体辅助器。
        /// 用于在池释放时调用 ReleaseEntity。
        /// </summary>
        private IEntityHelper m_EntityHelper;

        /// <summary>
        /// 创建实体实例对象。
        /// </summary>
        /// <param name="name">实体资源名称（作为池中的键）。</param>
        /// <param name="entityAsset">实体资源（PackedScene）。</param>
        /// <param name="entityInstance">实体实例（Node）。</param>
        /// <param name="entityHelper">实体辅助器。</param>
        /// <returns>创建的实体实例对象。</returns>
        public static EntityInstanceObject Create(string name, object entityAsset,
            object entityInstance, IEntityHelper entityHelper)
        {
            if (entityAsset == null)
            {
                throw new GameFramework.GameFrameworkException("Entity asset is invalid.");
            }

            if (entityHelper == null)
            {
                throw new GameFramework.GameFrameworkException("Entity helper is invalid.");
            }

            EntityInstanceObject entityInstanceObject = ReferencePool.Acquire<EntityInstanceObject>();
            entityInstanceObject.Initialize(name, entityInstance);
            entityInstanceObject.m_EntityAsset = entityAsset;
            entityInstanceObject.m_EntityHelper = entityHelper;
            return entityInstanceObject;
        }

        /// <summary>
        /// 清理实体实例对象。
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            m_EntityAsset = null;
            m_EntityHelper = null;
        }

        /// <summary>
        /// 释放实体实例对象。
        /// 当对象池决定释放此对象时调用（池满或过期）。
        /// 调用 EntityHelper.ReleaseEntity 真正销毁节点。
        /// </summary>
        /// <param name="isShutdown">是否是关闭时释放。</param>
        protected internal override void Release(bool isShutdown)
        {
            m_EntityHelper.ReleaseEntity(m_EntityAsset, Target);
        }
    }
}
