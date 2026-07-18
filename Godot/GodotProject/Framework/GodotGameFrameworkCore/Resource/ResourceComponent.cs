using Calcatz.EzpzInspector;
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
        private const int DefaultPriority = 0;
        private EventComponent m_EventComponent;
        private IResourceManager m_ResourceManager;
        [Export]
        private ResourceMode _resourceMode = ResourceMode.Package;
        [Export]
        private UpdateSettingRes _UpdateSettingRes;

        /// <summary>
        /// 获取更新配置（RemoteUrl 等）。
        /// </summary>
        public UpdateSettingRes UpdateSettingRes => _UpdateSettingRes;

        private LoadAssetCallbacks m_LoadAssetCallbacks;

        public ResourceMode ResourceMode
        {
            get => _resourceMode;
            set => _resourceMode = value;
        }

        private readonly Dictionary<string, TaskCompletionSource<Godot.Resource>> m_LoadingTasks = new();
        public override void OnInit()
        {
            base.OnInit();
            m_LoadAssetCallbacks = new LoadAssetCallbacks(LoadAssetSuccessCallback, LoadAssetFailureCallback, LoadAssetUpdateCallback);
            m_ResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            m_ResourceManager.SetResourceMode(_resourceMode);
            m_ResourceManager.SetReadWritePath(ProjectSettings.GlobalizePath("user://"));

            Log.Info("[ResourceComponent] Initialized. Mode: {0}", _resourceMode);
            ProcessMode = ProcessModeEnum.Always;
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
        /// 加载资源。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T LoadAsset<T>(string path) where T : Godot.Resource
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!Exists(path)) return null;
            try
            {
                return (T)Godot.ResourceLoader.Load(path);
            }
            catch (Exception ex)
            {
                Log.Warning("[ResourceComponent] LoadAsset failed: {0}", ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 检查资源是否存在。
        /// </summary>
        public bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) && (Godot.ResourceLoader.Exists(path) || FileAccess.FileExists(path));
        }
        public Task<Godot.Resource> LoadAssetAsync(string path) => LoadAssetAsync(path, DefaultPriority);
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

    }
}
