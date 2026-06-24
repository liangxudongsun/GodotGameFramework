//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameConfig.Constant;
using GameFramework;
using GameFramework.ObjectPool;
using GameFramework.Resource;
using Godot;
using System;

namespace GodotGameFramework.Resource
{
    /// <summary>资源组件。Package 模式直接使用 Godot 原生 ResourceLoader 加载；Updatable 模式（未实现）需要版本列表 + 下载管线。</summary>
    public sealed partial class ResourceComponent : GameFrameworkComponent
    {
        private IResourceManager m_ResourceManager;
        private ResourceMode m_EffectiveResourceMode;
        [Export] public int LoadResourceAgentHelperCount = 1;
        [Export]
        private string m_ResourceHelperTypeName = "GodotGameFramework.Resource.DefaultResourceHelper";
        [Export] private string m_LoadResourceAgentHelperTypeName = "GodotGameFramework.Resource.DefaultLoadResourceAgentHelper";

        private ResourceMode _resourceMode = ResourceMode.Package;

        /// <summary>
        /// 资源模式。
        /// - Package：单机模式，直接使用 Godot 原生 ResourceLoader 加载。
        /// - Updatable / UpdatableWhilePlaying：需要版本列表 + 下载管线（依赖 IDownloadManager
        ///   和 IFileSystemManager），当前尚未实现，运行时会自动回退到 Package 模式并输出警告日志。
        /// </summary>
        [Export]
        private ResourceMode ResourceMode
        {
            get => _resourceMode;
            set => _resourceMode = value;
        }
        private ResourceHelperBase m_ResourceHelper;

        /// <summary>当前实际生效的资源模式（未实现模式会回退到 Package）。</summary>
        public ResourceMode EffectiveResourceMode => m_EffectiveResourceMode;

        /// <summary>当前资源数量（管道模式下有效）。</summary>
        public int AssetCount => m_ResourceManager?.AssetCount ?? 0;

        /// <summary>当前资源信息数量（管道模式下有效）。</summary>
        public int ResourceCount => m_ResourceManager?.ResourceCount ?? 0;

        public override void OnInit()
        {
            base.OnInit();

            // 编辑器资源模式：跳过管道初始化，Entity/Sound 等模块由 EditorResourceManager 直接加载
            if (GF.Base.EditorResourceMode)
            {
                m_ResourceManager = GF.Base.EditorResourceManager;
                if (m_ResourceManager == null) { Log.Fatal("EditorResourceManager is invalid."); return; }

                Log.Info("[ResourceComponent] EditorResourceMode — Godot native ResourceLoader, no pipeline.");
                ProcessMode = ProcessModeEnum.Always;
                return;
            }

            // 运行时模式：完整管道
            m_ResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            if (m_ResourceManager == null) { Log.Fatal("Resource manager is invalid."); return; }

            m_EffectiveResourceMode = ResolveResourceMode(_resourceMode);

            m_ResourceManager.SetReadOnlyPath(GameFolderConstant.ReadOnlyPath);
            m_ResourceManager.SetReadWritePath(GameFolderConstant.ReadWritePath);
            m_ResourceManager.SetResourceMode(m_EffectiveResourceMode);
            m_ResourceManager.SetObjectPoolManager(GameFrameworkEntry.GetModule<IObjectPoolManager>());

            ResourceHelperBase helperBase = Helper.CreateHelper(m_ResourceHelperTypeName, m_ResourceHelper);
            m_ResourceManager.SetResourceHelper(helperBase);
            AddChild(helperBase);
            helperBase.Name = m_ResourceHelperTypeName;
            m_ResourceHelper = helperBase;
            for (int i = 0; i < LoadResourceAgentHelperCount; i++)
            {
                LoadResourceAgentHelperBase agentHelper = Create(m_LoadResourceAgentHelperTypeName) as LoadResourceAgentHelperBase;
                helperBase.AddChild(agentHelper);
                agentHelper.Name = Utility.Text.Format("{0}_{1}", m_LoadResourceAgentHelperTypeName, i);
                m_ResourceManager.AddLoadResourceAgentHelper(agentHelper);
            }

            // Package 模式：版本列表管道初始化
            // Updatable 模式已在 ResolveResourceMode 中回退到 Package 并输出 Warning
            InitRuntimeMode();
            ProcessMode = ProcessModeEnum.Always;
        }


        /// <summary>
        /// 注册版本列表反序列化回调，然后加载 GameFrameworkVersion.dat。
        /// </summary>
        private void InitRuntimeMode()
        {
            GDFBuiltinVersionListSerializer.RegisterPackageDeserializeCallbacks(
                m_ResourceManager.PackageVersionListSerializer);

            m_ResourceManager.InitResources(OnInitResourcesComplete);
        }

        private void OnInitResourcesComplete()
        {
            Log.Info("[ResourceComponent] Pipeline initialized. Assets: {0}, Resources: {1}",
                m_ResourceManager.AssetCount, m_ResourceManager.ResourceCount);
        }


        /// <summary>
        /// 检查资源模式是否可用。Updatable / UpdatableWhilePlaying 模式依赖
        /// IDownloadManager 和 IFileSystemManager，当前尚未在 Godot 层实现，
        /// 自动回退到 Package 模式并输出警告日志。
        /// </summary>
        private ResourceMode ResolveResourceMode(ResourceMode requestedMode)
        {
            switch (requestedMode)
            {
                case ResourceMode.Package:
                    return ResourceMode.Package;

                case ResourceMode.Updatable:
                    Log.Warning(
                        "[ResourceComponent] Updatable mode is not yet implemented. "
                        + "It requires IDownloadManager and IFileSystemManager bindings in the Godot layer. "
                        + "Falling back to Package mode.");
                    return ResourceMode.Package;

                case ResourceMode.UpdatableWhilePlaying:
                    Log.Warning(
                        "[ResourceComponent] UpdatableWhilePlaying mode is not yet implemented. "
                        + "It requires IDownloadManager and IFileSystemManager bindings in the Godot layer. "
                        + "Falling back to Package mode.");
                    return ResourceMode.Package;

                default:
                    Log.Warning(
                        "[ResourceComponent] Unknown ResourceMode '{0}'. Falling back to Package mode.",
                        requestedMode);
                    return ResourceMode.Package;
            }
        }

        public override void OnUpdate(double delta)
        {
            base.OnUpdate(delta);
            // No per-frame logic needed; pipeline callbacks are event-driven.
            // Keep SetProcess(true) for potential future use (e.g. LoadResourceAgent polling).
        }

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
        /// <summary>加载文件二进制数据（FileAccess 直接读取）不使用异步。</summary>
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

        /// <summary>异步加载资源</summary>
        public void LoadAssetAsync(string assetPath, Type assetType,
            Action<object> onSuccess, Action<string> onFailure = null)
        {
            var callbacks = new LoadAssetCallbacks(
                (name, asset, duration, userData) => onSuccess?.Invoke(asset),
                (name, status, errorMessage, userData) => onFailure?.Invoke(errorMessage));
            m_ResourceManager.LoadAsset(assetPath, assetType, Constant.DefaultPriority, callbacks, null);
        }

        /// <summary>检查资源或文件是否存在。管道模式下走核心版本列表，直接模式查 Godot 文件系统。</summary>
        public bool HasAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            if (m_ResourceManager == null) return false;
            var result = m_ResourceManager.HasAsset(assetPath);
            return result != HasAssetResult.NotExist && result != HasAssetResult.NotReady;
        }
    }
}
