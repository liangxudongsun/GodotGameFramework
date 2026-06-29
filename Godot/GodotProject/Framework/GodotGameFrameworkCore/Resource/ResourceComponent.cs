using GameFramework;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFramework.Resource
{
    public sealed partial class ResourceComponent : GameFrameworkComponent
    {
        private EventComponent m_EventComponent;
        private IResourceManager m_ResourceManager;
        private ResourceMode m_EffectiveResourceMode;
        [Export]
        private ResourceMode _resourceMode = ResourceMode.Package;
        private LoadAssetCallbacks m_LoadAssetCallbacks;

        /// <summary>当前实际生效的资源模式（未实现模式会回退到 Package）。</summary>
        public ResourceMode EffectiveResourceMode => m_EffectiveResourceMode;
        private ResourceMode ResourceMode
        {
            get => _resourceMode;
            set => _resourceMode = value;
        }

        private readonly Dictionary<string, TaskCompletionSource<Godot.Resource>> m_LoadingTasks = new();
        public override void OnInit()
        {
            base.OnInit();
            m_LoadAssetCallbacks = new LoadAssetCallbacks(LoadAssetSuccessCallback, LoadAssetFailureCallback, LoadAssetUpdateCallback, LoadAssetDependencyAssetCallback);
            m_ResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            m_EffectiveResourceMode = ResolveResourceMode(_resourceMode);
            m_ResourceManager.SetResourceMode(m_EffectiveResourceMode);

            Log.Info("[ResourceComponent] Initialized. Mode: {0}", m_EffectiveResourceMode);
            ProcessMode = ProcessModeEnum.Always;
        }

        private ResourceMode ResolveResourceMode(ResourceMode requested)
        {
            switch (requested)
            {
                case ResourceMode.Package:
                    return ResourceMode.Package;

                case ResourceMode.Updatable:
                    Log.Warning("[ResourceComponent] Updatable mode is not yet implemented. " +
                        "Falling back to Package mode.");
                    return ResourceMode.Package;

                case ResourceMode.UpdatableWhilePlaying:
                    Log.Warning("[ResourceComponent] UpdatableWhilePlaying mode is not yet implemented. " +
                        "Falling back to Package mode.");
                    return ResourceMode.Package;

                default:
                    Log.Warning("[ResourceComponent] Unknown ResourceMode '{0}'. Falling back to Package.", requested);
                    return ResourceMode.Package;
            }
        }

        /// <summary>
        /// 同步加载二进制文件。返回 null 表示文件不存在。
        /// </summary>
        public byte[] LoadBinary(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!FileAccess.FileExists(path)) return null;
            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                return file?.GetBuffer((long)file.GetLength());
            }
            catch (Exception ex)
            {
                Log.Warning("[ResourceComponent] LoadBinary failed: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 同步加载文本文件。返回 null 表示文件不存在。
        /// </summary>
        public string LoadText(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!FileAccess.FileExists(path)) return null;
            try
            {
                using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                return file?.GetAsText();
            }
            catch (Exception ex)
            {
                Log.Warning("[ResourceComponent] LoadText failed: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 检查资源是否存在。
        /// </summary>
        public bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) && (Godot.ResourceLoader.Exists(path) || FileAccess.FileExists(path));
        }

        /// <summary>
        /// 异步加载资源。
        /// </summary>
        public Task<Godot.Resource> LoadAssetAsync(string path, int priority, object userData = null)
        {
            var tcs = new TaskCompletionSource<Godot.Resource>();
            if (string.IsNullOrEmpty(path))
            {
                tcs.TrySetException(new ArgumentNullException(nameof(path)));
                return tcs.Task;
            }

            if (!Godot.ResourceLoader.Exists(path))
            {
                tcs.TrySetException(new InvalidOperationException(
                    Utility.Text.Format("Resource '{0}' does not exist.", path)));
                return tcs.Task;
            }

            m_ResourceManager.LoadAsset(path, priority, m_LoadAssetCallbacks, userData);
            if (!m_LoadingTasks.TryAdd(path, tcs))
            {
                tcs.TrySetException(new InvalidOperationException(
                    Utility.Text.Format("Resource '{0}' is already being loaded.", path)));
            }
            return tcs.Task;
        }

        private void LoadAssetSuccessCallback(string entityAssetName, object entityAsset, float duration, object userData)
        {
            if (m_LoadingTasks.TryGetValue(entityAssetName, out var tcs))
            {
                tcs.TrySetResult((Godot.Resource)entityAsset);
                m_LoadingTasks.Remove(entityAssetName);
            }
        }

        private void LoadAssetFailureCallback(string entityAssetName, LoadResourceStatus status, string errorMessage, object userData)
        {
            if (m_LoadingTasks.TryGetValue(entityAssetName, out var tcs))
            {
                tcs.TrySetException(new Exception(Utility.Text.Format(
                    "LoadAssetFailureCallback: {0} {1} {2}", entityAssetName, status, errorMessage)));
                m_LoadingTasks.Remove(entityAssetName);
            }
        }

        private void LoadAssetUpdateCallback(string entityAssetName, float progress, object userData)
        {

        }

        private void LoadAssetDependencyAssetCallback(string entityAssetName, string dependencyAssetName, int loadedCount, int totalCount, object userData)
        {

        }
    }
}
