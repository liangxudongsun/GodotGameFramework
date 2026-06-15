# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GGF** (Godot Game Framework) — **Godot 4.6.2 + C# (.NET 8)** 移植自 [Game Framework](https://gameframework.cn/)（Jiang Yin）。模块化架构：事件、FSM、流程、资源、实体、UI、音频、本地化、对象池、数据表、配置、设置等。

- `dotnet build` (quick) / `--build-solutions` (when adding .cs files)
- 日志由 `ENABLE_LOG` 编译开关控制，`Log.*` 使用 `[Conditional]` 零开销移除

## Repository Layout

The actual Godot project root is `GodotProject/` (contains `project.godot`).

```
Configs/                      ← Excel 配置源数据（Luban 管线输入）
docs/                         ← Godot 迁移 / 最佳实践文档
production/                   ← 元数据（stage, review-mode, session-logs）
GodotProject/                 ← Godot 项目根 (project.godot)
  Framework/
    GameFramework/             ← 纯 C# 框架（无 Godot 依赖）
    GodotGameFrameworkCore/    ← Godot 运行时组件
    GameEntry.tscn             ← 主场景
  AAAGame/                     ← 示例游戏
  TheGame/                     ← 当前活跃游戏项目
    DataTables/                ← Luban 生成的二进制数据
    GameScripts/GameProto/     ← Luban 生成的 C# 数据类
  addons/                      ← 编辑器插件
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

主场景 `Framework/GameEntry.tscn`，注册为 `run/main_scene`。`GameEntry`（根节点）驱动 `GameFrameworkEntry.Update()`，所有子节点均为 `GameFrameworkComponent` 派生脚本：

```
GameFramework (GameEntry)
├── Base / Resource / Event / Fsm / Procedure
├── Setting / Config / DataTable / DataNode
├── ObjectPool / Entity / UI / Sound / Localization
```

每种组件类型只允许注册一个实例。

## 双层架构

### Layer 1: Pure C# Framework (`Framework/GameFramework/`)

Namespace `GameFramework`. **No Godot dependency.** Port of the original Game Framework's C# core.
- `GameFrameworkEntry` — 模块入口，按优先级链表管理 `GameFrameworkModule`，按命名约定延迟创建（`IEventManager` → `EventManager`）
- `GameFrameworkModule` — 模块基类：`Priority`、`Update(float, float)`、`Shutdown()`
- `ReferencePool` / `EventPool<T>` / `TaskPool<T>` — 引用池、事件调度器、优先级任务池
- `GameFrameworkLinkedList<T>` / `GameFrameworkMultiDictionary` — 自定义集合

### Layer 2: Godot Runtime (`Framework/GodotGameFrameworkCore/`)

命名空间 `GodotGameFramework`（含 `.Entity`、`.UI` 等子命名空间）。桥接框架模块到 Godot 节点生命周期。
- **`GodotComponent`** — `Node` 基类，将所有 Godot 生命周期映射为虚方法（`OnInit`/`OnEnter`/`OnUpdate`/`OnPhysicsUpdate`/`OnExitTree` + 输入系统、通知、属性系统）
- **`GameFrameworkComponent`** → 继承 `GodotComponent`，`OnInit()` 中自动注册到 `GameEntry`
- **`GameEntry`** — 根节点，静态组件注册表，`_Process` 驱动 `GameFrameworkEntry.Update()`
- **`GF`** — 静态门面：`GF.Base`、`GF.Event`、`GF.Entity`、`GF.UI`、`GF.Sound` 等
- **`Log`** — `[Conditional("ENABLE_LOG")]` 门面，零开销移除

## 框架组件

Godot 组件均通过 `GameFrameworkEntry.GetModule<T>()` 获取核心 Manager 并委托操作：

| 组件 | 委托的 Manager |
|---|---|
| `BaseComponent` | 引擎设置（帧率/速度/Helper 初始化） |
| `EventComponent` | `IEventManager` |
| `FsmComponent` | `IFsmManager` |
| `ProcedureComponent` | `IProcedureManager` |
| `ResourceComponent` | `IResourceManager` |
| `EntityComponent` | `IEntityManager` |
| `UIComponent` | `IUIManager` |
| `SoundComponent` | `ISoundManager` |
| `ConfigComponent` | `IConfigManager` |
| `DataTableComponent` | `IDataTableManager` |
| `DataNodeComponent` | `IDataNodeManager` |
| `ObjectPoolComponent` | `IObjectPoolManager` |
| `SettingComponent` | `ISettingManager` |
| `LocalizationComponent` | `ILocalizationManager` |

## Key Patterns

- **组件委托模式**：`UIComponent`/`EntityComponent`/`SoundComponent` 等 Godot 组件持有核心 Manager 引用（`IUIManager`/`IEntityManager`/`ISoundManager`），所有操作委托给核心 Manager，不重复实现内部状态
- **Helper 基类模式**：每个组件系统定义 `XXHelperBase : GodotComponent, IXXHelper` 抽象基类，支持通过 `Helper.CreateHelper()` 创建和自定义。已有：`UIFormHelperBase`/`UIGroupHelperBase`、`EntityHelperBase`/`EntityGroupHelperBase`、`SoundHelperBase`/`SoundGroupHelperBase`/`SoundAgentHelperBase`
- **GF 门面**：`GF.Entity.ShowEntity<T>()`、`GF.UI.OpenUIForm()`、`GF.Sound.PlayMusic()`、`GF.Event.Fire()`、`GF.Localization.GetString()`、`GF.DataNode.SetData()`
- **GodotComponent**：自定义节点继承此类获得完整的生命周期虚方法（`OnInit`/`OnEnter`/`OnUpdate`/`OnPhysicsUpdate`/`OnExitTree` + 输入系统 + 通知 + 属性系统）
- **Entity**：继承 `GodotComponent`，既有 `IEntity` 框架生命周期也有 Godot 节点生命周期
- **Log**：`Log.Info()`/`Log.Warning()`/`Log.Error()`，`[Conditional("ENABLE_LOG")]` 编译时移除，可在编辑器中通过 GameFramework → TopMenu 切换日志级别
- **新增组件**：继承 `GameFrameworkComponent` → 挂到 `GameEntry.tscn` 根节点下 → 在 `GF.cs` 加静态属性

## Editor Plugins

Godot 编辑器 **Project > Tools** 菜单下有三个工具：
- **TopMenu** — GameFramework 菜单，切换日志级别（修改 csproj 的 DefineConstants）
- **LocalizationEditor** — `Configs/Localization/*.xlsx` → `res://TheGame/DataTables/Localizations/*.txt`
- **ResourcesCollection** — 扫描 `res://TheGame/` 非脚本资源，生成 `ResourcesCollectionConstant.cs`（文件路径常量）

## Luban 数据管线

Excel 配置源文件在 `Configs/GameConfig/Datas/`，运行 `gen_code_bin_to_project.bat` 生成 C# 数据类到 `TheGame/GameScripts/GameProto/GameConfig/`，二进制 `.bytes` 到 `TheGame/DataTables/`。运行时由 `LubanLib/ByteBuf` + `BeanBase` 反序列化。

## 示例/测试项目

- **AAAGame** — 框架子系统的演示示例（Procedure / Entity / UI / Event / DataTable）
- **TheGame** — 当前活跃游戏项目，使用 Luban 数据管线

## Production Metadata

- **Stage**: `Technical Setup` — 技术搭建阶段
- **Review Mode**: `lean`

## MCP Tools

CodeGraph (`@colbymchenry/codegraph`) 已配置在 `.mcp.json`。对于架构/流程问题优先使用 `codegraph_context`（一次调用组合搜索 + 节点 + 调用者/被调用者）。
