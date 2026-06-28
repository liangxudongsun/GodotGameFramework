# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GGF** (Godot Game Framework) — **Godot 4.6.2 + C# (.NET 8)** 移植自 [Game Framework](https://gameframework.cn/)（Jiang Yin）。模块化架构：事件、FSM、流程、资源、实体、UI、音频、本地化、对象池、数据表、设置等。

- `dotnet build` (quick) / `--build-solutions` (when adding .cs files)
- 默认日志编译开关：`ENABLE_LOG;ENABLE_INFO_AND_ABOVE_LOG`（见 `GodotProject.csproj` 的 `DefineConstants`）。`Log.*` 使用 `[Conditional]` 零开销移除
- `AllowUnsafeBlocks=true` — `Utility.Converter.cs` 使用指针操作
- 当前活跃游戏项目：`TheGame/`
- CodeGraph 知识图谱索引目录：`.codegraph/`（daemon.pid、索引文件等）

## Repository Layout

Git 仓库根 `E:/Godot/GodotProject/GodotGameFramework`。当前工作目录为 `Godot/` 子目录。

```
../Configs/GameConfig/         ← Excel 配置源数据 + Luban 生成脚本
GodotProject/                  ← Godot 项目根 (project.godot)
  Framework/
    GameFramework/              ← 纯 C# 框架（部分无 Godot 依赖，部分已 Godot 化）
      Base/DataProvider/       ← IResourceManager 数据加载消费者
      Resource/                ← IResourceManager 接口 + 回调类型定义
    GodotGameFrameworkCore/    ← Godot 运行时组件
      Resource/                ← ResourceManager（IResourceManager 实现）+ ResourceComponent
      Base/                    ← GF.cs 门面, GameEntry, BaseComponent
      Entity/ UI/ Sound/ Scene/ ← 各系统桥接组件
    GameFramework.tscn         ← 主场景（GameFramework 根节点）
  TheGame/                     ← 当前活跃游戏项目
    Audios/ DataTables/ Entitys/ GameScripts/ Scenes/ Sprites/ UIs/
  addons/                      ← 编辑器插件
```

## Scene Tree

主场景 `Framework/GameFramework.tscn`（uid `bggentry001`），注册为 `run/main_scene`：

```
GameFramework (GameEntry)
├── Base / Event / Resource / ResourceService
├── Procedure / Scene / Fsm
├── DataTable / DataNode / ObjectPool
├── Setting / Entity / UI / Sound / Localization
```

每种组件类型只允许注册一个实例。所有组件列表见 `GF.cs` 静态门面。

### 启动流程

1. Godot 加载 `Framework/GameFramework.tscn`
2. `GameEntry.OnInit()` 自动注册所有 `GameFrameworkComponent` 子节点
3. `GameEntry._Process()` 每帧驱动 `GameFrameworkEntry.Update()`，轮询 `GameFrameworkModule`
4. `GameEntry.CheckProcedure()` 检测 `ProcedureComponent` 注册完成后自动调用 `StartProcedure()`

## 资源系统（P0 精简版）

`IResourceManager` 从 97 个成员精简为 8 个，移除所有 Unity 管线遗留代码。

### 接口定义

```csharp
public interface IResourceManager
{
    ResourceMode ResourceMode { get; }
    void SetResourceMode(ResourceMode resourceMode);
    HasAssetResult HasAsset(string assetName);
    void LoadAsset(string assetName, int priority, LoadAssetCallbacks loadAssetCallbacks, object userData);
    void UnloadAsset(object asset);
    void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks, object userData);
    void LoadBinary(string binaryAssetName, LoadBinaryCallbacks loadBinaryCallbacks, object userData);
    int GetBinaryLength(string binaryAssetName);
    int LoadBinaryFromFileSystem(string binaryAssetName, byte[] buffer);
}
```

### 实现架构

```
ResourceManager : GameFrameworkModule, IResourceManager（GodotGameFrameworkCore/Resource/）
  ├─ m_AssetTaskPool（TaskPool<LoadAssetTask>）
  │   └─ LoadAssetAgent：同步 Godot.ResourceLoader.Load + 回调
  ├─ m_BinaryTaskPool（TaskPool<LoadBinaryTask>）
  │   └─ LoadBinaryAgent：同步 FileAccess + 回调
  └─ GameFrameworkEntry.GetModule<IResourceManager>() 自动注册

ResourceComponent : GameFrameworkComponent（GodotGameFrameworkCore/Resource/）
  ├─ 创建时通过 GameFrameworkEntry.GetModule<IResourceManager>() 获取 ResourceManager
  ├─ 便捷方法：LoadBinary() / LoadText() / LoadAsync<T>() / LoadSceneAsync()
  └─ ResourceMode 模式选择（Package / Updatable / UpdatableWhilePlaying）

桥接组件（Entity/Sound/Scene/UI Component）：
  └─ GameFrameworkEntry.GetModule<IResourceManager>() → SetResourceManager()
```

