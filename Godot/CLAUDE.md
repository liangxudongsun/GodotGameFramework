# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GGF** (Godot Game Framework) — **Godot 4.6.2 + C# (.NET 8)** port of [Game Framework](https://gameframework.cn/) (Jiang Yin). Modular architecture: Event, FSM, Procedure, Resource, Entity, UI, Audio, Localization, ObjectPool, DataTable, Setting.

- **Godot .NET SDK**: `Godot.NET.Sdk/4.7.0` (NuGet)
- **Build**: `cd GodotProject && dotnet build`
- **Add .cs files**: `"<godot_exe>" --build-solutions --path GodotProject --no-window -q`
- **Open editor**: `"<godot_exe>" --path GodotProject --editor`
- **Godot path**: `E:\Godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe` (note: the exe is nested two directory levels deep — `Godot_v4.6.2-stable_mono_win64/Godot_v4.6.2-stable_mono_win64/`). Bash on Windows here needs forward slashes.
- **Active game project**: `TheGame/`
- **No test framework detected** — game is runtime-only (no test files found)
- **Rendering**: D3D12 (Forward Plus), **Physics**: Jolt Physics (3D), **Stretch**: canvas_items / expand

## Dual-Layer Architecture

The framework has a strict **two-layer separation** mirroring the original Game Framework design. **Key rule:** `GameFramework/` knows nothing about Godot. `GodotGameFrameworkCore/` depends on both `GameFramework/` and Godot — new systems put interface/logic in `GameFramework/` and Godot bridge in `GodotGameFrameworkCore/`.

```
GodotProject/
  Framework/
    GameFramework/                  ← Pure C# modules (zero Godot dependency)
      Base/                         ← GameFrameworkEntry, GameFrameworkModule, ReferencePool, EventPool
      Fsm/                          ← State machine system
      Procedure/                    ← Procedure (game state) manager
      Entity/ UI/ Sound/ Scene/     ← Manager interfaces + logic (no Godot types)
      DataNode/ DataTable/ ObjectPool/
      Resource/                     ← IResourceManager interface
      Config/ Debugger/ Download/   ← Config, debugger windows, download manager
      Event/ Localization/          ← Event manager, localization system
      Network/ WebRequest/          ← Network channels, HTTP requests
      Properties/ Utility/          ← Assembly info, text/compression utilities
    GodotGameFrameworkCore/         ← Godot runtime components
      Base/                         ← GF.cs facade, GameEntry, GameFrameworkComponent, GodotComponent
      Base/Node/2D/                 ← Abstract entity base classes (Node2D, CharacterBody2D, Rb2D, Sprite2D, Area2D)
      Base/Node/UI/                 ← ControlUIForm base class
      Entity/ UI/ Sound/ Scene/    ← Godot bridge components (each delegates to the corresponding Manager)
      Resource/                     ← ResourceComponent, async load tasks (LoadAssetTask, LoadBinaryTask)
      DataTable/ DataNode/ Setting/ Localization/
      Event/ Fsm/ Procedure/ ObjectPool/
      Config/ Variable/             ← GameFolderConstant, VarInt32/VarString/VarBoolean/VarSingle
      Json/                         ← Newtonsoft.Json helper (local .dll reference)
      Lib/LubanLib/                 ← Luban runtime (ByteBuf, BeanBase, StringUtil)
      SingletonSystem/              ← SingletonNode<T> pattern
      Utility/                      ← PhysicsCheck2D, NodeExtension, Log helper, Version helper
  TheGame/                          ← Active game project
    GameScripts/
      Entity/                       ← ActorEntity, CatEntity, AngerEntity, GanTanEntity
      UI/                           ← MenuForm, MainForm, GameOverForm, PauseMenuForm, TestOverlayForm
      Procedure/                    ← ProcedureLaunch, ProcedureGame
      Event/                        ← BlockClickedEventArgs, ScoreChangedEventArgs, TestPhaseChangedEventArgs
      Resources/                    ← EntityGroup, SoundGroup, UIGroup definitions
      GameProto/GameConfig/         ← Luban-generated C# (EntityConfig, TbEntityConfig, EntityId, etc.)
  addons/                           ← Editor plugins
    ComponentInsoector/             ← Custom Godot Inspector for framework components
    LocalizationEditor/             ← Excel → .txt localization export
    Resources/                      ← Resources collection scanner
    TopMenu/                        ← Log level toggler (rewrites csproj DefineConstants)
```

### Newtonsoft.Json

