# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GGF** (Godot Game Framework) — **Godot 4.6.2 + C# (.NET 8)** 移植自 [Game Framework](https://gameframework.cn/)（Jiang Yin）。模块化架构：事件、FSM、流程、资源、实体、UI、音频、本地化、对象池、数据表、设置等。

- `dotnet build` (quick) / `--build-solutions` (when adding .cs files)
- 默认日志编译开关：`ENABLE_LOG;ENABLE_INFO_AND_ABOVE_LOG`（见 `GodotProject.csproj` 的 `DefineConstants`）。`Log.*` 使用 `[Conditional]` 零开销移除
- 当前活跃游戏项目：`TheGame/`

## Repository Layout

Git 仓库根 `E:/Godot/GodotProject/GodotGameFramework`。当前工作目录为 `Godot/` 子目录。

```
../Configs/GameConfig/         ← Excel 配置源数据 + Luban 生成脚本
GodotProject/                  ← Godot 项目根 (project.godot)
  Framework/
    GameFramework/              ← 纯 C# 框架（无 Godot 依赖）
    GodotGameFrameworkCore/     ← Godot 运行时组件
      Lib/LubanLib/             ← Luban 反序列化运行时（ByteBuf, BeanBase）
    GameFramework.tscn          ← 主场景
  TheGame/                      ← 当前活跃游戏项目
    Audios/                     ← 音频资源 (.mp3)
    DataTables/
      GameConfigs/              ← Luban 生成的二进制数据 (.bytes)
      Localizations/            ← 本地化文本 (.txt)
    Entitys/                    ← 实体场景 (.tscn)
    GameScripts/GameProto/      ← Luban 生成的 C# 数据类
    Scenes/ Sprites/ UIs/
  addons/                       ← 编辑器插件 (LocalizationEditor, Resources, TopMenu)
```

## Build

```bash
cd GodotProject
dotnet build                              # 快速编译（日常开发）
"<godot_exe>" --build-solutions --path . --no-window -q   # 添加 .cs 文件后需执行
"<godot_exe>" --path . --editor                             # 打开编辑器
```

Godot 编辑器路径：`E:\Godot\Godot_v4.6.2-stable_mono_win64\...\Godot_v4.6.2-stable_mono_win64.exe`

未配置测试框架。

## Scene Tree

主场景 `Framework/GameFramework.tscn`（uid `bggentry001`），注册为 `run/main_scene`。`GameFramework` 根节点（`GameEntry` 脚本）驱动 `GameFrameworkEntry.Update()`，所有子节点均为 `GameFrameworkComponent` 派生脚本：

```
GameFramework (GameEntry)
├── Base / Resource / Event / Fsm / Procedure
├── Setting / DataTable / DataNode
├── ObjectPool / Entity / UI / Sound / Localization
```

每种组件类型只允许注册一个实例。所有组件列表见 `GF.cs` 静态门面。

### 启动流程

1. Godot 引擎加载 `Framework/GameFramework.tscn` 场景
2. `GameEntry.OnInit()` 中自动注册所有 `GameFrameworkComponent` 子节点
3. `GameEntry._Process()` 每帧驱动 `GameFrameworkEntry.Update(elapseSeconds, realElapseSeconds)`，轮询各个 `GameFrameworkModule`
4. `GameEntry.CheckProcedure()` 检测 `ProcedureComponent` 是否完成注册，完成后自动调用 `StartProcedure()` 启动入口流程

## 双层架构

### Layer 1: Pure C# Framework (`Framework/GameFramework/`)

Namespace `GameFramework`. **No Godot dependency.**
- `GameFrameworkEntry` — 模块入口，按优先级链表管理 `GameFrameworkModule`，按命名约定延迟创建（`IEventManager` → `EventManager`）
- `GameFrameworkModule` — 模块基类：`Priority`、`Update(float, float)`、`Shutdown()`
- `ReferencePool` / `EventPool<T>` / `TaskPool<T>` — 引用池、事件调度器、优先级任务池
- `GameFrameworkLinkedList<T>` / `GameFrameworkMultiDictionary` — 自定义集合
- `GameFrameworkLog` — 纯 C# 日志（`Debug`/`Info`/`Warning`/`Error`/`Fatal`），通过 `ILogHelper` 接口输出

