# Godot 资源系统精简方案

## 目标

将 `IResourceManager` 从 97 个成员精简到 8 个，删除所有 Unity 管线遗留代码。保留 `IResourceManager` 作为核心接口，`ResourceManager` 实现，`ResourceComponent` 包装，桥接组件通过 `GF.Resource.ResourceManager` 获取的完整链条。

---

## 当前架构全景

```
IResourceManager（97 成员）← 接口层（GameFramework/Resource/）
  ▲ 实现（2 个）
  │
  ├─ EditorResourceManager（Godot 层）← 编辑器模式
  │     └─ GF.Base.EditorResourceManager（BaseComponent 惰性单例）
  │
  └─ ResourceManager（29 partials 管线）← 运行时模式
        └─ GameFrameworkEntry.GetModule<IResourceManager>()

ResourceComponent ← Godot 组件，负责：
  ├─ 编辑器模式：使用 GF.Base.EditorResourceManager
  └─ 运行时模式：使用 GameFrameworkEntry.GetModule<IResourceManager>() + 管线初始化

桥接组件（Entity/Sound/Scene/UI）：
  └─ GF.Base.EditorResourceMode ?
       → GF.Base.EditorResourceManager
       → GameFrameworkEntry.GetModule<IResourceManager>()
```

### 特殊路径
- **DataTableManager** → 不走 IResourceManager，直接 `ResourceComponent.LoadBinary(path)`
- **LocalizationComponent** → 不走 IResourceManager，直接 `ResourceComponent.LoadText/LoadBinary(path)`

---

## 同步 vs 异步总览

```
ResourceManager（IResourceManager 接口）— 全异步
  ├─ LoadAsset(path, callbacks)     → 异步，LoadThreadedRequest + 帧轮询
  ├─ LoadBinary(path, callbacks)    → 异步，Task.Run 后台线程读文件
  ├─ UnloadScene(path, callbacks)   → 异步
  ├─ HasAsset(path)                 → 同步（仅查询，无需异步）
  ├─ GetBinaryLength(path)          → 同步（仅查询）
  └─ LoadBinaryFromFileSystem(...)  → 同步（存根，永不触发）

ResourceComponent（便捷层）
  ├─ LoadAsync<T>(path)      → Task<T> 异步，内部调 ResourceManager 轮询
  ├─ LoadSceneAsync(path)    → Task<PackedScene> 异步
  ├─ LoadBinary(path)        → byte[] 同步（小文件，直接 FileAccess）
  └─ LoadText(path)          → string 同步（小文件）
```

### 异步实现机制

```
LoadAsset(path, callbacks)
  └─ ResourceLoader.LoadThreadedRequest(path)
      └─ ResourceComponent.OnUpdate() 每帧轮询 LoadThreadedGetStatus
          ├─ Loaded     → LoadThreadedGet → 回调成功
          ├─ Failed     → 回调失败
          └─ InProgress → 继续等待

LoadBinary(path, callbacks)
  └─ Task.Run(() => FileAccess.Open + GetBuffer)
      └─ 完成后 → 回调成功/失败

LoadAsync<T>(path)
  └─ 同上 LoadThreadedRequest 轮询，包装为 Task<T>
```

---

## 精简后架构

```
IResourceManager（8 成员）← 保留精简
  ▲ 实现
  │
  ResourceManager（单个文件，Godot 原生加载）← 新建于 GodotGameFrameworkCore/Resource/
  ▲ 创建 + 持有
  │
  ResourceComponent ← 轻量包装，提供：
  ├─ .ResourceManager → IResourceManager 实例
  ├─ .LoadBinary / .LoadText / .HasAsset → 便捷方法
  ├─ ._resourceMode（Inspector: Package/Updatable/...）
  └─ .EffectiveResourceMode / .ResolveResourceMode()

桥接组件（Entity/Sound/Scene/UI）：
  └─ m_XXManager.SetResourceManager(GF.Resource.ResourceManager)
```

### 精简要点

