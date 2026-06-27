using GameFramework;
using GameFramework.Download;
using GameFramework.FileSystem;
using GameFramework.ObjectPool;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFrameworkCore.Resource
{
	/// <summary>
	/// Godot 编辑器资源管理器。
	/// </summary>
	public partial class EditorResourceManager : IResourceManager
	{
		public string ReadOnlyPath { get; private set; } = "res://";
		public string ReadWritePath { get; private set; } = "user://";
		public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;
		public string CurrentVariant => null;
		public string ApplicableGameVersion => "0.0.0";
		public int InternalResourceVersion => 0;
		public int AssetCount => 0;
		public int ResourceCount => 0;
		public int ResourceGroupCount => 0;
		public string UpdatePrefixUri { get; set; }
		public int GenerateReadWriteVersionListLength { get; set; }
		public string ApplyingResourcePackPath => null;
		public int ApplyWaitingCount => 0;
		public int UpdateRetryCount { get; set; }
		public IResourceGroup UpdatingResourceGroup => null;
		public int UpdateWaitingCount => 0;
		public int UpdateWaitingWhilePlayingCount => 0;
		public int UpdateCandidateCount => 0;
		public int LoadTotalAgentCount => 0;
		public int LoadFreeAgentCount => 0;
		public int LoadWorkingAgentCount => 0;
		public int LoadWaitingTaskCount => 0;
		public float AssetAutoReleaseInterval { get; set; }
		public int AssetCapacity { get; set; }
		public float AssetExpireTime { get; set; }
		public int AssetPriority { get; set; }
		public float ResourceAutoReleaseInterval { get; set; }
		public int ResourceCapacity { get; set; }
		public float ResourceExpireTime { get; set; }
		public int ResourcePriority { get; set; }

		private readonly PackageVersionListSerializer _packageSerializer = new PackageVersionListSerializer();
		private readonly UpdatableVersionListSerializer _updatableSerializer = new UpdatableVersionListSerializer();
		private readonly ReadOnlyVersionListSerializer _readOnlySerializer = new ReadOnlyVersionListSerializer();
		private readonly ReadWriteVersionListSerializer _readWriteSerializer = new ReadWriteVersionListSerializer();
		private readonly ResourcePackVersionListSerializer _resourcePackSerializer = new ResourcePackVersionListSerializer();

		public PackageVersionListSerializer PackageVersionListSerializer => _packageSerializer;
		public UpdatableVersionListSerializer UpdatableVersionListSerializer => _updatableSerializer;
		public ReadOnlyVersionListSerializer ReadOnlyVersionListSerializer => _readOnlySerializer;
		public ReadWriteVersionListSerializer ReadWriteVersionListSerializer => _readWriteSerializer;
		public ResourcePackVersionListSerializer ResourcePackVersionListSerializer => _resourcePackSerializer;

		public event EventHandler<ResourceVerifyStartEventArgs> ResourceVerifyStart;
		public event EventHandler<ResourceVerifySuccessEventArgs> ResourceVerifySuccess;
		public event EventHandler<ResourceVerifyFailureEventArgs> ResourceVerifyFailure;
		public event EventHandler<ResourceApplyStartEventArgs> ResourceApplyStart;
		public event EventHandler<ResourceApplySuccessEventArgs> ResourceApplySuccess;
		public event EventHandler<ResourceApplyFailureEventArgs> ResourceApplyFailure;
		public event EventHandler<ResourceUpdateStartEventArgs> ResourceUpdateStart;
		public event EventHandler<ResourceUpdateChangedEventArgs> ResourceUpdateChanged;
		public event EventHandler<ResourceUpdateSuccessEventArgs> ResourceUpdateSuccess;
		public event EventHandler<ResourceUpdateFailureEventArgs> ResourceUpdateFailure;
		public event EventHandler<ResourceUpdateAllCompleteEventArgs> ResourceUpdateAllComplete;


		public void SetReadOnlyPath(string readOnlyPath) => ReadOnlyPath = readOnlyPath;
		public void SetReadWritePath(string readWritePath) => ReadWritePath = readWritePath;
		public void SetResourceMode(ResourceMode resourceMode) => ResourceMode = resourceMode;
		public void SetCurrentVariant(string currentVariant) { }
		public void SetObjectPoolManager(IObjectPoolManager objectPoolManager) { }
		public void SetFileSystemManager(IFileSystemManager fileSystemManager) { }
		public void SetDownloadManager(IDownloadManager downloadManager) { }
		public void SetDecryptResourceCallback(DecryptResourceCallback decryptResourceCallback) { }
		public void SetResourceHelper(IResourceHelper resourceHelper) { }
		public void AddLoadResourceAgentHelper(ILoadResourceAgentHelper loadResourceAgentHelper) { }


		// ================================================================
		//  HasAsset
		// ================================================================

		public HasAssetResult HasAsset(string assetName)
		{
			if (string.IsNullOrEmpty(assetName))
				return HasAssetResult.NotExist;

			if (Godot.ResourceLoader.Exists(assetName))
				return HasAssetResult.AssetOnDisk;

			if (FileAccess.FileExists(assetName))
				return HasAssetResult.BinaryOnDisk;

			return HasAssetResult.NotExist;
		}

		// ================================================================
		//  LoadAsset — 核心方法，Entity/UI/Sound 最终都调这里
		// ================================================================

		// 实际被调用的重载：LoadAsset(string, int, LoadAssetCallbacks, object)
		public void LoadAsset(string assetName, Type assetType, int priority,
			LoadAssetCallbacks loadAssetCallbacks)
		{
			LoadAssetInternal(assetName, loadAssetCallbacks, null, assetType);
		}

		public void LoadAsset(string assetName, Type assetType, int priority,
			LoadAssetCallbacks loadAssetCallbacks, object userData)
		{
			LoadAssetInternal(assetName, loadAssetCallbacks, userData, assetType);
		}

		public void LoadAsset(string assetName, int priority,
			LoadAssetCallbacks loadAssetCallbacks, object userData)
		{
			LoadAssetInternal(assetName, loadAssetCallbacks, userData, null);
		}

		private void LoadAssetInternal(string assetName, LoadAssetCallbacks loadAssetCallbacks, object userData, Type assetType = null)
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

			// 同步加载并校验类型
			try
			{
				Godot.Resource resource = Godot.ResourceLoader.Load(assetName);

				if (resource != null)
				{
					// 若指定了类型，校验加载结果是否匹配
					if (assetType != null && !assetType.IsInstanceOfType(resource))
					{
						loadAssetCallbacks.LoadAssetFailureCallback?.Invoke(
							assetName, LoadResourceStatus.AssetError,
							Utility.Text.Format("Loaded asset '{0}' is {1}, expected {2}.", assetName, resource.GetType().Name, assetType.Name), userData);
						return;
					}
					loadAssetCallbacks.LoadAssetSuccessCallback?.Invoke(
						assetName, resource, 0f, userData);
				}
				else
				{
					loadAssetCallbacks.LoadAssetFailureCallback?.Invoke(
						assetName, LoadResourceStatus.AssetError,
						Utility.Text.Format("Failed to load asset '{0}'.", assetName), userData);
				}
			}
			catch (Exception e)
			{
				loadAssetCallbacks.LoadAssetFailureCallback?.Invoke(
					assetName, LoadResourceStatus.AssetError,
					Utility.Text.Format("Exception loading asset '{0}': {1}", assetName, e.Message), userData);
			}
		}

		// 其他 LoadAsset 重载（未被直接调用，但实现接口要求）
		public void LoadAsset(string assetName, LoadAssetCallbacks loadAssetCallbacks)
			=> LoadAsset(assetName, 0, loadAssetCallbacks, null);

		public void LoadAsset(string assetName, Type assetType, LoadAssetCallbacks loadAssetCallbacks)
			=> LoadAsset(assetName, assetType, 0, loadAssetCallbacks, null);

		public void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks)
			=> LoadAsset(assetName, priority, loadAssetCallbacks, null);

		public void LoadAsset(string assetName, LoadAssetCallbacks loadAssetCallbacks, object userData)
			=> LoadAsset(assetName, 0, loadAssetCallbacks, userData);

		public void LoadAsset(string assetName, Type assetType, LoadAssetCallbacks loadAssetCallbacks, object userData)
			=> LoadAsset(assetName, assetType, 0, loadAssetCallbacks, userData);

		// ================================================================
		//  LoadScene
		// ================================================================

		public void LoadScene(string sceneAssetName, int priority,
			LoadSceneCallbacks loadSceneCallbacks, object userData)
		{
			if (string.IsNullOrEmpty(sceneAssetName))
			{
				loadSceneCallbacks.LoadSceneFailureCallback?.Invoke(
					sceneAssetName, LoadResourceStatus.NotExist, "Scene asset name is invalid.", userData);
				return;
			}

			try
			{
				// 同步加载 PackedScene，成功回调只传 name + duration + userData
				// （管线场景资产通过内部 SceneToAssetMap 管理，调用方不直接从回调拿资源对象）
				var resource = Godot.ResourceLoader.Load<PackedScene>(sceneAssetName);
				if (resource != null)
				{
					loadSceneCallbacks.LoadSceneSuccessCallback?.Invoke(
						sceneAssetName, 0f, userData);
				}
				else
				{
					loadSceneCallbacks.LoadSceneFailureCallback?.Invoke(
						sceneAssetName, LoadResourceStatus.AssetError,
						Utility.Text.Format("Failed to load scene '{0}'.", sceneAssetName), userData);
				}
			}
			catch (Exception e)
			{
				loadSceneCallbacks.LoadSceneFailureCallback?.Invoke(
					sceneAssetName, LoadResourceStatus.AssetError,
					Utility.Text.Format("Exception loading scene '{0}': {1}", sceneAssetName, e.Message), userData);
			}
		}

		public void LoadScene(string sceneAssetName, LoadSceneCallbacks loadSceneCallbacks)
			=> LoadScene(sceneAssetName, 0, loadSceneCallbacks, null);

		public void LoadScene(string sceneAssetName, int priority, LoadSceneCallbacks loadSceneCallbacks)
			=> LoadScene(sceneAssetName, priority, loadSceneCallbacks, null);

		public void LoadScene(string sceneAssetName, LoadSceneCallbacks loadSceneCallbacks, object userData)
			=> LoadScene(sceneAssetName, 0, loadSceneCallbacks, userData);

		// ================================================================
		//  LoadBinary
		// ================================================================

		public void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData)
		{
			if (!FileAccess.FileExists(binaryAssetName))
			{
				loadBinaryCallbacks.LoadBinaryFailureCallback?.Invoke(
					binaryAssetName, LoadResourceStatus.NotExist,
					Utility.Text.Format("Binary asset '{0}' does not exist.", binaryAssetName), userData);
				return;
			}

			try
			{
				using var file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
				if (file == null)
				{
					loadBinaryCallbacks.LoadBinaryFailureCallback?.Invoke(
						binaryAssetName, LoadResourceStatus.AssetError,
						Utility.Text.Format("Cannot open binary asset '{0}'.", binaryAssetName), userData);
					return;
				}
				var bytes = file.GetBuffer((long)file.GetLength());
				loadBinaryCallbacks.LoadBinarySuccessCallback?.Invoke(binaryAssetName, bytes, 0f, userData);
			}
			catch (Exception e)
			{
				loadBinaryCallbacks.LoadBinaryFailureCallback?.Invoke(
					binaryAssetName, LoadResourceStatus.AssetError, e.Message, userData);
			}
		}

		public void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks)
			=> LoadBinary(binaryAssetName, loadBinaryCallbacks, null);

		// ================================================================
		//  UnloadAsset / UnloadScene
		// ================================================================

		public void UnloadAsset(object asset)
		{
			// Godot 引擎通过引用计数自动管理
		}

		public void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks)
			=> UnloadScene(sceneAssetName, unloadSceneCallbacks, null);

		public void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks, object userData)
		{
			unloadSceneCallbacks.UnloadSceneSuccessCallback?.Invoke(sceneAssetName, userData);
		}

		public void InitResources(InitResourcesCompleteCallback initResourcesCompleteCallback)
		{
			// 编辑器模式无需初始化版本列表
			initResourcesCompleteCallback?.Invoke();
		}

		public void CheckResources(bool ignoreOtherVariant, CheckResourcesCompleteCallback checkResourcesCompleteCallback)
			=> throw new NotSupportedException("CheckResources is not supported in EditorResourceMode.");

		public CheckVersionListResult CheckVersionList(int latestInternalResourceVersion)
			=> throw new NotSupportedException("CheckVersionList is not supported in EditorResourceMode.");

		public void UpdateVersionList(int versionListLength, int versionListHashCode, int versionListCompressedLength, int versionListCompressedHashCode, UpdateVersionListCallbacks updateVersionListCallbacks)
			=> throw new NotSupportedException("UpdateVersionList is not supported in EditorResourceMode.");

		public void VerifyResources(int verifyResourceLengthPerFrame, VerifyResourcesCompleteCallback verifyResourcesCompleteCallback)
			=> throw new NotSupportedException("VerifyResources is not supported in EditorResourceMode.");

		public void ApplyResources(string resourcePackPath, ApplyResourcesCompleteCallback applyResourcesCompleteCallback)
			=> throw new NotSupportedException("ApplyResources is not supported in EditorResourceMode.");

		public void UpdateResources(UpdateResourcesCompleteCallback updateResourcesCompleteCallback)
			=> throw new NotSupportedException("UpdateResources is not supported in EditorResourceMode.");

		public void UpdateResources(string resourceGroupName, UpdateResourcesCompleteCallback updateResourcesCompleteCallback)
			=> throw new NotSupportedException("UpdateResources is not supported in EditorResourceMode.");

		public void StopUpdateResources()
			=> throw new NotSupportedException("StopUpdateResources is not supported in EditorResourceMode.");

		public bool VerifyResourcePack(string resourcePackPath)
			=> throw new NotSupportedException("VerifyResourcePack is not supported in EditorResourceMode.");

		// ================================================================
		//  Binary helpers（DataProvider 可能调用）
		// ================================================================

		public int GetBinaryLength(string binaryAssetName)
		{
			if (!FileAccess.FileExists(binaryAssetName)) return -1;
			using var file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
			return file != null ? (int)file.GetLength() : -1;
		}

		public string GetBinaryPath(string binaryAssetName) => binaryAssetName;

		public bool GetBinaryPath(string binaryAssetName, out bool storageInReadOnly,
			out bool storageInFileSystem, out string relativePath, out string fileName)
		{
			storageInReadOnly = true;
			storageInFileSystem = false;
			relativePath = binaryAssetName;
			fileName = null;
			return !string.IsNullOrEmpty(binaryAssetName);
		}

		public byte[] LoadBinaryFromFileSystem(string binaryAssetName)
		{
			if (!FileAccess.FileExists(binaryAssetName)) return null;
			using var file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
			return file?.GetBuffer((long)file.GetLength());
		}

		public int LoadBinaryFromFileSystem(string binaryAssetName, byte[] buffer)
			=> LoadBinaryFromFileSystem(binaryAssetName, buffer, 0, buffer?.Length ?? 0);

		public int LoadBinaryFromFileSystem(string binaryAssetName, byte[] buffer, int startIndex)
			=> LoadBinaryFromFileSystem(binaryAssetName, buffer, startIndex, (buffer?.Length ?? 0) - startIndex);

		public int LoadBinaryFromFileSystem(string binaryAssetName, byte[] buffer, int startIndex, int length)
		{
			var bytes = LoadBinaryFromFileSystem(binaryAssetName);
			if (bytes == null || buffer == null) return 0;
			int copyLen = Math.Min(bytes.Length, length);
			Array.Copy(bytes, 0, buffer, startIndex, copyLen);
			return copyLen;
		}

		public byte[] LoadBinarySegmentFromFileSystem(string binaryAssetName, int length)
			=> LoadBinarySegmentFromFileSystem(binaryAssetName, 0, length);

		public byte[] LoadBinarySegmentFromFileSystem(string binaryAssetName, int offset, int length)
		{
			var bytes = LoadBinaryFromFileSystem(binaryAssetName);
			if (bytes == null || offset >= bytes.Length) return null;
			int copyLen = Math.Min(bytes.Length - offset, length);
			var result = new byte[copyLen];
			Array.Copy(bytes, offset, result, 0, copyLen);
			return result;
		}

		public int LoadBinarySegmentFromFileSystem(string binaryAssetName, byte[] buffer)
			=> LoadBinarySegmentFromFileSystem(binaryAssetName, 0, buffer, 0, buffer?.Length ?? 0);

		public int LoadBinarySegmentFromFileSystem(string binaryAssetName, byte[] buffer, int length)
			=> LoadBinarySegmentFromFileSystem(binaryAssetName, 0, buffer, 0, length);

		public int LoadBinarySegmentFromFileSystem(string binaryAssetName, byte[] buffer, int startIndex, int length)
			=> LoadBinarySegmentFromFileSystem(binaryAssetName, 0, buffer, startIndex, length);

		public int LoadBinarySegmentFromFileSystem(string binaryAssetName, int offset, byte[] buffer)
			=> LoadBinarySegmentFromFileSystem(binaryAssetName, offset, buffer, 0, buffer?.Length ?? 0);

		public int LoadBinarySegmentFromFileSystem(string binaryAssetName, int offset, byte[] buffer, int length)
			=> LoadBinarySegmentFromFileSystem(binaryAssetName, offset, buffer, 0, length);

		public int LoadBinarySegmentFromFileSystem(string binaryAssetName, int offset, byte[] buffer, int startIndex, int length)
		{
			var bytes = LoadBinarySegmentFromFileSystem(binaryAssetName, offset, length);
			if (bytes == null || buffer == null) return 0;
			int copyLen = Math.Min(bytes.Length, length);
			Array.Copy(bytes, 0, buffer, startIndex, copyLen);
			return copyLen;
		}


		public bool HasResourceGroup(string resourceGroupName) => false;

		public IResourceGroup GetResourceGroup() => null;
		public IResourceGroup GetResourceGroup(string resourceGroupName) => null;

		public IResourceGroup[] GetAllResourceGroups() => Array.Empty<IResourceGroup>();
		public void GetAllResourceGroups(List<IResourceGroup> results) => results?.Clear();

		public IResourceGroupCollection GetResourceGroupCollection(params string[] resourceGroupNames) => null;
		public IResourceGroupCollection GetResourceGroupCollection(List<string> resourceGroupNames) => null;

		public TaskInfo[] GetAllLoadAssetInfos() => Array.Empty<TaskInfo>();
		public void GetAllLoadAssetInfos(List<TaskInfo> results) => results?.Clear();
	}
}
