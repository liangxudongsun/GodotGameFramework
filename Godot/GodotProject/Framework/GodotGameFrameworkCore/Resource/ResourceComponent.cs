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
    /// <summary>资源组件。支持管道模式（核心 ResourceManager）和直接模式（Godot ResourceLoader）两种加载方式。</summary>
    public sealed partial class ResourceComponent : GameFrameworkComponent
    {
        private IResourceManager m_ResourceManager;
        private readonly List<AsyncLoadTask> m_AsyncLoadTasks = new List<AsyncLoadTask>();
        private bool m_PipelineInitialized = false;
        private bool m_EditorMode = false;

        [Export] public int LoadResourceAgentHelperCount = 1;
        [Export] public string ReadOnlyPath = "res://";
        [Export] public string ReadWritePath = "user://";
        [Export] public string GameVersion = "1.0.0";
        [Export] public int InternalResourceVersion = 1;
        [Export] public bool UseResourcePipeline = true;
        [Export] private string m_ResourceHelperTypeName = "GodotGameFramework.Resource.DefaultResourceHelper";
        [Export] private string m_LoadResourceAgentHelperTypeName = "GodotGameFramework.Resource.DefaultLoadResourceAgentHelper";
        private ResourceHelperBase m_ResourceHelper;

        /// <summary>当前异步加载任务数量（仅直接模式）。</summary>
        public int AsyncLoadTaskCount => m_AsyncLoadTasks.Count;

        /// <summary>管道是否已初始化。</summary>
        public bool PipelineInitialized => m_PipelineInitialized;

        /// <summary>当前资源数量（管道模式下有效）。</summary>
        public int AssetCount => m_PipelineInitialized ? m_ResourceManager.AssetCount : 0;

        /// <summary>当前资源信息数量（管道模式下有效）。</summary>
        public int ResourceCount => m_PipelineInitialized ? m_ResourceManager.ResourceCount : 0;

        public override void OnInit()
        {
            base.OnInit();

            m_ResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            if (m_ResourceManager == null) { Log.Fatal("Resource manager is invalid."); return; }

            m_EditorMode = OS.HasFeature("editor");
            m_ResourceManager.SetReadOnlyPath(ReadOnlyPath);
            m_ResourceManager.SetReadWritePath(ReadWritePath);
            m_ResourceManager.SetResourceMode(ResourceMode.Package);
            m_ResourceManager.SetObjectPoolManager(GameFrameworkEntry.GetModule<IObjectPoolManager>());

            ResourceHelperBase helperBase = Helper.CreateHelper(m_ResourceHelperTypeName, m_ResourceHelper);
            m_ResourceManager.SetResourceHelper(helperBase);
            AddChild(helperBase);
            helperBase.Name = m_ResourceHelperTypeName;
            m_ResourceHelper = helperBase;
            for (int i = 0; i < LoadResourceAgentHelperCount; i++)
            {
                LoadResourceAgentHelperBase agentHelper = Create(m_LoadResourceAgentHelperTypeName) as LoadResourceAgentHelperBase;
                AddChild(agentHelper);
                agentHelper.Name = Utility.Text.Format("{0}_{1}", m_LoadResourceAgentHelperTypeName, i);
                m_ResourceManager.AddLoadResourceAgentHelper(agentHelper);
            }

            if (UseResourcePipeline)
            {
                GDFBuiltinVersionListSerializer.RegisterPackageDeserializeCallbacks(
                    m_ResourceManager.PackageVersionListSerializer);
                if (m_EditorMode)
                {
                    GDFBuiltinVersionListSerializer.RegisterPackageSerializeCallbacks(
                        m_ResourceManager.PackageVersionListSerializer);
                    string versionListPath = Utility.Path.GetRegularPath(ReadOnlyPath + "GameFrameworkVersion.dat");
                    GDFResourceBuilder.BuildVersionList(ReadOnlyPath, versionListPath, GameVersion, InternalResourceVersion);
                }
                m_ResourceManager.InitResources(OnInitResourcesComplete);
            }
            ProcessMode = ProcessModeEnum.Always;
        }

        private void OnInitResourcesComplete()
        {
            m_PipelineInitialized = true;
            Log.Info("Resource pipeline initialized. Assets: {0}, Resources: {1}",
                m_ResourceManager.AssetCount, m_ResourceManager.ResourceCount);
        }

        public override void OnUpdate(double delta)
        {
            base.OnUpdate(delta);
            if (m_AsyncLoadTasks.Count > 0) PollAsyncLoadTasks();
        }

        // ================================================================
        //  同步加载
        // ================================================================

        /// <summary>同步加载资源。使用 Godot ResourceLoader.Load。</summary>
        public T LoadAsset<T>(string assetPath) where T : class
        {
            if (string.IsNullOrEmpty(assetPath)) { Log.Warning("Asset path is invalid."); return null; }
            var resource = Godot.ResourceLoader.Load<T>(assetPath);
            if (resource == null) Log.Warning("Can not load asset '{0}'.", assetPath);
            return resource;
        }

        /// <summary>同步加载资源（指定类型）。byte[] 类型走 FileAccess 读取。</summary>
        public object LoadAsset(string assetPath, Type assetType)
        {
            if (string.IsNullOrEmpty(assetPath)) { Log.Warning("Asset path is invalid."); return null; }
            var resource = Godot.ResourceLoader.Load(assetPath, assetType?.Name);
            if (resource == null) Log.Warning("Can not load asset '{0}' with type '{1}'.", assetPath, assetType?.Name ?? "null");
            return resource;
        }

        // ================================================================
        //  异步加载
        // ================================================================

        /// <summary>异步加载资源。管道模式下走核心管道，否则走 Godot LoadThreadedRequest。</summary>
        public void LoadAssetAsync(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure = null)
        {
            if (m_PipelineInitialized)
                LoadAssetAsyncViaPipeline(assetPath, assetType, onSuccess, onFailure);
            else
                LoadAssetAsyncDirect(assetPath, assetType, onSuccess, onFailure);
        }

        /// <summary>异步加载资源，返回 Task&lt;T&gt;。</summary>
        public Task<T> LoadAssetAsync<T>(string assetPath) where T : class
        {
            var tcs = new TaskCompletionSource<T>();
            LoadAssetAsync(assetPath, typeof(T),
                asset => { if (asset is T result) tcs.TrySetResult(result); else tcs.TrySetException(new InvalidOperationException($"Type mismatch for '{assetPath}'")); },
                errorMsg => tcs.TrySetException(new InvalidOperationException($"Failed to load '{assetPath}': {errorMsg}")));
            return tcs.Task;
        }

        // ================================================================
        //  文件数据加载
        // ================================================================

        /// <summary>加载文件二进制数据（FileAccess 直接读取）。</summary>
        public byte[] LoadBinary(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) { Log.Warning("File path is invalid."); return null; }
            if (!FileAccess.FileExists(filePath)) { Log.Warning("File '{0}' does not exist.", filePath); return null; }
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null) { Log.Warning("Can not open file '{0}'.", filePath); return null; }
            return file.GetBuffer((long)file.GetLength());
        }

        /// <summary>加载文件文本数据（FileAccess 直接读取）。</summary>
        public string LoadText(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) { Log.Warning("File path is invalid."); return null; }
            if (!FileAccess.FileExists(filePath)) { Log.Warning("File '{0}' does not exist.", filePath); return null; }
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            if (file == null) { Log.Warning("Can not open file '{0}'.", filePath); return null; }
            return file.GetAsText();
        }

        // ================================================================
        //  资源检查与释放
        // ================================================================

        /// <summary>检查资源或文件是否存在。管道模式下走核心版本列表，直接模式查 Godot 文件系统。</summary>
        public bool HasAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            if (m_PipelineInitialized)
            {
                var result = m_ResourceManager.HasAsset(assetPath);
                return result != HasAssetResult.NotExist && result != HasAssetResult.NotReady;
            }
            return Godot.ResourceLoader.Exists(assetPath) || FileAccess.FileExists(assetPath);
        }

        /// <summary>卸载资源。Godot 引擎自动管理资源生命周期，当前为空实现。</summary>
        public void UnloadAsset(object asset) { }

        // ================================================================
        //  内部方法
        // ================================================================

        private void LoadAssetAsyncViaPipeline(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure)
        {
            var callbacks = new LoadAssetCallbacks(
                (name, asset, duration, userData) => onSuccess?.Invoke(asset),
                (name, status, errorMessage, userData) => onFailure?.Invoke(errorMessage));
            m_ResourceManager.LoadAsset(assetPath, assetType, Constant.DefaultPriority, callbacks, null);
        }

        private void LoadAssetAsyncDirect(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(assetPath)) { onFailure?.Invoke("Asset path is invalid."); return; }
            if (!Godot.ResourceLoader.Exists(assetPath, assetType?.Name))
            { onFailure?.Invoke($"Asset '{assetPath}' does not exist."); return; }

            var error = Godot.ResourceLoader.LoadThreadedRequest(assetPath, assetType?.Name);
            if (error != Error.Ok)
            { onFailure?.Invoke($"LoadThreadedRequest failed for '{assetPath}': {error}"); return; }

            m_AsyncLoadTasks.Add(new AsyncLoadTask
            {
                AssetPath = assetPath,
                AssetType = assetType,
                OnSuccess = onSuccess,
                OnFailure = onFailure
            });
        }

        private void PollAsyncLoadTasks()
        {
            if (m_AsyncLoadTasks.Count == 0) return;
            for (int i = m_AsyncLoadTasks.Count - 1; i >= 0; i--)
            {
                var task = m_AsyncLoadTasks[i];
                var progress = new Godot.Collections.Array();
                var status = Godot.ResourceLoader.LoadThreadedGetStatus(task.AssetPath, progress);
                switch (status)
                {
                    case ResourceLoader.ThreadLoadStatus.Loaded:
                        var resource = Godot.ResourceLoader.LoadThreadedGet(task.AssetPath);
                        if (resource != null) task.OnSuccess?.Invoke(resource);
                        else task.OnFailure?.Invoke($"LoadThreadedGet returned null for '{task.AssetPath}'.");
                        m_AsyncLoadTasks.RemoveAt(i);
                        break;
                    case ResourceLoader.ThreadLoadStatus.Failed:
                        task.OnFailure?.Invoke($"Async load failed for '{task.AssetPath}'.");
                        m_AsyncLoadTasks.RemoveAt(i);
                        break;
                    case ResourceLoader.ThreadLoadStatus.InvalidResource:
                        task.OnFailure?.Invoke($"Invalid resource '{task.AssetPath}'.");
                        m_AsyncLoadTasks.RemoveAt(i);
                        break;
                }
            }
        }

        private class AsyncLoadTask
        {
            public string AssetPath;
            public Type AssetType;
            public Action<object> OnSuccess;
            public Action<string> OnFailure;
        }
    }
}