| 删除 | 原因 |
|------|------|
| `EditorResourceManager.cs` | 新 ResourceManager 用 Godot API 直接加载，无管线可跳过 |
| `EditorResourceMode` | Godot 编辑器与运行时加载行为相同，无需区分 |
| `BaseComponent.EditorResourceManager` 属性 | 同上 |
| 管线 29 个 partial 文件 | 不再需要 |
| `FileSystem/` 目录 | Godot 无虚拟文件系统 |
| `Download/` 目录 | Updatable 模式未实现 |
| 所有序列化器/事件/回调类型 | 对应接口成员已移除 |
| 管线专用 Helper（IResourceHelper + 实现 + agent helpers） | 不再创建 |
| `GDFBuiltinVersionListSerializer.cs` / `GDFResourceBuilder.cs` | 对应序列化器已删 |

---

## 第一步：精简 IResourceManager 接口

**文件**：`GameFramework/Resource/IResourceManager.cs`

### 保留（8 个成员）

```csharp
public interface IResourceManager
{
    // 模式管理
    ResourceMode ResourceMode { get; }
    void SetResourceMode(ResourceMode resourceMode);

    // 核心加载
    HasAssetResult HasAsset(string assetName);
    void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks, object userData);
    void UnloadAsset(object asset);
    void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks, object userData);
    void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData);

    // DataProvider 兼容（BinaryOnFileSystem 分支，Godot 中永不触发但需编译通过）
    int GetBinaryLength(string binaryAssetName);
    int LoadBinaryFromFileSystem(string binaryAssetName, byte[] buffer);
}
```

**保留理由**：
- `ResourceMode` / `SetResourceMode`：模式是资源管理器核心状态，ResourceComponent 在 OnInit 时通过此方法将 Inspector 配置传递给 ResourceManager
- `GetBinaryLength` / `LoadBinaryFromFileSystem`：DataProvider 的 switch 分支需要编译通过，Godot 中 `HasAsset` 永不返回 `BinaryOnFileSystem`，所以实际永不执行，但接口方法仍需保留

### 删除（~89 个成员）

**属性（25 个）**：
```
ReadOnlyPath, ReadWritePath, CurrentVariant
ApplicableGameVersion, InternalResourceVersion
AssetCount, ResourceCount, ResourceGroupCount
UpdatePrefixUri, GenerateReadWriteVersionListLength
ApplyingResourcePackPath, ApplyWaitingCount, UpdateRetryCount
UpdatingResourceGroup, UpdateWaitingCount, UpdateWaitingWhilePlayingCount, UpdateCandidateCount
LoadTotalAgentCount, LoadFreeAgentCount, LoadWorkingAgentCount, LoadWaitingTaskCount
AssetAutoReleaseInterval, AssetCapacity, AssetExpireTime, AssetPriority
ResourceAutoReleaseInterval, ResourceCapacity, ResourceExpireTime, ResourcePriority
```

**序列化器属性（5 个）**：`PackageVersionListSerializer`, `UpdatableVersionListSerializer`, `ReadOnlyVersionListSerializer`, `ReadWriteVersionListSerializer`, `ResourcePackVersionListSerializer`

**事件（11 个）**：`ResourceVerifyStart/Success/Failure`, `ResourceApplyStart/Success/Failure`, `ResourceUpdateStart/Changed/Success/Failure/AllComplete`

**方法（含重载 ~48 个）**：
```
SetReadOnlyPath, SetReadWritePath, SetCurrentVariant
SetObjectPoolManager, SetFileSystemManager, SetDownloadManager
SetDecryptResourceCallback, SetResourceHelper, AddLoadResourceAgentHelper
InitResources, CheckVersionList, UpdateVersionList
VerifyResources, CheckResources, ApplyResources
UpdateResources(x2), StopUpdateResources, VerifyResourcePack
GetAllLoadAssetInfos(x2)
GetBinaryPath(x2)
LoadBinaryFromFileSystem(额外 3 个重载), LoadBinarySegmentFromFileSystem(x8)
HasResourceGroup, GetResourceGroup(x2), GetAllResourceGroups(x2)
GetResourceGroupCollection(x2)
```

### 验证

- `dotnet build` 失败（EditorResourceManager / ResourceManager / ResourceComponent 需要更新）

---

## 第二步：新建 ResourceManager（Godot 原生加载）

