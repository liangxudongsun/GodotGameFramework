# ResourceComponent 及相关模块全面分析

## 一、总体架构

资源系统是框架中最复杂的子系统之一，采用标准的 **双层委托架构**：

```
游戏层 (GodotGameFramework / GodotGameFramework.Resource)
  ResourceComponent ── 入口组件，管理双模式加载
    ├── ResourceHelperBase / DefaultResourceHelper ── 文件 I/O 桥接
    ├── LoadResourceAgentHelperBase / DefaultLoadResourceAgentHelper ── 加载代理
    ├── GDFBuiltinVersionListSerializer ── 版本列表序列化（V0/V1/V2）
    ├── GDFResourceBuilder ── 编辑器版本列表构建
    └── ResourceExtension ── async/await 扩展方法

核心层 (GameFramework.Resource)
  IResourceManager / ResourceManager ── 核心管理器（GameFrameworkModule, Priority=3）
    ├── ResourceIniter ── Package 模式初始化
    ├── ResourceLoader ── 加载引擎（TaskPool + ObjectPool）
    │   └── LoadResourceAgent ── 单个加载代理（ITaskAgent）
    ├── ResourceChecker ── 资源校验（Updatable 模式）
    ├── ResourceUpdater ── 资源更新（Updatable 模式）
    ├── ResourceVerifier ── 资源验证
    └── VersionListProcessor ── 版本列表处理

依赖的外部模块
    ├── IObjectPoolManager ── Asset / Resource 对象池
    ├── IFileSystemManager ── 文件系统（当前预留）
    └── IDownloadManager ── 下载管理（当前预留）
```

**核心设计原则**：纯 C# 层的 `ResourceManager` / `ResourceLoader` 通过 `IResourceHelper` 和 `ILoadResourceAgentHelper` 两个接口与 Godot 引擎解耦，核心逻辑完全独立于引擎，Godot 层通过 `Default*` 实现类完成实际的文件 I/O 和资源加载。

---

## 二、ResourceComponent（入口组件）

`GodotGameFramework.Resource.ResourceComponent` — 继承 `GameFrameworkComponent`。

### 2.1 导出属性（Godot 编辑器可配置）

| 属性 | 默认值 | 说明 |
|---|---|---|
| `LoadResourceAgentHelperCount` | `1` | 并行加载代理数量 |
| `ReadOnlyPath` | `"res://"` | 只读资源路径 |
| `ReadWritePath` | `"user://"` | 可读写资源路径 |
| `GameVersion` | `"1.0.0"` | 游戏版本号 |
| `InternalResourceVersion` | `1` | 内部资源版本号 |
| `UseResourcePipeline` | `true` | 是否启用管道模式 |
| `m_ResourceHelperTypeName` | `"GodotGameFramework.Resource.DefaultResourceHelper"` | 资源辅助器类型名（反射创建） |
| `m_LoadResourceAgentHelperTypeName` | `"GodotGameFramework.Resource.DefaultLoadResourceAgentHelper"` | 加载代理辅助器类型名（反射创建） |

### 2.2 初始化流程（OnInit）

```
OnInit()
├── GameFrameworkEntry.GetModule<IResourceManager>()     // 延迟创建 ResourceManager
├── m_ResourceManager.SetReadOnlyPath("res://")
├── m_ResourceManager.SetReadWritePath("user://")
├── m_ResourceManager.SetResourceMode(ResourceMode.Package)
├── m_ResourceManager.SetObjectPoolManager(...)          // 注入 IObjectPoolManager
├── Helper.CreateHelper → ResourceHelperBase             // 反射创建，AddChild 挂节点
├── 循环 LoadResourceAgentHelperCount 次:                 // 默认 1 次
│   ├── Create(m_LoadResourceAgentHelperTypeName)
│   ├── helperBase.AddChild(agentHelper)
│   └── m_ResourceManager.AddLoadResourceAgentHelper(agentHelper)
├── [if UseResourcePipeline == true]
│   ├── GDFBuiltinVersionListSerializer.RegisterPackageDeserializeCallbacks(...)
│   ├── [if EditorMode]
│   │   ├── RegisterPackageSerializeCallbacks(...)
│   │   └── GDFResourceBuilder.BuildVersionList(...)    // 生成 GameFrameworkVersion.dat
│   └── m_ResourceManager.InitResources(OnInitResourcesComplete)  // 加载版本列表
└── ProcessMode = ProcessModeEnum.Always                 // 始终轮询（轮询直接模式异步任务）
```

