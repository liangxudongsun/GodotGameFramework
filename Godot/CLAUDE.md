# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GGF** (Godot Game Framework) — **Godot 4.6.2 + C# (.NET 8)** port of [Game Framework](https://gameframework.cn/) (Jiang Yin). Modular architecture: Event, FSM, Procedure, Resource, Entity, UI, Audio, Localization, ObjectPool, DataTable, Setting.

- Build: `cd GodotProject && dotnet build`
- Add .cs files: `"<godot_exe>" --build-solutions --path . --no-window -q`
- Godot editor: `"<godot_exe>" --path . --editor` (path: `E:\Godot\Godot_v4.6.2-stable_mono_win64\...\Godot_v4.6.2-stable_mono_win64.exe`)
- Active game project: `TheGame/`
- No test framework detected — game is runtime-only (no test files found)

## Dual-Layer Architecture

The framework has a strict **two-layer separation** that mirrors the original Game Framework design:

```
GameFramework/                    ← Pure C# modules (zero Godot dependency)
  Base/                           ← GameFrameworkEntry, GameFrameworkModule, ReferencePool, EventPool
  Fsm/                            ← State machine system
  Procedure/                      ← Procedure (game state) manager
  Entity/ UI/ Sound/ Scene/       ← Manager interfaces + logic (no Godot types)
  DataNode/ DataTable/ ObjectPool/
  Resource/                       ← IResourceManager interface
  Utility/                        ← Text, compression, random, etc.

GodotGameFrameworkCore/           ← Godot runtime components
  Base/                           ← GF.cs facade, GameEntry (SceneTree root), GameFrameworkComponent
  Entity/ UI/ Sound/ Resource/    ← Godot bridge components (each delegates to the corresponding Manager)
  SingletonSystem/                ← SingletonNode<T> pattern
  Utility/                        ← PhysicsCheck2D etc.
```

**Key rule:** `GameFramework/` knows nothing about Godot. `GodotGameFrameworkCore/` depends on both `GameFramework/` and Godot. This means any new system should have its interface/logic in `GameFramework/` and its Godot bridge in `GodotGameFrameworkCore/`.

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

`ControlUIForm : Control, IUIForm` is the base class. Auto-collects localization text nodes.

UI lifecycle: `OnInit` → `OnOpen` → `OnCover`/`OnReveal` → `OnUpdate` → `OnClose`.

Opening: `GF.UI.OpenUIForm(UIFormId.MenuForm)` or `await GF.UI.OpenUIFormAsync<T>(UIFormId.MenuForm)`.

TheGame UIs: `MenuForm`, `MainForm`, `GameOverForm`, `PauseMenuForm`, `TestOverlayForm`.

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

## Luban Config Pipeline

Excel configs in `../Configs/GameConfig/Datas/` (实体.xlsx, 界面UI.xlsx, 角色.xlsx, etc.) → Luban code generation:

```
../Configs/GameConfig/
  Datas/              ← Excel source files (*.xlsx)
  Defines/            ← Luban type definitions
  luban.conf          ← Luban configuration
  gen_code_bin_to_project.bat/sh  ← Generate C# code + binary data
```

Generated code: `TheGame/GameScripts/GameProto/GameConfig/` (e.g., `EntityConfig.cs`, `EntityId.cs`, `TbEntityConfig.cs`). Auto-generated `ResourcesCollectionConstant.cs` via the ResourcesCollection editor plugin.

Config-driven entities: `GF.Entity.ShowEntity(EntityId.Cat)` → `TbEntityConfig` resolves the scene path.

## Editor Plugins (`addons/`)

**Project > Tools** menu:
- **TopMenu** — toggle log level (rewrites csproj `DefineConstants`)
- **LocalizationEditor** — `../Configs/Localization/*.xlsx` → `.txt` localization files
- **ResourcesCollection** — scan `res://TheGame/` resources, generate `ResourcesCollectionConstant.cs`

## Logging System

Compile-time conditional via `DefineConstants`: `ENABLE_LOG;ENABLE_INFO_AND_ABOVE_LOG` in `GodotProject.csproj`.

Level granularity: `ENABLE_DEBUG_LOG / INFO / WARNING / ERROR / FATAL_LOG` and composite `ENABLE_DEBUG_AND_ABOVE_LOG` etc.

`Log.Debug/Info/Warning/Error/Fatal` are `[Conditional]` — zero runtime overhead when the symbol is undefined.

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
cd GodotProject
dotnet build                              # Daily development build
"<godot_exe>" --build-solutions --path . --no-window -q   # After adding .cs files
"<godot_exe>" --path . --editor                             # Open Godot editor
```

## MCP & Claude Code Config

- **MCP**: CodeGraph (`@colbymchenry/codegraph`) in `.mcp.json` — provides code intelligence via SQLite knowledge graph of all symbols/edges/files
- **Hooks**: SessionStart, PreToolUse (Bash validation), PostToolUse (Write/Edit validation), Notification, PreCompact/PostCompact, Stop, SubagentStart/SubagentStop
- **Agent definitions**: `.claude/agents/` — godot-csharp-specialist, godot-specialist, gameplay-programmer, etc. for targeted sub-tasks