Referenced from a local .dll (not NuGet):
```xml
<Reference Include="Newtonsoft.Json">
  <HintPath>.\Framework\GodotGameFrameworkCore\Lib\Json\Newtonsoft.Json.dll</HintPath>
</Reference>
```

### Unsafe Code

`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` is enabled for pointer operations in `Utility.Converter.cs`.

## Scene Tree & Startup

Main scene: `Framework/GameFramework.tscn` (uid `bggentry001`), set as `run/main_scene`.

```
GameFramework (GameEntry : GodotComponent)
├── Base / Event / Resource / ResourceService
├── Procedure / Scene / Fsm
├── DataTable / DataNode / ObjectPool
├── Setting / Entity / UI / Sound / Localization
```

Each component type can only register one instance. Component list mirrors the `GF.cs` static facade.

### Startup Sequence

1. Godot loads `Framework/GameFramework.tscn`
2. `GameFrameworkComponent.OnInit()` auto-calls `GameEntry.RegisterComponent(this)`
3. `GameEntry._Process()` drives `GameFrameworkEntry.Update()` each frame, polling all `GameFrameworkModule`s
4. `GameEntry.CheckProcedure()` detects `ProcedureComponent` registration, then auto-calls `StartProcedure()` → enters `ProcedureLaunch`

### Component Lifecycle (GodotComponent)

```
OnInit()       → OnEnter()   → OnUpdate(delta)   → OnExit()   → OnShutdown()
 (constructor)   (ready)       (every frame)        (removed)    (destroyed)
```

`GameFrameworkComponent : GodotComponent` overrides `OnInit()` to self-register with `GameEntry`.

## GF Static Facade

`GodotGameFrameworkCore/Base/GF.cs` provides lazy-cached static access to all components:

```csharp
GF.Event / GF.Fsm / GF.Procedure
GF.Resource / GF.Entity / GF.UI / GF.Sound
GF.DataTable / GF.Localization / GF.Setting / GF.Base / GF.Scene
```

Each property calls `GameEntry.GetComponent<T>()` and caches the result.

## Key Patterns

### Entity System

Abstract base classes directly inherit Godot types + implement `IEntity`:
- `AbstractNode2DEntity : Node2D, IEntity`
- `AbstractCharacterBody2DEntity : CharacterBody2D, IEntity`
- `AbstractSprite2DEntity : Sprite2D, IEntity`
- `AbstractRb2DEntity : RigidBody2D, IEntity`
- `AbstractArea2DEntity : Area2D, IEntity`

All provide `OnInit/OnRecycle/OnShow/OnHide/OnUpdate` lifecycle matching the Game Framework entity lifecycle.

**TheGame project entity hierarchy:**
```
AbstractCharacterBody2DEntity
  └── ActorEntity              ← Has ActorData (Hp/MaxHp), EntityTeam, PhysicsCheck2D, Die()
       ├── CatEntity           ← Player cat: keyboard move, auto-aim, spawns GanTanEntity
       ├── AngerEntity         ← Enemy
       └── GanTanEntity        ← Projectile with BulletData (Direction, Speed, IsPlayerBullet)
```

Entity spawning via `GF.Entity.ShowEntity<T>(EntityId.Xxx)` or `ShowEntityAsync<T>(EntityId.Xxx, userData)` — config-driven from `TbEntityConfig`.

### UI System

`ControlUIForm : Control, IUIForm` is the base class. Auto-collects `UIStringLabelKey` localization text nodes.

UI lifecycle: `OnInit` → `OnOpen` → `OnCover`/`OnReveal` → `OnUpdate` → `OnClose`.

Opening: `GF.UI.OpenUIForm(UIFormId.MenuForm)` or `await GF.UI.OpenUIFormAsync<T>(UIFormId.MenuForm)`.

TheGame UIs: `MenuForm`, `MainForm`, `GameOverForm`, `PauseMenuForm`, `TestOverlayForm`.

`UIItemBase : Control` for reusable UI widgets (e.g., `ScorePopupItem`). Pooled via `UIItemInstanceObject`.

### Procedure (FSM) System

Procedures manage top-level game states. TheGame procedures:
- `ProcedureLaunch` — validates components, loads entity/UI/sound groups and localization, then transitions to `ProcedureGame`
- `ProcedureGame` — gameplay loop, opens `MenuForm` on entry

Change state: `ChangeState<T>(procedureOwner)`. Each procedure can have its own nested FSM for sub-states.

### Component Delegate Pattern

```
Godot Component (e.g., EntityComponent)
  → GameFrameworkEntry.GetModule<IEntityManager>()
  → Pure C# Manager (e.g., EntityManager)
  → Delegates all real work to the Manager
```

