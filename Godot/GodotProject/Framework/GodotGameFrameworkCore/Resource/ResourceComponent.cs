//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.ObjectPool;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFramework.Resource
{
    /// <summary>
    /// 资源组件。
    ///
    /// 这是资源管理系统的封装组件，提供通过框架加载 Godot 资源的能力。
    /// 支持 PackedScene、Texture2D、AudioStream、Resource 等所有 Godot 资源类型的加载。
    ///
    /// 架构说明：
    /// ResourceComponent 支持两种加载模式：
    ///
    /// 1. 管道模式（Pipeline Mode，默认开启）：
    ///    通过核心框架的 ResourceManager 管道加载资源。
    ///    流程：VersionList → ResourceIniter → ResourceLoader → Agent → Helper
    ///    提供对象池缓存、资源分组等高级功能。
    ///    需要先加载 GameFrameworkVersion.dat 版本列表文件。
    ///
    /// 2. 直接模式（Direct Mode，回退方案）：
    ///    绕过核心管道，直接使用 Godot 的 ResourceLoader API 加载。
    ///    简单直接，适合开发调试阶段。
    ///
    /// 使用方式：
    /// <code>
    /// ResourceComponent resource = GF.Resource;
    ///
    /// // 同步加载（始终走直接模式）
    /// PackedScene scene = resource.LoadAsset&lt;PackedScene&gt;("res://Scenes/Main.tscn");
    /// Texture2D tex = resource.LoadAsset&lt;Texture2D&gt;("res://Textures/Icon.png");
    ///
    /// // 异步加载（管道模式下走核心管道，有对象池缓存）
    /// resource.LoadAssetAsync("res://Scenes/Main.tscn", typeof(PackedScene),
    ///     asset => { GD.Print("Loaded: " + asset); },
    ///     error => { GD.PrintErr("Failed: " + error); });
    ///
    /// // 加载文件数据（始终走 FileAccess 直接读取）
    /// byte[] bytes = resource.LoadBinary("res://Data/Config.txt");
    /// string text = resource.LoadText("res://Data/Config.txt");
    ///
    /// // 检查资源是否存在
    /// bool exists = resource.HasAsset("res://Scenes/Main.tscn");
    /// </code>
    ///
    /// </summary>
    public sealed partial class ResourceComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层资源管理器实例。
        /// 注册为核心模块以满足框架内部的引用检查（如 DataTableManager 需要 ResourceManager）。
        /// 管道模式下，异步加载操作通过此管理器的版本列表管道执行。
        /// </summary>
        private IResourceManager m_ResourceManager;

        /// <summary>
        /// 异步加载任务队列（直接模式专用）。
        /// 存储所有正在进行中的直接异步加载任务。
        /// 管道模式下的异步加载由核心框架内部管理，不使用此队列。
        /// </summary>
        private readonly List<AsyncLoadTask> m_AsyncLoadTasks = new List<AsyncLoadTask>();

        /// <summary>
        /// 管道是否已初始化（InitResources 完成后为 true）。
        /// 为 true 时，异步加载走核心管道；为 false 时，走直接模式。
        /// </summary>
        private bool m_PipelineInitialized = false;

        /// <summary>
        /// 编辑器模式标志。
        /// 编辑器模式下会自动生成版本列表文件。
        /// </summary>
        private bool m_EditorMode = false;

        /// <summary>
        /// 加载代理辅助器数量。
        /// 每个代理可以并行处理一个资源加载请求。
        /// </summary>
        [Export]
        public int LoadResourceAgentHelperCount = 1;

        /// <summary>
        /// 只读资源路径。
        /// Godot 中固定为 "res://"，所有项目资源都在此路径下。
        /// </summary>
        [Export]
        public string ReadOnlyPath = "res://";

        /// <summary>
        /// 读写资源路径。
        /// Godot 中为 "user://"，用于持久化存储（存档、日志等）。
        /// </summary>
        [Export]
        public string ReadWritePath = "user://";

        /// <summary>
        /// 游戏版本号（用于版本列表）。
        /// </summary>
        [Export]
        public string GameVersion = "1.0.0";

        /// <summary>
        /// 内部资源版本号。
        /// 每次资源变更时应递增此值。
        /// </summary>
        [Export]
        public int InternalResourceVersion = 1;

        /// <summary>
        /// 是否使用资源管道。
        /// 开启时异步加载通过核心框架管道（有对象池缓存、资源分组等功能）。
        /// 关闭时回退到直接加载模式（仅使用 Godot ResourceLoader API）。
        /// </summary>
        [Export]
        public bool UseResourcePipeline = true;
        [Export]
        private string m_ResourceHelperTypeName = "GodotGameFramework.Resource.DefaultResourceHelper";
        [Export]
        private string m_LoadResourceAgentHelperTypeName = "GodotGameFramework.Resource.DefaultLoadResourceAgentHelper";
        private ResourceHelperBase m_ResourceHelper;

        /// <summary>
        /// 获取当前异步加载任务数量（仅直接模式的任务）。
        /// </summary>
        public int AsyncLoadTaskCount => m_AsyncLoadTasks.Count;

        /// <summary>
        /// 获取管道是否已初始化。
        /// </summary>
        public bool PipelineInitialized => m_PipelineInitialized;

        /// <summary>
        /// 获取当前资源模式下的资源数量（管道模式下有效）。
        /// </summary>
        public int AssetCount => m_PipelineInitialized ? m_ResourceManager.AssetCount : 0;

        /// <summary>
        /// 获取当前资源模式下的资源信息数量（管道模式下有效）。
        /// </summary>
        public int ResourceCount => m_PipelineInitialized ? m_ResourceManager.ResourceCount : 0;

        /// <summary>
        /// 节点初始化回调。
        /// 获取核心层 IResourceManager，创建辅助器并根据配置选择加载模式。
        /// </summary>
        public override void OnInit()
        {
            base.OnInit();

            m_ResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            if (m_ResourceManager == null)
            {
                Log.Fatal("Resource manager is invalid.");
                return;
            }

            // 检测编辑器模式
            m_EditorMode = OS.HasFeature("editor");

            // 核心框架基础配置（两种模式都需要）
            m_ResourceManager.SetReadOnlyPath(ReadOnlyPath);
            m_ResourceManager.SetReadWritePath(ReadWritePath);
            m_ResourceManager.SetResourceMode(ResourceMode.Package);
            m_ResourceManager.SetObjectPoolManager(GameFrameworkEntry.GetModule<IObjectPoolManager>());

            // 创建并设置资源辅助器
            ResourceHelperBase helperBase = Helper.CreateHelper(m_ResourceHelperTypeName, m_ResourceHelper);
            m_ResourceManager.SetResourceHelper(helperBase);
            AddChild(helperBase);
            helperBase.Name = m_ResourceHelperTypeName;
            m_ResourceHelper = helperBase;
            // 注册加载代理辅助器
            for (int i = 0; i < LoadResourceAgentHelperCount; i++)
            {
                LoadResourceAgentHelperBase agentHelper = Create(m_LoadResourceAgentHelperTypeName) as LoadResourceAgentHelperBase;
                AddChild(agentHelper);
                agentHelper.Name = Utility.Text.Format("{0}_{1}", m_LoadResourceAgentHelperTypeName, i);
                m_ResourceManager.AddLoadResourceAgentHelper(agentHelper);
            }

            if (UseResourcePipeline)
            {
                // 管道模式：注册版本列表反序列化回调
                GGFBuiltinVersionListSerializer.RegisterPackageDeserializeCallbacks(
                    m_ResourceManager.PackageVersionListSerializer);

                if (m_EditorMode)
                {
                    // 编辑器模式：每次启动自动生成版本列表
                    // 保持版本列表与项目文件同步，导出后此文件已包含在 PCK 中
                    GGFBuiltinVersionListSerializer.RegisterPackageSerializeCallbacks(
                        m_ResourceManager.PackageVersionListSerializer);

                    string versionListPath = Utility.Path.GetRegularPath(
                        ReadOnlyPath + "GameFrameworkVersion.dat");
                    GGFResourceBuilder.BuildVersionList(
                        ReadOnlyPath, versionListPath, GameVersion, InternalResourceVersion);
                }

                // 初始化资源管道（加载 GameFrameworkVersion.dat → 填充 AssetInfos/ResourceInfos）
                m_ResourceManager.InitResources(OnInitResourcesComplete);
            }

            // 启用 _Process 轮询（用于异步加载和管道更新）
            ProcessMode = ProcessModeEnum.Always;
        }

        /// <summary>
        /// 资源管道初始化完成回调。
        /// </summary>
        private void OnInitResourcesComplete()
        {
            m_PipelineInitialized = true;
            Log.Info("Resource pipeline initialized. Assets: {0}, Resources: {1}",
                m_ResourceManager.AssetCount, m_ResourceManager.ResourceCount);
        }

        /// <summary>
        /// 每帧更新回调。
        /// 轮询直接模式的异步加载任务。
        /// 管道模式的更新由 GameFrameworkEntry.Update 驱动。
        /// </summary>
        /// <param name="delta">帧间隔时间（秒）。</param>
        public override void OnUpdate(double delta)
        {
            base.OnUpdate(delta);

            // 只在有直接加载任务时轮询
            if (m_AsyncLoadTasks.Count > 0)
            {
                PollAsyncLoadTasks();
            }
        }

        // ================================================================
        //  同步加载方法（始终走直接模式，与 UGF 行为一致）
        // ================================================================

        /// <summary>
        /// 同步加载资源。
        ///
        /// 使用 Godot 的 ResourceLoader.Load 直接加载指定路径的资源。
        /// 同步加载始终使用直接模式，不走核心管道（与 UGF 行为一致）。
        /// </summary>
        /// <typeparam name="T">资源类型（如 PackedScene、Texture2D、AudioStream 等）。</typeparam>
        /// <param name="assetPath">资源路径（如 "res://Scenes/Main.tscn"）。</param>
        /// <returns>加载的资源对象，如果加载失败返回 null。</returns>
        public T LoadAsset<T>(string assetPath) where T : class
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Log.Warning("Asset path is invalid.");
                return null;
            }

            var resource = Godot.ResourceLoader.Load<T>(assetPath);
            if (resource == null)
            {
                Log.Warning("Can not load asset '{0}'.", assetPath);
            }

            return resource;
        }

        /// <summary>
        /// 同步加载资源（指定类型）。
        ///
        /// 支持两种加载方式：
        /// - Godot 资源类型（PackedScene、Texture2D 等）：通过 Godot.ResourceLoader.Load 加载
        /// - byte[]（原始二进制文件，如 .bytes 数据表）：通过 LoadBinary（FileAccess）直接读取
        /// </summary>
        /// <param name="assetPath">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <returns>加载的资源对象，如果加载失败返回 null。</returns>
        public object LoadAsset(string assetPath, Type assetType)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Log.Warning("Asset path is invalid.");
                return null;
            }

            var resource = Godot.ResourceLoader.Load(assetPath, assetType?.Name);
            if (resource == null)
            {
                Log.Warning("Can not load asset '{0}' with type '{1}'.",
                    assetPath, assetType?.Name ?? "null");
            }

            return resource;
        }

        // ================================================================
        //  异步加载方法
        // ================================================================

        /// <summary>
        /// 异步加载资源。
        ///
        /// 管道模式（m_PipelineInitialized = true）：
        ///   通过核心框架的 ResourceManager 管道加载。
        ///   提供对象池缓存、资源分组、依赖追踪等功能。
        ///
        /// 直接模式（m_PipelineInitialized = false）：
        ///   使用 Godot 的 ResourceLoader.LoadThreadedRequest 异步加载。
        ///   简单直接，适合开发调试阶段。
        /// </summary>
        /// <param name="assetPath">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <param name="onSuccess">加载成功回调，参数为加载的资源对象。</param>
        /// <param name="onFailure">加载失败回调，参数为错误信息（可选）。</param>
        public void LoadAssetAsync(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure = null)
        {
            if (m_PipelineInitialized)
            {
                // 通过核心管道加载
                LoadAssetAsyncViaPipeline(assetPath, assetType, onSuccess, onFailure);
            }
            else
            {
                // 回退到直接加载
                LoadAssetAsyncDirect(assetPath, assetType, onSuccess, onFailure);
            }
        }

        /// <summary>
        /// 异步加载资源，返回 Task&lt;T&gt;。
        ///
        /// 将回调式的 LoadAssetAsync 封装为标准 C# Task，
        /// 支持 async/await 语法。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="assetPath">资源路径（res:// 协议）。</param>
        /// <returns>加载完成的资源实例。</returns>
        public Task<T> LoadAssetAsync<T>(string assetPath) where T : class
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

            LoadAssetAsync(
                assetPath,
                typeof(T),
                asset =>
                {
                    if (asset is T result)
                    {
                        tcs.TrySetResult(result);
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException(
                            Utility.Text.Format("Loaded asset '{0}' is not of type '{1}'.",
                                assetPath, typeof(T).Name)));
                    }
                },
                errorMsg =>
                {
                    tcs.TrySetException(new InvalidOperationException(
                        Utility.Text.Format("Failed to load asset '{0}': {1}",
                            assetPath, errorMsg ?? "Unknown error")));
                });

            return tcs.Task;
        }

        // ================================================================
        //  文件数据加载方法（始终走 FileAccess 直接读取）
        // ================================================================

        /// <summary>
        /// 加载文件二进制数据。
        ///
        /// 使用 FileAccess 读取文件的原始字节数据。
        /// 适用于 Config/DataTable 等需要原始数据文件的场景。
        /// 文件数据加载不走资源管道。
        /// </summary>
        /// <param name="filePath">文件路径（如 "res://Data/Config.txt"）。</param>
        /// <returns>文件的字节数组，如果加载失败返回 null。</returns>
        public byte[] LoadBinary(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Log.Warning("File path is invalid.");
                return null;
            }

            if (!FileAccess.FileExists(filePath))
            {
                Log.Warning("File '{0}' does not exist.", filePath);
                return null;
            }

            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Log.Warning("Can not open file '{0}', error: {1}.",
                    filePath, FileAccess.GetOpenError());
                return null;
            }

            long length = (long)file.GetLength();
            return file.GetBuffer(length);
        }

        /// <summary>
        /// 加载文件文本数据。
        ///
        /// 使用 FileAccess 读取文件的文本内容。
        /// 适用于 Config 等需要文本格式数据的场景。
        /// 文件数据加载不走资源管道。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <returns>文件的文本内容，如果加载失败返回 null。</returns>
        public string LoadText(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Log.Warning("File path is invalid.");
                return null;
            }

            if (!FileAccess.FileExists(filePath))
            {
                Log.Warning("File '{0}' does not exist.", filePath);
                return null;
            }

            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                Log.Warning("Can not open file '{0}', error: {1}.",
                    filePath, FileAccess.GetOpenError());
                return null;
            }

            return file.GetAsText();
        }

        // ================================================================
        //  资源检查与释放
        // ================================================================

        /// <summary>
        /// 检查资源或文件是否存在。
        ///
        /// 管道模式下通过核心框架的版本列表查找。
        /// 直接模式下同时检查 Godot 资源系统和文件系统。
        /// </summary>
        /// <param name="assetPath">资源或文件路径。</param>
        /// <returns>是否存在。</returns>
        public bool HasAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (m_PipelineInitialized)
            {
                // 通过核心管道查找
                var result = m_ResourceManager.HasAsset(assetPath);
                return result != HasAssetResult.NotExist && result != HasAssetResult.NotReady;
            }

            // 直接模式：检查 Godot 资源系统和文件系统
            if (Godot.ResourceLoader.Exists(assetPath))
            {
                return true;
            }

            return FileAccess.FileExists(assetPath);
        }

        /// <summary>
        /// 卸载（释放）资源。
        ///
        /// 在 Godot 中，资源由引擎的引用计数系统自动管理。
        /// 此方法目前为空实现，预留供未来使用。
        /// </summary>
        /// <param name="asset">要卸载的资源对象。</param>
        public void UnloadAsset(object asset)
        {
            // Godot 引擎自动管理资源生命周期
            // 当资源引用计数为零时，引擎会自动回收
        }

        // ================================================================
        //  内部方法
        // ================================================================

        /// <summary>
        /// 通过核心管道异步加载资源。
        ///
        /// 使用核心框架的 ResourceManager.LoadAsset 方法，
        /// 经过版本列表查找 → TaskPool → Agent → Helper 的完整管道。
        /// 加载完成的资源会自动注册到对象池缓存。
        /// </summary>
        private void LoadAssetAsyncViaPipeline(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure)
        {
            var callbacks = new LoadAssetCallbacks(
                (name, asset, duration, userData) => onSuccess?.Invoke(asset),
                (name, status, errorMessage, userData) => onFailure?.Invoke(errorMessage));
            m_ResourceManager.LoadAsset(assetPath, assetType, Constant.DefaultPriority, callbacks, null);
        }

        /// <summary>
        /// 通过 Godot ResourceLoader 直接异步加载资源（回退方案）。
        ///
        /// 使用 Godot 的 ResourceLoader.LoadThreadedRequest 开始异步加载，
        /// 然后在 _Process 中轮询加载状态，完成后触发回调。
        /// </summary>
        private void LoadAssetAsyncDirect(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                onFailure?.Invoke("Asset path is invalid.");
                return;
            }

            if (!Godot.ResourceLoader.Exists(assetPath, assetType?.Name))
            {
                onFailure?.Invoke(Utility.Text.Format("Asset '{0}' does not exist.", assetPath));
                return;
            }

            // 发起异步加载请求
            var error = Godot.ResourceLoader.LoadThreadedRequest(assetPath, assetType?.Name);
            if (error != Error.Ok)
            {
                onFailure?.Invoke(Utility.Text.Format(
                    "LoadThreadedRequest failed for '{0}', error: {1}.", assetPath, error));
                return;
            }

            // 添加到轮询队列
            m_AsyncLoadTasks.Add(new AsyncLoadTask
            {
                AssetPath = assetPath,
                AssetType = assetType,
                OnSuccess = onSuccess,
                OnFailure = onFailure
            });
        }

        /// <summary>
        /// 轮询所有直接模式异步加载任务的状态。
        ///
        /// 在每帧的 _Process 中调用，检查每个异步任务是否已完成。
        /// 完成后触发对应的成功/失败回调并从队列中移除。
        /// </summary>
        private void PollAsyncLoadTasks()
        {
            if (m_AsyncLoadTasks.Count == 0)
            {
                return;
            }

            for (int i = m_AsyncLoadTasks.Count - 1; i >= 0; i--)
            {
                var task = m_AsyncLoadTasks[i];
                var progress = new Godot.Collections.Array();
                var status = Godot.ResourceLoader.LoadThreadedGetStatus(task.AssetPath, progress);

                switch (status)
                {
                    case ResourceLoader.ThreadLoadStatus.Loaded:
                        // 加载完成，获取资源
                        var resource = Godot.ResourceLoader.LoadThreadedGet(task.AssetPath);
                        if (resource != null)
                        {
                            task.OnSuccess?.Invoke(resource);
                        }
                        else
                        {
                            task.OnFailure?.Invoke(Utility.Text.Format(
                                "LoadThreadedGet returned null for '{0}'.", task.AssetPath));
                        }

                        m_AsyncLoadTasks.RemoveAt(i);
                        break;

                    case ResourceLoader.ThreadLoadStatus.Failed:
                        // 加载失败
                        task.OnFailure?.Invoke(Utility.Text.Format(
                            "Async load failed for '{0}'.", task.AssetPath));
                        m_AsyncLoadTasks.RemoveAt(i);
                        break;

                    case ResourceLoader.ThreadLoadStatus.InvalidResource:
                        // 无效资源
                        task.OnFailure?.Invoke(Utility.Text.Format(
                            "Invalid resource '{0}'.", task.AssetPath));
                        m_AsyncLoadTasks.RemoveAt(i);
                        break;

                        // ThreadLoadStatus.InProgress: 继续等待
                        // ThreadLoadStatus.NotStarted: 继续等待
                }
            }
        }

        /// <summary>
        /// 异步加载任务内部结构（直接模式专用）。
        /// 用于跟踪每个直接异步加载请求的状态和回调。
        /// </summary>
        private class AsyncLoadTask
        {
            /// <summary>
            /// 资源路径。
            /// </summary>
            public string AssetPath;

            /// <summary>
            /// 资源类型。
            /// </summary>
            public Type AssetType;

            /// <summary>
            /// 加载成功回调。
            /// </summary>
            public Action<object> OnSuccess;

            /// <summary>
            /// 加载失败回调。
            /// </summary>
            public Action<string> OnFailure;
        }
    }
}
