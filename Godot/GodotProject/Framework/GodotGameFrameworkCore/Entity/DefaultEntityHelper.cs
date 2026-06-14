//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Entity;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认实体辅助器。
    ///
    /// 实现 IEntityHelper 接口，提供基于 Godot 引擎的实体实例化、创建和释放功能。
    ///
    /// InstantiateEntity：从 PackedScene 实例化 Node。
    /// CreateEntity：创建 Entity(Node) 包装器，添加到实体组容器，设置 EntityLogic。
    /// ReleaseEntity：通过 QueueFree 释放节点。
    ///
    /// 对应 Unity 版本中的 EntityInstanceObjectHelper。
    /// </summary>
    public class DefaultEntityHelper : IEntityHelper
    {
        /// <summary>
        /// 实例化实体。
        ///
        /// 从给定的 PackedScene 资源实例化一个 Node。
        /// 这个 Node 是用户在编辑器中设计的场景实例，
        /// 之后会被添加为 Entity(Node) 的子节点。
        /// </summary>
        /// <param name="entityAsset">实体资源（期望为 PackedScene）。</param>
        /// <returns>实例化后的 Node，如果资源类型不匹配返回 null。</returns>
        public object InstantiateEntity(object entityAsset)
        {
            if (entityAsset is PackedScene packedScene)
            {
                return packedScene.Instantiate();
            }

            Log.Warning("Entity asset is not a PackedScene: {0}.",
                entityAsset?.GetType().Name ?? "null");
            return null;
        }

        /// <summary>
        /// 创建实体。
        ///
        /// 1. 创建 Entity(Node) 作为包装器
        /// 2. 将 instanceNode 添加为 Entity 的子节点
        /// 3. 将 Entity 添加到实体组的 DefaultEntityGroupHelper 容器节点下
        /// 4. 如果 userData 包含 ShowEntityInfo，创建对应的 EntityLogic 实例
        /// </summary>
        /// <param name="entityInstance">实体实例（期望为 Node）。</param>
        /// <param name="entityGroup">实体所属的实体组。</param>
        /// <param name="userData">用户自定义数据（可以是 ShowEntityInfo）。</param>
        /// <returns>创建的 IEntity（Entity 节点）。</returns>
        public IEntity CreateEntity(object entityInstance, IEntityGroup entityGroup, object userData)
        {
            if (entityInstance == null)
            {
                Log.Warning("Entity instance is invalid.");
                return null;
            }

            Node instanceNode = entityInstance as Node;
            if (instanceNode == null)
            {
                Log.Warning("Entity instance is not a Node: {0}.", entityInstance.GetType().Name);
                return null;
            }

            // 创建 Entity 包装器节点
            Entity entity = new Entity();

            // 将实际的场景节点作为 Entity 的子节点
            entity.AddChild(instanceNode);

            // 将 Entity 添加到实体组的容器节点下
            if (entityGroup != null && entityGroup.Helper is DefaultEntityGroupHelper groupHelper)
            {
                groupHelper.AddChild(entity);
            }

            // 如果 userData 包含 ShowEntityInfo，创建 EntityLogic
            ShowEntityInfo showInfo = userData as ShowEntityInfo;
            if (showInfo != null)
            {
                if (showInfo.EntityLogicType != null)
                {
                    // 通过反射创建 EntityLogic 实例
                    EntityLogic logic = System.Activator.CreateInstance(showInfo.EntityLogicType) as EntityLogic;
                    if (logic != null)
                    {
                        entity.SetEntityLogic(logic);
                    }
                    else
                    {
                        Log.Warning("Can not create EntityLogic instance of type '{0}'.",
                            showInfo.EntityLogicType.Name);
                    }
                }

                // 将 ShowEntityInfo 中的 UserData 传递给 OnInit/OnShow
                // 注意：这里将 userData 替换为内部的 ShowEntityInfo.UserData
                // EntityComponent.InternalShowEntity 会使用替换后的 userData
            }

            return entity;
        }

        /// <summary>
        /// 释放实体。
        ///
        /// 仅释放实例节点。不卸载 PackedScene 资源，因为同一资源可能被
        /// 对象池中的多个实例共享，卸载会导致其他实例失效。
        /// 资源生命周期由 Godot 引擎的资源引用计数自动管理。
        /// </summary>
        /// <param name="entityAsset">实体资源（PackedScene）。</param>
        /// <param name="entityInstance">实体实例（期望为 Node）。</param>
        public void ReleaseEntity(object entityAsset, object entityInstance)
        {
            if (entityInstance is Node node)
            {
                node.QueueFree();
            }
        }
    }
}