### SingletonNode<T>

`SingletonSystem/SingletonNode<T> : Node` — a generic singleton pattern for Godot nodes:
- `SingletonNode<T>.Instance` creates the node on first access if none exists in the scene tree
- `_Ready()` ensures only one instance survives (duplicates `QueueFree()`)

### PhysicsCheck2D

`Utility/PhysicsCheck2D : IReference` — wraps `PhysicsDirectSpaceState2D.IntersectShape` with object pooling (`ReferencePool`). Auto-excludes the target node, supports sorted results by distance, and debug drawing. Usage: `PhysicsCheck2D.Create(targetNode, shape, ...)`.

### Event System

Custom event args inherit `GameFrameworkEventArgs`. TheGame examples: `BlockClickedEventArgs`, `ScoreChangedEventArgs`, `TestPhaseChangedEventArgs`. Fire via `GF.Event.Fire(this, e)`.

### ReferencePool / Object Pool

`IReference` interface + `ReferencePool.Acquire<T>()`/`Release()` for lightweight object reuse. `ObjectPoolComponent` wraps `ObjectPoolManager` for pooled Godot objects.

## Component Inspector Addon

`addons/ComponentInsoector/` provides custom Godot Inspector plugins for the framework's component hierarchy. It registers `BaseComponentInspectorPlugin`, `ProcedureComponentInspectorPlugin`, `SceneComponentInspectorPlugin`, `SettingComponentInspectorPlugin`, `EntityComponentInspectorPlugin`, `UIComponentInspectorPlugin`, `SoundComponentInspectorPlugin`, `LocalizationComponentInspectorPlugin`, and `ScriptGenerateInspector` — each providing custom property editors, dropdowns, and debug info in the Godot editor inspector panel.

### UIForm Script Generation

`ScriptGenerateInspector` (an `EditorInspectorPlugin`) — shows a **"Generate Script"** button in the inspector for **any `Control` node** (`_CanHandle` returns `@object is Control`). It scaffolds a UIForm as a **split partial class** across two files in separate output directories:

- `<ClassName>.cs` (output: `OutPutPathGe`) — regenerated boilerplate: `[Export]` child-node fields, `IUIForm` properties, localization collector. **Always overwritten.**
- `<ClassName>.cs` (output: `OutPutPathLogic`) — user lifecycle code (`OnInit`/`OnOpen`/`OnClose`/…). **Only created if absent** (never clobbers edits).

> ⚠️ **Godot 要求文件名必须与类名一致**，否则 Inspector 无法识别 `[Export]` 字段。因此 Ge 和 Logic 输出在不同目录，文件名都是 `<类名>.cs`。

**Template placeholders:** `_NAMESPACE_` / `_PARENT_` / `_CLASSNAME_` / `_CHILDNODES_`

Templates: `Framework/GodotGameFrameworkCore/Templet/UIFormTemplet.txt` (Ge), `UIFormLogicTemplet.txt` (Logic).

**Config:** `TheGame/Resources/ScriptGenerateRes.tres` (`ScriptGenerateRes : Resource`):
| Field | Purpose | Default |
|-------|---------|---------|
| `NameSpace` | 生成的命名空间 | `"GameLogic"` |
| `OutPutPathGe` | Ge 脚本输出目录 | `"res://TheGame/"` |
| `OutPutPathLogic` | Logic 脚本输出目录 | `"res://TheGame/"` |
| `NodePrefix` | 子节点名称前缀（用于自动收集） | `"m_"` |

The plugin reads config **by property name** off the base `Resource` (not a typed cast) so it works even before the C# type is registered in the editor.

**子节点自动收集与赋值 (`ReadChildNodes`):**
- 递归遍历节点树，收集名称以 `NodePrefix`（默认 `m_`）开头的子节点
- 生成 `[Export] public Type NodeName;` 字段，替换模板中的 `_CHILDNODES_` 占位符
- `SetScript` 之后自动调用 `node.Set(child.Name, child)` 为每个 `[Export]` 字段赋值子节点引用
- 调用 `MarkSceneAsUnsaved()` 标记场景已修改

**生成流程:**
1. 写入 `.cs` 文件 → 刷新文件系统 → `fs.Scan()`
2. `GD.Load<CSharpScript>(gePath)` → `node.SetScript(script)` → 自动赋值子节点
3. 新生成的脚本需要一次构建（`dotnet build`）后才能编译并通过 Inspector 显示 `[Export]` 字段

## Luban Config Pipeline