### 2.3 双模式设计

资源加载有 **管道模式** 和 **直接模式** 两条路径：

**管道模式**（`m_PipelineInitialized == true`）：

```
LoadAssetAsync(assetPath, type, onSuccess, onFailure)
  └── LoadAssetAsyncViaPipeline(...)
        └── Build LoadAssetCallbacks (success/failure 委托)
              └── m_ResourceManager.LoadAsset(assetPath, type, priority, callbacks, userData)
                    ├── AssetInfo 查找 → ResourceInfo 查找 → 依赖解析
                    ├── 创建 LoadAssetTask → 递归创建 LoadDependencyAssetTask
                    └── TaskPool.AddTask(mainTask)
```

**直接模式**（`m_PipelineInitialized == false`）：

```
LoadAssetAsync(assetPath, type, onSuccess, onFailure)
  └── LoadAssetAsyncDirect(...)
        ├── Godot.ResourceLoader.Exists(assetPath, typeName)
        ├── Godot.ResourceLoader.LoadThreadedRequest(assetPath, typeName)  // 启动异步
        └── m_AsyncLoadTasks.Add(new AsyncLoadTask { ... })

_Process(delta) 每帧:
  └── PollAsyncLoadTasks()
        └── Godot.ResourceLoader.LoadThreadedGetStatus(assetPath)
              ├── Loaded → LoadThreadedGet → onSuccess
              ├── Failed → onFailure
              └── InvalidResource → onFailure
```

### 2.4 公开 API

```csharp
// ── 同步加载（直接走 Godot ResourceLoader.Load）──
public T LoadAsset<T>(string assetPath) where T : class
public object LoadAsset(string assetPath, Type assetType)

// ── 异步加载（管道模式 / 直接模式自动选择）──
public void LoadAssetAsync(string assetPath, Type assetType,
    Action<object> onSuccess, Action<string> onFailure = null)
public Task<T> LoadAssetAsync<T>(string assetPath) where T : class

// ── 文件二进制 / 文本加载（Godot FileAccess 直接读取）──
public byte[] LoadBinary(string filePath)
public string LoadText(string filePath)

// ── 资源检查与释放 ──
public bool HasAsset(string assetPath)   // 管道模式查版本列表，直接模式查 Godot 文件系统
public void UnloadAsset(object asset)    // 空实现，Godot 引擎自动管理引用计数

// ── 状态查询 ──
public int AsyncLoadTaskCount    // 直接模式当前排队任务数
public bool PipelineInitialized  // 管道是否就绪
public int AssetCount            // 管道模式已注册资产数
public int ResourceCount         // 管道模式已注册资源数
```

---

## 三、ResourceManager（核心管理器）

`GameFramework.Resource.ResourceManager` — `internal sealed partial class`，继承 `GameFrameworkModule`，实现 `IResourceManager`。

**Priority = 3**，在模块链表中处于中前位置。

### 3.1 内部数据结构

```csharp
// ── 资源索引字典 ──
Dictionary<string, AssetInfo> m_AssetInfos;                        // asset名称 → 资产信息
Dictionary<ResourceName, ResourceInfo> m_ResourceInfos;            // 资源名 → 资源信息
SortedDictionary<ResourceName, ReadWriteResourceInfo> m_ReadWriteResourceInfos;

// ── 文件系统（当前未使用）──
Dictionary<string, IFileSystem> m_ReadOnlyFileSystems;
Dictionary<string, IFileSystem> m_ReadWriteFileSystems;

// ── 资源组 ──
Dictionary<string, ResourceGroup> m_ResourceGroups;

// ── 版本列表序列化器 ──
PackageVersionListSerializer m_PackageVersionListSerializer;
UpdatableVersionListSerializer m_UpdatableVersionListSerializer;
ReadOnlyVersionListSerializer m_ReadOnlyVersionListSerializer;
ReadWriteVersionListSerializer m_ReadWriteVersionListSerializer;
ResourcePackVersionListSerializer m_ResourcePackVersionListSerializer;

// ── 子系统 ──
ResourceIniter m_ResourceIniter;             // Package 模式初始化
VersionListProcessor m_VersionListProcessor; // Updatable 模式：版本列表处理
ResourceVerifier m_ResourceVerifier;         // 资源校验
ResourceChecker m_ResourceChecker;           // 资源检查
ResourceUpdater m_ResourceUpdater;           // 资源更新
ResourceLoader m_ResourceLoader;             // 核心加载引擎

// ── 外部依赖 ──
IResourceHelper m_ResourceHelper;            // Godot 层注入
IFileSystemManager m_FileSystemManager;      // 预留
```

