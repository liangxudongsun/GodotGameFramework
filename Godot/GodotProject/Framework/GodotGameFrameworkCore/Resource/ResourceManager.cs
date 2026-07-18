using GameFramework;
using Godot;
using System;

namespace GameFramework.Resource
{
    internal sealed partial class ResourceManager : GameFrameworkModule, IResourceManager
    {
        public const string GameFrameworkVersionData = "GameFrameworkVersion.dat";
        public const string SubPack = "subpackages";

        /// <summary>最大并发加载数（= Agent 数量），复用 GF TaskPool 的优先级调度 + 并发控制。</summary>
        private const int MaxConcurrent = 16;

        public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;

        private TaskPool<LoadAssetTask> m_AssetTaskPool;
        private string m_ReadWritePath;

        public ResourceManager()
        {
            m_AssetTaskPool = new TaskPool<LoadAssetTask>();
            for (int i = 0; i < MaxConcurrent; i++)
            {
                m_AssetTaskPool.AddAgent(new LoadAssetAgent());
            }
        }

        public void SetReadWritePath(string readWritePath = null)
        {
            m_ReadWritePath = readWritePath ?? ProjectSettings.GlobalizePath("user://");
        }

        public void SetResourceMode(ResourceMode mode) => ResourceMode = mode;

        public HasAssetResult HasAsset(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return HasAssetResult.NotExist;
            if (ResourceLoader.Exists(assetName))
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

        public void LoadAsset(string assetName, int priority, LoadAssetCallbacks callbacks, object userData)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                callbacks.LoadAssetFailureCallback?.Invoke(
                    assetName, LoadResourceStatus.NotExist, "Asset name is invalid.", userData);
                return;
            }

            if (!ResourceLoader.Exists(assetName))
            {
                callbacks.LoadAssetFailureCallback?.Invoke(
                    assetName, LoadResourceStatus.NotExist,
                    Utility.Text.Format("Asset '{0}' does not exist.", assetName), userData);
                return;
            }

            // TaskPool 按优先级降序调度，Agent 不足时排队等待
            var task = LoadAssetTask.Create(assetName, priority, callbacks, userData);
            m_AssetTaskPool.AddTask(task);
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
                loadBinaryCallbacks.LoadBinarySuccessCallback?.Invoke(
                    binaryAssetName, file.GetBuffer((int)file.GetLength()), 0, userData);
            }
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // TaskPool 内部遍历所有 WorkingAgent
            // 有闲 Agent 时从等待队列取任务（按优先级降序）
            m_AssetTaskPool.Update(elapseSeconds, realElapseSeconds);
        }

        internal override void Shutdown()
        {
            m_AssetTaskPool.Shutdown();
        }
    }
}
