# 对象池系统 (Object Pool Module)

> 适用版本：Godot 4.6.2 + .NET 8 ｜ 对应代码：`Framework/GameFramework/ObjectPool/`、`Framework/GodotGameFrameworkCore/ObjectPool/`
> 本文档描述 GGF 的对象池系统：ObjectBase/IObjectPool 设计、Spawn/Unspawn 流程、释放策略、与 ReferencePool 的区别，以及框架内的实际使用点。

---

## 1. 概述

对象池系统是 [Game Framework](https://gameframework.cn/) ObjectPool 模块的移植（**逻辑几乎原封不动**——本模块本身不含任何 Godot 类型），用于重用"创建/销毁代价高"的对象（典型：Godot 节点实例）。遵循框架**双层架构**：

| 层 | 位置 | 职责 | Godot 依赖 |
|----|------|------|:--:|
| 纯 C# 层 | `GameFramework/ObjectPool/` | `ObjectPoolManager` 模块：池的创建/查询/销毁/轮询、内部 `ObjectPool<T>` 与 `Object<T>` 实现、释放筛选策略 | ❌ |
| Godot 桥接层 | `GodotGameFrameworkCore/ObjectPool/` | `ObjectPoolComponent`：纯透传封装（每个管理器方法一一对应），经 `GF.ObjectPool` 访问 | ✅ |

### 两种池模式

| 模式 | 创建方法 | 语义 |
|------|----------|------|
| SingleSpawn | `CreateSingleSpawnObjectPool<T>` | 同一对象同一时刻只能被获取一次（Entity/UIForm 实例即此模式） |
| MultiSpawn | `CreateMultiSpawnObjectPool<T>` | 同一对象可被同时获取多次（内部 `SpawnCount` 引用计数） |

池以 **`(对象类型 T, 池名称 name)`** 二元组唯一标识（`TypeNamePair`）——同一 `T` 可以建多个不同名的池（如每个实体组一个 `EntityInstanceObject` 池）。

### 与 ReferencePool 的区别

| | **ReferencePool**（引用池） | **ObjectPool**（对象池） |
|---|---|---|
| 定位 | 轻量 C# 对象复用（`IReference`），消 GC | 重量级资源实例复用（Godot 节点等） |
| 粒度 | 按类型全局一个池 | 按 `(类型, 池名)` 任意多个池，每池独立参数 |
| 获取/归还 | `ReferencePool.Acquire<T>()` / `Release(obj)` | `pool.Spawn(name)` / `pool.Unspawn(target)` |
| 生命周期回调 | 仅 `Clear()`（归还时清理） | `OnSpawn / OnUnspawn / Release(isShutdown)` |
| 淘汰策略 | 无（永不释放，只增不减） | 容量 + 过期时间 + 优先级 + 自动释放轮询 |
| 命名/查找 | 无 | 对象按 `Name` 键入池，可 `Spawn(name)` 定向取 |
| 典型使用 | 事件参数、任务对象、`PhysicsCheck2D`、**ObjectBase 包装壳本身** | 实体场景实例、UI 界面实例 |

两者是**嵌套关系**：`ObjectBase` 派生类实现 `IReference`，其包装壳照例从 `ReferencePool.Acquire` 创建、释放时归还——对象池管理"贵重货物"（`Target`），引用池管理"包装纸"。

> ✅（2026-07）场景新增 `ReferencePool` 节点（`ReferencePoolComponent`）：按 `ReferenceStrictCheckType` 策略（`AlwaysEnable`〈当前默认〉/ `OnlyEnableInEditor` / `OnlyOpenWhenDevelopment` / `AlwaysDisable`）统一设置 `ReferencePool.EnableStrictCheck`，双重 `Release` 直接抛异常而非静默污染池。运行时可在调试器 `Profiler/Object Pool`（逐池参数 + Release 按钮）与 `Profiler/Reference Pool`（7 列计数表 + 严格检查开关）页签观察两种池（见 `DebuggerSystem.md`）。

---

## 2. 架构与数据流

```
调用方（EntityManager.EntityGroup / UIManager / 业务代码）
    │  GF.ObjectPool.CreateSingleSpawnObjectPool<T>(name, autoReleaseInterval, capacity, expireTime, priority)
    ▼
ObjectPoolComponent (Godot 桥接层，场景节点 "ObjectPool")   ← 纯透传
    ▼
ObjectPoolManager : GameFrameworkModule (Priority=6，纯 C# 层)
    │  Dictionary<TypeNamePair, ObjectPoolBase>
    │  每帧 Update() 轮询每个池
    ▼
ObjectPool<T>（内部类）
    ├── m_Objects   : MultiDictionary<string, Object<T>>   ← 按对象 Name 索引（Spawn(name) 查找用）
    ├── m_ObjectMap : Dictionary<object, Object<T>>        ← 按 Target 反查（Unspawn/SetLocked 用）
    └── Object<T>（内部包装，ReferencePool 池化）
            ├── SpawnCount / IsInUse / Locked / Priority / LastUseTime
            └── Spawn() → obj.OnSpawn()   Unspawn() → obj.OnUnspawn()   Release() → obj.Release(isShutdown)
```

对象状态流转：

```
Register(obj, spawned) ──┬─ spawned=true  → 入池即"使用中"（实体新实例的典型路径）
                         └─ spawned=false → 入池即空闲
Spawn(name)  → SpawnCount++ → OnSpawn()  → 使用中（刷新 LastUseTime）
Unspawn(target) → OnUnspawn() → SpawnCount-- → 空闲（刷新 LastUseTime）
    │
    ├─ 空闲 + 未锁 + CustomCanReleaseFlag → "可释放"候选
    ▼
Release()（容量超限 / 自动间隔 / 手动） → 筛选 → obj.Release(false) → 移出池 → 包装壳还给 ReferencePool
Shutdown → 所有对象 obj.Release(true)
```

### 文件清单

| 文件 | 职责 |
|------|------|
| `GameFramework/ObjectPool/ObjectBase.cs` | 池化对象基类：Name/Target/Locked/Priority/LastUseTime + `OnSpawn`/`OnUnspawn`/`Release(isShutdown)`/`CustomCanReleaseFlag` |
| `GameFramework/ObjectPool/IObjectPool.cs` | 单池接口：Register/CanSpawn/Spawn/Unspawn/UnspawnAll/SetLocked/SetPriority/Release 系列 |
| `GameFramework/ObjectPool/IObjectPoolManager.cs` | 管理器接口：Has/Get/Create/Destroy/Release/ReleaseAllUnused |
| `GameFramework/ObjectPool/ObjectPoolManager.cs` | 管理器实现 + 30 余个 Create 重载（参数组合） |
| `GameFramework/ObjectPool/ObjectPoolManager.ObjectPool.cs` | 单池实现：双索引、释放筛选、自动释放计时 |
| `GameFramework/ObjectPool/ObjectPoolManager.Object.cs` | 内部对象包装 `Object<T>`（SpawnCount 计数） |
| `GameFramework/ObjectPool/ObjectPoolBase.cs` / `ObjectInfo.cs` | 非泛型池基类（调试面板用）/ 对象信息快照 |
| `GameFramework/ObjectPool/ReleaseObjectFilterCallback.cs` | 自定义释放筛选委托 |
| `GodotGameFrameworkCore/ObjectPool/ObjectPoolComponent.cs` | `GF.ObjectPool` 组件（透传） |

---

## 3. 核心机制

### 3.1 池参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `Capacity` | `int.MaxValue` | 池容量。`Register`/`Unspawn` 后 `Count > Capacity` 即触发一次 `Release()`（尝试释放超额部分） |
| `ExpireTime` | `float.MaxValue` | 对象过期秒数。空闲对象 `LastUseTime` 距今超过此值即成为优先释放对象；**运行时改小会立即触发 Release** |
| `AutoReleaseInterval` | 未显式指定时 = `ExpireTime` | 自动释放轮询间隔：每帧累计真实时间，达到间隔即 `Release()`。默认双 MaxValue 等于**关闭自动释放** |
| `Priority` | 0 | 对象/池优先级。释放时**优先级低的先被释放**；同优先级按 `LastUseTime` 旧者先释放 |

释放筛选（`DefaultReleaseObjectFilterCallback`）两轮：先释放**全部已过期**对象；仍需释放时再按 `Priority` 升序 + `LastUseTime` 升序补足 `Count - Capacity` 个。

### 3.2 可释放条件

对象同时满足以下三条才进入候选：

1. `!IsInUse`（SpawnCount == 0，未被获取）
2. `!Locked`（`SetLocked(target, true)` 可钉住常驻对象，如常用 UI）
3. `CustomCanReleaseFlag`（`ObjectBase` 虚属性，默认 true；子类可按业务否决，如"动画未播完不许销毁"）

`ReleaseObject(target)` 强制释放单个对象也受同样条件约束（不满足返回 false）。

### 3.3 Register 的 `spawned` 语义

`Register(obj, spawned: true)` 表示"入池即已被使用"——实体/UI 加载出新实例后**直接投入使用**，同时登记进池，避免"先入池再 Spawn"的一来一回。`spawned: true` 时 `Object<T>.Create` 会补调一次 `OnSpawn()`。

### 3.4 Name 定向获取

`Spawn(name)` 只在同名对象中找空闲实例——实体组池以**资源路径**为 Name，同组内 `CatEntity.tscn` 与 `AngerEntity.tscn` 的实例互不串用。`Spawn()` 等价于 `Spawn(string.Empty)`，只匹配注册时未命名的对象。

### 3.5 线程模型

无锁设计，所有 API 必须在主线程调用（框架 `Update` 驱动亦在主线程）。

---

## 4. 组件与 API

场景节点：`Framework/GameFramework.tscn` 中的 `ObjectPool` 节点，经 `GF.ObjectPool` 访问。组件无 Inspector 参数（池全部代码创建）。

### 4.1 API 总览

```csharp
// 池管理
IObjectPool<T> pool = GF.ObjectPool.CreateSingleSpawnObjectPool<T>(name, capacity, expireTime, priority);
IObjectPool<T> pool = GF.ObjectPool.CreateMultiSpawnObjectPool<T>(name);
// Create 重载覆盖 name/capacity/expireTime/priority/autoReleaseInterval 的各种组合（各 30+ 个）
bool has  = GF.ObjectPool.HasObjectPool<T>(name);
pool      = GF.ObjectPool.GetObjectPool<T>(name);          // 不存在返回 null
var pools = GF.ObjectPool.GetAllObjectPools(sort: true);   // 调试面板用（ObjectPoolBase）
GF.ObjectPool.DestroyObjectPool<T>(name);                  // 池内所有对象 Release(true)
GF.ObjectPool.Release();                                   // 全部池立即执行一次释放检查
GF.ObjectPool.ReleaseAllUnused();                          // 释放全部池的全部空闲对象（切场景后调用）

// 单池操作（IObjectPool<T>）
pool.Register(obj, spawned);        // 新对象入池
bool ok = pool.CanSpawn(name);      // 是否有可用实例（不获取）
T obj   = pool.Spawn(name);         // 无可用实例返回 null（不自动创建！）
pool.Unspawn(target);               // 按 Target 归还（找不到抛异常）
pool.UnspawnAll();                  // 归还全部（GGF 扩展，原版无）
pool.SetLocked(target, true);       // 钉住不释放
pool.SetPriority(target, 10);       // 调整单个对象释放优先级
pool.ReleaseObject(target);         // 强制释放单个（使用中/加锁/Custom 否决时返回 false）
pool.Release();                     // 手动触发一次释放检查
pool.ReleaseAllUnused();

// 运行时调参
pool.Capacity = 32;  pool.ExpireTime = 120f;  pool.AutoReleaseInterval = 60f;  pool.Priority = 1;
```

### 4.2 自定义池化对象（参考 `TheGame/GameScripts/ObjectPool/TestPoolObject.cs`）

```csharp
public class MyPoolObject : ObjectBase
{
    public MyPoolObject() { }                       // 无参构造必须有（ReferencePool 要求）

    public static MyPoolObject Create(string name, MyItem item)
    {
        var obj = ReferencePool.Acquire<MyPoolObject>();   // 包装壳来自引用池
        obj.Initialize(name, item);                        // name = 池内键；item = Target
        return obj;
    }

    public MyItem Item => (MyItem)Target;

    protected internal override void OnSpawn()   { /* 取出：重置/显示 */ }
    protected internal override void OnUnspawn() { /* 归还：隐藏/断开 */ }
    protected internal override void Release(bool isShutdown)
    {
        // 从池中永久移除：销毁 Target（Godot 节点应 QueueFree）
    }
}

// 使用
var pool = GF.ObjectPool.CreateSingleSpawnObjectPool<MyPoolObject>("MyPool", capacity: 16);
pool.Register(MyPoolObject.Create("item1", new MyItem()), spawned: false);
var obj = pool.Spawn("item1");     // → OnSpawn
pool.Unspawn(obj.Target);          // → OnUnspawn
```

---

## 5. 框架内实际使用点

| 使用方 | 池 | 模式 | 说明 |
|--------|-----|------|------|
| `EntityManager.EntityGroup`（纯 C# 层） | `Entity Instance Pool ({组名})`，对象 `EntityInstanceObject` | SingleSpawn | **每个实体组一个池**，参数来自 `EntityGroupRes`（ReleaseInterval/Capacity/ExpireTime/Priority）。对象 Name = 场景路径；`Release` 时经 `IEntityHelper.ReleaseEntity` → `QueueFree()`。详见 `EntitySystem.md` §3.2 |
| `UIManager`（纯 C# 层） | `UI Instance Pool`，对象为 UIManager 私有嵌套 `UIFormInstanceObject` | SingleSpawn | 全局一个 UI 实例池，`CloseUIForm` 后界面实例回池，`OpenUIForm` 复用。详见 `UISystem.md` |
| `GodotGameFrameworkCore/UI/UIFormInstanceObject.cs` `UIItemInstanceObject.cs` | — | — | Godot 层公开版包装（`UIItemInstanceObject.OnSpawn/OnUnspawn` 自带节点显隐与位置重置）。`UIItemInstanceObject` 面向 UIItem（列表项等）复用场景，**当前尚无运行时调用方**，作为模板保留 |
| `TheGame/GameScripts/ObjectPool/TestPoolObject.cs` | — | — | 独立参考示例，未在游戏流程中加载 |

> 注意与 **TaskPool**（`DownloadManager`/资源加载中的任务调度器）区分：TaskPool 是"任务队列 + 代理"，其任务对象走 ReferencePool，与本模块无关。

---

## 6. 注意事项 / FAQ

**Q: `Spawn` 返回 null 怎么办？**
对象池**不负责创建对象**——池空/无空闲实例时返回 null，由调用方自行创建实例并 `Register(obj, spawned: true)`（实体/UI 系统均为此模式："先问池，无则加载"）。

**Q: `Unspawn` 传什么？**
传 **Target**（被包装的真实对象，如 Node），不是 `ObjectBase` 壳（传壳的重载内部也是取 `obj.Target`）。目标不在池中会抛 `GameFrameworkException`。

**Q: 默认参数下对象会被释放吗？**
不会。Capacity/ExpireTime/AutoReleaseInterval 默认均为 MaxValue，池只进不出。要有淘汰行为必须显式给出容量或过期时间（实体组经 `EntityGroupRes` 配置）。

**Q: `Capacity` 是硬上限吗？**
不是。它是**释放水位线**：超过容量只是触发释放检查，若空闲对象不足（都在使用中/被锁），Count 可以持续大于 Capacity。

**Q: 切场景后如何一次性清掉缓存节点？**
`GF.ObjectPool.ReleaseAllUnused()`——释放所有池的全部空闲对象（使用中与加锁对象不受影响）。

**Q: 想让某个对象常驻（如主界面）？**
`pool.SetLocked(target, true)`。或重写 `CustomCanReleaseFlag` 按运行时条件动态否决释放。

**Q: MultiSpawn 池的对象什么时候算"空闲"？**
`SpawnCount` 归零时。每次 `Spawn` +1、`Unspawn` -1，必须严格配对；多归还会使计数为负并抛 `GameFrameworkException`。

**Q: 池本身的 `Priority` 影响什么？**
仅用于 `GetAllObjectPools(sort: true)` 的排序展示（调试面板）；释放顺序由**对象**的 Priority 决定，两者不要混淆。

---

## 7. 已知边界与后续计划

- [ ] `UIItemInstanceObject` 的 UIItem 复用链路接入实际调用方（当前为预留模板）
- [ ] 调试面板：`GetAllObjectInfos()` / `ObjectInfo` 已具备数据能力，缺编辑器/运行时可视化
- [ ] `GF.ObjectPool.Release()` 挂接内存告警（Godot 无 Unity `lowMemory` 回调，需自行监控）
