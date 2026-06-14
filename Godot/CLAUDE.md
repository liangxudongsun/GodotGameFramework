# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**GGF** (Godot Game Framework) is a **Godot 4.6 C# (Mono)** port of the [Game Framework](https://gameframework.cn/) — a production-grade game framework by Jiang Yin. It provides a modular architecture with subsystems for events, FSMs, procedures, resources, entities, UI, networking, audio, localization, object pooling, data tables, config, settings, and more.

- **Engine**: Godot 4.6.2 (D3D12 default renderer, Jolt Physics default)
- **Assembly**: `GGF` (`GGF.csproj`, `GGF.sln`)
- **Language**: C# (.NET 8+), `AllowUnsafeBlocks` enabled for `Utility.Converter.cs`
- **Build System**: `dotnet build` via `Godot.NET.Sdk/4.6.2`
- **Logging**: Gated by `ENABLE_LOG` define constant in `.csproj` — all `Log.*` calls use `[Conditional]` attributes

See `docs/engine-reference/godot/VERSION.md` for Godot 4.6.2 migration notes.

## Repository Layout

The actual Godot project root is `GodotProject/` (contains `project.godot`).

```
.gitignore / .gitattributes        ← repo root
CLAUDE.md
.mcp.json                          ← MCP server config (CodeGraph)
docs/
  engine-reference/godot/          ← Godot migration & best-practice docs
production/                        ← meta (stage, review-mode, session-logs)
GodotProject/                      ← Godot project root
  project.godot                    ← Engine config (D3D12, Jolt Physics)
  GGF.csproj                       ← .NET SDK project (Godot.NET.Sdk/4.6.2)
  GGF.sln
  icon.svg
  Framework/
    GameFramework/                  ← Pure C# framework (no Godot dependency)
      Base/                         ← Entry, modules, event pool, ref pool, task pool
      Config/ DataNode/ DataTable/ DataProvider/
      Debugger/ Download/ Entity/ Event/
      FileSystem/ Fsm/ Localization/
      Network/ ObjectPool/ Procedure/
      Properties/                   ← AssemblyInfo.cs
      Resource/ Scene/ Setting/
      Sound/ UI/ Utility/
      WebRequest/
    GodotGameFramework/             ← Godot-specific runtime components
      Base/                         ← GameEntry, GF facade, BaseComponent, GodotComponent, Log
      Config/ DataNode/ DataTable/
      Entity/ Event/ Fsm/
      Localization/ ObjectPool/
      Procedure/ Resource/ Setting/
      Sound/ UI/ Utility/
      Variable/
    GameEntry.tscn                  ← Main scene (uid://bggentry001)
  AAAGame/                          ← Example/test game: entities, UI, procedures, events
    Audio/ DataTable/ Entity/ Event/
    ObjectPool/ Procedure/ UI/
  Data/
    Localization/                   ← Localization data files
```

## Build & Run

The Godot editor is at `E:\Godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe`

```bash
# Build solutions (must run from GodotProject/)
cd GodotProject
"E:\Godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe" --build-solutions --path . --no-window -q

# Open in editor
"E:\Godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe" --path GodotProject --editor

# Build from repo root
"E:\Godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe" --build-solutions --path GodotProject --no-window -q
```

No test framework is set up yet. No editor plugin directory (`addons/`) exists.

## Scene Tree

The main scene is `Framework/GameEntry.tscn` (uid `bggentry001`, registered as `run/main_scene` in `project.godot`).

```
GameFramework (GameEntry.cs)         ← root, drives _Process → GameFrameworkEntry.Update()
├── Base (BaseComponent.cs)          ← initializes helpers, frame rate, game speed
├── Resource (ResourceComponent.cs)
├── Event (EventComponent.cs)
├── Fsm (FsmComponent.cs)
├── Procedure (ProcedureComponent.cs) ← configures procedures via exported PackedStringArray
├── Setting (SettingComponent.cs)
├── Config (ConfigComponent.cs)
├── DataTable (DataTableComponent.cs)
├── DataNode (DataNodeComponent.cs)
├── ObjectPool (ObjectPoolComponent.cs)
├── Entity (EntityComponent.cs)
├── UI (UIComponent.cs)              ← pre-configured with 4 UIGroups: Background/Normal/Popup/Tips
├── Sound (SoundComponent.cs)
└── Localization (LocalizationComponent.cs)
```

