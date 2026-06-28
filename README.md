# GGF

<div align="center">

**Godot 4.6.2 + C# (.NET 8) Game Framework**

[![Godot Version](https://img.shields.io/badge/Godot-4.6.2-blue?style=flat-square)](https://godotengine.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/NuoYan/GGF?style=flat-square)](GodotProject/LICENSE)
[![GameFramework](https://img.shields.io/badge/GameFramework-2025.07.10-green?style=flat-square)](https://gameframework.cn/)

</div>

---

## 📖 简介

**GGF** (Godot Game Framework) 是 [Game Framework](https://gameframework.cn/)（Jiang Yin）的 **Godot 4.6.2 C# 移植版**。提供一套完整的模块化游戏开发框架，包含事件、FSM、流程、资源、实体、UI、音频、本地化、对象池、数据表、设置等子系统。

### ✨ 核心特性

- 🧩 **模块化架构** — 13 个独立子系统，高内聚低耦合，可按需替换
- 🔄 **双层架构** — 纯 C# 核心层（无 Godot 依赖）+ Godot 运行时组件层
- 🎯 **直接继承模式** — Entity/UI 不再使用包装器，用户脚本直接继承 Godot 原生类型 + 框架接口
- 📊 **数据管线** — 集成 Luban，Excel 配置 → C# 代码 + 二进制数据
- 🎨 **Entity/UI 管理** — 支持对象池复用、生命周期管理、层级控制
- 🔊 **音频系统** — 声音组 + 优先级抢占 + 淡入淡出
- 📝 **条件日志** — `[Conditional("ENABLE_LOG")]` 编译时零开销移除，编辑器插件可切换
- 🔧 **编辑器插件** — 日志切换、本地化导出、资源路径常量生成

---

## 📚 目录

- [快速开始](#-快速开始)
- [架构概览](#-架构概览)
- [核心模块](#-核心模块)
- [项目结构](#-项目结构)
- [使用示例](#-使用示例)
- [数据管线](#-数据管线)
- [编辑器插件](#-编辑器插件)
- [系统要求](#-系统要求)
- [开源项目推荐](#-开源项目推荐)

---

## 🚀 快速开始

### 环境要求

- **Godot**: 4.6.2（.NET 版本）
- **.NET SDK**: 8.0+
- **渲染器**: D3D12（默认）
- **物理引擎**: Jolt Physics（默认）

### 快速上手

1. **克隆项目**
   ```bash
   git clone <repo-url>
   cd Godot
   ```

2. **快速编译**
   ```bash
   cd GodotProject
   dotnet build
   ```

3. **打开编辑器**
   ```bash
   "<godot_exe>" --path GodotProject --editor
   ```

4. **添加新 .cs 文件后需执行完整构建**
   ```bash
   "<godot_exe>" --build-solutions --path GodotProject --no-window -q
   ```

> 💡 **Godot 编辑器路径示例**: `E:\Godot\Godot_v4.6.2-stable_mono_win64\...\Godot_v4.6.2-stable_mono_win64.exe`

---

## 🏗️ 架构概览

### 双层架构

```
┌─────────────────────────────────────────────────────┐
│              Godot Runtime Layer                     │
│  GodotGameFrameworkCore/                             │
│  ├── GodotComponent (Node)        生命周期虚方法      │
│  ├── GameFrameworkComponent       自动注册到 GameEntry │
│  ├── GameEntry                    根节点，驱动 Update  │
│  ├── GF                           静态门面            │
│  ├── Abstract*Entity / ControlUIForm  实体/UI 基类   │
│  └── EntityComponent / UIComponent / SoundComponent   │
├─────────────────────────────────────────────────────┤
│               Pure C# Core Layer                     │
│  GameFramework/                                      │
│  ├── GameFrameworkEntry         模块入口              │
│  ├── GameFrameworkModule        模块基类              │
│  ├── ReferencePool/EventPool    引用池/事件调度        │
│  └── EntityManager/UIManager/... 核心 Manager         │
└─────────────────────────────────────────────────────┘
```

### 直接继承模式

Entity 和 UI 不再使用包装器 + Logic 双层分离。用户场景根节点直接继承框架抽象基类，基类同时继承 Godot 原生类型和框架接口：

```
AbstractSprite2DEntity : Sprite2D, IEntity
AbstractCharacterBody2DEntity : CharacterBody2D, IEntity
ControlUIForm : Control, IUIForm
```

场景实例本身就是 `IEntity`/`IUIForm`，无需额外包装。

---

## 🧩 核心模块

### 实体模块 (EntityComponent)

- ✅ 基于 `IEntityManager` 的实体生命周期管理
- ✅ `ShowEntity(EntityId)` Luban 配置驱动，支持对象池复用
- ✅ `ShowEntityAsync<T>()` 异步加载，返回类型安全的 `T : Node, IEntity`
- ✅ 实体组管理（容量、过期时间、优先级可配）
- ✅ 父子实体挂载 + Godot 场景树 Node 关系同步
- ✅ 四个抽象基类：`AbstractSprite2DEntity` / `AbstractNode2DEntity` / `AbstractCharacterBody2DEntity` / `AbstractRb2DEntity`，用户直接继承，子节点通过 `[Export]` 绑定或 `FindChildOfType<T>()` 获取

### UI 模块 (UIComponent)

- ✅ 基于 `IUIManager` 的窗体管理
- ✅ 4 个默认 UI 层级：Background / Normal / Popup / Tips
- ✅ 界面组管理，支持深度排序
- ✅ `OpenUIForm(UIFormId)` Luban 配置驱动
- ✅ `ControlUIForm : Control, IUIForm` 基类，自动收集 `IStringKey` 子节点并刷新本地化文本
- ✅ 内置 `Close()` 方法

### 音频模块 (SoundComponent)

- ✅ 基于 `ISoundManager` 的音频管理
- ✅ 声音组管理（Music / SFX / UI 默认组）
- ✅ 优先级抢占算法
- ✅ 淡入/淡出控制
- ✅ 组级静音/音量级联

### 资源模块

- ✅ **ResourceManager 异步队列架构** — 内部 `Queue<LoadAssetTask>` + `LoadThreadedGetStatus` 帧轮询，支持 `Loaded`/`InProgress`/`Failed` 全状态处理
- ✅ `ResourceComponent` 桥接层：`LoadBinary()` / `LoadText()` / `LoadAssetAsync()`

### 事件模块 (EventComponent)

- ✅ 基于 `IEventManager` 的线程安全事件系统
- ✅ 延迟分发（Fire）和立即分发（FireNow）
- ✅ 组件事件自动转发到 EventComponent

### 流程模块 (ProcedureComponent)

- ✅ 基于 `IFsmManager` 的流程状态机
- ✅ Inspector 配置可用的 Procedure 类型
- ✅ 流程间切换：`TestLaunchProcedure` → `TestMenuProcedure` → `TestGameProcedure`

### 数据表模块 (DataTableComponent)

- ✅ Luban 生成的二进制数据反序列化
- ✅ `GF.DataTable` 返回类型安全的 `Tables` 实例
- ✅ 懒加载支持

### NodeExtension 扩展

`NodeExtension` 提供常用 Node 查询扩展方法：
- `FindChildOfType<T>()` — 递归查找子节点
- `FindChildrenOfType<T>()` — 递归查找所有匹配子孙节点
- `GetChild<T>()` / `GetChildren<T>()` / `GetParent<T>()`

### Helper 基类体系

每个子系统定义了一套遵循统一可扩展模式的抽象 Helper 基类：

| 系统 | Helper 基类 | 职责 |
|------|------------|------|
| UI | `UIFormHelperBase` / `UIGroupHelperBase` | 界面实例化 / 组容器 |
| Entity | `EntityHelperBase` / `EntityGroupHelperBase` | 实体创建 / 组容器 |
| Sound | `SoundHelperBase` / `SoundGroupHelperBase` / `SoundAgentHelperBase` | 音频加载 / 组容器 / 播放代理 |

通过 `Helper.CreateHelper<T>()` 创建，可在编辑器插件中自定义替换。

---

## 📁 项目结构

```
Configs/                      ← Excel 配置源数据（Luban 管线输入）
GodotProject/                 ← Godot 项目根
├── Framework/
│   ├── GameFramework/        ← 纯 C# 框架（无 Godot 依赖）
│   │   ├── Base/             ← 入口、模块、引用池、事件池
│   │   ├── Entity/ UI/ Sound/ Resource/ Event/
│   │   ├── Fsm/ Procedure/ Config/ DataTable/
│   │   ├── DataNode/ ObjectPool/ Setting/
│   │   ├── Localization/ Network/ Download/
│   │   ├── Debugger/ FileSystem/ Scene/
│   │   └── Utility/ WebRequest/
│   └── GodotGameFrameworkCore/  ← Godot 运行时组件
│       ├── Base/             ← GameEntry, GF, GodotComponent, Log
│       │   └── Node/         ← 抽象实体/UI 基类（Abstract*Entity, ControlUIForm）
│       ├── Entity/           ← EntityComponent, Entity<T>, EntityExtension
│       ├── UI/               ← UIComponent, IStringKey
│       ├── Sound/            ← SoundComponent + Helpers
│       ├── Resource/         ← ResourceManager（队列轮询引擎）+ ResourceComponent 桥接
│       ├── Event/ Fsm/ Procedure/ Config/
│       ├── DataTable/ DataNode/ ObjectPool/ Setting/
│       ├── Localization/ Utility/ Variable/
│       └── Lib/LubanLib/     ← Luban 运行时（ByteBuf, BeanBase）
│   └── GameFramework.tscn    ← 主场景
├── TheGame/                  ← 当前活跃游戏项目
│   ├── Entitys/              ← 实体场景 (.tscn)，根节点为 Godot 原生类型
│   ├── GameScripts/
│   │   ├── Entity/           ← 实体脚本（继承 Abstract*Entity）
│   │   ├── UI/               ← UI 脚本（继承 ControlUIForm）
│   │   └── GameProto/GameConfig/ ← Luban 生成的 C# 数据类
│   ├── DataTables/           ← Luban 生成的二进制数据
│   ├── UIs/ Sprites/ Scenes/ Audios/
└── addons/                   ← 编辑器插件
    ├── TopMenu/              ← 日志级别切换
    ├── LocalizationEditor/   ← Excel→TXT 转换
    └── Resources/            ← 资源路径常量生成
```

---

## 🎯 使用示例

### 实体系统

```csharp
// 1. 定义实体类（继承对应 Godot 类型的抽象基类）
public partial class CatEntity : AbstractCharacterBody2DEntity
{
    [Export] private Sprite2D m_CatSprite; // Inspector 绑定视觉子节点

    public override void OnInit(int entityId, string entityAssetName,
        IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        // 加载配置等初始化逻辑
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        Velocity = new Vector2(Input.GetAxis("ui_left", "ui_right"),
            Input.GetAxis("ui_up", "ui_down")) * Speed;
        MoveAndSlide();
    }
}

// 2. 场景结构（CatEntity.tscn）
// CatEntity (CharacterBody2D, CatEntity 脚本)
//   ├── CollisionShape2D
//   └── Sprite2D (视觉)

// 3. 显示实体
int catId = GF.Entity.ShowEntity(EntityId.Cat);

// 4. 异步显示并获取实体引用
CatEntity cat = await GF.Entity.ShowEntityAsync<CatEntity>(EntityId.Cat);
cat.GlobalPosition = new Vector2(100, 200);

// 5. 隐藏实体
GF.Entity.HideEntity(catId);
GF.Entity.HideEntitySafe(catId); // 安全隐藏（检查存在性）
```

### UI 系统

```csharp
// 1. 定义 UI 类
public partial class MenuForm : ControlUIForm
{
    public override void OnInit(int serialId, string uiFormAssetName,
        IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        base.OnInit(serialId, uiFormAssetName, uiGroup, pauseCoveredUIForm, isNewInstance, userData);
        // IStringKey 子节点自动刷新本地化文本
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        // 播放打开动画
    }
}

// 2. 打开界面
int menuId = GF.UI.OpenUIForm(UIFormId.Menu);

// 3. 关闭界面
// 在 ControlUIForm 子类中直接调用 Close();
```

### 音频系统

```csharp
// 播放背景音乐（Music 组，循环）
int bgmId = GF.Sound.PlaySound("res://Audio/BGM.mp3", "Music");

// 播放音效
PlaySoundParams sfxParams = PlaySoundParams.Create();
sfxParams.VolumeInSoundGroup = 0.8f;
GF.Sound.PlaySound("res://Audio/Click.wav", "SFX", sfxParams);

// 停止（带淡出）
GF.Sound.StopSound(bgmId, 1f);

// 声音组控制
GF.Sound.SetSoundGroupVolume("Music", 0.5f);
GF.Sound.SetSoundGroupMute("SFX", true);
```

### 资源加载

```csharp
// 同步加载文本/二进制文件（Godot FileAccess）
string text = GF.Resource.LoadText("res://Data/Config.txt");
byte[] data = GF.Resource.LoadBinary("res://Data/Config.dat");

// 异步加载资源（ResourceManager 队列轮询）
Godot.Resource res = await GF.Resource.LoadAssetAsync("res://Sprites/Player.png", 0);

// 检查资源是否存在
if (GF.Resource.Exists("res://Scenes/Enemy.tscn"))
{
    // ...
}
```

### 事件系统

```csharp
// 自定义事件
public sealed class ScoreChangedEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(ScoreChangedEventArgs).GetHashCode();
    public override int Id => EventId;
    public int Score { get; private set; }
    // ...
}

// 订阅
GF.Event.Subscribe(ScoreChangedEventArgs.EventId, OnScoreChanged);

// 触发
GF.Event.Fire(this, new ScoreChangedEventArgs(100));
```

### 本地化与日志

```csharp
string title = GF.Localization.GetString("GameTitle");

Log.Info("Player {0} scored {1} points", name, score);
Log.Warning("Health low: {0}", currentHp);
Log.Error("Failed to load: {0}", path);
```

> 💡 日志通过 `[Conditional("ENABLE_LOG")]` 实现编译时移除，在 Godot 编辑器 **Project > Tools > GameFramework** 菜单中可切换日志级别。

---

## 📊 数据管线

集成 **Luban** 配置表解决方案：

```
Configs/GameConfig/Datas/*.xlsx
         │
         │ gen_code_bin_to_project.bat
         ▼
TheGame/GameScripts/GameProto/GameConfig/*.cs   ← C# 数据类（具类型访问）
TheGame/DataTables/*.bytes                        ← 二进制数据（运行时加载）
```

- **源文件**: `__tables__.xlsx`（表定义）、`__beans__.xlsx`（数据结构）、`__enums__.xlsx`（枚举）+ 业务 Excel
- **运行时**: `LubanLib/ByteBuf` + `BeanBase` 反序列化
- **入口**: `GF.DataTable` 返回类型安全的 `Tables` 实例
- **完整管线**: `luban.conf` → 自定义模板 → 目标代码

---

## 🔧 编辑器插件

Godot 编辑器 **Project > Tools** 菜单下有三个内置工具：

| 插件 | 功能 |
|------|------|
| **TopMenu** | 切换日志级别（Debug / Info / Warning / Error / Fatal / 全部关闭） |
| **LocalizationEditor** | `Configs/Localization/*.xlsx` → `res://TheGame/DataTables/Localizations/*.txt` |
| **ResourcesCollection** | 扫描 `res://TheGame/` 非脚本资源，生成 `ResourcesCollectionConstant.cs`（文件路径常量） |

---

## 📋 场景树

主场景 `Framework/GameFramework.tscn` 注册为 `run/main_scene`：

```
GameFramework (GameEntry)
├── Base / Resource / Event / Fsm / Procedure
├── Setting / DataTable / DataNode
├── ObjectPool / Entity / UI / Sound / Localization
```

每种组件类型只允许注册一个实例，`GameEntry.RegisterComponent()` 会校验唯一性。

---

## 🔧 系统要求

### Godot 版本

- **推荐版本**: Godot 4.6.2
- **渲染器**: D3D12（Windows 默认）
- **物理引擎**: Jolt Physics（3D 默认）

### 开发环境

- .NET SDK 8.0+
- Visual Studio 2022+ / Rider / VS Code
- Git

### 构建命令

| 命令 | 用途 |
|------|------|
| `dotnet build` | 日常开发快速编译 |
| `<godot> --build-solutions` | 添加 .cs 文件后必须执行 |
| `<godot> --editor` | 启动编辑器 |

---

## ⚠️ 开发注意事项

### 实体 ID 生成

实体 ID 使用 `Interlocked.Increment` 原子计数器生成，确保无碰撞。不再使用时间戳。

### Async/Task 时序注意

`EntityComponent.ShowEntityAsync` 和 `UIComponent.OpenUIFormAsync` 等异步方法依赖 `TaskCompletionSource` 监听底层事件。如果对应 Manager 在对象池中找到了缓存的实例，事件会**同步触发**。为此需要在调用 Manager 方法**之前**就注册好 tcs，避免事件先于 tcs 注册导致异步操作永远挂起。

### 组件事件取消订阅

`EntityComponent` 在 `OnExitTree()` 中取消订阅 `IEntityManager` 事件，防止场景重载时内存泄漏和事件重复触发。

### 实体类型选择

根据游戏需求选择合适的抽象基类：
| 基类 | 适用场景 |
|------|----------|
| `AbstractSprite2DEntity` | 静态/简单动画精灵 |
| `AbstractNode2DEntity` | 需要 2D 变换的通用实体 |
| `AbstractCharacterBody2DEntity` | 玩家/角色（物理交互） |
| `AbstractRb2DEntity` | 需要 RigidBody 物理模拟的实体（子弹、弹射物） |

---

## 🌟 开源项目推荐

| 项目 | 描述 | 链接 |
|------|------|------|
| **Game Framework** | 本项目的核心框架来源，Unity 游戏框架 | [GitHub](https://gameframework.cn/) |
| **Luban** | 游戏配置解决方案 | [GitHub](https://github.com/focus-creative-games/luban) |
| **Godot Engine** | 开源游戏引擎 | [GitHub](https://github.com/godotengine/godot) |
| **CodeGraph** | 代码知识图谱工具 | [GitHub](https://github.com/colbymchenry/codegraph) |

---

## 🚧 待实现功能

以下功能已规划但尚未实现，欢迎贡献：

### 资源系统

- [ ] **Updatable / UpdatableWhilePlaying 资源模式** — 需要实现 `IDownloadManager` 和 `IFileSystemManager` 的 Godot 层绑定，当前自动回退到 Package 模式

### UI 系统

- [ ] **UIItem 字符串键热更新** — `IStringKey` 接口已定义，需完善多语言切换时的自动刷新机制

### 网络系统

- [ ] **NetworkComponent** — 纯 C# 层 `INetworkManager` 已有接口定义，需在 Godot 层实现网络组件、Helper 基类及默认实现

### 场景系统

- [ ] **场景切换过渡** — 场景切换时的过渡动画、异步加载进度回调
- [ ] **SceneComponent 场景卸载完善** — 卸载时的资源释放确保和卸载回调

### 调试与工具

- [ ] **DebuggerComponent** — 纯 C# 层 `IDebuggerManager` 已有接口定义，需在 Godot 层实现调试窗口组件
- [ ] **单元测试框架** — 当前项目未配置测试框架，建议引入 `xUnit` + `NSubstitute` 覆盖核心模块

### 编辑器插件

- [ ] **Entity/UI 可视化编辑器** — 在 Godot 编辑器中提供实体组、界面组的可视化配置面板
- [ ] **Luban 一键生成菜单** — 将 `gen_code_bin_to_project.bat` 集成到 Godot 编辑器菜单中

---

## 📄 License

**MIT License**

本项目基于 MIT 协议开源。核心框架层 (`Framework/GameFramework/`) 版权归 © 2013-2021 Jiang Yin 所有，基于 [Game Framework](https://gameframework.cn/) 原项目移植。

查看 [LICENSE](GodotProject/LICENSE) 文件了解详情。

---

<div align="center">

**Made with ❤️ by GGF Contributors**

[⭐ Star](/) | [🐛 Issues](/) | [📖 Game Framework](https://gameframework.cn/)

</div>
