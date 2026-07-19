using GameFramework;
using Godot;
using System;

namespace GameFramework.Resource
{
    internal sealed partial class ResourceManager : GameFrameworkModule, IResourceManager
    {
        public const string GameFrameworkVersionData = "GameFrameworkVersion.dat";
        public const string SubPack = "subpackages";

        public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;

        /// <summary>
        /// 当前本地缓存的版本清单（与 user://GameFrameworkVersion.dat 同步）。
        /// 由 ProcedureUpdate 在加载后写入，全局统一从此读取。
        /// </summary>
        public static PackVersionList LocalPackVersionList { get; set; }

        private TaskPool<LoadAssetTask> m_AssetTaskPool;
        private TaskPool<LoadBinaryTask> m_BinaryTaskPool;
        private string m_ReadWritePath;

        public ResourceManager()
        {
            m_AssetTaskPool = new TaskPool<LoadAssetTask>();
            m_BinaryTaskPool = new TaskPool<LoadBinaryTask>();
        }

        public void SetReadWritePath(string readWritePath = null)
        {
            m_ReadWritePath = readWritePath ?? ProjectSettings.GlobalizePath("user://");
        }
        /// <summary>
        /// 最大并发加载数（= Agent 数量），TaskPool 的优先级调度 + 并发控制。
        /// </summary>
        public void SetLoadAssetAgentCount(int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                m_AssetTaskPool.AddAgent(new LoadAssetAgent());
            }
        }

        /// <summary>
        /// 二进制异步加载代理数量（默认 2，因为大文件 IO 不需要太多并发）。
        /// </summary>
        public void SetLoadBinaryAgentCount(int agentCount)
        {
            for (int i = 0; i < agentCount; i++)
            {
                m_BinaryTaskPool.AddAgent(new LoadBinaryAgent());
            }
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

        /// <summary>
        /// 异步加载二进制文件（线程池 IO，每帧轮询完成回调）。
        /// 适合大文件读取场景，不阻塞主线程。
        /// </summary>
        public void LoadBinaryAsync(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData)
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

            var task = LoadBinaryTask.Create(binaryAssetName, loadBinaryCallbacks, userData);
            m_BinaryTaskPool.AddTask(task);
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // TaskPool 内部遍历所有 WorkingAgent
            // 有闲 Agent 时从等待队列取任务（按优先级降序）
            m_AssetTaskPool.Update(elapseSeconds, realElapseSeconds);
            m_BinaryTaskPool.Update(elapseSeconds, realElapseSeconds);
        }

        internal override void Shutdown()
        {
            m_AssetTaskPool.Shutdown();
            m_BinaryTaskPool.Shutdown();
        }
    }
}
