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
- 🎯 **组件委托模式** — Godot 组件持有核心 Manager 引用，所有操作委托给核心，不重复实现内部状态
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

### 组件委托模式

所有 Godot 组件遵循统一模式：持有核心 Manager 引用，在 `OnInit()` 中初始化，所有操作委托给核心 Manager。

```
EntityComponent ──→ IEntityManager
UIComponent     ──→ IUIManager
SoundComponent  ──→ ISoundManager
ResourceComponent ─→ IResourceManager
... 共 14 个组件
```

---

## 🧩 核心模块

### 实体模块 (EntityComponent)

- ✅ 基于 `IEntityManager` 的实体生命周期管理
- ✅ `ShowEntity<T>()` 泛型创建，支持对象池复用
- ✅ `ShowEntityAsync()` 异步加载，支持 async/await
- ✅ 实体组管理（容量、过期时间、优先级可配）
- ✅ 父子实体挂载 + Godot 场景树 Node 关系同步
- ✅ `Entity` 继承 `GodotComponent`，同时拥有框架生命周期和 Godot 节点生命周期

### UI 模块 (UIComponent)

- ✅ 基于 `IUIManager` 的窗体管理
- ✅ 4 个默认 UI 层级：Background / Normal / Popup / Tips
- ✅ 界面组管理，支持深度排序
- ✅ `OpenUIForm<T>()` 泛型创建
- ✅ 事件驱动的窗体生命周期

### 音频模块 (SoundComponent)

- ✅ 基于 `ISoundManager` 的音频管理
- ✅ 声音组管理（Music / SFX / UI 默认组）
- ✅ 优先级抢占算法
- ✅ 淡入/淡出控制
- ✅ 组级静音/音量级联

### 资源模块 (ResourceComponent)

- ✅ 两种加载模式：**管道模式**（核心 ResourceManager）和 **直接模式**（Godot ResourceLoader）
- ✅ 同步/异步加载
- ✅ `LoadAsset<T>()` / `LoadAssetAsync()`
- ✅ `LoadBinary()` / `LoadText()` 文件读取
- ✅ 管道模式下，GameFrameworkVersion.dat 版本列表驱动

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
docs/                         ← Godot 迁移 / 最佳实践文档
production/                   ← 元数据（stage, review-mode）
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
│       ├── Entity/           ← EntityComponent, Entity, EntityLogic
│       ├── UI/               ← UIComponent, UIForm, UIFormLogic
│       ├── Sound/            ← SoundComponent + Helpers
│       ├── Resource/         ← ResourceComponent + 资源管线
│       ├── Event/ Fsm/ Procedure/ Config/
│       ├── DataTable/ DataNode/ ObjectPool/ Setting/
│       ├── Localization/ Utility/ Variable/
│       └── Lib/LubanLib/     ← Luban 运行时（ByteBuf, BeanBase）
│   └── GameEntry.tscn        ← 主场景
├── TheGame/                  ← 当前活跃游戏项目
│   ├── DataTables/           ← Luban 生成的二进制数据
│   └── GameScripts/GameProto/ ← Luban 生成的 C# 数据类
└── addons/                   ← 编辑器插件
    ├── TopMenu/              ← 日志级别切换
    ├── LocalizationEditor/   ← Excel→TXT 转换
    └── Resources/            ← 资源路径常量生成
```

---

## 🎯 使用示例

### 实体系统
```csharp
// 创建实体组
GF.Entity.AddEntityGroup("Enemy", 60f, 16, 60f, 0);

// 显示实体（指定 EntityLogic 类型）
GF.Entity.ShowEntity<EnemyLogic>(1, "res://Scenes/Enemy.tscn", "Enemy");

// 异步显示
IEntity entity = await GF.Entity.ShowEntityAsync<EnemyLogic>(1, "res://Enemy.tscn", "Enemy");

// 父子挂载
GF.Entity.AttachEntity(2, 1);    // 实体 2 挂到实体 1 下
GF.Entity.DetachEntity(2);       // 解除

// 隐藏
GF.Entity.HideEntity(1);

// 通过 EntityLogic 类型获取实体
var logic = GF.Entity.GetEntity<EnemyLogic>(1);
```

### UI 系统
```csharp
// 打开界面
GF.UI.OpenUIForm<MainMenuForm>("res://MainMenu.tscn", "Normal");

// 关闭
GF.UI.CloseUIForm(serialId);
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
// 同步加载
PackedScene scene = GF.Resource.LoadAsset<PackedScene>("res://Scenes/Enemy.tscn");

// 异步加载
GF.Resource.LoadAssetAsync("res://Scenes/Enemy.tscn", typeof(PackedScene),
    asset => { /* 加载成功 */ },
    error => { /* 加载失败 */ });

// 文本/二进制文件读取
string text = GF.Resource.LoadText("res://Data/Config.txt");
byte[] data = GF.Resource.LoadBinary("res://Data/Config.dat");
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

### 本地化
```csharp
string title = GF.Localization.GetString("GameTitle");
```

### 日志
```csharp
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

主场景 `Framework/GameEntry.tscn` 注册为 `run/main_scene`：

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

## 🌟 开源项目推荐

| 项目 | 描述 | 链接 |
|------|------|------|
| **Game Framework** | 本项目的核心框架来源，Unity 游戏框架 | [GitHub](https://gameframework.cn/) |
| **Luban** | 游戏配置解决方案 | [GitHub](https://github.com/focus-creative-games/luban) |
| **Godot Engine** | 开源游戏引擎 | [GitHub](https://github.com/godotengine/godot) |
| **CodeGraph** | 代码知识图谱工具 | [GitHub](https://github.com/colbymchenry/codegraph) |

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