### Layer 2: Godot Runtime (`Framework/GodotGameFrameworkCore/`)

命名空间 `GodotGameFramework`（含 `.Entity`、`.UI` 等子命名空间）。

- **`GodotComponent`** — `Node` 基类，映射所有 Godot 生命周期为虚方法。顺序：`_EnterTree` → `OnInit()` → `_Ready` → `OnEnter()` → `_Process` → `OnUpdate(float delta)`。还包括 `OnPhysicsUpdate`/`OnExitTree` + 输入系统（`OnInput`/`OnUnhandledInput`/`OnUnhandledKeyInput`/`OnShortcutInput`）+ 通知（`OnPostEnterTree`/`OnParented`/`OnPaused` 等）+ 属性系统。提供静态工具：`GodotComponent.Create<T>(parent)` / `GodotComponent.Destroy(node)`
- **`GameFrameworkComponent`** — 继承 `GodotComponent`，`OnInit()` 中自动注册到 `GameEntry`
- **`GameEntry`** — 根节点脚本，静态组件注册表，`_Process` 驱动 `GameFrameworkEntry.Update(elapseSeconds, realElapseSeconds)`。`Shutdown(ShutdownType)` 支持：`ShutdownType.None`（仅清理框架）/ `ShutdownType.Restart`（重载场景）/ `ShutdownType.Quit`（退出进程）
- **`GF`** — 静态门面，通过 `GameEntry.GetComponent<T>()` 获取各组件。可用的组件：`Event` / `Fsm` / `Procedure` / `ObjectPool` / `DataNode` / `Resource` / `Entity` / `UI` / `Sound` / `DataTable` (返回 `Tables`) / `Localization` / `Setting` / `Base`。`GF.Entity.ShowEntity<T>()`、`GF.UI.OpenUIForm()`、`GF.Sound.PlaySound()`、`GF.Event.Fire()` 等
- **`BaseComponent`** — 基础组件，管理帧率 (`Engine.MaxFps`)、游戏速度 (`Engine.TimeScale`)、暂停/恢复。初始化时按顺序创建 TextHelper → VersionHelper → LogHelper。提供 `BaseComponent.EditorResourceMode`：编辑器中使用 `EditorResourceManager` 直接加载资源（跳过发行资源版本管线），方便开发调试
- **`Log`** — `[Conditional]` 门面，委托到 `GameFrameworkLog`。细粒度编译控制：
  - `Log.Debug()`：需要 `ENABLE_LOG` / `ENABLE_DEBUG_LOG` / `ENABLE_DEBUG_AND_ABOVE_LOG`
  - `Log.Info()`：需要 `ENABLE_LOG` / `ENABLE_INFO_LOG` / `ENABLE_DEBUG_AND_ABOVE_LOG` / `ENABLE_INFO_AND_ABOVE_LOG`
  - `Log.Warning()`：需要 `ENABLE_LOG` / `ENABLE_WARNING_LOG` / `ENABLE_DEBUG_AND_ABOVE_LOG` / `ENABLE_INFO_AND_ABOVE_LOG` / `ENABLE_WARNING_AND_ABOVE_LOG`
  - `Log.Error()` / `Log.Fatal()` 同理。顶级 `ENABLE_LOG` 启用全部日志，默认开启 `ENABLE_INFO_AND_ABOVE_LOG`

## Key Patterns

