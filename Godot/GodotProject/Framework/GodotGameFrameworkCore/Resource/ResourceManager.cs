using GameFramework;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GameFramework.Resource
{
    internal sealed partial class ResourceManager : GameFrameworkModule, IResourceManager
    {
        public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;
        private readonly Queue<LoadAssetTask> m_TaskPool;

        public ResourceManager()
        {
            m_TaskPool = new Queue<LoadAssetTask>();
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
    }
}
