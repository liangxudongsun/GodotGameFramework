//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Entity;
using GameFramework.ObjectPool;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFramework.Entity
{
    /// <summary>
    /// 实体组件。
    ///
    /// 封装核心 IEntityManager，提供基于 Godot 引擎的实体管理功能。
    /// 包括实体的创建/显示/隐藏/回收、实体组管理、父子实体关系等功能。
    ///
    /// 架构说明：
    /// 与 UIComponent 使用 IUIManager 相同，EntityComponent 直接使用
    /// 核心 IEntityManager 管理实体，不重复实现内部数据结构。
    /// Godot 特有的场景树管理（Node 父子关系）在 Godot 层处理。
    ///
    /// 使用方式：
    /// <code>
    /// // 创建实体组
    /// GF.Entity.AddEntityGroup("Enemy", 60f, 16, 60f, 0);
    ///
    /// // 显示实体（指定 EntityLogic 类型）
    /// GF.Entity.ShowEntity&lt;EnemyLogic&gt;(1, "res://Scenes/Enemy.tscn", "Enemy");
    /// // 或异步
    /// IEntity entity = await GF.Entity.ShowEntityAsync&lt;EnemyLogic&gt;(1, "res://Scenes/Enemy.tscn", "Enemy");
    ///
    /// // 隐藏实体
    /// GF.Entity.HideEntity(1);
    ///
    /// // 父子挂载
    /// GF.Entity.AttachEntity(2, 1);  // 实体2 挂载到 实体1 下
    /// GF.Entity.DetachEntity(2);     // 解除挂载
    /// </code>
    ///
    /// </summary>
    public sealed partial class EntityComponent : GameFrameworkComponent
    {
        private const int DefaultPriority = 0;

        private IEntityManager m_EntityManager = null;
        private EventComponent m_EventComponent = null;
        private EntityHelperBase m_EntityHelper = null;

        [Export]
        private bool m_EnableShowEntitySuccessEvent = true;

        [Export]
        private bool m_EnableShowEntityFailureEvent = true;

        [Export]
        private bool m_EnableShowEntityUpdateEvent = false;

        [Export]
        private bool m_EnableShowEntityDependencyAssetEvent = false;

        [Export]
        private bool m_EnableHideEntityCompleteEvent = true;

        [Export]
        private float m_InstanceAutoReleaseInterval = 60f;

        [Export]
        private int m_InstanceCapacity = 16;

        [Export]
        private float m_InstanceExpireTime = 60f;

        [Export]
        private int m_InstancePriority = 0;

        [Export]
        private string m_EntityHelperTypeName = "GodotGameFramework.Entity.DefaultEntityHelper";
        [Export]
        private string m_EntityGroupHelperTypeName = "GodotGameFramework.Entity.DefaultEntityGroupHelper";

        /// <summary>
        /// 获取实体数量。
        /// </summary>
        public int EntityCount
        {
            get
            {
                return m_EntityManager.EntityCount;
            }
        }

        /// <summary>
        /// 获取实体组数量。
        /// </summary>
        public int EntityGroupCount
        {
            get
            {
                return m_EntityManager.EntityGroupCount;
            }
        }

        /// <summary>
        /// 游戏框架组件初始化。
        /// </summary>
        public override void OnInit()
        {
            base.OnInit();

            m_EntityManager = GameFrameworkEntry.GetModule<IEntityManager>();
            if (m_EntityManager == null)
            {
                Log.Fatal("Entity manager is invalid.");
                return;
            }

            if (m_EnableShowEntitySuccessEvent)
            {
                m_EntityManager.ShowEntitySuccess += OnShowEntitySuccess;
            }

            m_EntityManager.ShowEntityFailure += OnShowEntityFailure;

            if (m_EnableShowEntityUpdateEvent)
            {
                m_EntityManager.ShowEntityUpdate += OnShowEntityUpdate;
            }

            if (m_EnableShowEntityDependencyAssetEvent)
            {
                m_EntityManager.ShowEntityDependencyAsset += OnShowEntityDependencyAsset;
            }

            if (m_EnableHideEntityCompleteEvent)
            {
                m_EntityManager.HideEntityComplete += OnHideEntityComplete;
            }

            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            if (m_EventComponent == null)
            {
                Log.Fatal("Event component is invalid.");
                return;
            }

            m_EntityManager.SetResourceManager(GameFrameworkEntry.GetModule<IResourceManager>());
            m_EntityManager.SetObjectPoolManager(GameFrameworkEntry.GetModule<IObjectPoolManager>());
            EntityHelperBase entityHelper = Helper.CreateHelper(m_EntityHelperTypeName, m_EntityHelper);
            if (entityHelper == null)
            {
                Log.Fatal("Can not create entity helper with type '{0}'.", m_EntityHelperTypeName);
                return;
            }
            m_EntityHelper = entityHelper;
            m_EntityHelper.Name = m_EntityHelperTypeName;
            m_EntityManager.SetEntityHelper(m_EntityHelper);
            AddChild(m_EntityHelper);
        }

        /// <summary>
        /// 是否存在实体组。
        /// </summary>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasEntityGroup(string entityGroupName)
        {
            if (string.IsNullOrEmpty(entityGroupName))
            {
                throw new GameFrameworkException("Entity group name is invalid.");
            }
            return m_EntityManager.HasEntityGroup(entityGroupName);
        }

        /// <summary>
        /// 获取实体组。
        /// </summary>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <returns>实体组实例，不存在则返回 null。</returns>
        public IEntityGroup GetEntityGroup(string entityGroupName)
        {
            if (string.IsNullOrEmpty(entityGroupName))
            {
                throw new GameFrameworkException("Entity group name is invalid.");
            }
            return m_EntityManager.GetEntityGroup(entityGroupName);
        }

        /// <summary>
        /// 获取所有实体组。
        /// </summary>
        /// <returns>所有实体组数组。</returns>
        public IEntityGroup[] GetAllEntityGroups()
        {
            return m_EntityManager.GetAllEntityGroups();
        }

        /// <summary>
        /// 获取所有实体组。
        /// </summary>
        /// <param name="results">所有实体组。</param>
        public void GetAllEntityGroups(List<IEntityGroup> results)
        {
            m_EntityManager.GetAllEntityGroups(results);
        }

        /// <summary>
        /// 增加实体组。
        ///
        /// 创建 DefaultEntityGroupHelper(Node) 作为此组件的子节点，
        /// 用于在场景树中管理该组的所有实体节点。
        /// </summary>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="instanceAutoReleaseInterval">对象池自动释放间隔（秒）。</param>
        /// <param name="instanceCapacity">对象池容量。</param>
        /// <param name="instanceExpireTime">对象池过期时间（秒）。</param>
        /// <param name="instancePriority">对象池优先级。</param>
        /// <returns>是否添加成功。</returns>
        public bool AddEntityGroup(string entityGroupName, float instanceAutoReleaseInterval,
            int instanceCapacity, float instanceExpireTime, int instancePriority)
        {
            if (string.IsNullOrEmpty(entityGroupName))
            {
                throw new GameFrameworkException("Entity group name is invalid.");
            }

            if (m_EntityManager.HasEntityGroup(entityGroupName))
            {
                return false;
            }

            // 创建实体组容器节点并添加到场景树
            EntityGroupHelperBase entityGroup = Create(m_EntityGroupHelperTypeName) as EntityGroupHelperBase;
            if (entityGroup == null)
            {
                Log.Fatal("Can not create entity group helper with type '{0}'.", m_EntityGroupHelperTypeName);
                return false;
            }
            entityGroup.Name = Utility.Text.Format("{0}- {1}", m_EntityGroupHelperTypeName, entityGroupName);
            AddChild(entityGroup);
            // 委托给核心管理器新增实体组（含对象池）
            return m_EntityManager.AddEntityGroup(entityGroupName,
                instanceAutoReleaseInterval, instanceCapacity,
                instanceExpireTime, instancePriority, entityGroup);
        }

        // ================================================================
        //  显示实体
        // ================================================================

        /// <summary>
        /// 显示实体（带 EntityLogic 类型）。
        ///
        /// 使用泛型参数指定 EntityLogic 类型，这是推荐的使用方式。
        /// 内部将 EntityLogicType 通过 ShowEntityInfo 传递给 DefaultEntityHelper，
        /// 由其在 CreateEntity 中创建 EntityLogic 实例。
        /// </summary>
        /// <typeparam name="TLogic">实体逻辑类型。</typeparam>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径（如 "res://Scenes/Enemy.tscn"）。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void ShowEntity<TLogic>(int entityId, string entityAssetName,
            string entityGroupName, object userData = null) where TLogic : EntityLogic, new()
        {
            ShowEntityInfo showInfo = ShowEntityInfo.Create(typeof(TLogic), userData);
            // 传递给核心管理器，由 Entity.OnInit 解包 ShowEntityInfo
            m_EntityManager.ShowEntity(entityId, entityAssetName, entityGroupName, DefaultPriority, showInfo);
        }

        /// <summary>
        /// 显示实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="priority">加载实体资源的优先级。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void ShowEntity(int entityId, string entityAssetName,
            string entityGroupName, int priority, object userData = null)
        {
            m_EntityManager.ShowEntity(entityId, entityAssetName, entityGroupName, priority, userData);
        }

        /// <summary>
        /// 显示实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void ShowEntity(int entityId, string entityAssetName,
            string entityGroupName, object userData = null)
        {
            ShowEntity(entityId, entityAssetName, entityGroupName, DefaultPriority, userData);
        }

        // ================================================================
        //  异步显示实体（async/await 支持）
        // ================================================================

        /// <summary>
        /// 异步显示实体（带 EntityLogic 类型）。
        ///
        /// 返回 Task&lt;IEntity&gt;，支持 async/await 语法。
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示完成的实体。</returns>
        public async Task<IEntity> ShowEntityAsync<TLogic>(int entityId, string entityAssetName,
            string entityGroupName, object userData = null) where TLogic : EntityLogic, new()
        {
            ShowEntityInfo showInfo = ShowEntityInfo.Create(typeof(TLogic), userData);
            IEntity result = await ShowEntityAsyncInternal(entityId, entityAssetName,
                entityGroupName, DefaultPriority, showInfo);
            return result;
        }

        /// <summary>
        /// 异步显示实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="priority">加载实体资源的优先级。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示完成的实体。</returns>
        public async Task<IEntity> ShowEntityAsync(int entityId, string entityAssetName,
            string entityGroupName, int priority, object userData = null)
        {
            return await ShowEntityAsyncInternal(entityId, entityAssetName,
                entityGroupName, priority, userData);
        }

        /// <summary>
        /// 异步显示实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示完成的实体。</returns>
        public async Task<IEntity> ShowEntityAsync(int entityId, string entityAssetName,
            string entityGroupName, object userData = null)
        {
            return await ShowEntityAsyncInternal(entityId, entityAssetName,
                entityGroupName, DefaultPriority, userData);
        }

        /// <summary>
        /// 异步显示实体的内部实现。
        /// 使用 TaskCompletionSource 桥接 IEntityManager 的事件驱动管道。
        /// 所有异常通过 TCS 传递，不会同步抛出。
        /// </summary>
        private Task<IEntity> ShowEntityAsyncInternal(int entityId, string entityAssetName,
            string entityGroupName, int priority, object userData)
        {
            if (string.IsNullOrEmpty(entityAssetName))
            {
                return Task.FromException<IEntity>(
                    new GameFrameworkException("Entity asset name is invalid."));
            }

            if (string.IsNullOrEmpty(entityGroupName))
            {
                return Task.FromException<IEntity>(
                    new GameFrameworkException("Entity group name is invalid."));
            }

            var tcs = new TaskCompletionSource<IEntity>();

            EventHandler<ShowEntitySuccessEventArgs> onSuccess = null;
            EventHandler<ShowEntityFailureEventArgs> onFailure = null;

            onSuccess = (sender, e) =>
            {
                if (e.Entity != null && e.Entity.Id == entityId)
                {
                    m_EntityManager.ShowEntitySuccess -= onSuccess;
                    m_EntityManager.ShowEntityFailure -= onFailure;
                    tcs.TrySetResult(e.Entity);
                }
            };

            onFailure = (sender, e) =>
            {
                if (e.EntityId == entityId)
                {
                    m_EntityManager.ShowEntitySuccess -= onSuccess;
                    m_EntityManager.ShowEntityFailure -= onFailure;
                    tcs.TrySetException(new GameFrameworkException(
                        Utility.Text.Format("Show entity failure, asset name '{0}', error message '{1}'.",
                            entityAssetName, e.ErrorMessage)));
                }
            };

            m_EntityManager.ShowEntitySuccess += onSuccess;
            m_EntityManager.ShowEntityFailure += onFailure;

            try
            {
                m_EntityManager.ShowEntity(entityId, entityAssetName, entityGroupName, priority, userData);
            }
            catch (Exception ex)
            {
                m_EntityManager.ShowEntitySuccess -= onSuccess;
                m_EntityManager.ShowEntityFailure -= onFailure;
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }

        // ================================================================
        //  隐藏实体
        // ================================================================

        /// <summary>
        /// 隐藏实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void HideEntity(int entityId, object userData = null)
        {
            m_EntityManager.HideEntity(entityId, userData);
        }

        /// <summary>
        /// 隐藏实体。
        /// </summary>
        /// <param name="entity">要隐藏的实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void HideEntity(IEntity entity, object userData = null)
        {
            m_EntityManager.HideEntity(entity, userData);
        }

        /// <summary>
        /// 隐藏所有已加载的实体。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void HideAllLoadedEntities(object userData = null)
        {
            m_EntityManager.HideAllLoadedEntities(userData);
        }

        /// <summary>
        /// 隐藏所有正在加载的实体。
        /// </summary>
        public void HideAllLoadingEntities()
        {
            m_EntityManager.HideAllLoadingEntities();
        }

        // ================================================================
        //  实体查询
        // ================================================================

        /// <summary>
        /// 是否存在指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否存在。</returns>
        public bool HasEntity(int entityId)
        {
            return m_EntityManager.HasEntity(entityId);
        }

        /// <summary>
        /// 是否存在指定资源名称的实体。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasEntity(string entityAssetName)
        {
            return m_EntityManager.HasEntity(entityAssetName);
        }

        /// <summary>
        /// 获取实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体实例，不存在则返回 null。</returns>
        public IEntity GetEntity(int entityId)
        {
            return m_EntityManager.GetEntity(entityId);
        }

        /// <summary>
        /// 获取实体。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>第一个匹配的实体，不存在则返回 null。</returns>
        public IEntity GetEntity(string entityAssetName)
        {
            return m_EntityManager.GetEntity(entityAssetName);
        }

        /// <summary>
        /// 获取所有匹配资源名称的实体。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>匹配的实体数组。</returns>
        public IEntity[] GetEntities(string entityAssetName)
        {
            return m_EntityManager.GetEntities(entityAssetName);
        }

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <returns>所有实体数组。</returns>
        public IEntity[] GetAllLoadedEntities()
        {
            return m_EntityManager.GetAllLoadedEntities();
        }

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <param name="results">所有实体。</param>
        public void GetAllLoadedEntities(List<IEntity> results)
        {
            m_EntityManager.GetAllLoadedEntities(results);
        }

        /// <summary>
        /// 是否是合法的实体。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>实体是否合法。</returns>
        public bool IsValidEntity(IEntity entity)
        {
            return m_EntityManager.IsValidEntity(entity);
        }

        /// <summary>
        /// 是否正在加载实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否正在加载。</returns>
        public bool IsLoadingEntity(int entityId)
        {
            return m_EntityManager.IsLoadingEntity(entityId);
        }

        /// <summary>
        /// 获取所有正在加载实体的编号。
        /// </summary>
        /// <returns>所有正在加载实体的编号。</returns>
        public int[] GetAllLoadingEntityIds()
        {
            return m_EntityManager.GetAllLoadingEntityIds();
        }

        // ================================================================
        //  父子实体
        // ================================================================

        /// <summary>
        /// 附加子实体到父实体。
        /// 委托核心 IEntityManager 建立逻辑父子关系，
        /// 同时处理 Godot 场景树中的 Node 父子关系。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void AttachEntity(int childEntityId, int parentEntityId, object userData = null)
        {
            // 验证和初步处理
            DetachEntity(childEntityId, userData);

            // 委托核心管理器建立逻辑父子关系
            m_EntityManager.AttachEntity(childEntityId, parentEntityId, userData);

            // Godot 特有的场景树父子关系处理
            IEntity childEntity = m_EntityManager.GetEntity(childEntityId);
            IEntity parentEntity = m_EntityManager.GetEntity(parentEntityId);
            if (childEntity is Node childNode && parentEntity is Node parentNode)
            {
                Node originalParent = childNode.GetParent();
                if (originalParent != null && originalParent != parentNode)
                {
                    originalParent.RemoveChild(childNode);
                }
                if (childNode.GetParent() != parentNode)
                {
                    parentNode.AddChild(childNode);
                }
            }
        }

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        public void AttachEntity(int childEntityId, IEntity parentEntity, object userData = null)
        {
            if (parentEntity == null)
            {
                throw new GameFrameworkException("Parent entity is invalid.");
            }
            AttachEntity(childEntityId, parentEntity.Id, userData);
        }

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        public void AttachEntity(IEntity childEntity, int parentEntityId, object userData = null)
        {
            if (childEntity == null)
            {
                throw new GameFrameworkException("Child entity is invalid.");
            }
            AttachEntity(childEntity.Id, parentEntityId, userData);
        }

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        public void AttachEntity(IEntity childEntity, IEntity parentEntity, object userData = null)
        {
            if (childEntity == null)
            {
                throw new GameFrameworkException("Child entity is invalid.");
            }
            if (parentEntity == null)
            {
                throw new GameFrameworkException("Parent entity is invalid.");
            }
            AttachEntity(childEntity.Id, parentEntity.Id, userData);
        }

        /// <summary>
        /// 解除子实体的父子关系。
        /// 委托核心 IEntityManager 解除逻辑父子关系，
        /// 同时将子实体的 Node 移回所属实体组的容器节点下。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachEntity(int childEntityId, object userData = null)
        {
            // 获取子实体引用（在委托前记录，因为委托后父子关系已解除）
            IEntity childEntity = m_EntityManager.GetEntity(childEntityId);

            // 委托核心管理器解除逻辑父子关系
            m_EntityManager.DetachEntity(childEntityId, userData);

            // Godot 特有的场景树处理：将子 Node 移回组容器
            if (childEntity is Node childNode && childEntity != null)
            {
                IEntityGroup entityGroup = childEntity.EntityGroup;
                if (entityGroup != null && entityGroup.Helper is DefaultEntityGroupHelper groupHelper)
                {
                    Node currentParent = childNode.GetParent();
                    if (currentParent != null && currentParent != groupHelper)
                    {
                        currentParent.RemoveChild(childNode);
                        groupHelper.AddChild(childNode);
                    }
                }
            }
        }

        /// <summary>
        /// 解除子实体的父子关系。
        /// </summary>
        public void DetachEntity(IEntity childEntity, object userData = null)
        {
            if (childEntity == null)
            {
                throw new GameFrameworkException("Child entity is invalid.");
            }
            DetachEntity(childEntity.Id, userData);
        }

        /// <summary>
        /// 解除父实体的所有子实体。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachChildEntities(int parentEntityId, object userData = null)
        {
            m_EntityManager.DetachChildEntities(parentEntityId, userData);
        }

        /// <summary>
        /// 解除父实体的所有子实体。
        /// </summary>
        public void DetachChildEntities(IEntity parentEntity, object userData = null)
        {
            if (parentEntity == null)
            {
                throw new GameFrameworkException("Parent entity is invalid.");
            }
            m_EntityManager.DetachChildEntities(parentEntity, userData);
        }

        /// <summary>
        /// 获取父实体。
        /// </summary>
        public IEntity GetParentEntity(int childEntityId)
        {
            return m_EntityManager.GetParentEntity(childEntityId);
        }

        /// <summary>
        /// 获取父实体。
        /// </summary>
        public IEntity GetParentEntity(IEntity childEntity)
        {
            return m_EntityManager.GetParentEntity(childEntity);
        }

        /// <summary>
        /// 获取子实体数量。
        /// </summary>
        public int GetChildEntityCount(int parentEntityId)
        {
            return m_EntityManager.GetChildEntityCount(parentEntityId);
        }

        /// <summary>
        /// 获取第一个子实体。
        /// </summary>
        public IEntity GetChildEntity(int parentEntityId)
        {
            return m_EntityManager.GetChildEntity(parentEntityId);
        }

        /// <summary>
        /// 获取所有子实体。
        /// </summary>
        public IEntity[] GetChildEntities(int parentEntityId)
        {
            return m_EntityManager.GetChildEntities(parentEntityId);
        }

        /// <summary>
        /// 获取所有子实体。
        /// </summary>
        public IEntity[] GetChildEntities(IEntity parentEntity)
        {
            return m_EntityManager.GetChildEntities(parentEntity);
        }

        // ================================================================
        //  事件处理（转发核心事件到 EventComponent）
        // ================================================================

        private void OnShowEntitySuccess(object sender, ShowEntitySuccessEventArgs e)
        {
            m_EventComponent.Fire(this, e);
        }

        private void OnShowEntityFailure(object sender, ShowEntityFailureEventArgs e)
        {
            Log.Warning("Show entity failure, asset name '{0}', entity group name '{1}', error message '{2}'.",
                e.EntityAssetName, e.EntityGroupName, e.ErrorMessage);
            if (m_EnableShowEntityFailureEvent)
            {
                m_EventComponent.Fire(this, e);
            }
        }

        private void OnShowEntityUpdate(object sender, ShowEntityUpdateEventArgs e)
        {
            // ShowEntityUpdateEventArgs 继承自 GameFrameworkEventArgs（而非 GameEventArgs），
            // 无法通过 EventComponent.Fire 转发。如需订阅，请直接监听 IEntityManager.ShowEntityUpdate。
        }

        private void OnShowEntityDependencyAsset(object sender, ShowEntityDependencyAssetEventArgs e)
        {
            // ShowEntityDependencyAssetEventArgs 继承自 GameFrameworkEventArgs（而非 GameEventArgs），
            // 无法通过 EventComponent.Fire 转发。如需订阅，请直接监听 IEntityManager.ShowEntityDependencyAsset。
        }

        private void OnHideEntityComplete(object sender, HideEntityCompleteEventArgs e)
        {
            m_EventComponent.Fire(this, e);
        }
    }
}
