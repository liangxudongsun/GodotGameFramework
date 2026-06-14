//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Entity;
using GameFramework.ObjectPool;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFramework
{
    /// <summary>
    /// 实体组件。
    ///
    /// 这是实体管理系统的封装组件，提供通过框架动态创建/销毁实体的能力。
    /// 支持实体组管理、父子挂载、2D/3D 兼容。
    ///
    /// 架构说明：
    /// GGF 的 EntityComponent 采用与 ResourceComponent 相同的策略 — 绕过核心
    /// EntityManager 的 ShowEntity 管道（因为核心 EntityManager.ShowEntity 内部
    /// 调用 ResourceManager.LoadAsset，需要版本列表），直接使用 ResourceComponent
    /// 加载 PackedScene，自行管理实体生命周期。
    ///
    /// 核心 EntityManager 仍注册为模块（框架一致性），但不使用其 ShowEntity/HideEntity。
    /// EntityComponent 在内部实现了完整的实体管理功能，包括：
    /// - 实体追踪（ID → EntityInfo）
    /// - 实体组管理（Name → EntityGroup，使用 Node 容器）
    /// - 实体生命周期（OnInit/OnShow/OnHide/OnUpdate/OnRecycle）
    /// - 父子实体关系（Attach/Detach）
    /// - 事件（ShowEntitySuccess/Failure/HideEntityComplete）
    ///
    /// 使用方式：
    /// <code>
    /// EntityComponent entityComp = GF.Entity;
    ///
    /// // 创建实体组
    /// entityComp.AddEntityGroup("Enemy", 60f, 16, 60f, 0);
    ///
    /// // 显示实体（指定 EntityLogic 类型）
    /// entityComp.ShowEntity&lt;EnemyLogic&gt;(1, "res://Scenes/Enemy.tscn", "Enemy");
    ///
    /// // 显示实体（不指定 EntityLogic）
    /// entityComp.ShowEntity(2, "res://Scenes/Prop.tscn", "Enemy");
    ///
    /// // 隐藏实体
    /// entityComp.HideEntity(1);
    ///
    /// // 父子挂载
    /// entityComp.AttachEntity(2, 1);  // 实体2 挂载到 实体1 下
    /// entityComp.DetachEntity(2);     // 解除挂载
    /// </code>
    ///
    /// 对应 Unity 版本中的 EntityComponent。
    /// </summary>
    public sealed partial class EntityComponent : GameFrameworkComponent
    {
        // ================================================================
        //  内部类型
        // ================================================================

        /// <summary>
        /// 实体状态枚举（与核心框架保持一致）。
        /// </summary>
        private enum EntityStatus : byte
        {
            Unknown = 0,
            WillInit,
            Inited,
            WillShow,
            Showed,
            WillHide,
            Hidden,
            WillRecycle,
            Recycled
        }

        /// <summary>
        /// 实体信息（内部类，追踪单个实体的状态和关系）。
        /// </summary>
        private sealed class EntityInfo : IReference
        {
            private IEntity m_Entity;
            private EntityStatus m_Status;
            private IEntity m_ParentEntity;
            private List<IEntity> m_ChildEntities;

            public EntityInfo()
            {
                m_Entity = null;
                m_Status = EntityStatus.Unknown;
                m_ParentEntity = null;
                m_ChildEntities = new List<IEntity>();
            }

            public IEntity Entity => m_Entity;
            public EntityStatus Status
            {
                get => m_Status;
                set => m_Status = value;
            }
            public IEntity ParentEntity
            {
                get => m_ParentEntity;
                set => m_ParentEntity = value;
            }
            public int ChildEntityCount => m_ChildEntities.Count;

            public static EntityInfo Create(IEntity entity)
            {
                EntityInfo entityInfo = ReferencePool.Acquire<EntityInfo>();
                entityInfo.m_Entity = entity;
                entityInfo.m_Status = EntityStatus.WillInit;
                return entityInfo;
            }

            public void Clear()
            {
                m_Entity = null;
                m_Status = EntityStatus.Unknown;
                m_ParentEntity = null;
                m_ChildEntities.Clear();
            }

            public IEntity GetChildEntity()
            {
                return m_ChildEntities.Count > 0 ? m_ChildEntities[0] : null;
            }

            public IEntity[] GetChildEntities()
            {
                return m_ChildEntities.ToArray();
            }

            public void GetChildEntities(List<IEntity> results)
            {
                if (results == null) return;
                results.Clear();
                foreach (IEntity child in m_ChildEntities)
                {
                    results.Add(child);
                }
            }

            public void AddChildEntity(IEntity childEntity)
            {
                if (m_ChildEntities.Contains(childEntity))
                {
                    throw new GameFrameworkException("Can not add child entity which is already exist.");
                }
                m_ChildEntities.Add(childEntity);
            }

            public void RemoveChildEntity(IEntity childEntity)
            {
                if (!m_ChildEntities.Remove(childEntity))
                {
                    throw new GameFrameworkException("Can not remove child entity which is not exist.");
                }
            }
        }

        /// <summary>
        /// 实体组（内部类，管理同一组内的实体）。
        ///
        /// 每个实体组维护一个对象池（IObjectPool&lt;EntityInstanceObject&gt;），
        /// 用于缓存和复用实体实例节点。当实体被隐藏时，实例节点被归还到池中；
        /// 当池满或实例过期时，才通过 EntityHelper.ReleaseEntity 真正销毁节点。
        /// </summary>
        private sealed class EntityGroup : IEntityGroup
        {
            private readonly string m_Name;
            private readonly IEntityGroupHelper m_Helper;
            private readonly IObjectPool<EntityInstanceObject> m_InstancePool;
            private readonly GameFrameworkLinkedList<IEntity> m_Entities;
            private LinkedListNode<IEntity> m_CachedNode;

            public EntityGroup(string name, float instanceAutoReleaseInterval,
                int instanceCapacity, float instanceExpireTime, int instancePriority,
                IEntityGroupHelper helper, IObjectPoolManager objectPoolManager)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new GameFrameworkException("Entity group name is invalid.");
                }

                if (helper == null)
                {
                    throw new GameFrameworkException("Entity group helper is invalid.");
                }

                m_Name = name;
                m_Helper = helper;
                m_InstancePool = objectPoolManager.CreateSingleSpawnObjectPool<EntityInstanceObject>(
                    Utility.Text.Format("Entity Instance Pool ({0})", name),
                    instanceCapacity, instanceExpireTime, instancePriority);
                m_InstancePool.AutoReleaseInterval = instanceAutoReleaseInterval;
                m_Entities = new GameFrameworkLinkedList<IEntity>();
                m_CachedNode = null;
            }

            public string Name => m_Name;
            public int EntityCount => m_Entities.Count;
            public IEntityGroupHelper Helper => m_Helper;

            public float InstanceAutoReleaseInterval
            {
                get => m_InstancePool.AutoReleaseInterval;
                set => m_InstancePool.AutoReleaseInterval = value;
            }

            public int InstanceCapacity
            {
                get => m_InstancePool.Capacity;
                set => m_InstancePool.Capacity = value;
            }

            public float InstanceExpireTime
            {
                get => m_InstancePool.ExpireTime;
                set => m_InstancePool.ExpireTime = value;
            }

            public int InstancePriority
            {
                get => m_InstancePool.Priority;
                set => m_InstancePool.Priority = value;
            }

            public void AddEntity(IEntity entity)
            {
                m_Entities.AddLast(entity);
            }

            public void RemoveEntity(IEntity entity)
            {
                if (m_CachedNode != null && m_CachedNode.Value == entity)
                {
                    m_CachedNode = m_CachedNode.Next;
                }
                m_Entities.Remove(entity);
            }

            /// <summary>
            /// 注册实体实例对象到对象池。
            /// </summary>
            public void RegisterEntityInstanceObject(EntityInstanceObject obj, bool spawned)
            {
                m_InstancePool.Register(obj, spawned);
            }

            /// <summary>
            /// 从对象池中获取指定资源名称的实体实例。
            /// 如果池中没有可用实例，返回 null。
            /// </summary>
            public EntityInstanceObject SpawnEntityInstanceObject(string entityAssetName)
            {
                return m_InstancePool.Spawn(entityAssetName);
            }

            /// <summary>
            /// 将实体归还到对象池。
            /// Entity 节点不销毁，只是从活跃列表移除，等待复用或池自动释放。
            /// </summary>
            public void UnspawnEntity(IEntity entity)
            {
                m_InstancePool.Unspawn(entity);
            }

            public bool HasEntity(int entityId)
            {
                foreach (IEntity entity in m_Entities)
                {
                    if (entity.Id == entityId) return true;
                }
                return false;
            }

            public bool HasEntity(string entityAssetName)
            {
                foreach (IEntity entity in m_Entities)
                {
                    if (entity.EntityAssetName == entityAssetName) return true;
                }
                return false;
            }

            public IEntity GetEntity(int entityId)
            {
                foreach (IEntity entity in m_Entities)
                {
                    if (entity.Id == entityId) return entity;
                }
                return null;
            }

            public IEntity GetEntity(string entityAssetName)
            {
                foreach (IEntity entity in m_Entities)
                {
                    if (entity.EntityAssetName == entityAssetName) return entity;
                }
                return null;
            }

            public IEntity[] GetEntities(string entityAssetName)
            {
                List<IEntity> results = new List<IEntity>();
                foreach (IEntity entity in m_Entities)
                {
                    if (entity.EntityAssetName == entityAssetName) results.Add(entity);
                }
                return results.ToArray();
            }

            public void GetEntities(string entityAssetName, List<IEntity> results)
            {
                if (results == null) return;
                results.Clear();
                foreach (IEntity entity in m_Entities)
                {
                    if (entity.EntityAssetName == entityAssetName) results.Add(entity);
                }
            }

            public IEntity[] GetAllEntities()
            {
                List<IEntity> results = new List<IEntity>();
                foreach (IEntity entity in m_Entities)
                {
                    results.Add(entity);
                }
                return results.ToArray();
            }

            public void GetAllEntities(List<IEntity> results)
            {
                if (results == null) return;
                results.Clear();
                foreach (IEntity entity in m_Entities)
                {
                    results.Add(entity);
                }
            }

            /// <summary>
            /// 实体组轮询。遍历所有实体并调用 OnUpdate。
            /// </summary>
            public void Update(float elapseSeconds, float realElapseSeconds)
            {
                LinkedListNode<IEntity> current = m_Entities.First;
                while (current != null)
                {
                    m_CachedNode = current.Next;
                    current.Value.OnUpdate(elapseSeconds, realElapseSeconds);
                    current = m_CachedNode;
                    m_CachedNode = null;
                }
            }

            public void SetEntityInstanceLocked(object entityInstance, bool locked)
            {
                if (entityInstance == null) return;
                m_InstancePool.SetLocked(entityInstance, locked);
            }

            public void SetEntityInstancePriority(object entityInstance, int priority)
            {
                if (entityInstance == null) return;
                m_InstancePool.SetPriority(entityInstance, priority);
            }
        }

        // ================================================================
        //  字段
        // ================================================================

        /// <summary>
        /// 实体信息字典（实体ID → EntityInfo）。
        /// </summary>
        private readonly Dictionary<int, EntityInfo> m_EntityInfos = new Dictionary<int, EntityInfo>();

        /// <summary>
        /// 实体组字典（组名 → EntityGroup）。
        /// </summary>
        private readonly Dictionary<string, EntityGroup> m_EntityGroups =
            new Dictionary<string, EntityGroup>(StringComparer.Ordinal);

        /// <summary>
        /// 实体辅助器。
        /// </summary>
        private IEntityHelper m_EntityHelper;

        /// <summary>
        /// 框架是否已关闭。
        /// </summary>
        private bool m_IsShutdown;

        /// <summary>
        /// 事件组件引用。
        /// </summary>
        private EventComponent m_EventComponent;

        /// <summary>
        /// 正在加载的实体集合。
        /// </summary>
        private readonly Dictionary<int, int> m_EntitiesBeingLoaded = new Dictionary<int, int>();

        /// <summary>
        /// 加载完成后需要立即释放的实体序列号集合。
        /// </summary>
        private readonly HashSet<int> m_EntitiesToReleaseOnLoad = new HashSet<int>();

        /// <summary>
        /// 加载序列号自增计数器。
        /// </summary>
        private int m_Serial;

        /// <summary>
        /// 真实经过时间，用于计算异步加载耗时。
        /// </summary>
        private float m_RealElapseSeconds;

        // ================================================================
        //  公开属性
        // ================================================================

        /// <summary>
        /// 获取当前已加载的实体数量。
        /// </summary>
        public int EntityCount => m_EntityInfos.Count;

        /// <summary>
        /// 获取实体组数量。
        /// </summary>
        public int EntityGroupCount => m_EntityGroups.Count;

        // ================================================================
        //  Godot 生命周期
        // ================================================================

        /// <summary>
        /// 节点初始化回调。
        /// 设置实体辅助器，启用帧更新。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_EntityHelper = new DefaultEntityHelper();
            m_IsShutdown = false;
            m_Serial = 0;
            m_RealElapseSeconds = 0f;

            // 注册核心 EntityManager（框架一致性，但不使用其 ShowEntity）
            IEntityManager entityManager = GameFrameworkEntry.GetModule<IEntityManager>();
            if (entityManager != null)
            {
                entityManager.SetEntityHelper(m_EntityHelper);
            }

            // 启用 _Process 轮询（用于实体更新）
            ProcessMode = ProcessModeEnum.Always;
        }

        /// <summary>
        /// 每帧更新回调。
        /// 遍历所有实体组，驱动实体的 OnUpdate。
        /// </summary>
        /// <param name="delta">帧间隔时间（秒）。</param>
        public override void _Process(double delta)
        {
            base._Process(delta);

            if (m_IsShutdown) return;

            float elapseSeconds = (float)delta;
            // 计算真实经过时间，与 GGFEntry._Process 保持一致
            float realElapseSeconds = (float)Engine.TimeScale > 0f
                ? elapseSeconds / (float)Engine.TimeScale
                : 0f;
            m_RealElapseSeconds = realElapseSeconds;

            foreach (KeyValuePair<string, EntityGroup> pair in m_EntityGroups)
            {
                pair.Value.Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 节点离开场景树回调。
        /// 设置关闭标志，隐藏并回收所有活跃实体。
        /// </summary>
        public override void _ExitTree()
        {
            m_IsShutdown = true;
            HideAllLoadedEntities();
            base._ExitTree();
        }

        // ================================================================
        //  实体组管理
        // ================================================================

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
            return m_EntityGroups.ContainsKey(entityGroupName);
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
            m_EntityGroups.TryGetValue(entityGroupName, out EntityGroup group);
            return group;
        }

        /// <summary>
        /// 获取所有实体组。
        /// </summary>
        /// <returns>所有实体组数组。</returns>
        public IEntityGroup[] GetAllEntityGroups()
        {
            int index = 0;
            IEntityGroup[] results = new IEntityGroup[m_EntityGroups.Count];
            foreach (KeyValuePair<string, EntityGroup> pair in m_EntityGroups)
            {
                results[index++] = pair.Value;
            }
            return results;
        }

        /// <summary>
        /// 增加实体组。
        ///
        /// 创建一个 DefaultEntityGroupHelper(Node) 作为此组件的子节点，
        /// 用于在场景树中管理该组的所有实体节点。
        /// 同时创建对象池用于缓存和复用实体实例。
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

            if (HasEntityGroup(entityGroupName))
            {
                return false;
            }

            // 获取对象池管理器
            IObjectPoolManager objectPoolManager = GameFrameworkEntry.GetModule<IObjectPoolManager>();
            if (objectPoolManager == null)
            {
                throw new GameFrameworkException("Object pool manager is invalid.");
            }

            // 创建实体组容器节点
            var groupHelper = new DefaultEntityGroupHelper();
            groupHelper.Name = entityGroupName;
            AddChild(groupHelper);

            // 创建内部实体组（含对象池）
            var entityGroup = new EntityGroup(entityGroupName,
                instanceAutoReleaseInterval, instanceCapacity,
                instanceExpireTime, instancePriority,
                groupHelper, objectPoolManager);

            m_EntityGroups.Add(entityGroupName, entityGroup);
            return true;
        }

        // ================================================================
        //  显示实体
        // ================================================================

        /// <summary>
        /// 显示实体（带 EntityLogic 类型）。
        ///
        /// 使用泛型参数指定 EntityLogic 类型，这是推荐的使用方式。
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
            ShowEntity(entityId, entityAssetName, entityGroupName, showInfo);
        }

        /// <summary>
        /// 显示实体（不带 EntityLogic）。
        ///
        /// 工作流程：
        /// 1. 先尝试从实体组的对象池中获取可复用的实例
        /// 2. 如果池中有可用实例，直接复用（isNewInstance=false）
        /// 3. 如果池中没有，则通过 ResourceComponent 加载 PackedScene 并实例化
        /// 4. 新实例注册到对象池中，供后续复用
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据（可以是 ShowEntityInfo）。</param>
        public void ShowEntity(int entityId, string entityAssetName,
            string entityGroupName, object userData = null)
        {
            if (string.IsNullOrEmpty(entityAssetName))
            {
                throw new GameFrameworkException("Entity asset name is invalid.");
            }

            if (string.IsNullOrEmpty(entityGroupName))
            {
                throw new GameFrameworkException("Entity group name is invalid.");
            }

            if (HasEntity(entityId))
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Entity id '{0}' is already exist.", entityId));
            }

            if (m_EntitiesBeingLoaded.ContainsKey(entityId))
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Entity id '{0}' is already being loaded.", entityId));
            }

            EntityGroup entityGroup = (EntityGroup)GetEntityGroup(entityGroupName);
            if (entityGroup == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Entity group '{0}' is not exist.", entityGroupName));
            }

            // 提取实际传递给 OnInit/OnShow 的 userData
            object actualUserData = userData;
            ShowEntityInfo showInfo = userData as ShowEntityInfo;
            if (showInfo != null)
            {
                actualUserData = showInfo.UserData;
                showInfo.EntityId = entityId;
                showInfo.EntityGroupName = entityGroupName;
            }
            else
            {
                showInfo = ShowEntityInfo.Create(null, userData);
                showInfo.EntityId = entityId;
                showInfo.EntityGroupName = entityGroupName;
            }

            // 尝试从对象池中获取可复用的 Entity
            EntityInstanceObject entityInstanceObject = entityGroup.SpawnEntityInstanceObject(entityAssetName);
            if (entityInstanceObject != null)
            {
                // 池中有可用 Entity，直接复用
                IEntity entity = entityInstanceObject.Target as IEntity;
                if (entity == null)
                {
                    throw new GameFrameworkException("Pooled entity instance is not a valid IEntity.");
                }

                InternalShowEntity(entityId, entityAssetName, entityGroup,
                    entity, false, 0f, actualUserData);

                ReferencePool.Release(showInfo);
            }
            else
            {
                // 池中没有可用 Entity，需要异步加载
                int serialId = ++m_Serial;
                m_EntitiesBeingLoaded.Add(entityId, serialId);
                showInfo.SerialId = serialId;
                showInfo.StartTime = m_RealElapseSeconds;

                ResourceComponent resourceComp = GF.Resource;
                if (resourceComp == null)
                {
                    m_EntitiesBeingLoaded.Remove(entityId);
                    ReferencePool.Release(showInfo);
                    throw new GameFrameworkException("Resource component is invalid.");
                }

                resourceComp.LoadAssetAsync(
                    entityAssetName,
                    typeof(PackedScene),
                    asset => OnLoadAssetSuccess(entityId, entityAssetName, entityGroup, showInfo, asset),
                    errorMsg => OnLoadAssetFailure(entityId, entityAssetName, entityGroupName, showInfo, errorMsg)
                );
            }
        }

        /// <summary>
        /// 内部显示实体逻辑。
        ///
        /// UGF 风格：直接使用传入的 IEntity（不再调用 CreateEntity）。
        /// 池复用时 entity 已经是完整的 Entity（含 EntityLogic），
        /// 只需重新初始化标识字段和触发生命周期。
        /// </summary>
        private void InternalShowEntity(int entityId, string entityAssetName,
            EntityGroup entityGroup, IEntity entity, bool isNewInstance,
            float duration, object actualUserData)
        {
            try
            {
                // 注册到追踪字典
                EntityInfo entityInfo = EntityInfo.Create(entity);
                m_EntityInfos.Add(entityId, entityInfo);

                // 生命周期：Init
                entityInfo.Status = EntityStatus.WillInit;
                entity.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, actualUserData);
                entityInfo.Status = EntityStatus.Inited;

                // 加入实体组活跃列表
                entityGroup.AddEntity(entity);

                // 生命周期：Show
                // InternalShow 内部已通过 EntityLogic.InternalSetVisible(true) 控制可见性
                entityInfo.Status = EntityStatus.WillShow;
                entity.OnShow(actualUserData);
                entityInfo.Status = EntityStatus.Showed;

                // 触发 ShowEntitySuccess 事件（通过 EventComponent）
                EventComponent eventComponent = GetEventComponent();
                if (eventComponent != null)
                {
                    ShowEntitySuccessEventArgs args = ShowEntitySuccessEventArgs.Create(entity, duration, actualUserData);
                    eventComponent.Fire(this, args);
                    ReferencePool.Release(args);
                }
            }
            catch (Exception exception)
            {
                Log.Warning("InternalShowEntity for entity '{0}' with exception '{1}'.", entityId, exception);
            }
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
            // 如果实体正在加载中，取消加载
            if (m_EntitiesBeingLoaded.TryGetValue(entityId, out int serialId))
            {
                m_EntitiesToReleaseOnLoad.Add(serialId);
                m_EntitiesBeingLoaded.Remove(entityId);
                return;
            }

            EntityInfo entityInfo = GetEntityInfo(entityId);
            if (entityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find entity '{0}'.", entityId));
            }

            InternalHideEntity(entityInfo, userData);
        }

        /// <summary>
        /// 隐藏实体。
        /// </summary>
        /// <param name="entity">要隐藏的实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void HideEntity(IEntity entity, object userData = null)
        {
            if (entity == null)
            {
                throw new GameFrameworkException("Entity is invalid.");
            }
            HideEntity(entity.Id, userData);
        }

        /// <summary>
        /// 隐藏所有已加载的实体。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void HideAllLoadedEntities(object userData = null)
        {
            // 取消所有正在加载的实体（加载完成时会在 OnLoadAssetSuccess 中自动释放）
            foreach (int serialId in m_EntitiesBeingLoaded.Values)
            {
                m_EntitiesToReleaseOnLoad.Add(serialId);
            }
            m_EntitiesBeingLoaded.Clear();

            // 快照当前实体列表，避免迭代时修改字典
            EntityInfo[] entityInfos = new EntityInfo[m_EntityInfos.Count];
            m_EntityInfos.Values.CopyTo(entityInfos, 0);
            foreach (EntityInfo entityInfo in entityInfos)
            {
                if (m_EntityInfos.ContainsKey(entityInfo.Entity.Id))
                {
                    InternalHideEntity(entityInfo, userData);
                }
            }
        }

        /// <summary>
        /// 异步加载成功回调。
        /// </summary>
        private void OnLoadAssetSuccess(int entityId, string entityAssetName,
            EntityGroup entityGroup, ShowEntityInfo showInfo, object asset)
        {
            // 检查是否在加载过程中被取消
            if (m_EntitiesToReleaseOnLoad.Contains(showInfo.SerialId))
            {
                m_EntitiesToReleaseOnLoad.Remove(showInfo.SerialId);
                m_EntitiesBeingLoaded.Remove(entityId);

                // 释放已加载的资源
                GF.Resource?.UnloadAsset(asset);

                ReferencePool.Release(showInfo);
                return;
            }

            m_EntitiesBeingLoaded.Remove(entityId);

            try
            {
                // 计算加载耗时
                float duration = m_RealElapseSeconds - showInfo.StartTime;

                // 实例化场景
                object instance = m_EntityHelper.InstantiateEntity(asset);
                if (instance == null)
                {
                    throw new GameFrameworkException(Utility.Text.Format(
                        "Can not instantiate entity from asset '{0}'.", entityAssetName));
                }

                // 创建 Entity 包装器
                IEntity entity = m_EntityHelper.CreateEntity(instance, entityGroup, showInfo);
                if (entity == null)
                {
                    throw new GameFrameworkException("Can not create entity in entity helper.");
                }

                // 注册到对象池
                EntityInstanceObject entityInstanceObject = EntityInstanceObject.Create(
                    entityAssetName, asset, entity, m_EntityHelper);
                entityGroup.RegisterEntityInstanceObject(entityInstanceObject, true);

                // 显示实体
                InternalShowEntity(entityId, entityAssetName, entityGroup,
                    entity, true, duration, showInfo.UserData);
            }
            catch (Exception exception)
            {
                // 触发失败事件
                FireShowEntityFailure(entityId, entityAssetName, entityGroup.Name,
                    Utility.Text.Format("Show entity failure, asset name '{0}', error message '{1}'.",
                    entityAssetName, exception), showInfo.UserData);
            }
            finally
            {
                ReferencePool.Release(showInfo);
            }
        }

        /// <summary>
        /// 异步加载失败回调。
        /// </summary>
        private void OnLoadAssetFailure(int entityId, string entityAssetName,
            string entityGroupName, ShowEntityInfo showInfo, string errorMessage)
        {
            if (m_EntitiesToReleaseOnLoad.Contains(showInfo.SerialId))
            {
                m_EntitiesToReleaseOnLoad.Remove(showInfo.SerialId);
                m_EntitiesBeingLoaded.Remove(entityId);
                ReferencePool.Release(showInfo);
                return;
            }

            m_EntitiesBeingLoaded.Remove(entityId);

            FireShowEntityFailure(entityId, entityAssetName, entityGroupName,
                Utility.Text.Format("Show entity failure, asset name '{0}', error message '{1}'.",
                    entityAssetName, errorMessage), showInfo.UserData);

            ReferencePool.Release(showInfo);
        }

        // ================================================================
        //  异步显示实体（Phase 8: async/await 支持）
        // ================================================================

        /// <summary>
        /// 异步显示实体（带 EntityLogic 类型）。
        ///
        /// 与 ShowEntity 功能相同，但返回 Task&lt;IEntity&gt;，
        /// 支持 async/await 语法。当实体对象池命中时立即完成，
        /// 池未命中时等待异步资源加载完成后返回。
        ///
        /// 使用方式：
        /// <code>
        /// IEntity entity = await entityComponent.ShowEntityAsync&lt;EnemyLogic&gt;(
        ///     1, "res://Scenes/Enemy.tscn", "Enemy");
        /// </code>
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示完成的实体。</returns>
        public Task<IEntity> ShowEntityAsync<TLogic>(int entityId, string entityAssetName,
            string entityGroupName, object userData = null) where TLogic : EntityLogic, new()
        {
            ShowEntityInfo showInfo = ShowEntityInfo.Create(typeof(TLogic), userData);
            return ShowEntityAsync(entityId, entityAssetName, entityGroupName, showInfo);
        }

        /// <summary>
        /// 异步显示实体（不带 EntityLogic 类型）。
        ///
        /// 与 ShowEntity 功能相同，但返回 Task&lt;IEntity&gt;，
        /// 支持 async/await 语法。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源路径。</param>
        /// <param name="entityGroupName">实体组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示完成的实体。</returns>
        public async Task<IEntity> ShowEntityAsync(int entityId, string entityAssetName,
            string entityGroupName, object userData = null)
        {
            // 验证参数（与 ShowEntity 相同的验证逻辑）
            if (string.IsNullOrEmpty(entityAssetName))
            {
                throw new GameFrameworkException("Entity asset name is invalid.");
            }

            if (string.IsNullOrEmpty(entityGroupName))
            {
                throw new GameFrameworkException("Entity group name is invalid.");
            }

            if (HasEntity(entityId))
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Entity id '{0}' is already exist.", entityId));
            }

            if (m_EntitiesBeingLoaded.ContainsKey(entityId))
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Entity id '{0}' is already being loaded.", entityId));
            }

            EntityGroup entityGroup = (EntityGroup)GetEntityGroup(entityGroupName);
            if (entityGroup == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Entity group '{0}' is not exist.", entityGroupName));
            }

            // 提取实际传递给 OnInit/OnShow 的 userData
            object actualUserData = userData;
            ShowEntityInfo showInfo = userData as ShowEntityInfo;
            if (showInfo != null)
            {
                actualUserData = showInfo.UserData;
                showInfo.EntityId = entityId;
                showInfo.EntityGroupName = entityGroupName;
            }
            else
            {
                showInfo = ShowEntityInfo.Create(null, userData);
                showInfo.EntityId = entityId;
                showInfo.EntityGroupName = entityGroupName;
            }

            // 尝试从对象池中获取可复用的 Entity
            EntityInstanceObject entityInstanceObject = entityGroup.SpawnEntityInstanceObject(entityAssetName);
            if (entityInstanceObject != null)
            {
                // 池中有可用 Entity，直接复用（同步完成）
                IEntity entity = entityInstanceObject.Target as IEntity;
                if (entity == null)
                {
                    ReferencePool.Release(showInfo);
                    throw new GameFrameworkException("Pooled entity instance is not a valid IEntity.");
                }

                InternalShowEntity(entityId, entityAssetName, entityGroup,
                    entity, false, 0f, actualUserData);

                ReferencePool.Release(showInfo);
                return entity;
            }

            // 池中没有可用 Entity，需要异步加载
            TaskCompletionSource<IEntity> tcs = new TaskCompletionSource<IEntity>();
            int serialId = ++m_Serial;
            m_EntitiesBeingLoaded.Add(entityId, serialId);
            showInfo.SerialId = serialId;
            showInfo.StartTime = m_RealElapseSeconds;

            ResourceComponent resourceComp = GF.Resource;
            if (resourceComp == null)
            {
                m_EntitiesBeingLoaded.Remove(entityId);
                ReferencePool.Release(showInfo);
                throw new GameFrameworkException("Resource component is invalid.");
            }

            resourceComp.LoadAssetAsync(
                entityAssetName,
                typeof(PackedScene),
                asset => OnLoadAssetSuccessForAsync(entityId, entityAssetName, entityGroup, showInfo, asset, tcs),
                errorMsg => OnLoadAssetFailureForAsync(entityId, entityAssetName, entityGroupName, showInfo, errorMsg, tcs)
            );

            return await tcs.Task;
        }

        /// <summary>
        /// 异步加载成功回调（带 TaskCompletionSource）。
        ///
        /// 调用现有的 OnLoadAssetSuccess 完成实体创建和生命周期触发，
        /// 然后通过 TaskCompletionSource 返回结果给 await 调用方。
        /// </summary>
        private void OnLoadAssetSuccessForAsync(int entityId, string entityAssetName,
            EntityGroup entityGroup, ShowEntityInfo showInfo, object asset,
            TaskCompletionSource<IEntity> tcs)
        {
            // 复用现有的加载成功逻辑
            OnLoadAssetSuccess(entityId, entityAssetName, entityGroup, showInfo, asset);

            // 加载成功后实体已注册，通过 ID 获取并返回
            IEntity entity = GetEntity(entityId);
            if (entity != null)
            {
                tcs.TrySetResult(entity);
            }
            else
            {
                tcs.TrySetException(new GameFrameworkException(Utility.Text.Format(
                    "Entity '{0}' not found after successful load.", entityId)));
            }
        }

        /// <summary>
        /// 异步加载失败回调（带 TaskCompletionSource）。
        ///
        /// 调用现有的 OnLoadAssetFailure 触发失败事件，
        /// 然后通过 TaskCompletionSource 通知 await 调用方加载失败。
        /// </summary>
        private void OnLoadAssetFailureForAsync(int entityId, string entityAssetName,
            string entityGroupName, ShowEntityInfo showInfo, string errorMessage,
            TaskCompletionSource<IEntity> tcs)
        {
            // 复用现有的加载失败逻辑
            OnLoadAssetFailure(entityId, entityAssetName, entityGroupName, showInfo, errorMessage);

            // 通知 await 调用方加载失败
            tcs.TrySetException(new GameFrameworkException(Utility.Text.Format(
                "Failed to load entity '{0}': {1}", entityAssetName, errorMessage)));
        }

        /// <summary>
        /// 触发显示实体失败事件。
        /// </summary>
        private void FireShowEntityFailure(int entityId, string entityAssetName,
            string entityGroupName, string errorMessage, object userData)
        {
            EventComponent eventComponent = GetEventComponent();
            if (eventComponent != null)
            {
                ShowEntityFailureEventArgs args = ShowEntityFailureEventArgs.Create(
                    entityId, entityAssetName, entityGroupName, errorMessage, userData);
                eventComponent.Fire(this, args);
                ReferencePool.Release(args);
                Log.Warning("Show entity failure, entity id '{0}', asset name '{1}'.", entityId, entityAssetName);
            }
            else
            {
                Log.Error("Show entity failure, entity id '{0}', asset name '{1}', error message '{2}'.",
                    entityId, entityAssetName, errorMessage);
            }
        }

        /// <summary>
        /// 获取事件组件引用（延迟获取，避免初始化顺序问题）。
        /// </summary>
        private EventComponent GetEventComponent()
        {
            if (m_EventComponent == null)
            {
                m_EventComponent = GF.Event;
            }

            return m_EventComponent;
        }

        /// <summary>
        /// 是否正在加载指定实体。
        /// </summary>
        public bool IsLoadingEntity(int entityId)
        {
            return m_EntitiesBeingLoaded.ContainsKey(entityId);
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
            return m_EntityInfos.ContainsKey(entityId);
        }

        /// <summary>
        /// 是否存在指定资源名称的实体。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasEntity(string entityAssetName)
        {
            if (string.IsNullOrEmpty(entityAssetName))
            {
                throw new GameFrameworkException("Entity asset name is invalid.");
            }

            foreach (KeyValuePair<int, EntityInfo> pair in m_EntityInfos)
            {
                if (pair.Value.Entity.EntityAssetName == entityAssetName) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体实例，不存在则返回 null。</returns>
        public IEntity GetEntity(int entityId)
        {
            EntityInfo entityInfo = GetEntityInfo(entityId);
            return entityInfo?.Entity;
        }

        /// <summary>
        /// 获取实体。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>第一个匹配的实体，不存在则返回 null。</returns>
        public IEntity GetEntity(string entityAssetName)
        {
            if (string.IsNullOrEmpty(entityAssetName))
            {
                throw new GameFrameworkException("Entity asset name is invalid.");
            }

            foreach (KeyValuePair<int, EntityInfo> pair in m_EntityInfos)
            {
                if (pair.Value.Entity.EntityAssetName == entityAssetName) return pair.Value.Entity;
            }
            return null;
        }

        /// <summary>
        /// 获取所有匹配资源名称的实体。
        /// </summary>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <returns>匹配的实体数组。</returns>
        public IEntity[] GetEntities(string entityAssetName)
        {
            if (string.IsNullOrEmpty(entityAssetName))
            {
                throw new GameFrameworkException("Entity asset name is invalid.");
            }

            List<IEntity> results = new List<IEntity>();
            foreach (KeyValuePair<int, EntityInfo> pair in m_EntityInfos)
            {
                if (pair.Value.Entity.EntityAssetName == entityAssetName) results.Add(pair.Value.Entity);
            }
            return results.ToArray();
        }

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <returns>所有实体数组。</returns>
        public IEntity[] GetAllLoadedEntities()
        {
            int index = 0;
            IEntity[] results = new IEntity[m_EntityInfos.Count];
            foreach (KeyValuePair<int, EntityInfo> pair in m_EntityInfos)
            {
                results[index++] = pair.Value.Entity;
            }
            return results;
        }

        /// <summary>
        /// 是否是合法的实体。
        /// </summary>
        /// <param name="entity">实体。</param>
        /// <returns>实体是否合法。</returns>
        public bool IsValidEntity(IEntity entity)
        {
            if (entity == null) return false;
            return HasEntity(entity.Id);
        }

        // ================================================================
        //  父子实体
        // ================================================================

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void AttachEntity(int childEntityId, int parentEntityId, object userData = null)
        {
            if (childEntityId == parentEntityId)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not attach entity when child entity id equals to parent entity id '{0}'.", parentEntityId));
            }

            EntityInfo childEntityInfo = GetEntityInfo(childEntityId);
            if (childEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find child entity '{0}'.", childEntityId));
            }

            if (childEntityInfo.Status >= EntityStatus.WillHide)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not attach entity when child entity status is '{0}'.", childEntityInfo.Status));
            }

            EntityInfo parentEntityInfo = GetEntityInfo(parentEntityId);
            if (parentEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find parent entity '{0}'.", parentEntityId));
            }

            if (parentEntityInfo.Status >= EntityStatus.WillHide)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not attach entity when parent entity status is '{0}'.", parentEntityInfo.Status));
            }

            IEntity childEntity = childEntityInfo.Entity;
            IEntity parentEntity = parentEntityInfo.Entity;

            // 先解除之前的父子关系
            DetachEntity(childEntity.Id, userData);

            // 建立新的父子关系
            childEntityInfo.ParentEntity = parentEntity;
            parentEntityInfo.AddChildEntity(childEntity);

            // 在场景树中也建立父子关系
            if (childEntity is Node childNode && parentEntity is Node parentNode)
            {
                // 将子实体的 Entity 节点移动到父实体下
                Node originalParent = childNode.GetParent();
                if (originalParent != null)
                {
                    originalParent.RemoveChild(childNode);
                }
                parentNode.AddChild(childNode);
            }

            // 触发回调
            parentEntity.OnAttached(childEntity, userData);
            childEntity.OnAttachTo(parentEntity, userData);
        }

        /// <summary>
        /// 附加子实体到父实体。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="parentEntity">父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
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
        /// <param name="childEntity">子实体。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
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
        /// <param name="childEntity">子实体。</param>
        /// <param name="parentEntity">父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
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
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachEntity(int childEntityId, object userData = null)
        {
            EntityInfo childEntityInfo = GetEntityInfo(childEntityId);
            if (childEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find child entity '{0}'.", childEntityId));
            }

            IEntity parentEntity = childEntityInfo.ParentEntity;
            if (parentEntity == null)
            {
                return;
            }

            EntityInfo parentEntityInfo = GetEntityInfo(parentEntity.Id);
            if (parentEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find parent entity '{0}'.", parentEntity.Id));
            }

            IEntity childEntity = childEntityInfo.Entity;

            // 解除逻辑关系
            childEntityInfo.ParentEntity = null;
            parentEntityInfo.RemoveChildEntity(childEntity);

            // 在场景树中恢复到组容器下
            if (childEntity is Node childNode)
            {
                // 获取子实体所属组的容器节点
                EntityGroup childGroup = (EntityGroup)childEntity.EntityGroup;
                if (childGroup != null && childGroup.Helper is DefaultEntityGroupHelper groupHelper)
                {
                    Node originalParent = childNode.GetParent();
                    if (originalParent != null && originalParent != groupHelper)
                    {
                        originalParent.RemoveChild(childNode);
                        groupHelper.AddChild(childNode);
                    }
                }
            }

            // 触发回调
            parentEntity.OnDetached(childEntity, userData);
            childEntity.OnDetachFrom(parentEntity, userData);
        }

        /// <summary>
        /// 解除子实体的父子关系。
        /// </summary>
        /// <param name="childEntity">子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
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
            EntityInfo parentEntityInfo = GetEntityInfo(parentEntityId);
            if (parentEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find parent entity '{0}'.", parentEntityId));
            }

            while (parentEntityInfo.ChildEntityCount > 0)
            {
                IEntity childEntity = parentEntityInfo.GetChildEntity();
                DetachEntity(childEntity.Id, userData);
            }
        }

        /// <summary>
        /// 解除父实体的所有子实体。
        /// </summary>
        /// <param name="parentEntity">父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachChildEntities(IEntity parentEntity, object userData = null)
        {
            if (parentEntity == null)
            {
                throw new GameFrameworkException("Parent entity is invalid.");
            }
            DetachChildEntities(parentEntity.Id, userData);
        }

        /// <summary>
        /// 获取父实体。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <returns>父实体。</returns>
        public IEntity GetParentEntity(int childEntityId)
        {
            EntityInfo childEntityInfo = GetEntityInfo(childEntityId);
            if (childEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find child entity '{0}'.", childEntityId));
            }
            return childEntityInfo.ParentEntity;
        }

        /// <summary>
        /// 获取父实体。
        /// </summary>
        /// <param name="childEntity">子实体。</param>
        /// <returns>父实体。</returns>
        public IEntity GetParentEntity(IEntity childEntity)
        {
            if (childEntity == null)
            {
                throw new GameFrameworkException("Child entity is invalid.");
            }
            return GetParentEntity(childEntity.Id);
        }

        /// <summary>
        /// 获取子实体数量。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>子实体数量。</returns>
        public int GetChildEntityCount(int parentEntityId)
        {
            EntityInfo parentEntityInfo = GetEntityInfo(parentEntityId);
            if (parentEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find parent entity '{0}'.", parentEntityId));
            }
            return parentEntityInfo.ChildEntityCount;
        }

        /// <summary>
        /// 获取第一个子实体。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>第一个子实体。</returns>
        public IEntity GetChildEntity(int parentEntityId)
        {
            EntityInfo parentEntityInfo = GetEntityInfo(parentEntityId);
            if (parentEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find parent entity '{0}'.", parentEntityId));
            }
            return parentEntityInfo.GetChildEntity();
        }

        /// <summary>
        /// 获取所有子实体。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>所有子实体。</returns>
        public IEntity[] GetChildEntities(int parentEntityId)
        {
            EntityInfo parentEntityInfo = GetEntityInfo(parentEntityId);
            if (parentEntityInfo == null)
            {
                throw new GameFrameworkException(Utility.Text.Format(
                    "Can not find parent entity '{0}'.", parentEntityId));
            }
            return parentEntityInfo.GetChildEntities();
        }

        /// <summary>
        /// 获取所有子实体。
        /// </summary>
        /// <param name="parentEntity">父实体。</param>
        /// <returns>所有子实体。</returns>
        public IEntity[] GetChildEntities(IEntity parentEntity)
        {
            if (parentEntity == null)
            {
                throw new GameFrameworkException("Parent entity is invalid.");
            }
            return GetChildEntities(parentEntity.Id);
        }

        // ================================================================
        //  内部方法
        // ================================================================

        /// <summary>
        /// 获取实体信息。
        /// </summary>
        private EntityInfo GetEntityInfo(int entityId)
        {
            m_EntityInfos.TryGetValue(entityId, out EntityInfo entityInfo);
            return entityInfo;
        }

        /// <summary>
        /// 内部隐藏实体逻辑。
        ///
        /// UGF 风格：Entity 节点不销毁，只是从活跃追踪中移除并归还对象池。
        /// Entity.OnRecycle 重置状态但保留 EntityLogic 和 CachedNode。
        /// 仅当对象池释放（池满/过期）时才真正 QueueFree。
        /// </summary>
        private void InternalHideEntity(EntityInfo entityInfo, object userData)
        {
            // 先隐藏所有子实体
            while (entityInfo.ChildEntityCount > 0)
            {
                IEntity childEntity = entityInfo.GetChildEntity();
                HideEntity(childEntity.Id, userData);
            }

            if (entityInfo.Status == EntityStatus.Hidden)
            {
                return;
            }

            IEntity entity = entityInfo.Entity;

            // 解除父子关系
            DetachEntity(entity.Id, userData);

            // 生命周期：Hide
            entityInfo.Status = EntityStatus.WillHide;
            entity.OnHide(m_IsShutdown, userData);
            entityInfo.Status = EntityStatus.Hidden;

            // 从实体组活跃列表中移除
            EntityGroup entityGroup = (EntityGroup)entity.EntityGroup;
            if (entityGroup != null)
            {
                entityGroup.RemoveEntity(entity);
            }

            // 从追踪字典中移除
            if (!m_EntityInfos.Remove(entity.Id))
            {
                throw new GameFrameworkException("Entity info is unmanaged.");
            }

            // 触发 HideEntityComplete 事件（通过 EventComponent）
            EventComponent eventComponent = GetEventComponent();
            if (eventComponent != null)
            {
                HideEntityCompleteEventArgs args = HideEntityCompleteEventArgs.Create(
                    entity.Id, entity.EntityAssetName, entityGroup, userData);
                eventComponent.Fire(this, args);
                ReferencePool.Release(args);
            }

            // 生命周期：Recycle（重置状态，但保留 EntityLogic 和 CachedNode）
            entityInfo.Status = EntityStatus.WillRecycle;
            entity.OnRecycle();
            entityInfo.Status = EntityStatus.Recycled;

            // 释放 EntityInfo（引用池回收）
            ReferencePool.Release(entityInfo);

            // 将 Entity 归还到对象池（不销毁！Entity 节点仍存在于场景树中）
            if (entityGroup != null)
            {
                entityGroup.UnspawnEntity(entity);
            }
        }
    }
}