### 资源模式

| 模式 | 说明 | 状态 |
|------|------|------|
| `ResourceMode.Package` | 单机模式，Godot.ResourceLoader 直接加载 | ✅ 当前可用 |
| `ResourceMode.Updatable` | 预下载热更模式 | 📅 P2 规划（.pck） |
| `ResourceMode.UpdatableWhilePlaying` | 边玩边更 | 📅 P2 规划 |

### 删除的内容

| 删除项 | 原因 |
|--------|------|
| `IResourceManager` ~89 个冗余成员 | Unity 管线概念（序列化器、事件、资源组等） |
| `EditorResourceManager.cs` | 不再需要区分编辑器/运行时模式 |
| `EditorResourceMode` | Godot 编辑器与运行时加载行为相同 |
| 管线 29 个 partial 文件 | Unity 版本列表/文件系统/更新管线 |
| `GameFramework/FileSystem/`, `Download/` | Godot 无虚拟文件系统 |
| 各种序列化器/事件/管线回调类型 | 对应接口成员已移除 |

### TaskPool 使用

框架的 `TaskPool<T>` 提供优先级任务队列和并发管理。两套独立的 TaskPool：
- `m_AssetTaskPool`（<LoadAssetTask>）：1 个 agent，同步加载 Godot 资源
- `m_BinaryTaskPool`（<LoadBinaryTask>）：1 个 agent，同步读取文件

任务通过 `ReferencePool` 复用，`LoadAssetTask.Create()` / `ReferencePool.Release(task)`。

## GF 静态门面

`Base/GF.cs` 提供所有组件的静态入口：

```csharp
GF.Event / GF.Fsm / GF.Procedure
GF.Resource / GF.Entity / GF.UI / GF.Sound
GF.DataTable / GF.Localization / GF.Setting / GF.Base / GF.Scene
```

## Key Patterns

### 实体系统

抽象基类直接继承 Godot 原生类型 + `IEntity`：
- `AbstractNode2DEntity : Node2D, IEntity`
- `AbstractCharacterBody2DEntity : CharacterBody2D, IEntity`
- `AbstractSprite2DEntity : Sprite2D, IEntity`
- `AbstractRb2DEntity : RigidBody2D, IEntity`

### UI 系统

`ControlUIForm : Control, IUIForm` 为 UI 面板基类，自动处理本地化文本收集。

### 组件委托模式

Godot 组件 → `GameFrameworkEntry.GetModule<T>()` 获取纯 C# Manager → 委托给 Manager。

### Luban 配置驱动

- Entity：`GF.Entity.ShowEntity(EntityId.Cat)` → `TbEntityConfig` 查路径
- UI：`GF.UI.OpenUIForm(UIFormId.Menu)` → `TbUIFormConfig` 查路径
- `GF.Entity.ShowEntityAsync<CatEntity>(EntityId.Cat)`（返回实体实例）

### 初始化顺序

场景树中节点的顺序决定初始化顺序（父→子 `OnInit` / 子→父 `OnEnter`）。

## Editor Plugins

**Project > Tools** 菜单下：
- **TopMenu** — 切换日志级别（修改 csproj 的 DefineConstants）
- **LocalizationEditor** — `../Configs/Localization/*.xlsx` → `.txt`
- **ResourcesCollection** — 扫描 `res://TheGame/` 资源，生成 `ResourcesCollectionConstant.cs`

## Build

```bash
cd GodotProject
dotnet build                              # 日常开发
"<godot_exe>" --build-solutions --path . --no-window -q   # 添加 .cs 文件后执行
"<godot_exe>" --path . --editor                             # 打开编辑器
```

Godot 编辑器路径：`E:\Godot\Godot_v4.6.2-stable_mono_win64\...\Godot_v4.6.2-stable_mono_win64.exe`

## Claude Code Configuration

- **MCP**: CodeGraph (`@colbymchenry/codegraph`) 已配置在 `.mcp.json`
- **Hooks**: SessionStart, PreToolUse (Bash 验证), PostToolUse (Write/Edit 验证), Notification, PreCompact/PostCompact, Stop, SubagentStart/SubagentStop
