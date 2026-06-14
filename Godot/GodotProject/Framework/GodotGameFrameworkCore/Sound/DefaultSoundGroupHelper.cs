//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Sound;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认声音组辅助器。
    ///
    /// 作为声音组的场景树容器节点，承载该组内所有 AudioStreamPlayer 节点。
    /// 实现 ISoundGroupHelper 标记接口（核心框架中为空接口），
    /// 为未来扩展预留接口兼容性。
    ///
    /// 场景树结构示例：
    /// <code>
    /// SoundComponent
    ///   └── DefaultSoundGroupHelper "Sound Group - Music"
    ///       ├── AudioStreamPlayer "Agent 0"
    ///       └── AudioStreamPlayer "Agent 1"
    /// </code>
    ///
    /// 对应 Unity 版本中的 DefaultSoundGroupHelper（MonoBehaviour）。
    /// </summary>
    public sealed partial class DefaultSoundGroupHelper : Node, ISoundGroupHelper
    {
    }
}