**文件**：`GodotGameFrameworkCore/Resource/ResourceManager.cs`

### 说明

新建一个 Godot 层 ResourceManager，实现精简后的 IResourceManager 8 个方法。这是桥接组件实际使用的实现。

### 同步 vs 异步

| 层级 | 方法 | 方式 |
|------|------|------|
| **IResourceManager** | `LoadAsset(callbacks)` | 回调式异步（调用者通过回调接收结果） |
| （接口层） | `LoadBinary(callbacks)` | 回调式异步 |
| | `HasAsset` / `GetBinaryLength` | 同步 |
| **ResourceComponent** | `LoadAsync<T>(path)` | Task 异步（推荐，await 用） |
| （便捷层） | `Load<T>(path)` | 同步阻塞 |
| | `LoadBinary(path)` | 同步（返回 byte[]） |
| | `LoadText(path)` | 同步（返回 string） |

```csharp
// GodotGameFramework.Resource 命名空间
internal sealed class ResourceManager : IResourceManager
{
    public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;
    public void SetResourceMode(ResourceMode mode) => ResourceMode = mode;
    // 待处理的异步加载任务
    private readonly List<AsyncLoadTask> m_PendingTasks = new();

    // ================================================================
    //  同步查询
    // ================================================================

    public HasAssetResult HasAsset(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return HasAssetResult.NotExist;
        if (Godot.ResourceLoader.Exists(assetName)) return HasAssetResult.AssetOnDisk;
        if (FileAccess.FileExists(assetName)) return HasAssetResult.BinaryOnDisk;
        return HasAssetResult.NotExist;
    }

    public int GetBinaryLength(string binaryAssetName)
    {
        if (!FileAccess.FileExists(binaryAssetName)) return -1;
        using var file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
        return file != null ? (int)file.GetLength() : -1;
    }

    public int LoadBinaryFromFileSystem(string name, byte[] buf) => 0;

    // ================================================================
    //  异步加载：LoadAsset（回调式）
    //  使用 Godot.ResourceLoader.LoadThreadedRequest 实现真异步，
    //  由 ResourceComponent.OnUpdate 每帧轮询完成状态。
    // ================================================================

    public void LoadAsset(string assetName, int priority,
        LoadAssetCallbacks callbacks, object userData)
    {
        if (!Godot.ResourceLoader.Exists(assetName))
        {
            callbacks.LoadAssetFailureCallback?.Invoke(assetName,
                LoadResourceStatus.NotExist, "Asset not found.", userData);
            return;
        }

        Error err = Godot.ResourceLoader.LoadThreadedRequest(assetName);
        if (err != Error.Ok)
        {
            callbacks.LoadAssetFailureCallback?.Invoke(assetName,
                LoadResourceStatus.AssetError, "Failed to start async load.", userData);
            return;
        }

        m_PendingTasks.Add(new AsyncLoadTask
        {
            Path = assetName,
            Type = AsyncLoadType.Asset,
            AssetCallbacks = callbacks,
            BinaryCallbacks = null,
            UserData = userData
        });
    }

    // ================================================================
    //  异步加载：LoadBinary（回调式）
    //  使用 Task.Run 后台线程读文件，避免阻塞主线程。
    // ================================================================

    public void LoadBinary(string binaryAssetName,
        LoadBinaryCallbacks callbacks, object userData)
    {
        if (!FileAccess.FileExists(binaryAssetName))
        {
            callbacks.LoadBinaryFailureCallback?.Invoke(binaryAssetName,
                LoadResourceStatus.NotExist, "File not found.", userData);
            return;
        }

        // 后台线程读文件
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var file = FileAccess.Open(binaryAssetName, FileAccess.ModeFlags.Read);
                var bytes = file.GetBuffer((long)file.GetLength());
                // 回到主线程回调
                GodotObject.CallDeferred("call_deferred", ...); // 简化：通过信号回主线程
                callbacks.LoadBinarySuccessCallback?.Invoke(binaryAssetName, bytes, 0f, userData);
            }
            catch (Exception ex)
            {
                callbacks.LoadBinaryFailureCallback?.Invoke(binaryAssetName,
                    LoadResourceStatus.AssetError, ex.Message, userData);
            }
        });
    }

    public void UnloadAsset(object asset) { }

    public void UnloadScene(string sceneAssetName,
        UnloadSceneCallbacks callbacks, object userData)
    {
        callbacks.UnloadSceneSuccessCallback?.Invoke(sceneAssetName, userData);
    }

    // ================================================================
    //  帧轮询 — 由 ResourceComponent.OnUpdate 每帧调用
    // ================================================================

    public void PollPendingTasks()
    {
        for (int i = m_PendingTasks.Count - 1; i >= 0; i--)
        {
            var task = m_PendingTasks[i];
            var status = Godot.ResourceLoader.LoadThreadedGetStatus(task.Path, out var progress);

            switch (status)
            {
                case Godot.ResourceLoader.ThreadLoadStatus.Loaded:
                    var resource = Godot.ResourceLoader.LoadThreadedGet(task.Path);
                    task.AssetCallbacks?.LoadAssetSuccessCallback?.Invoke(
                        task.Path, resource, 0f, task.UserData);
                    m_PendingTasks.RemoveAt(i);
                    break;

                case Godot.ResourceLoader.ThreadLoadStatus.Failed:
                case Godot.ResourceLoader.ThreadLoadStatus.InvalidResource:
                    task.AssetCallbacks?.LoadAssetFailureCallback?.Invoke(
                        task.Path, LoadResourceStatus.AssetError,
                        "Async load failed.", task.UserData);
                    m_PendingTasks.RemoveAt(i);
                    break;

                case Godot.ResourceLoader.ThreadLoadStatus.InProgress:
                    // 继续等待
                    break;
            }
        }
    }

    // ================================================================
    //  内部类型
    // ================================================================

    private enum AsyncLoadType { Asset, Binary }
    private class AsyncLoadTask
    {
        public string Path;
        public AsyncLoadType Type;
        public LoadAssetCallbacks AssetCallbacks;
        public LoadBinaryCallbacks BinaryCallbacks;
        public object UserData;
    }
}
```