- **组件委托模式**：Godot 组件（`UIComponent`/`EntityComponent`/`SoundComponent` 等）通过 `GameFrameworkEntry.GetModule<T>()` 获取纯 C# 层 Manager，所有操作委托给 Manager。Godot 组件主要负责：Inspector 可配置参数、事件转发到 `EventComponent`、Helper 创建和场景树管理
- **Helper 基类模式**：每个组件系统定义 `XXHelperBase : GodotComponent, IXXHelper` 抽象基类，通过 `Helper.CreateHelper()` 创建。已有：`UIFormHelperBase`/`UIGroupHelperBase`、`EntityHelperBase`/`EntityGroupHelperBase`、`SoundHelperBase`/`SoundGroupHelperBase`/`SoundAgentHelperBase`。Helper 类型名称在 Inspector 中通过 `Export` 字符串属性配置，支持运行时替换
- **ReferencePool（引用池）**：位于 `GameFramework/Base/ReferencePool/`，提供 `T Acquire<T>()` 和 `Release<T>(T)` 来复用对象，减少 GC。所有事件参数（`GameEventArgs` 子类）和常用数据结构都应实现 `IReference` 接口并使用引用池管理
- **Variable（变量系统）**：`GameFramework/Base/Variable/` 提供 `Variable<T>` 泛型类，用于在 FSM/Procedure 等模块间传递类型安全的值。基类 `Variable` 提供 `GetValue()`/`SetValue()`，支持通过引用池复用
- **事件参数模式**：自定义事件参数需继承 `GameEventArgs`（实现 `IReference`），提供 `Create()` 静态工厂方法从引用池获取实例，`Clear()` 方法重置状态。事件 ID 通过 `typeof(T).GetHashCode()` 生成
- **组件初始化顺序**：`OnInit()`（对应 `_Ready` 阶段，子→父顺序）是所有组件初始化逻辑的入口。组件间依赖通过 `GameEntry.GetComponent<T>()` 解决，因此 `OnInit` 中可安全获取其他已注册组件
- **EditorResourceMode**：编辑器开发时，`BaseComponent.EditorResourceMode` 为 true，框架使用 `EditorResourceManager` 直接加载 Godot 资源文件（绕过资源版本管理管线）。发布时设为 false 以启用完整资源管线
- **新增组件流程**：继承 `GameFrameworkComponent` → 挂到 `GameFramework.tscn` 根节点下 → 在 `GF.cs` 加静态属性
- **GameFolderConstant**：资源路径常量 `res://TheGame/...`，含格式模板（如 `AUDIO = "res://TheGame/Audios/{0}.{1}"`、`ENTITY = "res://TheGame/DataTables/Entitiys/{0}.tscn"`、`DATATABLE = "res://TheGame/DataTables/GameConfigs/{0}.bytes"`），手动维护

## Editor Plugins

**Project > Tools** 菜单下：
- **TopMenu** — GameFramework 菜单，切换日志级别（修改 csproj 的 DefineConstants）
- **LocalizationEditor** — `../Configs/Localization/*.xlsx` → `res://TheGame/DataTables/Localizations/*.txt`
- **ResourcesCollection** — 扫描 `res://TheGame/` 非脚本资源，生成 `ResourcesCollectionConstant.cs`

## Luban 数据管线

Excel 源文件 `../Configs/GameConfig/Datas/`（当前有：`__beans__.xlsx`、`__enums__.xlsx`、`__tables__.xlsx`、实体、界面UI 等）。运行 `../Configs/GameConfig/gen_code_bin_to_project.bat` 生成 C# 到 `TheGame/GameScripts/GameProto/GameConfig/`，`.bytes` 到 `TheGame/DataTables/GameConfigs/`。运行时由 `LubanLib/ByteBuf` + `BeanBase` 反序列化。

## Claude Code Configuration

- **MCP**: CodeGraph (`@colbymchenry/codegraph`) 已配置在 `.mcp.json`
- **Hooks**: `SessionStart`、`PreToolUse`（Bash 验证）、`PostToolUse`（Write/Edit 验证）、`Notification`、`PreCompact`/`PostCompact`、`Stop`、`SubagentStart`/`SubagentStop`
