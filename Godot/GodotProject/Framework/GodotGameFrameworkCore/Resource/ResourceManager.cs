using GameFramework;
using GameFramework.Resource;
using Godot;
using GodotGameFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GameFramework.Resource
{
    internal sealed partial class ResourceManager : GameFrameworkModule, IResourceManager
    {
        public const string GameFrameworkVersionData = "GameFrameworkVersion.dat";
        public const string SubPack = "subpackages";
        public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;
        private readonly Queue<LoadAssetTask> m_TaskPool;
        private string m_ReadWritePath;
        PackVersionList m_PackVersionList;
        public PackVersionList PackVersionList => m_PackVersionList;

        public ResourceManager()
        {
            m_TaskPool = new Queue<LoadAssetTask>();
        }

        public void SetReadWritePath(string readWritePath = null)
        {
            m_ReadWritePath = readWritePath ?? ProjectSettings.GlobalizePath("user://");
            switch (ResourceMode)
            {
                case ResourceMode.Package:
                    Log.Info("单机包只有默认只有主包,无需版本对比");
                    break;
                case ResourceMode.Updatable:
                    DeserializeUpdatablePackVersion();
                    break;
            }
        }




        public void SetResourceMode(ResourceMode mode) => ResourceMode = mode;

        public HasAssetResult HasAsset(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return HasAssetResult.NotExist;
            if (Godot.ResourceLoader.Exists(assetName))
                return HasAssetResult.AssetOnDisk;
            if (FileAccess.FileExists(assetName) && assetName.EndsWith(".bytes"))
                return HasAssetResult.BinaryOnDisk;
            return HasAssetResult.NotExist;
        }

        public int GetBinaryLength(string binaryAssetName)
        {
            if (!FileAccess.FileExists(binaryAssetName)) return -1;
            using var file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
            return file != null ? (int)file.GetLength() : -1;
        }
        public void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks, object userData)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                loadAssetCallbacks.LoadAssetFailureCallback?.Invoke(
                    assetName, LoadResourceStatus.NotExist, "Asset name is invalid.", userData);
                return;
            }

            if (!Godot.ResourceLoader.Exists(assetName))
            {
                loadAssetCallbacks.LoadAssetFailureCallback?.Invoke(
                    assetName, LoadResourceStatus.NotExist,
                    Utility.Text.Format("Asset '{0}' does not exist.", assetName), userData);
                return;
            }

            ResourceLoader.LoadThreadedRequest(assetName);
            m_TaskPool.Enqueue(LoadAssetTask.Create(assetName, priority, loadAssetCallbacks, userData));
        }
        public void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData)
        {
            if (string.IsNullOrEmpty(binaryAssetName))
            {
                loadBinaryCallbacks.LoadBinaryFailureCallback?.Invoke(
                    binaryAssetName, LoadResourceStatus.NotExist, "Binary asset name is invalid.", userData);
                return;
            }
            if (!FileAccess.FileExists(binaryAssetName))
            {
                loadBinaryCallbacks.LoadBinaryFailureCallback?.Invoke(
                    binaryAssetName, LoadResourceStatus.NotExist,
                    Utility.Text.Format("Binary asset '{0}' does not exist.", binaryAssetName), userData);
                return;
            }

            using FileAccess file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
            if (file != null)
            {
                loadBinaryCallbacks.LoadBinarySuccessCallback?.Invoke(binaryAssetName, file.GetBuffer((int)file.GetLength()), 0, userData);
            }
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (m_TaskPool.Count > 0)
            {
                LoadAssetTask task = m_TaskPool.Peek();
                var state = ResourceLoader.LoadThreadedGetStatus(task.AssetPath);

                switch (state)
                {
                    case ResourceLoader.ThreadLoadStatus.Loaded:
                        {
                            var result = ResourceLoader.LoadThreadedGet(task.AssetPath);
                            task.Callbacks.LoadAssetSuccessCallback?.Invoke(
                                task.AssetPath, result, task.Duration, task.UserData);
                            m_TaskPool.Dequeue();
                            ReferencePool.Release(task);
                            break;
                        }
                    case ResourceLoader.ThreadLoadStatus.InProgress:
                        {
                            task.Duration += elapseSeconds;
                            task.Callbacks.LoadAssetUpdateCallback?.Invoke(
                                task.AssetPath, task.Duration, task.UserData);
                            break;  // 等下一帧
                        }
                    case ResourceLoader.ThreadLoadStatus.Failed:
                    case ResourceLoader.ThreadLoadStatus.InvalidResource:
                        {
                            task.Callbacks.LoadAssetFailureCallback?.Invoke(
                                task.AssetPath, LoadResourceStatus.AssetError,
                                Utility.Text.Format("Failed to load '{0}'.", task.AssetPath), task.UserData);
                            m_TaskPool.Dequeue();
                            ReferencePool.Release(task);
                            break;
                        }
                }
            }
        }

        internal override void Shutdown()
        {
            m_TaskPool.Clear();
        }

        public void DeserializeUpdatablePackVersion()
        {
            // 尝试读取本地版本信息
            string filePath = System.IO.Path.Combine(m_ReadWritePath, GameFrameworkVersionData);
            if (!System.IO.File.Exists(filePath))
            {
                Log.Info("[ResourceManager] 热更模式下，版本文件不存在 {0}", filePath);
                return;
            }

            try
            {
                using var content = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
                m_PackVersionList = Utility.Json.ToObject<PackVersionList>(content?.GetAsText());

                if (m_PackVersionList.Packs == null || m_PackVersionList.Packs.Length == 0)
                {
                    Log.Warning("[ResourceManager] 版本文件无包记录。");
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[ResourceManager] 版本文件解析失败: {0}", ex.Message);
            }
        }
    }
}