### 3.2 内部子系统一览

| 子系统 | 类名（嵌套类） | 职责 | 关联模式 |
|---|---|---|---|
| 资源初始化器 | `ResourceIniter` | 加载 `GameFrameworkVersion.dat` → 反序列化 PackageVersionList → 构建 AssetInfo / ResourceInfo 字典 → 构建 ResourceGroup | Package |
| 版本列表处理器 | `VersionListProcessor` | 处理远程版本列表的下载、合并、序列化 | Updatable |
| 资源验证器 | `ResourceVerifier` | 校验本地资源完整性（长度 / CRC32） | Updatable |
| 资源检查器 | `ResourceChecker` | 对比只读区 / 读写区差异，标记需要更新的资源 | Updatable |
| 资源更新器 | `ResourceUpdater` | 下载远端资源、应用更新包、处理重试逻辑 | Updatable |
| **资源加载器** | **`ResourceLoader`** | 核心加载引擎：任务队列 + 对象池缓存 + 依赖解析 + 解密 | **全部模式** |

`ResourceLoader` 是唯一在所有模式下都工作的子系统（构造函数中直接创建），其余子系统按需创建。

### 3.3 三种资源模式

| 模式 | 枚举值 | 生命周期方法 | 说明 |
|---|---|---|---|
| **Package** | `ResourceMode.Package` | `InitResources(InitResourcesCompleteCallback)` | **当前项目使用**。资源全部打包在只读区，一次初始化即可。 |
| `Updatable` | `ResourceMode.Updatable` | `CheckVersionList` → `UpdateVersionList` → `VerifyResources` → `CheckResources` → `UpdateResources` | 预下载模式，进入游戏前完成全部资源更新。 |
| `UpdatableWhilePlaying` | `ResourceMode.UpdatableWhilePlaying` | 同上 + 边玩边按需下载 | 使用时下载模式，未就绪的资源标记为 NotReady 状态。 |

当前 GGF 项目固定使用 `Package` 模式。`Updatable` 系列依赖 `IFileSystemManager` 和 `IDownloadManager`，这两个模块在 Godot 层尚未实现。

### 3.4 版本列表序列化器体系

五种序列化器对应不同场景：

| 序列化器 | 用途 | 当前状态 |
|---|---|---|
| `PackageVersionListSerializer` | 单机模式版本列表，支持 V0/V1/V2 三种二进制格式 | **已使用**，通过 GDFBuiltinVersionListSerializer 注册回调 |
| `UpdatableVersionListSerializer` | 可更新模式远程版本列表 | 预留 |
| `ReadOnlyVersionListSerializer` | 本地只读区版本列表（`GameFrameworkList.dat`） | 预留 |
| `ReadWriteVersionListSerializer` | 本地读写区版本列表 | 预留 |
| `ResourcePackVersionListSerializer` | 资源包（DLC / 热更包）版本列表 | 预留 |

---

## 四、ResourceLoader（加载引擎）

`ResourceManager.ResourceLoader` — 三层嵌套私有类，是整个加载系统的心脏。

### 4.1 核心组成

```
ResourceLoader
├── TaskPool<LoadResourceTaskBase> m_TaskPool        ← 优先级任务调度
│   └── LoadResourceAgent (实现 ITaskAgent)          ← 每个 Agent 绑定一个 ILoadResourceAgentHelper
├── IObjectPool<AssetObject> m_AssetPool             ← 已加载资产的缓存池
├── IObjectPool<ResourceObject> m_ResourcePool       ← 已加载原始资源的缓存池
├── Dictionary<object, int> m_AssetDependencyCount   ← 资产引用计数
├── Dictionary<object, int> m_ResourceDependencyCount← 资源引用计数
├── Dictionary<object, object> m_AssetToResourceMap  ← 资产 → 资源反向映射
└── Dictionary<string, object> m_SceneToAssetMap     ← 场景名称 → 资产映射
```

