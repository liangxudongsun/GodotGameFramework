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
    /// 默认实体组辅助器。
    ///
    /// 继承自 Godot 的 Node，作为实体组在场景树中的容器节点。
    /// 所有属于该实体组的 Entity 节点会自动添加为此节点的子节点，
    /// 形成清晰的层级结构，便于调试和场景树可视化。
    ///
    /// 实现了 IEntityGroupHelper 空接口（核心框架要求），
    /// 同时通过继承 Node 提供 Godot 场景树管理能力。
    ///
    /// 场景树结构示例：
    /// <code>
    /// GGFEntry
    /// └── EntityComponent
    ///     ├── DefaultEntityGroupHelper (组: "Enemy")
    ///     │   ├── Entity #1
    ///     │   │   └── [实际的 Node2D/Node3D 子节点]
    ///     │   └── Entity #2
    ///     │       └── [实际的 Node2D/Node3D 子节点]
    ///     └── DefaultEntityGroupHelper (组: "Item")
    ///         └── Entity #3
    ///             └── [实际的 Node2D/Node3D 子节点]
    /// </code>
    ///
    /// 对应 Unity 版本中的 EntityGroupHelper（管理 Transform 层级）。
    /// </summary>
    public sealed partial class DefaultEntityGroupHelper : Node, IEntityGroupHelper
    {
    }
}
