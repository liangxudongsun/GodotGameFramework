//------------------------------------------------------------
// EntityExtension - 实体组件便捷扩展方法
// 提供 EntityComponent 的常用便捷方法，简化游戏代码编写。
//
// 使用方式：
//   GF.Entity.GetEntity<EnemyLogic>(1);
//   GF.Entity.HideEntitySafe(1);
//   GF.Entity.GetChildEntities<BulletLogic>(playerEntityId);
//
// 对应 UGF 参考项目中的 EntityExtension（游戏特定的路径解析、
// 特效/浮文创建等逻辑不移植，只保留通用便捷方法）。
//------------------------------------------------------------

using GameFramework.Entity;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 实体组件扩展方法。
    ///
    /// 提供 EntityComponent 的常用便捷方法，
    /// 包括通过 EntityLogic 类型获取实体、安全隐藏、子实体查询等。
    ///
    /// 对应 Unity 版本中游戏项目的 EntityExtension。
    /// </summary>
    public static class EntityExtension
    {
        /// <summary>
        /// 通过实体编号获取 EntityLogic 子类。
        ///
        /// 等价于：
        /// <code>
        /// IEntity entity = comp.GetEntity(entityId);
        /// EnemyLogic logic = (entity as Entity)?.Logic as EnemyLogic;
        /// </code>
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="entityId">实体编号。</param>
        /// <returns>对应的 EntityLogic 实例，未找到返回 null。</returns>
        public static TLogic GetEntity<TLogic>(this EntityComponent entityComponent, int entityId)
            where TLogic : EntityLogic
        {
            IEntity entity = entityComponent.GetEntity(entityId);
            if (entity is Entity ggfEntity)
            {
                return ggfEntity.Logic as TLogic;
            }

            return null;
        }

        /// <summary>
        /// 通过实体资源名获取所有匹配的 EntityLogic 子类列表。
        ///
        /// 等价于：
        /// <code>
        /// IEntity[] entities = comp.GetEntities(assetName);
        /// // 遍历并转换为 TLogic 列表
        /// </code>
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>匹配的 EntityLogic 实例列表。</returns>
        public static List<TLogic> GetEntities<TLogic>(this EntityComponent entityComponent, string assetName)
            where TLogic : EntityLogic
        {
            List<TLogic> result = new List<TLogic>();
            IEntity[] entities = entityComponent.GetEntities(assetName);
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] is Entity ggfEntity && ggfEntity.Logic is TLogic logic)
                {
                    result.Add(logic);
                }
            }

            return result;
        }

        /// <summary>
        /// 通过实体编号获取所有已加载实体的 EntityLogic 子类列表。
        ///
        /// 适用于需要遍历所有实体的场景（如批量更新、统计等）。
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <returns>匹配的 EntityLogic 实例列表。</returns>
        public static List<TLogic> GetAllEntities<TLogic>(this EntityComponent entityComponent)
            where TLogic : EntityLogic
        {
            List<TLogic> result = new List<TLogic>();
            IEntity[] entities = entityComponent.GetAllLoadedEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i] is Entity ggfEntity && ggfEntity.Logic is TLogic logic)
                {
                    result.Add(logic);
                }
            }

            return result;
        }

        /// <summary>
        /// 安全隐藏实体。
        ///
        /// 在隐藏前检查实体是否存在和正在加载中，
        /// 避免因实体不存在而抛出异常。
        ///
        /// 适用于不确定实体是否还存活的场景（如实体可能已自行销毁）。
        /// </summary>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public static void HideEntitySafe(this EntityComponent entityComponent, int entityId, object userData = null)
        {
            if (entityComponent.HasEntity(entityId) && !entityComponent.IsLoadingEntity(entityId))
            {
                entityComponent.HideEntity(entityId, userData);
            }
        }

        /// <summary>
        /// 通过实体编号安全隐藏实体。
        ///
        /// 在隐藏前检查实体引用是否有效。
        /// </summary>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="entity">要隐藏的实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        public static void HideEntitySafe(this EntityComponent entityComponent, IEntity entity, object userData = null)
        {
            if (entity != null && entityComponent.IsValidEntity(entity))
            {
                entityComponent.HideEntity(entity, userData);
            }
        }

        /// <summary>
        /// 获取父实体的 EntityLogic 子类。
        ///
        /// 适用于通过子实体反查父实体逻辑的场景。
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="childEntityId">子实体编号。</param>
        /// <returns>父实体的 EntityLogic 实例，无父实体返回 null。</returns>
        public static TLogic GetParentEntity<TLogic>(this EntityComponent entityComponent, int childEntityId)
            where TLogic : EntityLogic
        {
            IEntity parentEntity = entityComponent.GetParentEntity(childEntityId);
            if (parentEntity is Entity ggfEntity)
            {
                return ggfEntity.Logic as TLogic;
            }

            return null;
        }

        /// <summary>
        /// 获取第一个子实体的 EntityLogic 子类。
        ///
        /// 适用于父子实体一对多的场景（如武器挂载到角色上）。
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>第一个子实体的 EntityLogic 实例，无子实体返回 null。</returns>
        public static TLogic GetChildEntity<TLogic>(this EntityComponent entityComponent, int parentEntityId)
            where TLogic : EntityLogic
        {
            IEntity childEntity = entityComponent.GetChildEntity(parentEntityId);
            if (childEntity is Entity ggfEntity)
            {
                return ggfEntity.Logic as TLogic;
            }

            return null;
        }

        /// <summary>
        /// 获取所有子实体的 EntityLogic 子类列表。
        ///
        /// 适用于需要遍历所有子实体的场景（如角色身上的所有装备）。
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>匹配的子实体 EntityLogic 实例列表。</returns>
        public static List<TLogic> GetChildEntities<TLogic>(this EntityComponent entityComponent, int parentEntityId)
            where TLogic : EntityLogic
        {
            List<TLogic> result = new List<TLogic>();
            IEntity[] childEntities = entityComponent.GetChildEntities(parentEntityId);
            for (int i = 0; i < childEntities.Length; i++)
            {
                if (childEntities[i] is Entity ggfEntity && ggfEntity.Logic is TLogic logic)
                {
                    result.Add(logic);
                }
            }

            return result;
        }

        /// <summary>
        /// 通过父实体引用获取所有子实体的 EntityLogic 子类列表。
        /// </summary>
        /// <typeparam name="TLogic">EntityLogic 子类类型。</typeparam>
        /// <param name="entityComponent">实体组件。</param>
        /// <param name="parentEntity">父实体引用。</param>
        /// <returns>匹配的子实体 EntityLogic 实例列表。</returns>
        public static List<TLogic> GetChildEntities<TLogic>(this EntityComponent entityComponent, IEntity parentEntity)
            where TLogic : EntityLogic
        {
            List<TLogic> result = new List<TLogic>();
            IEntity[] childEntities = entityComponent.GetChildEntities(parentEntity);
            for (int i = 0; i < childEntities.Length; i++)
            {
                if (childEntities[i] is Entity ggfEntity && ggfEntity.Logic is TLogic logic)
                {
                    result.Add(logic);
                }
            }

            return result;
        }

    }
}
