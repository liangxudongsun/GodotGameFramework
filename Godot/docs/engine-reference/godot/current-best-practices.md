# Godot 4.6 — Current Best Practices

Best practices for Godot 4.6.x C# projects, covering new features and changes
since the LLM's May 2025 training cutoff.
Last verified: 2026-06-13

---

## Rendering

### Use D3D12 on Windows
D3D12 is now the **default** renderer for new Windows projects. Vulkan drivers
on Windows remain unreliable for many users. Your project already uses D3D12 —
keep it.

- **Project setting**: `rendering/rendering_device/driver.windows = d3d12`
- **Exception**: If targeting Wine/Proton on Linux, keep Vulkan

### Enable the Shader Baker
Pre-compiles shaders during export, eliminating shader compilation stutter.
Critical for mobile, VR, and any project where frame timing matters.

- **Export setting**: Enable "Shader Baker" in export presets
- **Performance**: Up to 20× faster shader loading on Metal/D3D12

### SSR Overhaul (4.6)
Screen Space Reflections were rewritten with Hi-Z tracing, full/half-resolution
modes, and better roughness handling. If you use SSR, revisit your quality
settings — you may get better visuals at the same cost.

### Octahedral Probes (4.6)
Reflection and radiance probes now use octahedral maps instead of cube maps,
reducing GPU memory. Prefer these over cube maps for new projects.

---

## Physics

### Jolt is the Default (4.6)
Jolt Physics is now the default 3D physics engine. It's battle-tested in AAA
titles (Horizon Forbidden West, Death Stranding 2). Your project already uses
Jolt — keep it.

- **Project setting**: `physics/3d/physics_engine = JoltPhysics3D`
- **Fall back to GodotPhysics** only if you encounter Jolt-specific issues

### Auto Collision Shapes (4.6)
Collision shapes can be auto-generated for 3D primitives (boxes, spheres,
cylinders, capsules). Use this for rapid prototyping but replace with manual
shapes for production.

---

## Animation

### Use the New IK Framework (4.6)
Inverse Kinematics is now built-in after being removed in 4.0. Includes:
- Deterministic two-bone and spline IK solvers
- Iterative solvers for complex chains
- Twist and angular velocity constraints
- Target snapping to 3D nodes

Prefer this over third-party IK solutions unless you need features not yet covered.

### AnimationPlayer StringName (4.6)
All animation name properties are now `StringName`, not `String`. In C#, use
`StringName` variables when interacting with `AnimationPlayer.current_animation`,
`assigned_animation`, `autoplay`, and `get_queue()`.

---

## Editor & Workflow

### Unique Node IDs (4.6)
Nodes now have stable unique IDs that survive scene reorganization and
refactoring. Use `GetNode(nodeId)` with the ID rather than path-based
`GetNode("../../../SomeNode")` for more robust scene references.

### Direct Keyboard Shortcut Binding (4.6)
Define shortcuts directly in Editor Settings — no more code-based workarounds.

### Select and Transform are Separate (4.6)
Select and Transform modes are now decoupled, preventing accidental moves.
This aligns with standard 3D software (Blender, Maya).

---

## Export & Platform

### Delta-Encoded PCK Patching (4.6)
For live games, enable delta-encoded PCK patches to ship only modified bytes.
Massive bandwidth savings for games with large assets that update frequently.

### Android: Target .NET 9
C# Android exports **must** target .NET 9 (as of 4.5+). Other platforms can
stay on .NET 8.

### Android: 16KB Page Alignment (4.6.2+)
Android 15+ (API 35+) requires 16KB page-aligned native libraries. AAB builds
may include Microsoft .NET CLR diagnostic libraries that are not aligned.
Workaround: add to `.csproj`:
```xml
<AndroidEnableProfiler>false</AndroidEnableProfiler>
```

### LibGodot (4.6)
Godot can now be embedded as a library in any application (Linux, Windows,
macOS initially). Useful for custom editors, hybrid apps, or embedding Godot
scenes in non-game applications.

---

## C# Specific

### Signal Delegates
Use `[Signal]` delegate pattern for type-safe signals:
```csharp
[Signal]
public delegate void HealthChangedEventHandler(float newHealth);
```

### Export Attributes
Use `[Export]` on public properties for editor exposure:
```csharp
[Export]
public float MoveSpeed { get; set; } = 300f;
```

### GLTF Type Safety (4.5+)
`GLTFAccessor` and `GLTFBufferView` now use `long` instead of `int` for
byte-level fields. Update any GLTF processing code accordingly.

### Editor-only Code
XR and editor plugin code that moved from Core to Editor namespace in 4.5
must be wrapped in `#if TOOLS` preprocessor directives.

---

## Project Settings to Verify

These defaults changed in 4.6 — verify they match your intent:

| Setting | Old Default | New Default (4.6) |
|---------|-------------|-------------------|
| `rendering/rendering_device/driver.windows` | vulkan | d3d12 |
| `physics/3d/physics_engine` | GodotPhysics3D | JoltPhysics3D |
| `rendering/environment/glow/glow_blend_mode` | 2 (Softlight) | 1 (Screen) |
| Editor theme | Default | Minimal |

Your project already uses D3D12 and Jolt — no changes needed.