**两个对象池是关键**：
- **AssetPool**：缓存加载完成的资产对象（含依赖资产引用、资源反向映射）。同一资产名命中缓存时直接返回，无需重新加载。
- **ResourcePool**：缓存原始资源数据。同一资源（如纹理文件）被多个资产共享时只加载一次。

两个池通过 `IObjectPoolManager` 创建，支持自动释放间隔（`AutoReleaseInterval`）、容量（`Capacity`）、过期时间（`ExpireTime`）、优先级（`Priority`）。

### 4.2 加载任务继承体系

```
TaskBase (abstract)
  └── LoadResourceTaskBase (abstract)
        ├── LoadAssetTask           ← 加载普通资产（含依赖解析）
        ├── LoadDependencyAssetTask ← 加载依赖资产（子任务，挂载到 mainTask）
        └── LoadSceneTask           ← 加载场景（特殊资产类型，isScene=true）
```

`LoadDependencyAssetTask` 是递归创建的子任务，其 `mainTask` 指向最终的 `LoadAssetTask` 或 `LoadSceneTask`。依赖加载完成后将依赖资产列表传递回主任务。

### 4.3 LoadAsset 完整流程

```
LoadAsset(assetName, assetType, priority, callbacks, userData)
│
├── CheckAsset(assetName)
│   ├── string.IsNullOrEmpty → false
│   ├── m_AssetInfos.TryGetValue → AssetInfo
│   │   └── GetResourceInfo(assetInfo.ResourceName) → ResourceInfo
│   │       ├── resourceInfo.IsLoadFromBinary → false (二进制应走 LoadBinary)
│   │       ├── resourceInfo.Ready == false && mode != UpdatableWhilePlaying → false
│   │       └── 提取 dependencyAssetNames
│   └── 任一失败 → 回调 LoadAssetFailureCallback (NotExist / NotReady / TypeError)
│
├── 创建 LoadAssetTask mainTask
│
├── foreach dependencyAssetName:
│   └── LoadDependencyAsset(depName, priority, mainTask, userData)
│       ├── 递归 CheckAsset
│       ├── 递归创建 LoadDependencyAssetTask
│       └── TaskPool.AddTask(dependencyTask)
│
├── TaskPool.AddTask(mainTask)          ← 加入优先级任务池
│
└── [if !resourceInfo.Ready]
    └── m_ResourceManager.UpdateResource(resourceInfo.ResourceName)
```

### 4.4 LoadResourceAgent 状态机

每个 `LoadResourceAgent` 绑定一个 `ILoadResourceAgentHelper`（即 Godot 层的 `DefaultLoadResourceAgentHelper`），通过 **事件驱动** 完成加载流程：

```
Start(task)
├── 条件检查（任一不满足 → return HasToWait）
│   ├── resourceInfo.Ready == false          → HasToWait
│   ├── IsAssetLoading(assetName) == true    → HasToWait (同名资产正在加载)
│   ├── [非场景] AssetPool.Spawn(assetName)   → Done (缓存命中，直接回调用户)
│   ├── 依赖资产未全部 CanSpawn               → HasToWait (等待依赖)
│   ├── IsResourceLoading(resourceName)        → HasToWait (同名资源正在加载)
│   └── ResourcePool.Spawn(resourceName) != null → CanResume (资源缓存命中，跳到 Parse/Load)
│
├── s_LoadingAssetNames.Add(assetName)
├── s_LoadingResourceNames.Add(resourceName)
│
└── 根据 LoadType 发起加载：
    ├── LoadFromFile → m_Helper.ReadFile(fullPath)
    └── LoadFromMemory* → m_Helper.ReadBytes(fullPath)
        └── [解密后] → m_Helper.ParseBytes(bytes)

事件驱动链：
  ReadFileComplete
    └── ResourceObject.Create → ResourcePool.Register
    └── OnResourceObjectReady → task.LoadMain(this, resourceObject)
          └── m_Helper.LoadAsset(resource, assetName, type, isScene)
                └── LoadComplete 事件
                      ├── AssetObject.Create (含依赖资产列表 + resourceObject.Target)
                      ├── AssetPool.Register
                      ├── m_AssetToResourceMap[asset] = resource.Target
                      ├── s_LoadingAssetNames.Remove
                      ├── s_LoadingResourceNames.Remove
                      └── OnAssetObjectReady → task.OnLoadAssetSuccess → 用户回调

  ReadBytesComplete
    └── [解密] → m_Helper.ParseBytes(bytes)
          └── ParseBytesComplete 事件 → ResourceObject.Create → 同上

  Error (任何步骤)
    └── m_Helper.Reset()
    └── s_LoadingAssetNames / s_LoadingResourceNames Remove
    └── task.OnLoadAssetFailure → 用户回调
```

