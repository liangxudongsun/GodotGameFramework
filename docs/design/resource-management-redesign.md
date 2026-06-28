# 资源管理系统重构设计方案

> **日期**: 2026-06-28
> **状态**: 设计草案
> **关联**: [Game Framework](https://gameframework.cn/) → Godot 4.6.2 移植

---

## 目录

1. [问题分析](#1-问题分析)
2. [设计目标](#2-设计目标)
3. [新架构总览](#3-新架构总览)
4. [详细设计](#4-详细设计)
   - [4.1 IResourceManager 接口精简](#41-iresourcemanager-接口精简)
   - [4.2 GodotResourceManager](#42-godotresourcemanager)
   - [4.3 ResourceComponent 简化](#43-resourcecomponent-简化)
   - [4.4 HotUpdateManager](#44-hotupdatemanager)
   - [4.5 Entity/UI/Sound/Scene 组件变更](#45-entityuisoundscene-组件变更)
   - [4.6 BaseComponent 清理](#46-basecomponent-清理)
   - [4.7 ResourceMode 简化](#47-resourcemode-简化)
5. [文件变更清单](#5-文件变更清单)
   - [5.1 删除的文件](#51-删除的文件)
   - [5.2 修改的文件](#52-修改的文件)
   - [5.3 新建的文件](#53-新建的文件)
6. [向后兼容性](#6-向后兼容性)
7. [迁移步骤](#7-迁移步骤)
8. [验证方案](#8-验证方案)
9. [风险与应对](#9-风险与应对)

---

## 1. 问题分析

当前资源管理系统移植自 **Game Framework**（Jiang Yin），其设计基于 **Unity AssetBundle 管线**，在 Godot 环境下存在以下根本性问题：

### 1.1 热更新模式完全未实现

`ResourceComponent.cs:121-148` 的 `ResolveResourceMode()` 方法：

```csharp
case ResourceMode.Updatable:
    Log.Warning("Updatable mode is not yet implemented. ... Falling back to Package mode.");
    return ResourceMode.Package;

case ResourceMode.UpdatableWhilePlaying:
    Log.Warning("... Falling back to Package mode.");
    return ResourceMode.Package;
```

`Updatable` 和 `UpdatableWhilePlaying` 枚举值虽然存在，但运行时全部回退到 `Package` 模式。

### 1.2 下载管线不可用

- `IDownloadManager` 和 `DownloadManager` 在纯 C# 层有完整实现
- **但没有任何 Godot 层的 `IDownloadAgentHelper` 实现**—HTTP 下载实际无法进行
- `DownloadCounter` 等速度测量类也未被使用

### 1.3 自定义文件系统不可用

- `IFileSystem` / `FileSystem` 在纯 C# 层有完整实现
- `DefaultLoadResourceAgentHelper.ReadFile(IFileSystem, ...)` 直接抛出异常：

```csharp
// DefaultLoadResourceAgentHelper.cs
public void ReadFile(IFileSystem fileSystem, string fullPath)
    => throw new GameFrameworkException("Not supported.");
```

### 1.4 编辑器与运行时使用两套完全不同代码

| 维度 | 编辑器模式 | 运行时模式 |
|------|-----------|-----------|
| **类** | `EditorResourceManager` | GF `ResourceManager` |
| **加载方式** | `ResourceLoader.Load()` 直接调用 | `TaskPool` → `LoadResourceAgent` → `DefaultLoadResourceAgentHelper` → `ResourceLoader.Load()` |
| **管线初始化** | 无（立即可用） | 需加载 `GameFrameworkVersion.dat` 版本列表 |
| **管线操作** | 全部抛出 `NotSupportedException` | 全部走 GF 管线 |

**最终都调用 `ResourceLoader.Load()`**，但中间隔了多层不必要的抽象。

### 1.5 过度抽象层

```
IResourceManager
├── IResourceHelper           ← 最终只是读文件
│   └── ResourceHelperBase
│       └── DefaultResourceHelper
├── ILoadResourceAgentHelper  ← 最终只是调 ResourceLoader.Load()
│   └── LoadResourceAgentHelperBase
│       └── DefaultLoadResourceAgentHelper
├── IObjectPool<AssetObject>  ← 重复 Godot 内置资源缓存
├── IObjectPool<ResourceObject>
├── IFileSystemManager        ← 完全不可用
├── IDownloadManager          ← 缺少 AgentHelper 实现
└── ...
```

### 1.6 代码规模与可用性对比

| 层次 | 文件数 | 代码行 | 实际可用 |
|------|--------|--------|----------|
| `GameFramework/Resource/` | ~25 个 | ~8000 行 | 仅接口定义 |
| `GodotGameFrameworkCore/Resource/` | ~8 个 | ~1200 行 | 仅 EditorResourceManager ~400 行 |
| **合计** | **~33 个** | **~9200 行** | **~4% 可用** |

### 1.7 核心矛盾

Game Framework 的资源管线是为 **Unity AssetBundle** 设计的：
- AssetBundle 需要版本列表管理（哪个资源在哪个 Bundle 中）
- 需要 CRC 校验（Bundle 可能损坏）
- 需要资源包 Apply 机制（将下载的 Bundle 合并到本地存储）
- Unity 没有内置的 "按路径加载资源" 机制

**Godot 完全不同**：
- Godot 的 `ResourceLoader.Load("res://path")` 原生就支持按路径加载
- Godot 内置引用计数缓存，无需自定义对象池
- Godot 的 PCK 文件格式就是天然的 "资源包" 格式
- `ProjectSettings.LoadResourcePack()` 原生支持运行时装载资源包

---

## 2. 设计目标

### 2.1 核心原则

1. **Let Godot be Godot** — 直接使用 `ResourceLoader` / `ProjectSettings.LoadResourcePack()` / `ResourceSaver` 等 Godot 原生 API，不再在之上封装 Unity 风格的抽象
2. **统一代码路径** — 编辑器与运行时使用同一套实现，仅热更新路径有差异
3. **最小化抽象** — 移除不必要的接口层和辅助器层
4. **真正可用的热更新** — 版本检查 + PCK 下载 + PCK 装载的完整管线

### 2.2 具体目标

| 目标 | 说明 |
|------|------|
| 替换 GF `ResourceManager` | 删除其内部类：ResourceLoader、ResourceIniter、VersionListProcessor、ResourceVerifier、ResourceChecker、ResourceUpdater 等约 20 个文件 |
| 简化接口 | IResourceManager 从 90+ 成员精简到 ~30 个 |
| 统合代码 | GodotResourceManager 同时替换 EditorResourceManager 和 GF ResourceManager |
| 热更新可用 | HotUpdateManager 处理版本检查 + PCK 下载 + 装载的完整流程 |
| 向后兼容 | Entity/UI/Sound/Scene 的 ShowEntity/OpenUIForm 等 API 无需修改 |

### 2.3 非目标

- 不改变 EntityManager/UIManager/SoundManager 等纯 C# 层的内部逻辑
- 不改变实体/UI 的基类继承体系（AbstractNode2DEntity、ControlUIForm 等）
- 不改变 GF 静态门面的调用方式（`GF.Entity.ShowEntity()` 继续可用）
- 不改变 Luban 配置管线

---

## 3. 新架构总览

```
┌──────────────────────────────────────────────────────────────────┐
│                     Godot 引擎层 (Godot API)                        │
│  ResourceLoader.{Load, Exists, HasCached, RemoveFromCache}          │
│  ProjectSettings.LoadResourcePack(pckPath)                         │
│  ResourceSaver.Save(resource, path)                                │
│  HTTPRequest node                                                  │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│               GodotResourceManager (GodotGameFrameworkCore)       │
│                                                                   │
│  ┌─────────────────────┐  ┌──────────────────────────────────┐   │
│  │  同步加载 LoadAsset  │  │  异步加载 LoadAssetAsync          │   │
│  │  ResourceLoader.Load│  │  LoadThreadedRequest/LoadThreaded │   │
│  │  → 回调             │  │  Get → TaskCompletionSource      │   │
│  └─────────────────────┘  └──────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  ResolvePath(assetName)                                 │   │
│  │  ┌─────────────────────────────────────┐                 │   │
│  │  │ user:// 路径优先（已装载的 PCK 资源）→ 存在则返回     │   │
│  │  │ res:// 路径回退（原生资源）              │                 │   │
│  │  └─────────────────────────────────────┘                 │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  加载引用计数 (m_LoadRefCount<string, int>)               │   │
│  │  用于 UnloadAsset 时的安全释放                            │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                     ResourceComponent                             │
│  Godot 场景树节点，薄封装层                                         │
│  - 实例化 GodotResourceManager                                     │
│  - 可选创建 HotUpdateManager                                       │
│  - 暴露快捷方法 LoadAsset<T> / LoadBinary / LoadText               │
└──────────────────────────┬───────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────────┐
│                      HotUpdateManager                             │
│  Node 派生组件                                                     │
│                                                                   │
│  ┌─────────────────┐  ┌──────────────┐  ┌────────────────────┐   │
│  │ CheckForUpdates │→│DownloadAndApply│→│ 资源装载完成        │   │
│  │ HTTP GET        │  │逐个下载 PCK   │  │PCK 已装载到 Godot  │   │
│  │ 版本 JSON 比较  │  │MD5 完整性校验 │  │后续 Load 自动生效  │   │
│  └─────────────────┘  └──────────────┘  └────────────────────┘   │
└──────────────────────────────────────────────────────────────────┘
```

### 文件结构

```
Framework/GodotGameFrameworkCore/Resource/
├── GodotResourceManager.cs    ← 核心实现（替换 Editor + GF 两个管理器）
├── ResourceComponent.cs       ← 简化组件（薄封装）
├── HotUpdateManager.cs        ← 热更新管理器（全新）
└── HotUpdateConfig.cs         ← 热更新配置模型（全新）

Framework/GameFramework/Resource/
├── IResourceManager.cs        ← 精简接口（保留被调用的成员）
├── ResourceMode.cs            ← 简化枚举（移除 UpdatableWhilePlaying）
├── LoadAssetCallbacks.cs      ← 保留不变（Entity/UI/Sound Manager 依赖）
├── LoadSceneCallbacks.cs      ← 保留不变
├── UnloadSceneCallbacks.cs    ← 保留不变
├── LoadBinaryCallbacks.cs     ← 保留不变
├── HasAssetResult.cs          ← 保留不变
├── LoadResourceStatus.cs      ← 保留不变
├── TaskInfo.cs                ← 保留不变
└── Delegates/*.cs             ← 回调委托保留不变
```

---

## 4. 详细设计

### 4.1 IResourceManager 接口精简

**当前问题**：接口有约 90 个成员，其中约 60 个从未被 EntityManager/UIManager/SoundManager/SceneManager 调用。

**保留的成员**（全部有实际调用者）：

```csharp
public interface IResourceManager
{
    // === 属性 ===
    string ReadOnlyPath { get; }
    string ReadWritePath { get; }
    ResourceMode ResourceMode { get; }
    int AssetCount { get; }
    int ResourceCount { get; }

    // === 资源查询 ===
    HasAssetResult HasAsset(string assetName);

    // === 同步加载（8 个重载，Entity/UI/Sound/Scene Manager 均使用）===
    void LoadAsset(string assetName, LoadAssetCallbacks loadAssetCallbacks);
    void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks);
    void LoadAsset(string assetName, LoadAssetCallbacks loadAssetCallbacks, object userData);
    void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks, object userData);
    void LoadAsset(string assetName, Type assetType, LoadAssetCallbacks loadAssetCallbacks);
    void LoadAsset(string assetName, Type assetType, int priority, LoadAssetCallbacks loadAssetCallbacks);
    void LoadAsset(string assetName, Type assetType, LoadAssetCallbacks loadAssetCallbacks, object userData);
    void LoadAsset(string assetName, Type assetType, int priority, LoadAssetCallbacks loadAssetCallbacks, object userData);

    // === 资源卸载 ===
    void UnloadAsset(object asset);

    // === 场景加载 ===
    void LoadScene(string sceneAssetName, LoadSceneCallbacks loadSceneCallbacks);
    void LoadScene(string sceneAssetName, int priority, LoadSceneCallbacks loadSceneCallbacks);
    void LoadScene(string sceneAssetName, LoadSceneCallbacks loadSceneCallbacks, object userData);
    void LoadScene(string sceneAssetName, int priority, LoadSceneCallbacks loadSceneCallbacks, object userData);
    void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks);
    void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks, object userData);

    // === 二进制加载 ===
    void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks);
    void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData);

    // === 任务信息 ===
    TaskInfo[] GetAllLoadAssetInfos();
    void GetAllLoadAssetInfos(List<TaskInfo> results);
}
```

**移除的所有成员清单**（按类别）：

| 类别 | 移除成员 | 原因 |
|------|---------|------|
| **事件** | `ResourceVerifyStart/Success/Failure`、`ResourceApplyStart/Success/Failure`、`ResourceUpdateStart/Changed/Success/Failure/AllComplete` | GF 管线事件，无人订阅 |
| **序列化器** | `PackageVersionListSerializer`、`UpdatableVersionListSerializer`、`ReadOnlyVersionListSerializer`、`ReadWriteVersionListSerializer`、`ResourcePackVersionListSerializer` | GF 版本列表格式不再使用 |
| **更新属性** | `UpdatePrefixUri`、`UpdateRetryCount`、`ApplyingResourcePackPath`、`ApplyWaitingCount`、`UpdatingResourceGroup`、`UpdateWaitingCount`、`UpdateWaitingWhilePlayingCount`、`UpdateCandidateCount` | GF 管线状态，无人使用 |
| **Agent 属性** | `LoadTotalAgentCount`、`LoadFreeAgentCount`、`LoadWorkingAgentCount`、`LoadWaitingTaskCount` | GF 内部实现细节 |
| **Pool 属性** | `AssetAutoReleaseInterval`、`AssetCapacity`、`AssetExpireTime`、`AssetPriority`、`ResourceAutoReleaseInterval`、`ResourceCapacity`、`ResourceExpireTime`、`ResourcePriority` | Godot 内置缓存自动管理 |
| **配置方法** | `SetReadOnlyPath`、`SetReadWritePath`、`SetResourceMode`、`SetCurrentVariant`、`SetObjectPoolManager`、`SetFileSystemManager`、`SetDownloadManager`、`SetDecryptResourceCallback`、`SetResourceHelper`、`AddLoadResourceAgentHelper` | 构造时一次性配置，不需要在接口中暴露 |
| **管线方法** | `InitResources`、`CheckVersionList`、`UpdateVersionList`、`VerifyResources`、`CheckResources`、`ApplyResources`、`UpdateResources`（含 group 重载）、`StopUpdateResources`、`VerifyResourcePack` | GF 管线方法，从未被调用 |
| **二进制路径** | `GetBinaryPath`（含 out 重载）、`GetBinaryLength`、`LoadBinaryFromFileSystem`（含 buffer 重载）、`LoadBinarySegmentFromFileSystem`（含 buffer 重载） | DataTable 等使用 `LoadBinary` 回调，不走这些方法 |
| **资源组** | `HasResourceGroup`、`GetResourceGroup()`、`GetResourceGroup(params string[])`、`GetAllResourceGroups()`、`GetAllResourceGroups(List)`、`GetResourceGroupCollection(params string[])`、`GetResourceGroupCollection(List)` | 无人使用 |

### 4.2 GodotResourceManager

**文件**: `Framework/GodotGameFrameworkCore/Resource/GodotResourceManager.cs`

这是整个重构的核心，同时替换当前的 `EditorResourceManager` 和 GF `ResourceManager`。

#### 4.2.1 类结构

```csharp
namespace GodotGameFramework.Resource
{
    public sealed partial class GodotResourceManager : IResourceManager, IDisposable
    {
        // ===== 常量 =====
        private const string RES_SCHEME = "res://";
        private const string USER_SCHEME = "user://";

        // ===== 属性 =====
        public string ReadOnlyPath { get; private set; } = RES_SCHEME;
        public string ReadWritePath { get; private set; } = USER_SCHEME;
        public ResourceMode ResourceMode { get; private set; } = ResourceMode.Package;
        public int AssetCount => m_LoadedAssets.Count;
        public int ResourceCount => m_LoadedAssets.Count;

        // ===== 内部状态 =====
        private bool m_UsePckOverride;
        private readonly Dictionary<string, Godot.Resource> m_LoadedAssets = new();
        private readonly Dictionary<string, int> m_LoadRefCount = new();  // 引用计数

        // ===== 初始化 =====
        public void Initialize(ResourceMode mode, bool usePckOverride);

        // ===== IResourceManager 实现 =====
        public HasAssetResult HasAsset(string assetName);
        public void UnloadAsset(object asset);
        public void LoadScene(...);      // 4 个重载
        public void UnloadScene(...);   // 2 个重载
        public void LoadBinary(...);    // 2 个重载
        public TaskInfo[] GetAllLoadAssetInfos();

        // ===== 核心内部方法 =====
        private void LoadAssetInternal(string assetName, Type assetType,
            LoadAssetCallbacks callbacks, object userData);
        private void LoadSceneInternal(string sceneAssetName,
            LoadSceneCallbacks callbacks, object userData);
        private void LoadBinaryInternal(string binaryAssetName,
            LoadBinaryCallbacks callbacks, object userData);

        // ===== PCK 感知路径解析 =====
        /// <summary>
        /// 解析资源路径。若启用了 PCK 覆盖模式，优先检测 user:// 路径
        ///（热更新 PCK 中的资源），不存在时回退到原始路径。
        /// </summary>
        private string ResolvePath(string assetName);

        // ===== Godot 原生异步加载（增强）=====
        /// <summary>
        /// 使用 Godot 的 LoadThreadedRequest/LoadThreadedGet 进行异步加载。
        /// 不在 IResourceManager 接口中（保持接口简单），可直接调用。
        /// </summary>
        public async Task<Godot.Resource> LoadAssetAsync(string assetPath,
            Type assetType = null, IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        // ===== 引用计数 == ==
        private void AddRef(string assetPath);
        private void ReleaseRef(string assetPath);
        public int GetRefCount(string assetPath);
    }
}
```

#### 4.2.2 核心流程

**LoadAssetInternal 流程**:

```
LoadAssetInternal(assetName, assetType, callbacks, userData)
  │
  ├─1. 参数校验
  │   └─ assetName 为空 → LoadAssetFailure(NotExist)
  │
  ├─2. 路径解析 ResolvePath(assetName)
  │   ├─ 启用了 PCK 覆盖且路径以 res:// 开头？
  │   │   └─ 构造 user:// 等效路径
  │   │       └─ ResourceLoader.Exists()?
  │   │           ├─ 存在 → 使用 user:// 路径
  │   │           └─ 不存在 → 使用原始路径
  │   └─ 未启用 PCK 覆盖 → 使用原始路径
  │
  ├─3. 存在性检查 ResourceLoader.Exists(path)
  │   └─ 不存在 → LoadAssetFailure(NotExist)
  │
  ├─4. 同步加载 ResourceLoader.Load(path, typeHint)
  │
  ├─5. 类型校验（若指定了 assetType）
  │   └─ 类型不匹配 → LoadAssetFailure(AssetError)
  │
  ├─6. 成功回调 LoadAssetSuccess(assetName, resource, duration, userData)
  │
  └─7. 记录加载信息
      ├─ m_LoadedAssets[assetName] = resource
      └─ AddRef(assetName)
```

**ResolvePath 实现**:

```csharp
private string ResolvePath(string assetName)
{
    if (string.IsNullOrEmpty(assetName))
        return assetName;

    // 如果未启用 PCK 覆盖，直接返回原始路径
    if (!m_UsePckOverride)
        return assetName;

    // 仅对 res:// 路径进行 PCK 覆盖检测
    if (assetName.StartsWith(RES_SCHEME, StringComparison.OrdinalIgnoreCase))
    {
        string userPath = USER_SCHEME + assetName.Substring(RES_SCHEME.Length);

        // 优先使用 user://（热更新资源）
        if (Godot.ResourceLoader.Exists(userPath))
            return userPath;
    }

    return assetName;
}
```

**LoadSceneInternal 流程**:

```
LoadSceneInternal(sceneAssetName, callbacks, userData)
  │
  ├─1. 路径解析（同上）
  ├─2. ResourceLoader.Exists() 校验
  ├─3. ResourceLoader.Load<PackedScene>(path)
  └─4. success: LoadSceneSuccess(name, duration, userData)
      failure: LoadSceneFailure(name, status, msg, userData)
```

**LoadBinaryInternal 流程**:

```
LoadBinaryInternal(binaryAssetName, callbacks, userData)
  │
  ├─1. FileAccess.FileExists(path)？
  │   └─ 不存在 → LoadBinaryFailure(NotExist)
  ├─2. FileAccess.Open(path, Read)
  ├─3. file.GetBuffer(file.GetLength())
  └─4. LoadBinarySuccess(name, bytes, duration, userData)
```

#### 4.2.3 异步加载（增强）

使用 Godot 的 `LoadThreadedRequest` / `LoadThreadedGet`，不阻塞主线程：

```csharp
public async Task<Godot.Resource> LoadAssetAsync(
    string assetPath,
    Type assetType = null,
    IProgress<float> progress = null,
    CancellationToken cancellationToken = default)
{
    // 解析路径（PCK 感知）
    string resolvedPath = ResolvePath(assetPath);

    if (!Godot.ResourceLoader.Exists(resolvedPath))
        throw new GameFrameworkException(
            $"Asset '{assetPath}' (resolved: '{resolvedPath}') does not exist.");

    // 发起异步加载请求
    Error error = Godot.ResourceLoader.LoadThreadedRequest(
        resolvedPath, assetType?.Name, useSubThreads: true);

    if (error != Error.Ok)
        throw new GameFrameworkException(
            $"Failed to start threaded load for '{resolvedPath}': {error}");

    // 轮询加载进度（每一帧检查一次）
    while (!cancellationToken.IsCancellationRequested)
    {
        Godot.ResourceLoader.ThreadLoadStatus status =
            Godot.ResourceLoader.LoadThreadedGetStatus(resolvedPath, out float loadProgress);

        progress?.Report(loadProgress);

        switch (status)
        {
            case Godot.ResourceLoader.ThreadLoadStatus.Loaded:
                var resource = Godot.ResourceLoader.LoadThreadedGet(resolvedPath);
                AddRef(resolvedPath);
                return resource;

            case Godot.ResourceLoader.ThreadLoadStatus.Failed:
            case Godot.ResourceLoader.ThreadLoadStatus.InvalidResource:
                throw new GameFrameworkException(
                    $"Threaded load failed for '{resolvedPath}' (status: {status})");

            case Godot.ResourceLoader.ThreadLoadStatus.InProgress:
            case Godot.ResourceLoader.ThreadLoadStatus.Started:
                await Task.Delay(16, cancellationToken); // ~1 帧
                break;
        }
    }

    cancellationToken.ThrowIfCancellationRequested();
    return null; // unreachable
}
```

#### 4.2.4 引用计数管理

```csharp
private readonly Dictionary<string, int> m_LoadRefCount = new();

private void AddRef(string assetPath)
{
    if (!m_LoadRefCount.ContainsKey(assetPath))
        m_LoadRefCount[assetPath] = 0;
    m_LoadRefCount[assetPath]++;
}

private void ReleaseRef(string assetPath)
{
    if (!m_LoadRefCount.TryGetValue(assetPath, out int count))
        return;

    if (count <= 1)
    {
        m_LoadRefCount.Remove(assetPath);
        // 可以选择性地从 Godot 缓存中移除，但不强制
        // Godot 的引用计数会自行管理生命周期
    }
    else
    {
        m_LoadRefCount[assetPath] = count - 1;
    }
}
```

**UnloadAsset 实现**（与当前 EditorResourceManager 行为一致）：

```csharp
public void UnloadAsset(object asset)
{
    // Godot 引擎通过引用计数自动管理资源生命周期。
    // 若需要显式从缓存移除，可调用 ResourceLoader.RemoveFromCache()。
    // 此处保留为空操作，与当前 EditorResourceManager 行为一致。
}
```

#### 4.2.5 与当前实现的对比

| 维度 | EditorResourceManager（当前） | GodotResourceManager（新） |
|------|-----------------------------|--------------------------|
| 加载方式 | `ResourceLoader.Load()` | `ResourceLoader.Load()` + `LoadThreadedRequest` |
| PCK 感知 | 无 | `ResolvePath()` 优先检测 user:// |
| 异步加载 | 无 | `LoadAssetAsync()` 使用 Godot 原生线程加载 |
| 引用计数 | 无 | `m_LoadRefCount` 追踪 |
| 异常处理 | 手动 try-catch | 手动 try-catch + 回调 |
| 代码行 | ~425 行 | ~300 行（精简后） |

### 4.3 ResourceComponent 简化

**文件**: `Framework/GodotGameFrameworkCore/Resource/ResourceComponent.cs`

#### 4.3.1 当前问题

当前 `ResourceComponent.OnInit()` 包含：

- Helper 类型名配置（`m_ResourceHelperTypeName`）
- Agent Helper 类型名配置（`m_LoadResourceAgentHelperTypeName`）
- Agent 创建循环
- `ResolveResourceMode()` 回退逻辑
- `InitRuntimeMode()` 版本列表加载
- 约 210 行代码

**所有这些最终都只做了一件事：让 `ResourceLoader.Load()` 能被调用。**

#### 4.3.2 新设计

```csharp
namespace GodotGameFramework.Resource
{
    public sealed partial class ResourceComponent : GameFrameworkComponent
    {
        // ===== 编辑器可配置参数 =====
        [Export]
        private bool m_UsePckOverride = false;

        [Export]
        private string m_HotUpdateServerUrl = "";

        // ===== 核心管理器 =====
        private GodotResourceManager m_ResourceManager;

        // ===== 热更新管理器（可选）=====
        private HotUpdateManager m_HotUpdateManager;

        // ===== 对外暴露 =====
        public IResourceManager ResourceManager => m_ResourceManager;
        public GodotResourceManager GodotResourceManager => m_ResourceManager;
        public HotUpdateManager HotUpdateManager => m_HotUpdateManager;

        // ===== 初始化 =====
        public override void OnInit()
        {
            base.OnInit();

            // 1. 创建统一资源管理器
            m_ResourceManager = new GodotResourceManager();

            // 2. 确定资源模式
            ResourceMode mode = m_UsePckOverride
                ? ResourceMode.Updatable
                : ResourceMode.Package;

            // 3. 初始化管理器
            //    - Package 模式：直接从 res:// 加载
            //    - Updatable 模式：启用 PCK 覆盖（user:// 优先）
            m_ResourceManager.Initialize(mode, m_UsePckOverride);

            Log.Info("[ResourceComponent] Initialized. Mode={0}, UsePckOverride={1}",
                mode, m_UsePckOverride);

            // 4. 可选：启动热更新
            if (!string.IsNullOrEmpty(m_HotUpdateServerUrl) && m_UsePckOverride)
            {
                m_HotUpdateManager = new HotUpdateManager();
                AddChild(m_HotUpdateManager);
                m_HotUpdateManager.CheckForUpdates(m_HotUpdateServerUrl);
                Log.Info("[ResourceComponent] HotUpdate enabled. Server={0}",
                    m_HotUpdateServerUrl);
            }

            ProcessMode = ProcessModeEnum.Always;
        }

        // ===== 快捷方法 =====
        public T LoadAsset<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
            {
                Log.Warning("[ResourceComponent] Asset path is invalid.");
                return null;
            }
            return Godot.ResourceLoader.Load<T>(path);
        }

        public object LoadAsset(string path, Type assetType)
        {
            if (string.IsNullOrEmpty(path))
            {
                Log.Warning("[ResourceComponent] Asset path is invalid.");
                return null;
            }
            return Godot.ResourceLoader.Load(path, assetType?.Name);
        }

        public byte[] LoadBinary(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Log.Warning("[ResourceComponent] File path is invalid.");
                return null;
            }
            if (!FileAccess.FileExists(filePath))
            {
                Log.Warning("[ResourceComponent] File '{0}' does not exist.", filePath);
                return null;
            }
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            return file?.GetBuffer((long)file.GetLength());
        }

        public string LoadText(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Log.Warning("[ResourceComponent] File path is invalid.");
                return null;
            }
            if (!FileAccess.FileExists(filePath))
            {
                Log.Warning("[ResourceComponent] File '{0}' does not exist.", filePath);
                return null;
            }
            using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            return file?.GetAsText();
        }

        // ===== GF 静态门面集成 =====
        // 注意：GF.Resource 返回的就是本组件实例
        // Entity/UI/Sound/Scene 组件通过本组件的 ResourceManager 属性获取 IResourceManager
    }
}
```

#### 4.3.3 变更摘要

| 项目 | 当前（~210 行） | 新（~80 行） |
|------|----------------|--------------|
| 辅助器类型名配置 | `m_ResourceHelperTypeName` (Export) | 删除 |
| Agent Helper 类型名配置 | `m_LoadResourceAgentHelperTypeName` (Export) | 删除 |
| Agent 数量配置 | `LoadResourceAgentHelperCount` (Export) | 删除 |
| Agent 创建循环 | `for (int i = 0; i < count; i++)` | 删除 |
| 模式回退 | `ResolveResourceMode()` | 直接判断 |
| 管线初始化 | `InitRuntimeMode()` + `GDFBuiltinVersionListSerializer` | 删除 |
| 版本列表加载 | `m_ResourceManager.InitResources()` | 删除 |
| 核心加载 | `ResourceLoader.Load()` | 保留（移至 GodotResourceManager） |
| 热更新 | 无 | 新增 `HotUpdateManager` 可选 |

### 4.4 HotUpdateManager

**文件**: `Framework/GodotGameFrameworkCore/Resource/HotUpdateManager.cs`

全新组件，处理热更新的完整生命周期。

#### 4.4.1 设计思路

Godot 的热更新机制与 Unity 完全不同：

| 需求 | Unity 方案 | Godot 方案 |
|------|-----------|-----------|
| 资源打包 | AssetBundle | PCK 文件（Godot 原生格式） |
| 运行时装载 | `AssetBundle.LoadFromFile()` | `ProjectSettings.LoadResourcePack()` |
| 路径映射 | 自定义 AB 名 → 资源 | PCK 中 res:// 路径自动可用 |
| 资源加载 | AssetBundle.Get/LoadAsset | ResourceLoader.Load() 直接可用 |

因此 HotUpdateManager 的职责非常清晰：

1. **版本检查**：获取远程版本信息，决定是否需要更新
2. **PCK 下载**：下载需要更新的 PCK 文件
3. **PCK 装载**：校验完整性并装载到 Godot 引擎
4. **持久化**：记录已应用的更新，下次启动自动装载

#### 4.4.2 配置模型

```csharp
// HotUpdateConfig.cs
namespace GodotGameFramework.Resource
{
    /// <summary>
    /// 版本清单 — 服务端 version.json 的数据模型。
    /// </summary>
    [Serializable]
    public class VersionManifest
    {
        public string version;                 // 语义版本号，如 "1.0.3"
        public int internalResourceVersion;   // 内部递增版本号
        public PckEntry[] pcks;                // 需要下载的 PCK 列表
    }

    [Serializable]
    public class PckEntry
    {
        public string name;    // "patch_1.0.3.pck"
        public string url;     // "https://cdn.example.com/patch_1.0.3.pck"
        public long size;      // 文件大小（字节）
        public string md5;     // MD5 校验和
    }

    /// <summary>
    /// 本地已装载版本记录 — 存储在 user://version.json
    /// </summary>
    [Serializable]
    public class LocalVersionRecord
    {
        public string version;
        public int internalResourceVersion;
        public string[] loadedPcks;   // 已装载的 PCK 文件名列表
    }
}
```

#### 4.4.3 类设计

```csharp
namespace GodotGameFramework.Resource
{
    public partial class HotUpdateManager : Node
    {
        // ===== 状态枚举 =====
        public enum UpdateState
        {
            Idle,               // 空闲（未开始检查）
            Checking,           // 检查更新中（HTTP 请求）
            UpdateAvailable,    // 发现新版本，等待下载
            Downloading,        // 下载中
            Verifying,          // 校验完整性
            Applying,           // 装载 PCK
            Complete,           // 更新完成
            Error,              // 出错
            UpToDate            // 已是最新
        }

        // ===== 事件 =====
        public event Action<UpdateState, UpdateState> OnStateChanged;
        public event Action<float> OnProgressChanged;     // 0.0 ~ 1.0
        public event Action OnUpdateComplete;
        public event Action<string> OnError;

        // ===== 属性 =====
        public UpdateState State { get; private set; } = UpdateState.Idle;
        public float Progress { get; private set; }
        public string LocalVersion { get; private set; }
        public string RemoteVersion { get; private set; }

        // ===== 配置 =====
        public string ServerUrl { get; set; }
        public string VersionFilePath { get; set; } = "user://version.json";

        // ===== 启动时自动装载已有 PCK =====
        /// <summary>
        /// 从 user://version.json 读取已记录版本并装载已有 PCK。
        /// 在游戏启动时、CheckForUpdates 之前调用。
        /// </summary>
        public void LoadExistingPcks();

        // ===== 检查更新 =====
        /// <summary>
        /// 检查远程服务器是否有新版本。
        /// 异步返回 true 表示有可用更新。
        /// </summary>
        public Task<bool> CheckForUpdates(string serverUrl);

        // ===== 下载并应用 =====
        /// <summary>
        /// 逐个下载待更新的 PCK，校验 MD5，装载到 Godot 引擎。
        /// </summary>
        public Task DownloadAndApply();

        // ===== 跳过此次更新 =====
        /// <summary>
        /// 跳过此版本更新（下次启动仍会检查）。
        /// </summary>
        public void SkipUpdate();

        // ===== 内部方法 =====
        private async Task<VersionManifest> FetchRemoteManifest(string url);
        private Task DownloadPck(PckEntry entry, string savePath);
        private bool VerifyMd5(string filePath, string expectedMd5);
        private void ApplyPck(string pckPath);
        private void SaveLocalVersionRecord(LocalVersionRecord record);
        private LocalVersionRecord LoadLocalVersionRecord();
    }
}
```

#### 4.4.4 完整生命周期

```
游戏启动
  │
  ├─ HotUpdateManager.LoadExistingPcks()
  │   └─ 读取 user://version.json
  │       └─ 对每个 loadedPck:
  │           ├─ user://{pck} 文件存在？
  │           │   ├─ 是 → ProjectSettings.LoadResourcePack()
  │           │   └─ 否 → 从记录中移除（文件丢失，下次全量更新）
  │           └─ 记录到 m_LoadedPcks
  │
  ├─ 游戏进入主菜单 / 更新检查界面
  │
  ├─ HotUpdateManager.CheckForUpdates(serverUrl)
  │   ├─ State → Checking
  │   ├─ 发送 HTTP GET {serverUrl}/version.json
  │   ├─ 解析 VersionManifest
  │   ├─ 比较 remote.internalResourceVersion > local.internalResourceVersion?
  │   │   ├─ 否 → State → UpToDate
  │   │   └─ 是 → State → UpdateAvailable
  │   │       └─ 保存 RemoteVersion
  │   └─ 返回 (available: bool)
  │
  ├─ [用户确认更新 / 自动更新]
  │
  ├─ HotUpdateManager.DownloadAndApply()
  │   ├─ State → Downloading
  │   │
  │   ├─ 对每个 PckEntry 循环:
  │   │   ├─ 下载: HTTP GET {pck.url} → 流式写入 user://downloads/{pck.name}
  │   │   ├─ 进度: (已下载字节 / 总字节) → OnProgressChanged
  │   │   ├─ State → Verifying
  │   │   ├─ 校验: ComputeMD5(user://downloads/{pck.name}) == pck.md5?
  │   │   │   ├─ 不匹配 → 删除文件, OnError("MD5 mismatch")
  │   │   │   │            → State → Error
  │   │   │   │            → 可重试
  │   │   │   └─ 匹配 → 继续
  │   │   └─ 移动到 user://{pck.name}
  │   │
  │   ├─ State → Applying
  │   ├─ 对每个下载好的 PCK:
  │   │   └─ ProjectSettings.LoadResourcePack("user://{pck.name}")
  │   │
  │   ├─ 写入 user://version.json
  │   │   └─ { version, internalResourceVersion, loadedPcks: [...] }
  │   │
  │   ├─ State → Complete
  │   ├─ OnUpdateComplete.Invoke()
  │   │
  │   └─ [GodotResourceManager.ResolvePath 自动优先从 user:// 加载]
  │
  └─ 后续资源加载
      └─ Entity/UI/Sound/Scene 的 Load/LoadAsync 调用
          └─ GodotResourceManager.ResolvePath(assetName)
              └─ 启用了 PCK 覆盖?
                  ├─ 是 → 检测 user:// 等效路径
                  │   ├─ 存在 → 使用 user:// 版本
                  │   └─ 不存在 → 使用原始 res:// 路径
                  └─ 否 → 使用原始 res:// 路径
```

#### 4.4.5 下载实现

使用 Godot 的 `HTTPRequest` 节点进行异步 HTTP 下载：

```csharp
private async Task<bool> DownloadPck(PckEntry entry, string savePath)
{
    var httpRequest = new HttpRequest();
    AddChild(httpRequest);

    var tcs = new TaskCompletionSource<bool>();

    httpRequest.RequestCompleted += (result, responseCode, headers, body) =>
    {
        if (result == (long)HttpRequest.Result.Success && responseCode == 200)
        {
            // 流式写入文件
            using var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreBuffer(body);
                tcs.TrySetResult(true);
                return;
            }
        }
        tcs.TrySetResult(false);
    };

    Error error = httpRequest.Request(entry.url);
    if (error != Error.Ok)
    {
        httpRequest.QueueFree();
        return false;
    }

    bool success = await tcs.Task;
    httpRequest.QueueFree();
    return success;
}
```

#### 4.4.6 MD5 校验

```csharp
private bool VerifyMd5(string filePath, string expectedMd5)
{
    if (!FileAccess.FileExists(filePath))
        return false;

    using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
    if (file == null)
        return false;

    byte[] data = file.GetBuffer((long)file.GetLength());

    // 使用 System.Security.Cryptography 计算 MD5
    using var md5 = System.Security.Cryptography.MD5.Create();
    byte[] hash = md5.ComputeHash(data);

    // 转换为小写十六进制字符串
    var sb = new System.Text.StringBuilder();
    foreach (byte b in hash)
        sb.Append(b.ToString("x2"));

    return sb.ToString() == expectedMd5.ToLowerInvariant();
}
```

### 4.5 Entity/UI/Sound/Scene 组件变更

#### 4.5.1 当前模式（四个组件完全相同）

```csharp
// EntityComponent.cs:61-65
var resourceManager = GF.Base.EditorResourceMode
    ? GF.Base.EditorResourceManager
    : GameFrameworkEntry.GetModule<GameFramework.Resource.IResourceManager>();
if (resourceManager == null) { Log.Fatal("Resource manager is invalid."); return; }
m_EntityManager.SetResourceManager(resourceManager);
```

#### 4.5.2 改为

```csharp
// 统一写法，四个组件一致
var resourceManager = GF.Resource.ResourceManager;
if (resourceManager == null) { Log.Fatal("Resource manager is invalid."); return; }
m_EntityManager.SetResourceManager(resourceManager);
```

`GF.Resource.ResourceManager` 返回 `ResourceComponent` 实例的 `IResourceManager` 属性，该属性返回 `GodotResourceManager`（实现了 `IResourceManager`）。

#### 4.5.3 受影响的文件

| 文件 | 修改位置 | 变更内容 |
|------|---------|---------|
| `EntityComponent.cs` | ~L61-65 | 替换资源管理器获取方式 |
| `UIComponent.cs` | ~L80-90 | 同上 |
| `SoundComponent.cs` | ~L95-105 | 同上 |
| `SceneComponent.cs` | ~L50-55 | 同上 |

#### 4.5.4 关于 SetObjectPoolManager

当前这些组件还调用：
```csharp
m_EntityManager.SetObjectPoolManager(GameFrameworkEntry.GetModule<IObjectPoolManager>());
```

这个与资源系统无关（是实体实例的对象池），**保持不变**。

### 4.6 BaseComponent 清理

**文件**: `Framework/GodotGameFrameworkCore/Base/BaseComponent.cs`

#### 4.6.1 删除内容

```csharp
// 删除以下全部内容：

[Export]
private bool m_EditorResourceMode = true;

public bool EditorResourceMode
{
    get
    {
        return m_EditorResourceMode && OS.HasFeature("editor");
    }
}

private IResourceManager m_ResourceManager;
public IResourceManager EditorResourceManager
{
    get
    {
        if (m_ResourceManager == null)
        {
            m_ResourceManager = new EditorResourceManager();
        }
        return m_ResourceManager;
    }
}
```

#### 4.6.2 保留内容

BaseComponent 的所有其他功能保持不变：
- FrameRate / GameSpeed / Pause / Resume
- InitTextHelper / InitVersionHelper / InitLogHelper / InitJsonHelper
- Shutdown 流程

### 4.7 ResourceMode 简化

**文件**: `Framework/GameFramework/Resource/ResourceMode.cs`

**当前**：
```csharp
public enum ResourceMode : byte
{
    Unspecified = 0,
    Package,
    Updatable,
    UpdatableWhilePlaying
}
```

**改为**：
```csharp
public enum ResourceMode : byte
{
    Unspecified = 0,
    Package,     // 默认：所有资源从 res:// 加载（编辑器/开发构建）
    Updatable    // PCK 覆盖模式：优先检测 user://，支持热更新
}
```

**理由**：在 Godot 中，`ProjectSettings.LoadResourcePack()` 装载 PCK 后对后续加载立即生效，不存在 Unity 中"运行时加载 vs 预下载"的区分，因此 `UpdatableWhilePlaying` 没有意义。

---

## 5. 文件变更清单

### 5.1 删除的文件

#### GF 管线层（Framework/GameFramework/Resource/）

| 文件路径 | 行数 | 说明 |
|---------|------|------|
| `ResourceManager.cs` | ~2500 | 主管理器 + 18 个嵌套 partial 类 |
| `IResourceHelper.cs` | ~30 | 辅助器接口 |
| `ILoadResourceAgentHelper.cs` | ~50 | 加载代理接口 |
| `IResourceGroup.cs` | ~40 | 资源组接口 |
| `IResourceGroupCollection.cs` | ~20 | 资源组集合接口 |
| `PackageVersionList.cs` | ~150 | 单机版本列表数据 |
| `PackageVersionListSerializer.cs` | ~100 | 单机版本列表序列化器 |
| `UpdatableVersionList.cs` | ~150 | 可更新版本列表数据 |
| `UpdatableVersionListSerializer.cs` | ~100 | 可更新版本列表序列化器 |
| `ReadOnlyVersionList.cs` | ~50 | 只读版本列表 |
| `ReadOnlyVersionListSerializer.cs` | ~30 | 只读版本列表序列化器 |
| `ReadWriteVersionList.cs` | ~50 | 读写版本列表 |
| `ReadWriteVersionListSerializer.cs` | ~30 | 读写版本列表序列化器 |
| `ResourcePackVersionList.cs` | ~100 | 资源包版本列表 |
| `ResourcePackVersionListSerializer.cs` | ~30 | 资源包版本列表序列化器 |
| `LocalVersionList.cs` | ~80 | 本地版本列表 |
| `ResourceVerifyStartEventArgs.cs` | ~40 | 校验开始事件参数 |
| `ResourceVerifySuccessEventArgs.cs` | ~40 | 校验成功事件参数 |
| `ResourceVerifyFailureEventArgs.cs` | ~40 | 校验失败事件参数 |
| `ResourceApplyStartEventArgs.cs` | ~40 | 应用开始事件参数 |
| `ResourceApplySuccessEventArgs.cs` | ~40 | 应用成功事件参数 |
| `ResourceApplyFailureEventArgs.cs` | ~40 | 应用失败事件参数 |
| `ResourceUpdateStartEventArgs.cs` | ~40 | 更新开始事件参数 |
| `ResourceUpdateChangedEventArgs.cs` | ~40 | 更新进度事件参数 |
| `ResourceUpdateSuccessEventArgs.cs` | ~40 | 更新成功事件参数 |
| `ResourceUpdateFailureEventArgs.cs` | ~40 | 更新失败事件参数 |
| `ResourceUpdateAllCompleteEventArgs.cs` | ~40 | 更新完成事件参数 |
| `DecryptResourceCallback.cs` | ~20 | 解密回调委托 |
| `InitResourcesCompleteCallback.cs` | ~20 | 初始化完成回调 |
| `CheckResourcesCompleteCallback.cs` | ~20 | 资源检查完成回调 |
| `ApplyResourcesCompleteCallback.cs` | ~20 | 资源应用完成回调 |
| `UpdateResourcesCompleteCallback.cs` | ~20 | 资源更新完成回调 |
| `VerifyResourcesCompleteCallback.cs` | ~20 | 资源校验完成回调 |
| `UpdateVersionListCallbacks.cs` | ~30 | 版本列表更新回调集合 |

**小计：约 35 个文件，约 4200 行**

#### Godot 层（Framework/GodotGameFrameworkCore/Resource/）

| 文件路径 | 行数 | 说明 |
|---------|------|------|
| `EditorResourceManager.cs` | ~425 | 功能合并到 GodotResourceManager |
| `DefaultResourceHelper.cs` | ~60 | 不再需要 |
| `DefaultLoadResourceAgentHelper.cs` | ~100 | 不再需要 |
| `ResourceHelperBase.cs` | ~30 | 基类删除 |
| `LoadResourceAgentHelperBase.cs` | ~30 | 基类删除 |
| `GDFBuiltinVersionListSerializer.cs` | ~200 | GF 序列化器删除 |
| `GDFResourceBuilder.cs` | ~300 | GF 资源构建器删除 |

**小计：7 个文件，约 1145 行**

#### 总计删除：约 **42 个文件**，约 **5345 行**

### 5.2 修改的文件

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `IResourceManager.cs` | 精简 | 移除 60+ 成员，保留 ~30 个 |
| `ResourceMode.cs` | 简化 | 移除 `UpdatableWhilePlaying` |
| `ResourceComponent.cs` | 重写 | 约 210→80 行，移除管线初始化 |
| `EntityComponent.cs` | 修改 | ~L61-65 替换资源管理器获取 |
| `UIComponent.cs` | 修改 | 同上模式 |
| `SoundComponent.cs` | 修改 | 同上模式 |
| `SceneComponent.cs` | 修改 | 同上模式 |
| `BaseComponent.cs` | 清理 | 删除 `EditorResourceMode`/`EditorResourceManager` |

### 5.3 新建的文件

| 文件 | 说明 |
|------|------|
| `GodotResourceManager.cs` | 统一资源管理器，~300 行 |
| `HotUpdateManager.cs` | 热更新管理器，~350 行 |
| `HotUpdateConfig.cs` | 配置模型，~50 行 |

#### 总计新增：**3 个文件**，约 **700 行**

### 5.4 净效果

| 指标 | 当前 | 重构后 | 变化 |
|------|------|--------|------|
| 文件数 | ~33+ | ~15 | **-55%** |
| 代码行 | ~9200 | ~1800 | **-80%** |
| 热更新 | ❌ 不可用 | ✅ 可用 | 从无到有 |

---

## 6. 向后兼容性

### 6.1 兼容性矩阵

| API / 功能 | 当前行为 | 重构后行为 | 兼容性 |
|-----------|---------|-----------|--------|
| `GF.Entity.ShowEntity(EntityId.Cat)` | 正常 | 正常 | ✅ 不变 |
| `GF.Entity.ShowEntityAsync<CatEntity>(...)` | 正常 | 正常 | ✅ 不变 |
| `GF.UI.OpenUIForm(UIFormId.Menu)` | 正常 | 正常 | ✅ 不变 |
| `GF.UI.OpenUIFormAsync(...)` | 正常 | 正常 | ✅ 不变 |
| `GF.Sound.PlaySound(...)` | 正常 | 正常 | ✅ 不变 |
| `GF.Scene.LoadSceneAsync(...)` | 正常 | 正常 | ✅ 不变 |
| `GF.Resource.LoadAsset<T>(string)` | 正常 | 正常 | ✅ 不变 |
| `GF.Resource.LoadBinary(string)` | 正常 | 正常 | ✅ 不变 |
| `GF.Resource.LoadText(string)` | 正常 | 正常 | ✅ 不变 |
| `GF.Resource.HasAsset(string)` | 正常 | 正常 | ✅ 不变 |
| `IResourceManager.LoadAsset(..., LoadAssetCallbacks, ...)` | 正常 | 正常 | ✅ 不变 |
| `IResourceManager.LoadScene(..., LoadSceneCallbacks, ...)` | 正常 | 正常 | ✅ 不变 |
| `IResourceManager.LoadBinary(..., LoadBinaryCallbacks, ...)` | 正常 | 正常 | ✅ 不变 |
| `IResourceManager.UnloadAsset(object)` | 空操作 | 空操作 | ✅ 不变 |
| `IResourceManager.ReadOnlyPath` | `"res://"` | `"res://"` | ✅ 不变 |
| `IResourceManager.ReadWritePath` | `"user://"` | `"user://"` | ✅ 不变 |
| `GF.Base.EditorResourceMode` | 返回 bool | **已删除** | ❌ 编译错误 |
| `GF.Base.EditorResourceManager` | 返回 IResourceManager | **已删除** | ❌ 编译错误 |
| 管线方法（Check/Update/Apply/Verify） | 部分抛出异常 | **已删除** | ❌ 编译错误 |

### 6.2 破坏性变更

1. **`GF.Base.EditorResourceMode`** — 任何引用了此属性的代码需要改为判断运行环境：
   ```csharp
   // 旧代码
   if (GF.Base.EditorResourceMode) { ... }
   
   // 新代码（等价逻辑，仅在 Godot 编辑器中返回 true）
   if (OS.HasFeature("editor")) { ... }
   ```

2. **`GF.Base.EditorResourceManager`** — 直接使用 `GF.Resource.ResourceManager` 替代

3. **管线方法调用** — 如果任何自定义代码调用了 `InitResources`、`CheckResources`、`UpdateResources` 等，需要删除这些调用。**搜索确认**：当前项目中 `ProcedureLaunch.cs` 未调用任何管线方法。

### 6.3 搜索清单（实施前需确认）

搜索以下在项目实施期间可能遗留的引用：

```
搜索 "EditorResourceMode"    → 确认仅在 Entity/UI/Sound/Scene/Base 组件中
搜索 "EditorResourceManager" → 确认仅在 Entity/UI/Sound/Scene/Base 组件中
搜索 "InitResources"         → 确认无调用
搜索 "CheckResources"        → 确认无调用
搜索 "UpdateResources"       → 确认无调用
搜索 "ResourcePackVersion"   → 确认无调用
搜索 "ResourceVerify"        → 确认无调用
搜索 "m_ResourceManager.Init" → 确认无调用
搜索 "SetResourceHelper"     → 确认仅在 ResourceComponent 中
搜索 "AddLoadResourceAgentHelper" → 确认仅在 ResourceComponent 中
```

---

## 7. 迁移步骤

### Step 1: 预备工作 — 搜索并确认无外部依赖

- 搜索代码库中是否存在对即将删除的类/方法的引用
- 特别关注：编辑器插件（`addons/` 目录）、非游戏项目（如有）

### Step 2: 新建 GodotResourceManager

- 在 `Framework/GodotGameFrameworkCore/Resource/` 下创建
- 实现精简后的 `IResourceManager` 接口
- 包含 `ResolvePath()` PCK 感知路径解析
- 包含 `LoadAssetAsync()` Godot 原生异步加载

### Step 3: 精简 IResourceManager 接口

- 从 `IResourceManager.cs` 中移除 60+ 个未使用的成员声明
- **此时编译会失败**（GF `ResourceManager` 实现了这些成员，而它即将被删除）

### Step 4: 删除 GF 管线文件

删除 `Framework/GameFramework/Resource/` 下约 35 个文件：
```
ResourceManager.cs
ResourceManager.*.cs         (所有嵌套 partial 类)
IResourceHelper.cs
ILoadResourceAgentHelper.cs
IResourceGroup.cs
IResourceGroupCollection.cs
*VersionList*.cs             (所有版本列表文件)
*VersionListSerializer*.cs   (所有序列化器)
*EventArgs.cs                (所有事件参数)
DecryptResourceCallback.cs
InitResourcesCompleteCallback.cs
*ResourcesCompleteCallback.cs
*VersionListCallbacks.cs
```

### Step 5: 删除 Godot 层废弃文件

删除 `Framework/GodotGameFrameworkCore/Resource/` 下 7 个文件：
```
EditorResourceManager.cs
DefaultResourceHelper.cs
DefaultLoadResourceAgentHelper.cs
ResourceHelperBase.cs
LoadResourceAgentHelperBase.cs
GDFBuiltinVersionListSerializer.cs
GDFResourceBuilder.cs
```

### Step 6: 简化 ResourceComponent

重写 `ResourceComponent.cs`：
- 移除 Helper/Agent 类型名配置和创建代码
- 移除 `ResolveResourceMode()` 和 `InitRuntimeMode()`
- 实例化 `GodotResourceManager` 替代 GF 管线
- 可选集成 `HotUpdateManager`

### Step 7: 清理 BaseComponent

删除 `BaseComponent.cs` 中的：
- `m_EditorResourceMode` 字段和属性
- `EditorResourceManager` 字段和属性

### Step 8: 更新 Entity/UI/Sound/Scene 组件

四个组件模式相同：
```csharp
// 修改前
var resourceManager = GF.Base.EditorResourceMode
    ? GF.Base.EditorResourceManager
    : GameFrameworkEntry.GetModule<IResourceManager>();

// 修改后
var resourceManager = GF.Resource.ResourceManager;
```

### Step 9: 简化 ResourceMode

- 从枚举中移除 `UpdatableWhilePlaying`

### Step 10: 编译验证

```bash
cd GodotProject
dotnet build
```

修复所有编译错误。

### Step 11: 编辑器运行测试

1. 启动 Godot 编辑器
2. 打开 `GameFramework.tscn` 主场景
3. 运行游戏
4. 验证：
   - GameFramework 场景正常加载
   - 无红色错误输出
   - `Log.Info` 中显示 ResourceComponent 初始化成功

### Step 12: 创建 HotUpdateManager

在确认基础资源加载正常后，创建热更新组件：
- `HotUpdateManager.cs`
- `HotUpdateConfig.cs`

### Step 13: 热更新端到端测试

1. 构建测试 PCK 文件（通过 Godot 的 `--export-pack` 命令行）
2. 本地搭建 HTTP 服务器，放置 `version.json` + PCK
3. 配置游戏指向本地服务器
4. 启动游戏，验证：
   - 版本检查请求发送到服务器
   - PCK 文件下载到 `user://`
   - `ProjectSettings.LoadResourcePack()` 成功
   - 资源从 PCK 中正确加载（旧资源被覆盖）

---

## 8. 验证方案

### 8.1 编译验证

```bash
cd GodotProject
dotnet build
```
- 预期：0 错误，0 警告
- 检查：无 `CS0117`（成员不存在）、`CS0246`（类型找不到）等编译错误

### 8.2 功能验证

#### 测试 1：基础资源加载

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 启动 Godot 编辑器 | 场景正常加载 |
| 2 | 按 F5 运行游戏 | 游戏启动，无错误 |
| 3 | 检查 Output 面板 | 应有 `[ResourceComponent] Initialized. Mode=Package` |
| 4 | 触发实体创建（如进入游戏场景） | 实体正常显示，无资源加载错误 |
| 5 | 触发 UI 打开（如按 Esc 打开菜单） | UI 正常显示 |
| 6 | 触发音频播放 | 音频正常播放 |

#### 测试 2：异步加载

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 调用 `ResourceComponent.GodotResourceManager.LoadAssetAsync(...)` | 返回 `Task<Godot.Resource>` |
| 2 | `await` 等待加载完成 | 资源正确返回 |
| 3 | 传入 `CancellationToken` | 取消后抛出 `OperationCanceledException` |

#### 测试 3：编辑器 vs 导出构建

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 在编辑器中运行 | `ResourceMode.Package`，从 `res://` 加载 |
| 2 | 导出发行版 | `ResourceMode.Updatable`（若 `m_UsePckOverride=true`） |

### 8.3 热更新测试

#### 测试 1：无更新场景

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 服务器 version.json 中 version = "1.0.0" |  |
| 2 | 游戏本地版本也是 "1.0.0" |  |
| 3 | 启动游戏 | 状态: `UpToDate`，无下载行为 |

#### 测试 2：有新更新

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 服务器 version.json 中 version = "1.0.1" |  |
| 2 | 游戏本地版本为 "1.0.0" |  |
| 3 | 启动游戏 | `CheckForUpdates` 返回 `true` |
| 4 | 调用 `DownloadAndApply()` | 下载 PCK → 校验 MD5 → 装载 → `Complete` |
| 5 | 后续加载 `res://Scenes/NewLevel.tscn` | 从 PCK 中加载成功 |

#### 测试 3：断点续传（可选增强）

| 步骤 | 操作 | 预期结果 |
|------|------|---------|
| 1 | 开始下载大 PCK | 下载中... |
| 2 | 中断下载（断网） | `OnError` 触发 |
| 3 | 恢复网络 | 可重试，从上次断点继续（需 HTTP Range 支持） |

### 8.4 边界情况

| 场景 | 预期行为 |
|------|---------|
| `LoadAsset("")` | 回调 `LoadAssetFailure(NotExist, "Asset name is invalid.")` |
| `LoadAsset("res://nonexistent.tscn")` | 回调 `LoadAssetFailure(NotExist, "...does not exist.")` |
| `LoadAsset("res://texture.png", typeof(PackedScene), ...)` | 回调 `LoadAssetFailure(AssetError, "...is Texture2D, expected PackedScene")` |
| `HotUpdateManager` 下载时用户关闭游戏 | 下次启动 `LoadExistingPcks()` 检测上次已下载的 PCK，继续应用 |
| PCK 文件 MD5 不匹配 | 删除损坏文件，触发 `OnError`，可重试下载 |
| 无网络连接 | `CheckForUpdates` 返回 `false`，状态保持 `Idle` |
| 服务器返回 404 | `CheckForUpdates` 返回 `false`，记录错误日志 |

---

## 9. 风险与应对

| 风险 | 影响 | 概率 | 应对 |
|------|------|------|------|
| 现有项目代码引用了即将删除的 GF 管线 API | 编译错误 | 低 | Step 1 中已全面搜索确认；如有遗漏，编译时修复 |
| 编辑器插件或其他非游戏模块使用了 `EditorResourceManager` | 功能异常 | 低 | 搜索 `addons/` 目录确认；如有，改为 `GodotResourceManager` |
| `ResourceLoader.LoadThreadedRequest` 在部分导出平台有不同行为 | 异步加载异常 | 中 | 先在编辑器充分测试；同步路径 `ResourceLoader.Load()` 作为后备 |
| `ProjectSettings.LoadResourcePack()` 在特定平台（如 iOS）有沙盒限制 | 热更新不可用 | 中 | 需要平台特定测试；iOS 上需确认应用沙盒内路径可用 |
| 大型 PCK 下载导致内存峰值 | 内存不足 | 中 | 使用流式写入（当前实现已是流式）；考虑分块下载 |
| MD5 计算大文件耗时 | 校验时帧率下降 | 低 | 大文件分片校验；或在后台线程计算 |
| 删除大量文件后 git 历史混乱 | 代码审查困难 | 低 | 一次提交所有删除 + 新增；使用 `git diff --stat` 查看变更概览 |

---

## 附录 A: 调用链参考

以下展示重构前后各系统的完整调用链对比：

### 实体加载调用链（重构前后）

**重构前（当前）**：
```
GF.Entity.ShowEntity(EntityId.Cat)
  → EntityComponent.ShowEntity()
    → EntityManager.ShowEntity()
      → EntityManager.ShowEntity(EntityId.Cat, "res://TheGame/.../Cat.tscn", "Default", 0, null)
        → EntityGroup.SpawnEntityInstanceObject("res://...Cat.tscn")
          未命中对象池 → 加载:
          → m_ResourceManager.LoadAsset("res://...Cat.tscn", 0, LoadAssetCallbacks, ShowEntityInfo)
            → GF 管线: ResourceManager → ResourceLoader → TaskPool → LoadResourceAgent
              → DefaultLoadResourceAgentHelper.ReadFile(path)
                → Godot.ResourceLoader.Load(path)   ← 真正加载
```

**重构后**：
```
GF.Entity.ShowEntity(EntityId.Cat)
  → EntityComponent.ShowEntity()
    → EntityManager.ShowEntity()
      → EntityManager.ShowEntity(EntityId.Cat, "res://TheGame/.../Cat.tscn", "Default", 0, null)
        → EntityGroup.SpawnEntityInstanceObject("res://...Cat.tscn")
          未命中对象池 → 加载:
          → m_ResourceManager.LoadAsset("res://...Cat.tscn", 0, LoadAssetCallbacks, ShowEntityInfo)
            → GodotResourceManager.LoadAssetInternal()
              → ResolvePath()  ← PCK 感知路径解析（新增）
                → Godot.ResourceLoader.Load(path)   ← 直接加载
```

**差异**：移除了 GF 管线 5 层间接调用，直接走 `GodotResourceManager → ResourceLoader.Load()`。

### UI 打开调用链

**重构前**：
```
GF.UI.OpenUIForm(UIFormId.Menu)
  → UIComponent.OpenUIForm()
    → UIManager.OpenUIForm()
      → m_ResourceManager.LoadAsset(...) → GF 管线 → ResourceLoader.Load()
```

**重构后**：
```
GF.UI.OpenUIForm(UIFormId.Menu)
  → UIComponent.OpenUIForm()
    → UIManager.OpenUIForm()
      → m_ResourceManager.LoadAsset(...) → GodotResourceManager → ResourceLoader.Load()
```

### 音频播放调用链

**重构前**：
```
GF.Sound.PlaySound(...)
  → SoundComponent.PlaySound()
    → SoundManager.PlaySound()
      → m_ResourceManager.LoadAsset(...) → GF 管线 → ResourceLoader.Load()
```

**重构后**：
```
GF.Sound.PlaySound(...)
  → SoundComponent.PlaySound()
    → SoundManager.PlaySound()
      → m_ResourceManager.LoadAsset(...) → GodotResourceManager → ResourceLoader.Load()
```

### 热更新流程（全新）

```
游戏启动:
  HotUpdateManager.LoadExistingPcks()
    → 读取 user://version.json
    → 对每个已记录的 PCK: ProjectSettings.LoadResourcePack("user://{pck}")

运行中:
  HotUpdateManager.CheckForUpdates("https://cdn.example.com/game")
    → HTTP GET https://cdn.example.com/game/version.json
    → 比较版本号

  HotUpdateManager.DownloadAndApply()
    → HTTP GET {pck.url} → 保存到 user://downloads/{pck.name}
    → MD5 校验
    → ProjectSettings.LoadResourcePack("user://{pck.name}")
    → 写入 user://version.json

后续资源加载:
  任何 IResourceManager.LoadAsset("res://...")
    → GodotResourceManager.ResolvePath("res://...")
      → 检测 user:// 等效路径（PCK 已装载时生效）
      → user:// 存在 → 从 PCK 加载
      → 不存在 → 从 res:// 加载
```

---

## 附录 B: 关键代码参考

### B.1 当前 EditorResourceManager 的核心加载逻辑（保留到 GodotResourceManager）

```csharp
// EditorResourceManager.cs:126-174
// 这是当前唯一实际可用的加载逻辑，重构后保留到 GodotResourceManager
private void LoadAssetInternal(string assetName, LoadAssetCallbacks loadAssetCallbacks,
    object userData, Type assetType = null)
{
    // 参数校验
    if (string.IsNullOrEmpty(assetName))
    { ... failure callback ... }

    // 存在性校验
    if (!Godot.ResourceLoader.Exists(assetName))
    { ... failure callback ... }

    // 同步加载
    try
    {
        Godot.Resource resource = Godot.ResourceLoader.Load(assetName);

        if (resource != null)
        {
            // 类型校验
            if (assetType != null && !assetType.IsInstanceOfType(resource))
            { ... failure callback ... }

            loadAssetCallbacks.LoadAssetSuccessCallback(assetName, resource, 0f, userData);
        }
        else
        { ... failure callback ... }
    }
    catch (Exception e)
    { ... failure callback ... }
}
```

### B.2 当前四个组件中资源管理器获取模式的对比

| 组件 | 文件 | 行号 | 代码 |
|------|------|------|------|
| EntityComponent | `EntityComponent.cs` | 61-65 | `GF.Base.EditorResourceMode ? GF.Base.EditorResourceManager : GameFrameworkEntry.GetModule<IResourceManager>()` |
| UIComponent | `UIComponent.cs` | ~80-84 | 同上 |
| SoundComponent | `SoundComponent.cs` | ~95-99 | 同上 |
| SceneComponent | `SceneComponent.cs` | ~50-54 | 同上 |

---

## 附录 C: 设计决策记录

| 决策 | 选项 | 选择 | 理由 |
|------|------|------|------|
| 异步加载方式 | GF TaskPool vs Godot LoadThreadedRequest | Godot LoadThreadedRequest | 原生支持、无需自建线程池、与 Godot 资源系统深度集成 |
| 打包格式 | GF ResourcePack vs Godot PCK | Godot PCK | 原生支持、工具链完整、支持加密和增量更新 |
| 缓存管理 | GF IObjectPool vs Godot 内置缓存 | Godot 内置缓存 | 引用计数自动管理、无需额外抽象层 |
| 是否保留 IResourceManager | 完全替换 vs 保留接口 | 保留接口但精简 | Entity/UI/Sound Manager 依赖此接口，保持向后兼容 |
| 编辑器/运行时代码 | 两套 vs 一套 | 一套（仅 PCK 标志位不同）| 大幅减少维护成本，两套代码的差异仅在于热更新路径 |
| `SetResourceHelper`/`AddLoadResourceAgentHelper` | 保留 vs 删除 | 从接口删除 | 这是 GF 管线的内部实现细节，不应暴露在接口中 |