### 删除的旧文件

删除 `GameFramework/Resource/` 下的 29 个 partial 管线文件：
```
ResourceManager.AssetInfo.cs, LoadType.cs, ReadWriteResourceInfo.cs
ResourceChecker.cs + CheckInfo.cs + 3 个子文件
ResourceGroup.cs, ResourceGroupCollection.cs
ResourceInfo.cs, ResourceIniter.cs
ResourceLoader.cs + 7 个子文件
ResourceName.cs, ResourceNameComparer.cs
ResourceUpdater.cs + 2 个子文件
ResourceVerifier.cs + VerifyInfo.cs
VersionListProcessor.cs
```

同时删除 `GameFramework/Resource/ResourceManager.cs`（旧管线主文件），被新文件替代。

---

## 第三步：精简 ResourceComponent

**文件**：`GodotGameFrameworkCore/Resource/ResourceComponent.cs`

### 改动

```csharp
public sealed partial class ResourceComponent : GameFrameworkComponent
{
    // 内部持有 ResourceManager
    private ResourceManager m_ResourceManager;
    private ResourceMode m_EffectiveResourceMode;
    private ResourceMode _resourceMode = ResourceMode.Package;

    // Inspector 配置
    [Export] private ResourceMode ResourceMode
    {
        get => _resourceMode;
        set => _resourceMode = value;
    }

    // 对外暴露
    public IResourceManager ResourceManager => m_ResourceManager;
    public ResourceMode EffectiveResourceMode => m_EffectiveResourceMode;

    public override void OnInit()
    {
        base.OnInit();
        m_ResourceManager = new ResourceManager();
        m_EffectiveResourceMode = ResolveResourceMode(_resourceMode);
        m_ResourceManager.SetResourceMode(m_EffectiveResourceMode);
        Log.Info("[ResourceComponent] Initialized. Mode: {0}", m_EffectiveResourceMode);
        ProcessMode = ProcessModeEnum.Always;
    }

    // 模式回退逻辑
    private ResourceMode ResolveResourceMode(ResourceMode requested)
    {
        switch (requested)
        {
            case ResourceMode.Package: return ResourceMode.Package;
            case ResourceMode.Updatable:
                Log.Warning("[ResourceComponent] Updatable not yet implemented. Falling back to Package.");
                return ResourceMode.Package;
            case ResourceMode.UpdatableWhilePlaying:
                Log.Warning("[ResourceComponent] UpdatableWhilePlaying not yet implemented. Falling back to Package.");
                return ResourceMode.Package;
            default:
                return ResourceMode.Package;
        }
    }

    // ================================================================
    //  帧轮询 — 驱动 ResourceManager 的异步加载
    // ================================================================

    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);
        m_ResourceManager?.PollPendingTasks();
    }

    // ================================================================
    //  同步便捷方法（DataTableManager / LocalizationComponent 直接调用）
    // ================================================================

    /// <summary>同步加载二进制文件，返回 byte[]（DataTableManager 用）</summary>
    public byte[] LoadBinary(string path) { ... FileAccess ... }

    /// <summary>同步加载文本文件，返回 string（LocalizationComponent 用）</summary>
    public string LoadText(string path) { ... FileAccess.GetAsText ... }

    /// <summary>检查资源是否存在</summary>
    public bool HasAsset(string path) => m_ResourceManager.HasAsset(path) != HasAssetResult.NotExist;

    /// <summary>同步加载 Godot 资源（小资源用；大资源请用 LoadAsync）</summary>
    public T LoadAsset<T>(string path) where T : class => Godot.ResourceLoader.Load<T>(path);

    // ================================================================
    //  异步便捷方法（推荐）
    // ================================================================

    /// <summary>异步加载资源，返回 Task&lt;T&gt;，支持 await</summary>
    public async Task<T> LoadAsync<T>(string path) where T : Godot.Resource
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        if (!Godot.ResourceLoader.Exists(path)) throw new FileNotFoundException(path);

        var tcs = new TaskCompletionSource<T>();

        Godot.ResourceLoader.LoadThreadedRequest(path);
        // 注册到任务列表，由 OnUpdate 轮询
        // 完成后回调 tcs
        m_ResourceManager.RegisterAsyncTask(path, (resource) =>
        {
            if (resource is T result)
                tcs.TrySetResult(result);
            else
                tcs.TrySetException(new InvalidCastException(...));
        }, () => tcs.TrySetException(new Exception("Load failed")));

        return await tcs.Task;
    }

    /// <summary>异步加载场景</summary>
    public async Task<PackedScene> LoadSceneAsync(string path)
        => await LoadAsync<PackedScene>(path);
}
```