### 4.5 加载类型与解密

| LoadType 值 | 含义 | LoadAsset 路径 |
|---|---|---|
| `LoadFromFile = 0` | 从文件加载为 Godot Resource | `ReadFile` → `LoadAsset` |
| `LoadFromMemory = 1` | 从内存加载（二进制数据） | `ReadBytes` → `ParseBytes` → `LoadAsset` |
| `LoadFromMemoryAndQuickDecrypt = 2` | 内存加载 + 快速解密 | `ReadBytes` → XOR 解密 → `ParseBytes` |
| `LoadFromMemoryAndDecrypt = 3` | 内存加载 + 完整解密 | `ReadBytes` → XOR 解密 → `ParseBytes` |
| `LoadFromBinary = 4` | 纯二进制（不走 Asset 加载） | 仅 `LoadBinary` 可用 |
| `LoadFromBinaryAndQuickDecrypt = 5` | 二进制 + 快速解密 | 仅 `LoadBinary` 可用 |
| `LoadFromBinaryAndDecrypt = 6` | 二进制 + 完整解密 | 仅 `LoadBinary` 可用 |

默认解密算法：取资源 `HashCode` 的 4 字节作为密钥，执行 XOR 运算。`QuickDecrypt` 使用 `GetQuickSelfXorBytes`（更快的变体），`Decrypt` 使用 `GetSelfXorBytes`。

---

## 五、ResourceIniter（Package 模式初始化器）

`ResourceManager.ResourceIniter` — 私有嵌套类。

### 5.1 流程

```
InitResources(currentVariant)
└── m_ResourceHelper.LoadBytes(
        "res://GameFrameworkVersion.dat",  ← 版本列表文件
        new LoadBytesCallbacks(OnSuccess, OnFailure),
        null)

OnLoadPackageVersionListSuccess:
├── new MemoryStream(bytes) → PackageVersionListSerializer.Deserialize(memoryStream)
│   └── 走 GDFBuiltinVersionListSerializer 注册的 V0/V1/V2 回调
├── 从 versionList 提取:
│   ├── ApplicableGameVersion / InternalResourceVersion
│   ├── Assets[] → m_AssetInfos (Dictionary<string, AssetInfo>)
│   ├── Resources[] → m_ResourceInfos (Dictionary<ResourceName, ResourceInfo>)
│   │   └── 跳过 Variant 不匹配的资源
│   ├── FileSystems[] → m_CachedFileSystemNames (临时)
│   └── ResourceGroups[] → m_ResourceGroups
│       └── defaultResourceGroup = GetOrAddResourceGroup("")
├── ResourceInitComplete()  ← 触发 OnInitResourcesComplete 回调
└── [finally] m_CachedFileSystemNames.Clear() + memoryStream.Dispose()

OnLoadPackageVersionListFailure:
└── throw GameFrameworkException
```

---

## 六、Godot 层 Helper 体系

### 6.1 ResourceHelperBase / DefaultResourceHelper

**文件**：`GodotGameFrameworkCore/Resource/ResourceHelperBase.cs` / `DefaultResourceHelper.cs`

```csharp
public abstract partial class ResourceHelperBase : GameFrameworkComponent, IResourceHelper
{
    public abstract void LoadBytes(string fileUri, LoadBytesCallbacks callbacks, object userData);
    public abstract void UnloadScene(string sceneAssetName, UnloadSceneCallbacks callbacks, object userData);
    public abstract void Release(object objectToRelease);
}
```

`DefaultResourceHelper` 实现：

