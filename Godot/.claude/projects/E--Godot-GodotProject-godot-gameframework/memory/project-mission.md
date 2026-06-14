---
name: project-mission
description: Core project goal — porting Unity GameFramework (Jiang Yin) to Godot 4.6 C#
metadata:
  type: project
---

The primary goal of this project is to port the [Game Framework](https://gameframework.cn/) — a production-grade C# game framework by Jiang Yin originally built for Unity — to Godot 4.6.2 C# (Mono).

The framework is being adapted to Godot's node/scene architecture and C# idioms while preserving the modular subsystem design (Event, FSM, Procedure, Resource, Entity, UI, Network, etc.).

The user is **not** starting from scratch on a game idea. The framework port is the active work. Any game built with this framework comes *after* the port is mature.

- The engine, language, and tooling were already decided (Godot 4.6 C#) when work began — the /setup-engine flow was informative but the choices were locked in.
- Framework code lives in `Plugins/GameFramework/` with modular subdirectories per subsystem.
- Runtime lives in `Scripts/` (RootNode, BaseNode, utility classes).
- Editor plugin at `addons/Editor/`.

**Why:** The Game Framework provides a battle-tested modular architecture that Unity developers are familiar with. Porting it to Godot brings this ecosystem to Godot C# projects.

**How to apply:** When discussing "what are we building" or "next steps," look at the framework port status first — not a game concept. Game concepts are downstream. /brainstorm would be about what game to build ON TOP of this framework, not the framework itself.