### 删除的代码

- `: IResourceManager` 从类声明中移除
- `m_ResourceManager` 类型从 `IResourceManager` 改为 `ResourceManager`
- `m_ResourceHelperTypeName`, `m_LoadResourceAgentHelperTypeName`, `LoadResourceAgentHelperCount`
- 整个管线初始化路径（`SetReadOnlyPath`, `SetResourceHelper`, `AddLoadResourceAgentHelper` 等）
- `InitRuntimeMode()`, `OnInitResourcesComplete()`
- `AssetCount`, `ResourceCount` 属性（已从 IResourceManager 移除）
- `using GameConfig.Constant;`, `using GameFramework.ObjectPool;`

### 保留的便捷方法

```csharp
public byte[] LoadBinary(string filePath);      // DataTableManager 调用
public string LoadText(string filePath);        // LocalizationComponent 调用
public bool HasAsset(string assetPath);         // 旧 API 兼容
public T LoadAsset<T>(string assetPath);        // 旧 API 兼容
public object LoadAsset(string, Type);          // 旧 API 兼容
public void LoadAssetAsync(string, Type, Action<object>, Action<string>);  // 旧 API 兼容
```

---

## 第四步：删除 EditorResourceManager

**文件**：`GodotGameFrameworkCore/Resource/EditorResourceManager.cs`

### 操作

直接删除。新 ResourceManager 使用 Godot API 直接加载，没有管线可以"跳过"，EditorResourceManager 不再需要。

---

## 第五步：修改 BaseComponent

**文件**：`GodotGameFrameworkCore/Base/BaseComponent.cs`

### 改动