| 方法 | 实现 | 备注 |
|---|---|---|
| `LoadBytes` | `FileAccess.FileExists` → `FileAccess.Open` → `file.GetBuffer(length)` → `Stopwatch` 计时 → `LoadBytesSuccessCallback(fileUri, bytes, duration, userData)` | 错误时走 `LoadBytesFailureCallback`，异常走 `try/catch` |
| `UnloadScene` | 直接触发 `UnloadSceneFailureCallback` | **未实现**，预留后续 Phase |
| `Release` | 空实现 | Godot 引擎通过引用计数自动管理资源生命周期，强制 Dispose 会导致悬挂指针异常 |

### 6.2 LoadResourceAgentHelperBase / DefaultLoadResourceAgentHelper

**文件**：`LoadResourceAgentHelperBase.cs` / `DefaultLoadResourceAgentHelper.cs`

```csharp
public abstract partial class LoadResourceAgentHelperBase : GodotComponent, ILoadResourceAgentHelper
{
    // 六个事件
    public abstract event EventHandler<LoadResourceAgentHelperUpdateEventArgs> LoadResourceAgentHelperUpdate;
    public abstract event EventHandler<LoadResourceAgentHelperReadFileCompleteEventArgs> LoadResourceAgentHelperReadFileComplete;
    public abstract event EventHandler<LoadResourceAgentHelperReadBytesCompleteEventArgs> LoadResourceAgentHelperReadBytesComplete;
    public abstract event EventHandler<LoadResourceAgentHelperParseBytesCompleteEventArgs> LoadResourceAgentHelperParseBytesComplete;
    public abstract event EventHandler<LoadResourceAgentHelperLoadCompleteEventArgs> LoadResourceAgentHelperLoadComplete;
    public abstract event EventHandler<LoadResourceAgentHelperErrorEventArgs> LoadResourceAgentHelperError;

    // 六个方法
    public abstract void ReadFile(string fullPath);
    public abstract void ReadFile(IFileSystem fileSystem, string name);
    public abstract void ReadBytes(string fullPath);
    public abstract void ReadBytes(IFileSystem fileSystem, string name);
    public abstract void ParseBytes(byte[] bytes);
    public abstract void LoadAsset(object resource, string assetName, Type assetType, bool isScene);
    public abstract void Reset();
}
```

`DefaultLoadResourceAgentHelper` 实现：

| 方法 | 实现 | 备注 |
|---|---|---|
| `ReadFile(fullPath)` | `FileAccess.FileExists` → `ResourceLoader.Load(fullPath)` → 触发 `ReadFileComplete` 事件 | 调用 Godot 引擎 API 加载为 Godot.Resource |
| `ReadFile(fileSystem, name)` | 直接触发 `OnError(NotExist, "...")` | **不支持 FileSystem 模式** |
| `ReadBytes(fullPath)` | `FileAccess.Open` → `file.GetBuffer` → 触发 `ReadBytesComplete` 事件 | 纯字节流读取 |
| `ReadBytes(fileSystem, name)` | 直接触发 `OnError(NotExist, "...")` | **不支持 FileSystem 模式** |
| `ParseBytes(bytes)` | 直接触发 `ParseBytesComplete`，将 `bytes` 作为资源对象传递 | 在 Godot 单机模式下，字节流本身即是"资源" |
| `LoadAsset(resource, ...)` | 场景 → 直接返回；已是 `Godot.Resource` → 直接返回；否则 `ResourceLoader.Load(assetName, typeName)` | 有类型检查 |
| `Reset` | 空实现 | 当前无内部状态需重置 |

### 6.3 ResourceExtension（async/await）

**文件**：`ResourceExtension.cs`

```csharp
public static Task<T> LoadAssetAsync<T>(this ResourceComponent resourceComponent, string assetPath)
    where T : class
{
    var tcs = new TaskCompletionSource<T>();
    resourceComponent.LoadAssetAsync(assetPath, typeof(T),
        asset => { if (asset is T result) tcs.TrySetResult(result);
                   else tcs.TrySetException(new InvalidOperationException(...)); },
        error => tcs.TrySetException(new InvalidOperationException(...)));
    return tcs.Task;
}
```

将回调式 API 封装为 `Task<T>`，支持 `await GF.Resource.LoadAssetAsync<Texture2D>("res://icon.png")`。