Excel configs in `Configs/GameConfig/Datas/` → Luban code generation:

```
Configs/                            ← Repo root (sibling to Godot/)
  GameConfig/
    Datas/
      __beans__.xlsx                ← Shared type definitions
      __enums__.xlsx                ← Enum definitions
      __tables__.xlsx               ← Table/index definitions
      实体.xlsx                     ← Entity configs (scenes, paths, groups)
      界面UI.xlsx                   ← UI form configs
      角色.xlsx                     ← Character/actor configs
    Defines/                        ← Luban type definitions (XML)
    luban.conf                      ← Luban configuration
    gen_code_bin_to_project.bat/sh  ← Generate C# code + binary data
```

Generated code: `TheGame/GameScripts/GameProto/GameConfig/` (e.g., `EntityConfig.cs`, `EntityId.cs`, `TbEntityConfig.cs`). Auto-generated `ResourcesCollectionConstant.cs` via the Resources editor plugin.

Config-driven usage: `GF.Entity.ShowEntity(EntityId.Cat)` → `TbEntityConfig` resolves the scene path.

## Source Generators (Tools/)

`Tools/GameEventSourceGenerator/` at the repo root contains a C# Source Generator project:
- `GameEventAnalyzer/` — Roslyn analyzer for game events
- `SourceGenerator/` — Roslyn source generator (auto-generates event boilerplate)

## Editor Plugins (`addons/`)

| Plugin | Function |
|--------|----------|
| **ComponentInsoector** | Custom inspector plugins for framework components (Base, Procedure, Scene, Setting, Entity, UI, Sound, Localization) + UIForm script generator (`ScriptGenerateInspector`) with auto child-node collection and assignment |
| **TopMenu** | Toggle log level (rewrites csproj `DefineConstants`) |
| **LocalizationEditor** | `Configs/Localization/*.xlsx` → `.txt` localization files |
| **Resources** | Scan `res://TheGame/` resources, generate `ResourcesCollectionConstant.cs` |

Enabled in `project.godot`:
```
editor_plugins/enabled = [
  "res://addons/ComponentInsoector/plugin.cfg",
  "res://addons/LocalizationEditor/plugin.cfg",
  "res://addons/Resources/plugin.cfg",
  "res://addons/TopMenu/plugin.cfg"
]
```

## Logging System

Compile-time conditional via `DefineConstants` in `GodotProject.csproj`:
```xml
<DefineConstants>ENABLE_LOG;ENABLE_INFO_AND_ABOVE_LOG</DefineConstants>
```

Level granularity: `ENABLE_DEBUG_LOG / INFO / WARNING / ERROR / FATAL_LOG` and composite `ENABLE_DEBUG_AND_ABOVE_LOG` etc.

`Log.Debug/Info/Warning/Error/Fatal` are `[Conditional]` — zero runtime overhead when the symbol is undefined. Release builds can remove the entire `DefineConstants` line.

## Resource System (P0 Minimal)

`IResourceManager` with 8 members (reduced from ~97 Unity-era members):

| Mode | Status |
|------|--------|
| `ResourceMode.Package` | ✅ Active (Godot.ResourceLoader) |
| `Updatable` / `UpdatableWhilePlaying` | 📅 P2 (.pck hot-update) |

Two `TaskPool<T>` instances for async loading:
- `m_AssetTaskPool` (LoadAssetTask) — Godot.ResourceLoader.Load + callback
- `m_BinaryTaskPool` (LoadBinaryTask) — FileAccess + callback

Convenience on `ResourceComponent`: `LoadBinary()`, `LoadText()`, `LoadAsync<T>()`, `LoadSceneAsync()`.

## Build & Development Commands

```bash
# From the Godot/ directory:
cd GodotProject

dotnet build                              # Daily development build

# After adding new .cs files (regenerate solution):
"<godot_exe>" --build-solutions --path GodotProject --no-window -q

# Open Godot editor:
"<godot_exe>" --path GodotProject --editor
```

## MCP & Claude Code Config

- **MCP**: CodeGraph (`@colbymchenry/codegraph`) in `.mcp.json` — provides code intelligence via SQLite knowledge graph of all symbols/edges/files
- **Hooks**: SessionStart, PreToolUse (Bash validation), PostToolUse (Write/Edit validation), Notification, PreCompact/PostCompact, Stop, SubagentStart/SubagentStop
- **Agent definitions**: `.claude/agents/` — specialized agents (godot-csharp-specialist, godot-specialist, gameplay-programmer, etc.) for targeted sub-tasks
