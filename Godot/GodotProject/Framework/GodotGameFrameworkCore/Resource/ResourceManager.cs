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

        public ResourceManager()
        {
            m_TaskPool = new Queue<LoadAssetTask>();
        }

        public void SetReadWritePath(string readWritePath = null)
        {
            m_ReadWritePath = readWritePath ?? ProjectSettings.GlobalizePath("user://");
            if (ResourceMode == ResourceMode.Updatable)
                DeserializeUpdatablePackVersion();
            else if (ResourceMode == ResourceMode.Package)
                DeserializePackagePackVersion();
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

        private void DeserializeUpdatablePackVersion()
        {
            // 尝试读取本地版本信息
            string filePath = System.IO.Path.Combine(m_ReadWritePath, GameFrameworkVersionData);
            if (!System.IO.File.Exists(filePath))
            {
                Log.Info("[ResourceManager] 版本文件不存在，跳过子包加载: {0}", filePath);
                return;
            }

            try
            {
                using var content = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
                m_PackVersionList = Utility.Json.ToObject<PackVersionList>(content?.GetAsText());

                if (m_PackVersionList.Packs == null || m_PackVersionList.Packs.Length == 0)
                {
                    Log.Warning("[ResourceManager] 版本文件无包记录。");
                    return;
                }

                int loaded = 0;
                foreach (var pack in m_PackVersionList.Packs)
                {
                    string packFileName = pack.Name + ".pck";
                    string subDir = System.IO.Path.Combine(m_ReadWritePath, SubPack);
                    string packPath = System.IO.Path.Combine(subDir, packFileName);

                    if (System.IO.File.Exists(packPath))
                    {
                        bool ok = ProjectSettings.LoadResourcePack(packPath);
                        if (ok)
                        {
                            loaded++;
                            Log.Info("[ResourceManager] 子包加载成功: {0}", packPath);
                        }
                        else
                        {
                            Log.Warning("[ResourceManager] 子包加载失败: {0}", packPath);
                        }
                    }
                    else
                    {
                        Log.Warning("[ResourceManager] 子包文件不存在: {0}", packPath);
                    }
                }

                Log.Info("[ResourceManager] 子包加载完成: {0}/{1}", loaded, m_PackVersionList.Packs.Length);
            }
            catch (Exception ex)
            {
                Log.Warning("[ResourceManager] 版本文件解析失败: {0}", ex.Message);
            }
        }
        private void DeserializePackagePackVersion()
        {
            string projectRoot = ProjectSettings.GlobalizePath("res://");
            string exeDir = OS.HasFeature("editor") ? $"{projectRoot}" + "../../Godot" : System.IO.Path.GetDirectoryName(OS.GetExecutablePath());
            string manifestPath = System.IO.Path.Combine(exeDir, SubPack, GameFrameworkVersionData);
            if (!System.IO.File.Exists(manifestPath))
            {
                Log.Info("[ResourceManager] 清单文件不存在，跳过子包加载: {0}", manifestPath);
                return;
            }

            try
            {
                string content = System.IO.File.ReadAllText(manifestPath);
                m_PackVersionList = System.Text.Json.JsonSerializer.Deserialize<PackVersionList>(content);

                if (m_PackVersionList.Packs == null || m_PackVersionList.Packs.Length == 0)
                {
                    Log.Warning("[ResourceManager] 清单文件无包记录。");
                    return;
                }

                int loaded = 0;

                foreach (var pack in m_PackVersionList.Packs)
                {
                    string packFileName = pack.Name + ".pck";
                    string packPath = System.IO.Path.Combine(exeDir, SubPack, packFileName);

                    if (System.IO.File.Exists(packPath))
                    {
                        bool ok = ProjectSettings.LoadResourcePack(packPath);
                        if (ok)
                        {
                            loaded++;
                            Log.Info("[ResourceManager] 子包加载成功: {0}", packPath);
                        }
                        else
                        {
                            Log.Warning("[ResourceManager] 子包加载失败: {0}", packPath);
                        }
                    }
                    else
                    {
                        Log.Warning("[ResourceManager] 子包文件不存在: {0}", packPath);
                    }
                }

                Log.Info("[ResourceManager] 子包加载完成: {0}/{1}", loaded, m_PackVersionList.Packs.Length);
            }
            catch (Exception ex)
            {
                Log.Warning("[ResourceManager] 清单文件解析失败: {0}", ex.Message);
            }
        }
    }
}