### 6.4 AsyncLoadTask（直接模式内部数据结构）

```csharp
private class AsyncLoadTask
{
    public string AssetPath;
    public Type AssetType;
    public Action<object> OnSuccess;
    public Action<string> OnFailure;
}
```

---

## 七、版本列表体系

### 7.1 GDFBuiltinVersionListSerializer

**文件**：`GodotGameFrameworkCore/Resource/GDFBuiltinVersionListSerializer.cs`

静态类，为 `PackageVersionListSerializer` 注册 **V0 / V1 / V2** 三个版本的反序列化回调和 V2 的序列化回调：

```
RegisterPackageDeserializeCallbacks(serializer)
├── RegisterDeserializeCallback(0, V0_Callback)
├── RegisterDeserializeCallback(1, V1_Callback)
└── RegisterDeserializeCallback(2, V2_Callback)

RegisterPackageSerializeCallbacks(serializer)
└── RegisterSerializeCallback(2, V2_Callback)
```

**V2 格式**（当前使用，最完整）：

```
Header: 4 字节随机加密密钥 + 版本号
ApplicableGameVersion: 加密字符串
InternalResourceVersion: 7 位编码整数
Assets[]: { Name(加密), DependencyAssetIndexCount, DependencyAssetIndexes[] }
Resources[]: { Name, Variant, Extension, LoadType, Length, HashCode, AssetIndexCount, AssetIndexes[] }
FileSystems[]: { Name, ResourceIndexCount, ResourceIndexes[] }
ResourceGroups[]: { Name, ResourceIndexCount, ResourceIndexes[] }
```

- **V0**：Asset 名称内嵌在 Resource 中，二分查找建立索引，无 FileSystem
- **V1**：Asset / Resource 分离，7 位编码整数，无 FileSystem
- **V2**：完整格式，增加 FileSystem 和 ResourceGroup 支持

### 7.2 GDFResourceBuilder

**文件**：`GodotGameFrameworkCore/Resource/GDFResourceBuilder.cs`

编辑器模式下由 `ResourceComponent.OnInit()` 自动调用，扫描 `res://` 生成 `GameFrameworkVersion.dat`。

```
BuildVersionList(readOnlyPath, outputPath, gameVersion, resourceVersion)
├── ScanDirectory(readOnlyPath, files)                  // 递归扫描
│   ├── 排除目录: ".godot"
│   └── 排除扩展名: .import, .uid, .cs, .gd, .meta, .csproj, .sln, .dll, .asmdef, .tmp, .log
├── 对每个文件:
│   ├── 创建 Asset (Name = 相对路径, DependencyAssetIndexes = [i])
│   └── 创建 Resource (Name, Extension, Length=file.GetLength, HashCode=CRC32, AssetIndexes=[i])
├── new PackageVersionList → SerializeVersionList(outputPath, versionList)
│   └── [内容未变化则跳过写入，避免触发不必要的资源重载]
└── Log.Info("Version list generated. Assets: X, Resources: Y")
```

---

## 八、事件系统

`IResourceManager` 暴露 11 个事件（全部为 Updatable 模式预留，Package 模式下不触发）：

| 事件 | 触发时机 |
|---|---|
| `ResourceVerifyStart` / `ResourceVerifySuccess` / `ResourceVerifyFailure` | 资源校验阶段 |
| `ResourceApplyStart` / `ResourceApplySuccess` / `ResourceApplyFailure` | 应用资源包阶段 |
| `ResourceUpdateStart` / `ResourceUpdateChanged` / `ResourceUpdateSuccess` / `ResourceUpdateFailure` | 资源下载更新阶段 |
| `ResourceUpdateAllComplete` | 全部资源更新完成 |

---

## 九、当前限制与预留功能