All children are `Node` with `GameFrameworkComponent`-derived scripts attached. The `Base` node is the system entry point.

## Two-Layer Architecture

The project cleanly separates **pure framework logic** from **Godot runtime binding**:

### Layer 1: Pure C# Framework (`Framework/GameFramework/`)

Namespace: `GameFramework`. **No Godot dependency.** Mirror of the original Game Framework's C# core.

- `GameFrameworkEntry` — Static entry. Manages `GameFrameworkModule` instances in a priority-ordered linked list. Modules created lazily by naming convention: `IEventManager` → `GameFramework.Event.EventManager` (strip `I` prefix, same namespace).
- `GameFrameworkModule` — Abstract base with `Priority`, `Update(float, float)`, `Shutdown()`.
- `GameFrameworkLog` — Log facade (not conditional).
- `EventPool<T>` — Internal event dispatcher with configurable modes (allow no/multi/duplicate handlers). Events are `IReference`-pooled.
- `ReferencePool` — `IReference` pooling. `Acquire<T>()` / `Release()`.
- `TaskPool<T>` — Priority-ordered task pool with free/working agent stacks.
- `GameFrameworkLinkedList<T>`, `GameFrameworkMultiDictionary<TKey, TValue>` — Custom collections.

### Layer 2: Godot Runtime (`Framework/GodotGameFramework/`)

Namespace: `GodotGameFramework`. Bridges framework modules to Godot node lifecycle.

- **`GodotComponent`** (`Base/GodotComponent.cs`) — Base class extending `Node`. Maps all Godot lifecycle callbacks to virtual methods (`OnInit`, `OnEnter`, `OnUpdate`, `OnPhysicsUpdate`, `OnExitTree`, plus input system, notifications, property system). Replaces the old `BaseNode`.
- **`GameFrameworkComponent`** (`Base/GameFrameworkComponent.cs`) — Extends `GodotComponent`. Auto-registers with `GameEntry.RegisterComponent(this)` in `OnInit()`.
- **`GameEntry`** (`Base/GameEntry.cs`) — Root node. Static component registry (`GameFrameworkLinkedList<GameFrameworkComponent>`). Drives `GameFrameworkEntry.Update()` each frame via `_Process`. `Shutdown(ShutdownType)` for None/Restart/Quit.
- **`GF`** (`Base/GF.cs`) — Static typed facade. Provides `GF.Base`, `GF.Event`, `GF.Fsm`, `GF.Procedure`, `GF.Resource`, `GF.Entity`, `GF.UI`, `GF.Sound`, `GF.Config`, `GF.DataTable`, `GF.DataNode`, `GF.ObjectPool`, `GF.Localization`, `GF.Setting`.
- **`BaseComponent`** (`Base/BaseComponent.cs`) — Initializes helpers (Text, Version, Log), sets `Engine.MaxFps` / `Engine.TimeScale`, manages pause/resume.
- **`Log`** (`Base/Log.cs`) — Static facade wrapping `GameFrameworkLog`. All methods are `[Conditional("ENABLE_LOG")]` — zero overhead when not defined. Supports `Debug`/`Info`/`Warning`/`Error`/`Fatal` with generic overloads up to 16 type parameters.

## Framework Modules (Godot-side components)

Each module has a Godot component wrapping the pure framework manager via `GameFrameworkEntry.GetModule<T>()`:

| Component | File | Wraps |
|---|---|---|
| `BaseComponent` | `Base/BaseComponent.cs` | Helper init + engine settings |
| `EventComponent` | `Event/EventComponent.cs` | `IEventManager` |
| `FsmComponent` | `Fsm/FsmComponent.cs` | `IFsmManager` |
| `ProcedureComponent` | `Procedure/ProcedureComponent.cs` | `IProcedureManager` |
| `ResourceComponent` | `Resource/ResourceComponent.cs` | `IResourceManager` |
| `EntityComponent` | `Entity/EntityComponent.cs` | `IEntityManager` |
| `UIComponent` | `UI/UIComponent.cs` | `IUIManager` |
| `SoundComponent` | `Sound/SoundComponent.cs` | `ISoundManager` |
| `ConfigComponent` | `Config/ConfigComponent.cs` | `IConfigManager` |
| `DataTableComponent` | `DataTable/DataTableComponent.cs` | `IDataTableManager` |
| `DataNodeComponent` | `DataNode/DataNodeComponent.cs` | `IDataNodeManager` |
| `ObjectPoolComponent` | `ObjectPool/ObjectPoolComponent.cs` | `IObjectPoolManager` |
| `SettingComponent` | `Setting/SettingComponent.cs` | `ISettingManager` |
| `LocalizationComponent` | `Localization/LocalizationComponent.cs` | `ILocalizationManager` |

