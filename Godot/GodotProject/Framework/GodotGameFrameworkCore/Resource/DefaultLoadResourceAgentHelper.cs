//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.FileSystem;
using GameFramework.Resource;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认加载资源代理辅助器。
    ///
    /// 实现 ILoadResourceAgentHelper 接口，提供 Godot 引擎下的资源加载代理功能。
    /// 该辅助器被核心框架的 LoadResourceAgent 调用，负责实际的文件读取和资源加载操作。
    ///
    /// 工作流程：
    /// 1. LoadResourceAgent 调用 ReadFile/ReadBytes 读取文件内容
    /// 2. 读取完成后触发对应事件，将数据返回给 Agent
    /// 3. Agent 根据加载类型决定下一步操作（ParseBytes 或 LoadAsset）
    /// 4. LoadAsset 使用 ResourceLoader.Load 加载实际资源
    /// 5. 加载完成后触发 LoadComplete 事件
    ///
    /// 对应 Unity 版本中的 DefaultLoadResourceAgentHelper。
    /// </summary>
    public sealed class DefaultLoadResourceAgentHelper : ILoadResourceAgentHelper
    {
        /// <summary>
        /// 加载资源代理辅助器异步加载资源更新事件。
        /// </summary>
        public event EventHandler<LoadResourceAgentHelperUpdateEventArgs> LoadResourceAgentHelperUpdate;

        /// <summary>
        /// 加载资源代理辅助器异步读取资源文件完成事件。
        /// </summary>
        public event EventHandler<LoadResourceAgentHelperReadFileCompleteEventArgs>
            LoadResourceAgentHelperReadFileComplete;

        /// <summary>
        /// 加载资源代理辅助器异步读取资源二进制流完成事件。
        /// </summary>
        public event EventHandler<LoadResourceAgentHelperReadBytesCompleteEventArgs>
            LoadResourceAgentHelperReadBytesComplete;

        /// <summary>
        /// 加载资源代理辅助器异步将资源二进制流转换为加载对象完成事件。
        /// </summary>
        public event EventHandler<LoadResourceAgentHelperParseBytesCompleteEventArgs>
            LoadResourceAgentHelperParseBytesComplete;

        /// <summary>
        /// 加载资源代理辅助器异步加载资源完成事件。
        /// </summary>
        public event EventHandler<LoadResourceAgentHelperLoadCompleteEventArgs>
            LoadResourceAgentHelperLoadComplete;

        /// <summary>
        /// 加载资源代理辅助器错误事件。
        /// </summary>
        public event EventHandler<LoadResourceAgentHelperErrorEventArgs> LoadResourceAgentHelperError;

        /// <summary>
        /// 通过加载资源代理辅助器开始异步读取资源文件。
        ///
        /// 使用 Godot 的 FileAccess API 读取文件，然后通过 ResourceLoader.Load
        /// 将文件内容加载为 Godot Resource 对象。
        /// </summary>
        /// <param name="fullPath">要加载资源的完整路径名。</param>
        public void ReadFile(string fullPath)
        {
            try
            {
                if (!FileAccess.FileExists(fullPath))
                {
                    OnError(LoadResourceStatus.NotExist,
                        Utility.Text.Format("File '{0}' does not exist.", fullPath));
                    return;
                }

                // 使用 ResourceLoader.Load 将文件加载为资源对象
                var resource = Godot.ResourceLoader.Load(fullPath);
                if (resource == null)
                {
                    OnError(LoadResourceStatus.AssetError,
                        Utility.Text.Format("Can not load resource from '{0}'.", fullPath));
                    return;
                }

                // 触发读取完成事件
                LoadResourceAgentHelperReadFileComplete?.Invoke(this,
                    LoadResourceAgentHelperReadFileCompleteEventArgs.Create(resource));
            }
            catch (Exception e)
            {
                OnError(LoadResourceStatus.AssetError,
                    Utility.Text.Format("Read file exception: {0}", e.Message));
            }
        }

        /// <summary>
        /// 通过加载资源代理辅助器开始异步读取资源文件（从文件系统）。
        ///
        /// 预留实现。当前 GGF 不使用 FileSystem 模块，直接触发错误。
        /// </summary>
        /// <param name="fileSystem">要加载资源的文件系统。</param>
        /// <param name="name">要加载资源的名称。</param>
        public void ReadFile(IFileSystem fileSystem, string name)
        {
            OnError(LoadResourceStatus.NotExist,
                "Load from file system is not supported in current mode.");
        }

        /// <summary>
        /// 通过加载资源代理辅助器开始异步读取资源二进制流。
        ///
        /// 使用 Godot 的 FileAccess API 读取文件内容为字节数组。
        /// </summary>
        /// <param name="fullPath">要加载资源的完整路径名。</param>
        public void ReadBytes(string fullPath)
        {
            try
            {
                if (!FileAccess.FileExists(fullPath))
                {
                    OnError(LoadResourceStatus.NotExist,
                        Utility.Text.Format("File '{0}' does not exist.", fullPath));
                    return;
                }

                using var file = FileAccess.Open(fullPath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    OnError(LoadResourceStatus.AssetError,
                        Utility.Text.Format("Can not open file '{0}', error: {1}.",
                            fullPath, FileAccess.GetOpenError()));
                    return;
                }

                long length = (long)file.GetLength();
                byte[] bytes = file.GetBuffer(length);

                // 触发字节读取完成事件
                LoadResourceAgentHelperReadBytesComplete?.Invoke(this,
                    LoadResourceAgentHelperReadBytesCompleteEventArgs.Create(bytes));
            }
            catch (Exception e)
            {
                OnError(LoadResourceStatus.AssetError,
                    Utility.Text.Format("Read bytes exception: {0}", e.Message));
            }
        }

        /// <summary>
        /// 通过加载资源代理辅助器开始异步读取资源二进制流（从文件系统）。
        ///
        /// 预留实现。当前 GGF 不使用 FileSystem 模块，直接触发错误。
        /// </summary>
        /// <param name="fileSystem">要加载资源的文件系统。</param>
        /// <param name="name">要加载资源的名称。</param>
        public void ReadBytes(IFileSystem fileSystem, string name)
        {
            OnError(LoadResourceStatus.NotExist,
                "Load bytes from file system is not supported in current mode.");
        }

        /// <summary>
        /// 通过加载资源代理辅助器开始异步将资源二进制流转换为加载对象。
        ///
        /// 将字节数组包装为资源对象返回。
        /// 在 Godot 中，字节流通常不直接转换为 Godot.Resource，
        /// 此处将 bytes 作为资源对象传递。
        /// </summary>
        /// <param name="bytes">要加载资源的二进制流。</param>
        public void ParseBytes(byte[] bytes)
        {
            // 在 Godot 单机模式下，字节流本身就是"资源"
            // 触发解析完成事件，将 bytes 作为资源对象传递
            LoadResourceAgentHelperParseBytesComplete?.Invoke(this,
                LoadResourceAgentHelperParseBytesCompleteEventArgs.Create(bytes));
        }

        /// <summary>
        /// 通过加载资源代理辅助器开始异步加载资源。
        ///
        /// 使用 Godot 的 ResourceLoader.Load 加载指定类型的资源。
        /// </summary>
        /// <param name="resource">已读取的资源对象（Godot.Resource 或 byte[]）。</param>
        /// <param name="assetName">要加载的资源名称（路径）。</param>
        /// <param name="assetType">要加载资源的类型。</param>
        /// <param name="isScene">要加载的资源是否是场景。</param>
        public void LoadAsset(object resource, string assetName, Type assetType, bool isScene)
        {
            try
            {
                if (isScene)
                {
                    // 场景加载由 SceneComponent 处理，此处直接返回已加载的资源
                    LoadResourceAgentHelperLoadComplete?.Invoke(this,
                        LoadResourceAgentHelperLoadCompleteEventArgs.Create(resource));
                    return;
                }

                // 如果 resource 已经是目标类型的 Godot 资源，直接返回
                if (resource is Godot.Resource godotResource)
                {
                    LoadResourceAgentHelperLoadComplete?.Invoke(this,
                        LoadResourceAgentHelperLoadCompleteEventArgs.Create(godotResource));
                    return;
                }

                // 否则使用 ResourceLoader.Load 重新加载
                var loadedAsset = Godot.ResourceLoader.Load(assetName, assetType?.Name);
                if (loadedAsset == null)
                {
                    OnError(LoadResourceStatus.AssetError,
                        Utility.Text.Format("Can not load asset '{0}' with type '{1}'.",
                            assetName, assetType?.Name ?? "null"));
                    return;
                }

                LoadResourceAgentHelperLoadComplete?.Invoke(this,
                    LoadResourceAgentHelperLoadCompleteEventArgs.Create(loadedAsset));
            }
            catch (Exception e)
            {
                OnError(LoadResourceStatus.AssetError,
                    Utility.Text.Format("Load asset exception: {0}", e.Message));
            }
        }

        /// <summary>
        /// 重置加载资源代理辅助器。
        ///
        /// 重置内部状态，为下一次加载任务做准备。
        /// </summary>
        public void Reset()
        {
            // 当前无需重置的内部状态
        }

        /// <summary>
        /// 触发加载错误事件。
        /// </summary>
        /// <param name="status">加载资源状态。</param>
        /// <param name="errorMessage">错误信息。</param>
        private void OnError(LoadResourceStatus status, string errorMessage)
        {
            LoadResourceAgentHelperError?.Invoke(this,
                LoadResourceAgentHelperErrorEventArgs.Create(status, errorMessage));
        }
    }
}
