//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------
//
// 精简：从 97 成员缩减到 8 个核心方法。
// 移除所有 Unity 管线专属成员（序列化器、事件、资源组等）。
//
//------------------------------------------------------------

using System;

namespace GameFramework.Resource
{
    public interface IResourceManager
    {
        /// <summary>
        /// 获取当前资源模式（单机/热更）。
        /// </summary>
        ResourceMode ResourceMode { get; }

        /// <summary>
        /// 设置资源模式。由 ResourceComponent 在 OnInit 时调用。
        /// </summary>
        void SetResourceMode(ResourceMode resourceMode);

        /// <summary>
        /// 检查资源是否存在。
        /// </summary>
        HasAssetResult HasAsset(string assetName);

        /// <summary>
        /// 异步加载资源。使用 Godot.ResourceLoader.LoadThreadedRequest。
        /// </summary>
        void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks, object userData);

        /// <summary>
        /// 异步加载二进制资源。
        /// </summary>
        void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData);

        /// <summary>
        /// 获取二进制资源的长度
        /// </summary>
        int GetBinaryLength(string binaryAssetName);
    }
}
