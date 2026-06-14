//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

namespace GodotGameFramework
{
    /// <summary>
    /// 关闭游戏框架类型。
    /// 定义了框架关闭时的不同行为方式。
    /// </summary>
    public enum ShutdownType : byte
    {
        /// <summary>
        /// 仅关闭游戏框架。
        /// 不影响游戏进程，只清理框架内部的模块和资源。
        /// </summary>
        None = 0,

        /// <summary>
        /// 关闭游戏框架并重启游戏。
        /// 会重新加载初始场景，从头开始运行游戏。
        /// </summary>
        Restart,

        /// <summary>
        /// 关闭游戏框架并退出游戏。
        /// 会终止整个游戏进程。
        /// </summary>
        Quit,
    }
}
