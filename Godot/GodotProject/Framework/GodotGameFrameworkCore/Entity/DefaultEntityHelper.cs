//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Entity;
using Godot;

namespace GodotGameFramework.Entity
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
    /// </summary>
    public partial class DefaultEntityHelper : EntityHelperBase
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
        public override object InstantiateEntity(object entityAsset)
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
        /// </summary>
        /// <returns>创建的 IEntity（Entity 节点）。</returns>
        public override IEntity CreateEntity(object entityInstance, IEntityGroup entityGroup, object userData)
        {
            if (entityInstance == null)
            {
                Log.Warning("Entity instance is invalid.");
                return null;
            }
            // 创建 Entity 包装器节点
            Entity entity = new Entity();
            if (entityInstance is EntityLogic logic)
            {
                // 将实际的场景节点作为 Entity 的子节点
                entity.AddChild(logic);

                // 将 Entity 添加到实体组的容器节点下
                if (entityGroup != null && entityGroup.Helper is EntityGroupHelperBase groupHelper)
                {
                    groupHelper.AddChild(entity);
                }
                entity.SetEntityLogic(logic);
            }
            else
            {
                Log.Warning("Entity instance is not a EntityLogic: {0}.", entityInstance.GetType().Name);
                return null;
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
        public override void ReleaseEntity(object entityAsset, object entityInstance)
        {
            if (entityInstance is Node node)
            {
                node.QueueFree();
            }
        }
    }
}