| 功能 | 状态 | 影响范围 |
|---|---|---|
| **FileSystem 模式** | 不可用 | `DefaultLoadResourceAgentHelper` 中 FileSystem 重载直接报错 `NotExist` |
| **UnloadScene** | 不可用 | `DefaultResourceHelper.UnloadScene()` 直接触发失败回调 |
| **Updatable 模式** | 不可用 | 依赖未实现的 `IDownloadManager` + `IFileSystemManager` |
| **UpdatableWhilePlaying** | 不可用 | 同上 |
| **直接模式 UnloadAsset** | 空实现 | Godot 引擎自动引用计数管理，不手动释放 |
| **资源加密** | 可用 | XOR 加密/解密回调完整实现，可选 QuickDecrypt 或 Decrypt |
| **对象池自动释放** | 可用 | `AssetPool` / `ResourcePool` 通过 `IObjectPoolManager` 配置间隔、容量、过期时间、优先级 |

---

## 十、完整类图

```
GameFrameworkModule (abstract, internal)
  └── ResourceManager (internal sealed partial)  ← Priority = 3
        ├── ResourceIniter (private sealed)
        │     └── 加载 + 反序列化 GameFrameworkVersion.dat → 构建索引字典
        ├── ResourceLoader (private sealed partial)
        │     ├── TaskPool<LoadResourceTaskBase>
        │     │     └── LoadResourceAgent (private sealed partial, ITaskAgent<LoadResourceTaskBase>)
        │     │           ├── 绑定一个 ILoadResourceAgentHelper
        │     │           └── 事件链: ReadFile/ReadBytes → ParseBytes → LoadAsset → 回调用户
        │     ├── IObjectPool<AssetObject> (m_AssetPool)
        │     ├── IObjectPool<ResourceObject> (m_ResourcePool)
        │     ├── AssetDependencyCount / ResourceDependencyCount (引用计数)
        │     ├── AssetToResourceMap (反向映射)
        │     └── SceneToAssetMap (场景映射)
        ├── ResourceChecker (private sealed)      ← Updatable 模式
        ├── ResourceUpdater (private sealed)      ← Updatable 模式
        ├── ResourceVerifier (private sealed)     ← Updatable 模式
        └── VersionListProcessor (private sealed) ← Updatable 模式

GodotComponent → GameFrameworkComponent → ResourceComponent (partial, 入口组件)
                                               └── AsyncLoadTask (private class)

GodotComponent → GameFrameworkComponent → ResourceHelperBase (abstract, IResourceHelper)
                                               └── DefaultResourceHelper (sealed)

GodotComponent → LoadResourceAgentHelperBase (abstract, ILoadResourceAgentHelper)
                     └── DefaultLoadResourceAgentHelper (sealed)

static GDFBuiltinVersionListSerializer   ← V0/V1/V2 序列化/反序列化注册
static GDFResourceBuilder                ← 编辑器构建版本列表（扫描 res:// → .dat）
static ResourceExtension                 ← LoadAssetAsync<T>() 扩展方法 (Task<T>)

纯 C# 接口层:
  IResourceManager / IResourceHelper / ILoadResourceAgentHelper
  IResourceGroup / IResourceGroupCollection
```

---

## 十一、数据流总结

```
  Editor                          Runtime (Package Mode)
  ──────                          ──────────────────────
  GDFResourceBuilder              ResourceComponent.OnInit()
    │                               │
    ├─ 扫描 res://                  ├─ 创建 ResourceHelper
    ├─ 计算 CRC32                   ├─ 创建 LoadResourceAgentHelper
    ├─ 构建 PackageVersionList      ├─ SetObjectPoolManager
    └─ 序列化 V2 ──────────────────►├─ InitResources()
                                     │    └─ ResourceIniter
                                     │         └─ DefaultResourceHelper.LoadBytes
                                     │              └─ FileAccess.Open("res://GameFrameworkVersion.dat")
                                     │                   └─ GDFBuiltinVersionListSerializer.Deserialize V2
                                     │                        └─ 构建 AssetInfos + ResourceInfos + ResourceGroups
                                     │                             └─ OnInitResourcesComplete
                                     │
                                     └─ 用户调用 LoadAssetAsync
                                          ├─ [管道模式]
                                          │   └─ ResourceManager.LoadAsset
                                          │        └─ ResourceLoader.LoadAsset
                                          │             └─ LoadResourceAgent.Start
                                          │                  └─ DefaultLoadResourceAgentHelper.ReadFile/ReadBytes
                                          │                       └─ (事件链) → 用户回调
                                          └─ [直接模式]
                                              └─ Godot.ResourceLoader.LoadThreadedRequest
                                                   └─ _Process 轮询 → 用户回调
```