```csharp
// 删除以下代码（约 20 行）：
[Export] private bool m_EditorResourceMode = true;
public bool EditorResourceMode { get { return m_EditorResourceMode && OS.HasFeature("editor"); } }
private IResourceManager m_ResourceManager;
public IResourceManager EditorResourceManager { get { ... new EditorResourceManager(); } }

// 同时删除 using GodotGameFrameworkCore.Resource;
```

---

## 第六步：修改桥接组件

### 文件（4 个）

```
EntityComponent.cs（第 61-65 行）
SoundComponent.cs（第 64-68 行）
SceneComponent.cs（第 61-70 行）
UIComponent.cs（第 192 行）
```

### 改动

```csharp
// 原来（5 行）
var resourceManager = GF.Base.EditorResourceMode
    ? GF.Base.EditorResourceManager
    : GameFrameworkEntry.GetModule<GameFramework.Resource.IResourceManager>();
if (resourceManager == null) { Log.Fatal("Resource manager is invalid."); return; }
m_EntityManager.SetResourceManager(resourceManager);

// 改为（1 行）
m_EntityManager.SetResourceManager(GF.Resource.ResourceManager);
```

---

## 第七步：删除废弃文件

### GameFramework/FileSystem/ 整目录（8 个文件）
```
IFileSystem.cs, IFileSystemManager.cs, IFileSystemHelper.cs
FileSystemManager.cs, FileSystem.cs, FileSystem.StringData.cs, FileSystem.HeaderData.cs, FileSystem.BlockData.cs
```

### GameFramework/Download/ 整目录（7 个文件）
```
IDownloadManager.cs, DownloadManager.cs + 5 partials
```

### GameFramework/Resource/ 下删除的独立文件

**序列化器（20 个文件）**：
```
PackageVersionListSerializer.cs + PackageVersionList.cs + Asset/Resource/FileSystem/ResourceGroup
UpdatableVersionListSerializer.cs + UpdatableVersionList.cs + Asset/Resource/FileSystem/ResourceGroup
ReadOnlyVersionListSerializer.cs, ReadWriteVersionListSerializer.cs
ResourcePackVersionListSerializer.cs + ResourcePackVersionList.cs + Resource
LocalVersionList.cs + Resource + FileSystem
```

**事件参数（11 个）**：
```
ResourceVerifyStart/Success/FailureEventArgs.cs
ResourceApplyStart/Success/FailureEventArgs.cs
ResourceUpdateStart/Changed/Success/Failure/AllCompleteEventArgs.cs
```

**回调类型（8 个）**：
```
UpdateVersionListCallbacks.cs + Success/FailureCallback.cs
VerifyResourcesCompleteCallback.cs, CheckResourcesCompleteCallback.cs
ApplyResourcesCompleteCallback.cs, UpdateResourcesCompleteCallback.cs
CheckVersionListResult.cs
```

**资源组（2 个）**：
```
IResourceGroup.cs, IResourceGroupCollection.cs
```

**加载代理类型（7 个）**：
```
ILoadResourceAgentHelper.cs
LoadResourceAgentHelperUpdateEventArgs.cs
LoadResourceAgentHelperReadFileCompleteEventArgs.cs
LoadResourceAgentHelperReadBytesCompleteEventArgs.cs
LoadResourceAgentHelperParseBytesCompleteEventArgs.cs
LoadResourceAgentHelperLoadCompleteEventArgs.cs
LoadResourceAgentHelperErrorEventArgs.cs
```

**Helper 类型（5 个）**：
```
IResourceHelper.cs, ResourceHelperBase.cs, DefaultResourceHelper.cs
LoadBytesCallbacks.cs, LoadBytesSuccessCallback.cs, LoadBytesFailureCallback.cs
```

**其他（2 个）**：
```
DecryptResourceCallback.cs, LoadResourceProgress.cs
```

### GodotGameFrameworkCore/Resource/ 下删除

```
DefaultLoadResourceAgentHelper.cs
LoadResourceAgentHelperBase.cs
GDFBuiltinVersionListSerializer.cs
GDFResourceBuilder.cs
EditorResourceManager.cs
```

---

## 第八步：验证

```bash
cd GodotProject
dotnet build
```

期望结果：**0 错误**

### 编译要点

