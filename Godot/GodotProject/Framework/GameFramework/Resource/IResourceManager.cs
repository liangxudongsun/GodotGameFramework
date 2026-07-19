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
        /// 初始化资源管理器（加载子包和版本清单）。由 ResourceComponent 在 OnInit 时调用。
        /// </summary>
        void SetReadWritePath(string readWritePath);

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
        /// 同步加载二进制资源（仅用于小文件，大文件请用 LoadBinaryAsync）。
        /// </summary>
        void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData);

        /// <summary>
        /// 异步加载二进制资源（线程池读取 + 每帧轮询完成）。
        /// </summary>
        void LoadBinaryAsync(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData);

        /// <summary>
        /// 获取二进制资源的长度
        /// </summary>
        int GetBinaryLength(string binaryAssetName);
        /// <summary>
        /// 设置资源加载代理数量
        /// </summary>
        /// <param name="agentCount">
        void SetLoadAssetAgentCount(int agentCount);

        /// <summary>
        /// 设置二进制加载代理数量
        /// </summary>
        void SetLoadBinaryAgentCount(int agentCount);
    }
}