Helper base classes and default implementations exist for Resource, Entity, UI, Sound, Setting, Config, Localization, and DataTable.

## Key Coding Patterns

### Using framework modules
```csharp
IEventManager eventManager = GameFrameworkEntry.GetModule<IEventManager>();
eventManager.Subscribe(SomeEventArgs.EventId, OnSomeEvent);
```

### Accessing runtime components via GF
```csharp
GF.Base.PauseGame();
GF.Entity.ShowEntity<EnemyLogic>(1, "res://Enemy.tscn", "EnemyGroup");
GF.UI.OpenUIForm<MainMenuForm>("res://MainMenu.tscn", "Normal");
GF.Sound.PlayMusic("res://bgm.ogg");
GF.Event.Fire(this, scoreChangedArgs);
GF.Localization.GetString("GameTitle");
GF.DataNode.SetData("Player/Score", new VarInt32(100));
```

### Custom Godot node with lifecycle
Extend `GodotComponent` for game logic nodes:
```csharp
public partial class MyNode : GodotComponent
{
    public override void OnInit() { /* _Ready — register, init */ }
    public override void OnEnter() { /* _EnterTree */ }
    public override void OnUpdate(double delta) { /* _Process — per frame */ }
    public override void OnPhysicsUpdate(double delta) { /* _PhysicsProcess */ }
    public override void OnExitTree() { /* cleanup */ }
}
```

`GodotComponent` also provides `OnInput`, `OnUnhandledInput`, `OnUnhandledKeyInput`, `OnShortcutInput`, scene-tree notification hooks (`OnPostEnterTree`, `OnParented`, `OnRenamed`, `OnPreDestory`, etc.), and editor property system overrides.

### Logging
```csharp
Log.Info("Player {0} scored {1} points", playerName, score);
Log.Warning("Health low: {0}", currentHp);
Log.Error("Failed to load: {0}", path);
```
All `[Conditional("ENABLE_LOG")]` — compile-time removed without the define.

### Adding a new runtime component
1. Create a class extending `GameFrameworkComponent` in `Framework/GodotGameFramework/<Module>/`
2. Add a child `Node` to the root in `GameEntry.tscn` and attach the script
3. Add a static property on `GF.cs` for typed access
4. The component auto-registers with `GameEntry` in its `OnInit()`

## Example/Test Game (`AAAGame/`)

Contains a working example using framework subsystems:
- **Procedures**: `TestLaunchProcedure` → `TestMenuProcedure` → `TestGameProcedure` (set in `ProcedureComponent`'s exported `AvailableProcedureTypeNames`)
- **Entities**: `BlockLogic`, `RedBlockLogic`, `ScoreBlockLogic` with corresponding `.tscn` files
- **UI Forms**: `MainMenuForm`, `GameHUDForm`, `GameOverForm`, `PauseMenuForm`, `ScorePopupItem`, `TestOverlayForm`
- **Events**: `BlockClickedEventArgs`, `ScoreChangedEventArgs`, `TestPhaseChangedEventArgs`
- **Data Tables**: `TestItemData`, `BlockTypeData`
- **Object Pool**: `TestPoolObject`

## Production Metadata

- **Stage**: `Technical Setup` (from `production/stage.txt`)
- **Review Mode**: `lean` (from `production/review-mode.txt`)

## MCP Tools

CodeGraph (`@colbymchenry/codegraph`) is configured in `.mcp.json` as an MCP server for code intelligence. Use `codegraph_context` as the primary tool for architecture/flow questions — it composes search + node + callers + callees in one call.