1. 删除顺序：先删接口依赖，再删实现文件
2. `using GameFramework.FileSystem;` 和 `using GameFramework.Download;` 只在已删除文件中出现，不会影响编译
3. `DataTableManager.cs` 的 `using GameFramework.Resource;` 可保留（引用了 LoadResourceStatus 等类型）
4. 其余回调类型（`LoadAssetCallbacks`, `LoadSceneCallbacks`, `UnloadSceneCallbacks`, `LoadBinaryCallbacks`）以及 `HasAssetResult`, `LoadResourceStatus` 等保留不变

---

## 保留的模式概念

### ResourceMode 枚举

```csharp
public enum ResourceMode : byte
{
    Package,              // 单机模式，直接加载
    Updatable,            // 预下载热更（P2 实现）
    UpdatableWhilePlaying // 边玩边更（P2 实现）
}
```

### Package 模式（单机）— 当前可用

```
ResourceManager.HasAsset    → Godot.ResourceLoader.Exists / FileAccess.FileExists
ResourceManager.LoadAsset   → Godot.ResourceLoader.Load → 回调
ResourceManager.LoadBinary  → FileAccess.Open → 回调
```

### Updatable 模式（热更）— P2 实现

```
启动时：
1. 检查本地版本 (user://version.json)
2. 请求远程版本 (CDN /api/version)
3. 比对版本号
   ├─ 一致 → 直接进入游戏
   └─ 有更新 → 下载差异 .pck → SHA256 校验 → LoadResourcePack() → 更新本地记录
4. 进入游戏（资源通过 res:// 正常访问）
```

### 运行时流程

```
ResourceComponent.OnInit()
├─ 创建 ResourceManager 实例
├─ ResolveResourceMode(_resourceMode) → 确定 EffectiveResourceMode
│  ├─ Package → 正常
│  ├─ Updatable → Log.Warning + 回退 Package（P2 实现热更）
│  └─ UpdatableWhilePlaying → Log.Warning + 回退 Package
├─ m_ResourceManager.SetResourceMode(EffectiveResourceMode)  ← 传给实现
└─ 桥接组件 → GF.Resource.ResourceManager → IResourceManager
```

---

## 最终文件结构

```
GodotProject/
├── Framework/
│   ├── GameFramework/Resource/                    ← 纯 C# 层
│   │   ├── IResourceManager.cs                    ← 8 方法
│   │   ├── ResourceMode.cs                        ← 枚举（保留）
│   │   ├── HasAssetResult.cs                      ← 枚举（保留）
│   │   ├── LoadResourceStatus.cs                  ← 枚举（保留）
│   │   ├── LoadAssetCallbacks.cs + 4 sub          ← 回调类型（保留）
│   │   ├── LoadSceneCallbacks.cs + 2 sub          ← 保留
│   │   ├── UnloadSceneCallbacks.cs + 2 sub        ← 保留
│   │   ├── LoadBinaryCallbacks.cs + 2 sub         ← 保留
│   │   └── Constant.cs                            ← 保留（DefaultPriority）
│   │
│   └── GodotGameFrameworkCore/Resource/           ← Godot 层
│       ├── ResourceManager.cs                     ← [新建] 8 方法实现
│       ├── ResourceComponent.cs                   ← [精简] 轻量包装
│       ├── ResourceExtension.cs                   ← [保留] 扩展方法
│       ├── DefaultResourceHelper.cs               ← [删除]
│       ├── ResourceHelperBase.cs                  ← [删除]
│       ├── EditorResourceManager.cs               ← [删除]
│       ├── GDFBuiltinVersionListSerializer.cs     ← [删除]
│       └── GDFResourceBuilder.cs                  ← [删除]
│
├── GameFramework/FileSystem/ → 整目录删除
├── GameFramework/Download/   → 整目录删除
└── Framework/GameFramework/Resource/ → partial 管线文件全部删除
```

**统计**：
- 删除：~85 个文件（3 个目录 + ~60 个独立文件 + ~22 个管线 partial）
- 保留精简：3 个文件（IResourceManager / ResourceManager / ResourceComponent）
- 保留不变：~12 个文件（回调类型 + 枚举）
