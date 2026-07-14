//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

namespace GameFramework.Resource
{
    public enum ResourceMode : byte
    {
        /// <summary>
        /// 编辑器模式，不会加载资源目录
        /// </summary>
        Editor = 0,
        /// <summary>
        /// 单机模式。所有资源打包在游戏内，直接加载。
        /// </summary>
        Package = 1,

        /// <summary>
        /// 预下载的可更新模式。启动时检查远程版本，下载差异 .pck。
        /// </summary>
        Updatable = 2,
    }
}
