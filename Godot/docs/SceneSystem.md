# 场景系统 (Scene Module)

> 适用版本：Godot 4.6.2 + .NET 8 ｜ 对应代码：`Framework/GameFramework/Scene/`、`Framework/GodotGameFrameworkCore/Scene/`
> 本文档描述 GGF 的场景系统：场景加载/卸载流程、`LoadSceneAsync` 的 TCS 模式、场景实例在 Godot SceneTree 中的挂载位置、与资源系统的关系及已知边界。

---

## 1. 概述

场景系统是 [Game Framework](https://gameframework.cn/) Scene 模块的 Godot 移植，遵循框架的**双层架构**：

| 层 | 位置 | 职责 | Godot 依赖 |
|----|------|------|:--:|
| 纯 C# 层 | `GameFramework/Scene/` | SceneManager：加载/卸载状态机（loaded/loading/unloading 三张表）、实例登记、事件定义 | ❌ |
| Godot 桥接层 | `GodotGameFrameworkCore/Scene/` | SceneComponent 组件封装、PackedScene 实例化与挂树、可 await API、事件转发 | ✅ |

**与 Godot 原生场景切换的关系（重要）**：GGF **不使用** `SceneTree.ChangeSceneToFile/ChangeSceneToPacked`，也不替换 `CurrentScene`。主场景永远是 `Framework/GameFramework.tscn`；框架加载的"场景"只是一个普通 `PackedScene` 实例化出的 `Node`，**作为子节点挂到 `Scene` 组件节点下**（`GameFramework/Scene/<场景名>`）。因此可以同时加载多个"场景"（地图、关卡分块等），它们与 UI、实体共存于同一棵 SceneTree。

### 能力清单

- ✅ 异步加载（经 `IResourceManager.LoadAsset` 的任务池，支持优先级）
- ✅ 加载状态查询（已加载 / 加载中 / 卸载中）与重复加载防护（抛异常）
- ✅ `LoadScene`（事件驱动）与 `LoadSceneAsync`（TCS 可 await）两种消费方式
- ✅ 实例登记：`GetLoadedScene<T>(assetPath)` 直接取回已挂树的场景根节点
- ✅ Godot 层全局事件：`LoadSceneSuccessEventArgs` / `LoadSceneFailureEventArgs`（经 EventComponent 分发）
- ⚠️ 卸载流程当前不完整（见 §5 已知边界）

---

## 2. 架构与数据流

```
调用方（MainForm.OnInit / Procedure）
    │  GF.Scene.LoadSceneAsync("res://TheGame/Scenes/Map.tscn")
    ▼
SceneComponent (Godot 桥接层，场景节点 "Scene")
    │  m_LoadingTasks[assetName] = TCS        ▲ C# 事件
    │  委托                                    │
    ▼                                          │
SceneManager : GameFrameworkModule (纯 C# 层，Priority=2)
    │  m_LoadingSceneAssetNames.Add(assetName)
    │  IResourceManager.LoadAsset(assetName, priority, callbacks, userData)
    │        │ 成功（拿到 PackedScene）
    │        ▼
    │  ISceneHelper.InstantiateScene ──▶ DefaultSceneHelper: packedScene.Instantiate()
    │  m_LoadedSceneInstances[assetName] = 实例
    │  LoadSceneSuccess 事件（含 SceneInstance）
    ▼
SceneComponent.OnLoadSceneSuccess
    ├── instance.Name = 场景名（路径截取，"Map.tscn" → "Map"）
    ├── AddChild(instance)               ← 挂到 Scene 组件节点下
    ├── TCS.TrySetResult(instance)       ← 完结 await
    └── EventComponent.Fire(LoadSceneSuccessEventArgs)   ← 全局事件
```

失败路径：`LoadSceneFailure` 事件 → `Log.Warning` → `TCS.TrySetException` → 全局 `LoadSceneFailureEventArgs`。

### 文件清单

| 文件 | 职责 |
|------|------|
| `GameFramework/Scene/ISceneManager.cs` / `SceneManager.cs` | 管理器接口 / 实现（三状态表、实例字典、加载回调） |
| `GameFramework/Scene/ISceneHelper.cs` | 场景辅助器抽象（InstantiateScene / ReleaseScene） |
| `GameFramework/Scene/LoadScene*EventArgs.cs`、`UnloadScene*EventArgs.cs` | 纯 C# 层事件参数（池化） |
| `GodotGameFrameworkCore/Scene/SceneComponent.cs` | 组件封装 + `LoadSceneAsync`（TCS）+ 挂树 |
| `GodotGameFrameworkCore/Scene/SceneHelperBase.cs` / `DefaultSceneHelper.cs` | 辅助器基类 / 默认实现（`Instantiate` / `QueueFree`） |
| `GodotGameFrameworkCore/Scene/SceneEventArgs.cs` | Godot 层全局事件参数（Load 成功/失败、Unload 成功；池化） |

---

## 3. 核心机制

### 3.1 加载流程与状态防护

`SceneManager.LoadScene` 在提交加载前做三重检查，**命中即抛 `GameFrameworkException`**：

| 状态 | 结果 |
|------|------|
| 该资源正在卸载（`SceneIsUnloading`） | 抛 "is being unloaded" |
| 该资源正在加载（`SceneIsLoading`） | 抛 "is being loaded" |
| 该资源已加载（`SceneIsLoaded`） | 抛 "is already loaded" |

即：**同一 assetPath 全局唯一实例**。需要重复实例化同一 `.tscn`（如批量刷怪、瓦片块）应使用实体系统或直接 `GF.Resource` 加载 `PackedScene` 自行实例化，场景系统只适合"同一时刻至多一份"的地图/关卡级内容。

### 3.2 LoadSceneAsync（TCS 模式）

`SceneComponent.LoadSceneAsyncInternal`：

```csharp
var tcs = new TaskCompletionSource<Node>();
m_SceneManager.LoadScene(sceneAssetName, priority, userData);  // 状态非法在此抛出
m_LoadingTasks.Add(sceneAssetName, tcs);                       // 以 assetName 为键
return tcs.Task;
```

- 与 UI 的 serialId 键不同，场景 TCS **以资源路径为键**（正因 §3.1 的全局唯一约束，同名并发不可能成功发起）
- 成功：`TrySetResult(挂树后的 Node)`；实例为 null 时 `TrySetException`
- 失败：`TrySetException(new Exception(errorMessage))`——**await 处会抛异常**，需要 try/catch
- 传入空路径：返回 `Task.FromResult<Node>(null)`，不抛

### 3.3 实例挂载与命名

成功回调中实例被重命名为 `GetSceneName(assetPath)`（取最后一个 `/` 与最后一个 `.` 之间的部分）并 `AddChild` 到 **Scene 组件节点**下。运行时树：

```
GameFramework
├── Scene (SceneComponent)
│   ├── GodotGameFramework.Scene.DefaultSceneHelper
│   └── Map        ← LoadScene("res://TheGame/Scenes/Map.tscn") 的实例
├── UI (...)
└── Entity (...)
```

随后可用两种方式取回：

```csharp
Node2D map = GF.Scene.GetLoadedScene<Node2D>("res://TheGame/Scenes/Map.tscn");
// 或直接持有 LoadSceneAsync 的返回值（推荐）
```

### 3.4 事件

| 事件 | 层 | 触发时机 |
|------|----|---------|
| `LoadSceneSuccess/Failure/Update/DependencyAsset` | 纯 C#（`ISceneManager` C# 事件） | 加载各阶段（Update/DependencyAsset 组件未订阅） |
| `GodotGameFramework.Scene.LoadSceneSuccessEventArgs` | Godot 全局（EventComponent） | 挂树完成后，携带 `SceneAssetPath` + `SceneInstance` |
| `GodotGameFramework.Scene.LoadSceneFailureEventArgs` | Godot 全局 | 加载失败，携带 `ErrorMessage` |
| `GodotGameFramework.Scene.UnloadSceneSuccessEventArgs` | Godot 全局 | **已定义但当前无触发点**（见 §5） |

由 Inspector 开关 `m_EnableLoadSceneSuccessEvent` / `m_EnableLoadSceneFailureEvent`（默认均 true）控制转发。事件参数池化，回调返回后即回收，不可持有。

---

## 4. SceneComponent 与 API

场景节点：`Framework/GameFramework.tscn` 中的 `Scene` 节点，经 `GF.Scene` 访问。

### 4.1 Inspector 参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `m_EnableLoadSceneSuccessEvent` | true | 转发加载成功全局事件 |
| `m_EnableLoadSceneFailureEvent` | true | 转发加载失败全局事件 |
| `m_SceneHelperTypeName` | `GodotGameFramework.Scene.DefaultSceneHelper` | 场景辅助器类型名（反射创建，可替换） |

### 4.2 方法总览

```csharp
// 加载（事件驱动）
GF.Scene.LoadScene(sceneAssetName);
GF.Scene.LoadScene(sceneAssetName, priority);
GF.Scene.LoadScene(sceneAssetName, priority, userData);

// 加载（可 await，推荐）
Node scene = await GF.Scene.LoadSceneAsync(sceneAssetName);
Node scene = await GF.Scene.LoadSceneAsync(sceneAssetName, priority);
Node scene = await GF.Scene.LoadSceneAsync(sceneAssetName, priority, userData);

// 查询
GF.Scene.IsSceneLoaded(assetPath);
GF.Scene.IsSceneLoading(assetPath);
T instance = GF.Scene.GetLoadedScene<T>(assetPath);   // T : Node，未加载返回 null

// 卸载（当前流程不完整，见 §5）
GF.Scene.UnloadScene(assetPath);
GF.Scene.UnloadAllScenes();
```

### 4.3 使用示例（TheGame/MainForm.Logic.cs 实际代码）

```csharp
public virtual async void OnInit(int serialId, ..., object userData)
{
    // 加载地图场景 → 实例已挂在 GameFramework/Scene 下
    Node2D scene = (Node2D)await GF.Scene.LoadSceneAsync(ResourcesCollectionConstant.Scenes_Map);

    // 直接读取场景内的标记节点组织玩法
    Node2D spawnPoint = scene.GetNode<Node2D>("SpawnPoint");
    Line2D line2D = scene.GetNode<Line2D>("Line2D");

    CatEntity cat = await GF.Entity.ShowEntityAsync<CatEntity>(EntityId.Cat);
    cat.Position = spawnPoint.Position;
}
```

**事件驱动方式：**

```csharp
GF.Event.Subscribe(GodotGameFramework.Scene.LoadSceneSuccessEventArgs.EventId, OnSceneLoaded);
GF.Scene.LoadScene("res://TheGame/Scenes/Map.tscn");

private void OnSceneLoaded(object sender, GameEventArgs e)
{
    var args = (GodotGameFramework.Scene.LoadSceneSuccessEventArgs)e;
    Log.Info("场景就绪: {0}", args.SceneAssetPath);
    // args 池化，勿持有；SceneInstance 需要跨帧使用请拷贝引用
}
```

---

## 5. 与 ResourceComponent 的关系

- SceneManager 自身不做任何 IO：加载统一走 `IResourceManager.LoadAsset`（Package 模式下即 `ResourceManager` 的 `m_AssetTaskPool` → `Godot.ResourceLoader`），享受与 UI/实体/声音一致的任务池、优先级与子包（.pck）解析——热更子包里的场景无需任何特殊处理
- 场景资源就是普通的 `PackedScene`；`HasScene()` 委托 `IResourceManager.HasAsset`
- 区别于 `GF.Resource.LoadAssetAsync`（只加载资源不实例化、不登记）：场景系统额外提供**实例化 + 挂树 + 全局唯一登记 + 事件**。若只想拿 `PackedScene` 自己控制实例化，用 `GF.Resource.LoadAssetAsync(path)` 即可
- 注：CLAUDE.md 提到的 `ResourceComponent.LoadSceneAsync()/LoadBinary()/LoadText()` 便捷方法在当前代码中不存在，`ResourceComponent` 仅有 `LoadAsset<T>` / `LoadAssetAsync`

---

## 6. 注意事项 / FAQ 与已知边界

**Q: 加载的场景挂在哪？会替换当前场景吗？**
挂在 `GameFramework/Scene` 组件节点下，不替换 `SceneTree.CurrentScene`。"切场景" = 卸载旧实例 + 加载新实例，UI/实体/BGM 全程不受影响。

**Q: 重复加载同一场景抛异常？**
是（"is already loaded"）。加载前先 `IsSceneLoaded` / `IsSceneLoading` 判断。需要多实例请走实体系统或 `GF.Resource`。

**Q: `LoadSceneAsync` 失败时表现？**
await 处抛异常（TCS.TrySetException）。空路径除外（返回 null）。

**⚠️ 已知边界：卸载流程未闭环。**
`SceneManager.UnloadScene` 当前只把资源名加入 `m_UnloadingSceneAssetNames` 便返回——构造函数里准备的 `m_UnloadSceneCallbacks`（`UnloadSceneSuccessCallback`：移出三表、经 `ISceneHelper.ReleaseScene` 对实例 `QueueFree`、发 `UnloadSceneSuccess` 事件）**没有任何调用点**（Unity 版此处由 `m_ResourceManager.UnloadScene(...)` 驱动，Godot 版 `IResourceManager` 精简后未保留该成员）。实际后果：

1. 调用 `UnloadScene` 后场景节点**不会被释放**，仍留在树中
2. 该资源永久停在 "unloading" 状态，之后再 `LoadScene` 同一场景会抛 "is being unloaded"
3. `UnloadSceneSuccessEventArgs`（Godot 层）永远不会触发

**当前替代做法**：持有 `LoadSceneAsync` 返回的 `Node` 自行 `QueueFree()`；如需继续使用框架登记，需等待卸载链路补全（在 `UnloadScene` 中直接调用 `m_UnloadSceneCallbacks` 的成功回调即可闭环）。

**Q: 组件销毁时场景怎么办？**
`GameFrameworkEntry.Shutdown` → `SceneManager.Shutdown` 会对所有已加载场景调用 `UnloadScene`（受上述边界影响，仅清理登记表）；场景节点本身随 `Scene` 组件节点一起被 Godot 树销毁回收，进程退出无泄漏。

### 后续计划

- [ ] 补全卸载链路（触发 `UnloadSceneSuccessCallback` → `ReleaseScene`/`QueueFree` → 事件）
- [ ] `UnloadSceneAsync` 可 await 封装
- [ ] `LoadSceneUpdate` 进度转发（配合 loading 界面）
