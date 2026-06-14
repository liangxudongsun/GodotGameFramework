# Godot — Breaking Changes (Post-Cutoff)

Covers breaking changes from Godot 4.4 through 4.6.2 that affect C# projects.
Last verified: 2026-06-13

---

## 4.5 → 4.6 Breaking Changes

### C# Source-Incompatible (must update code)

| Area | Change | Migration |
|------|--------|-----------|
| `AnimationPlayer.assigned_animation` | `String` → `StringName` | Change variable type to `StringName` |
| `AnimationPlayer.autoplay` | `String` → `StringName` | Change variable type to `StringName` |
| `AnimationPlayer.current_animation` | `String` → `StringName` | Change variable type to `StringName` |
| `AnimationPlayer.get_queue()` | Returns `PackedStringArray` → `StringName[]` | Update return type |
| `AnimationPlayer.current_animation_changed` signal | Parameter type `String` → `StringName` | Update signal handler signature |
| `EditorExportPreset.get_script_export_mode()` | Returns `int` → `EditorExportPreset.ScriptExportMode` | Cast to enum |
| `OpenXRExtensionWrapper._get_requested_extensions` | New `xr_version` parameter added | Add parameter to overrides |
| `EditorFileDialog.add_side_menu` | **Removed entirely** | Use `FileDialog` instead; stub provided |

### C# Binary-Incompatible (recompile required, no code changes)

| Area | Change |
|------|--------|
| `SpringBoneSimulator3D` | `BoneDirection` → `SkeletonModifier3D.BoneDirection`; `RotationAxis` → `SkeletonModifier3D.RotationAxis` |
| `StreamPeerTCP.get_status` | Moved to base class `StreamPeerSocket` |
| `EditorFileDialog` properties | `access`, `display_mode`, `file_mode` moved to `FileDialog` |
| `EditorFileDialog` signals | `dir_selected`, `filename_filter_changed`, `file_selected`, `files_selected` moved to `FileDialog` |

### Fully Compatible (Binary + Source)

- `FileAccess.create_temp()` — mode_flags type change (✔️✔️)
- `FileAccess.get_as_text()` — `skip_cr` param removed (✔️✔️)
- `Performance.add_custom_monitor()` — new optional param (✔️✔️)
- All `DisplayServer` changes (✔️✔️)
- All GUI changes: `Control`, `FileDialog`, `LineEdit`, `SplitContainer` (✔️✔️)
- `TCPServer` methods moved to `SocketServer` base (✔️✔️)

### Default Changes (new projects only — existing projects unaffected)

- **D3D12** is now the default Windows renderer (was Vulkan)
- **Jolt Physics** is now the default 3D physics engine (was GodotPhysics)
- **Editor theme**: new "Minimal" theme is default
- **Environment glow**: `glow_blend_mode` default changed from 2 → 1

---

## 4.4 → 4.5 Breaking Changes

### C# Source-Incompatible

| Area | Change | Migration |
|------|--------|-----------|
| `JSONRPC.set_scope` | Renamed to `set_method` | Use `set_method` |
| `RenderingDevice.Features.Address` | Renamed to `BufferDeviceAddress` | Use `BufferDeviceAddress` |
| `GLTFAccessor.byte_offset` | `int` → `long` | Change type to `long` |
| `GLTFAccessor.component_type` | `int` → `GLTFComponentType` | Cast to enum |
| `GLTFAccessor.count` | `int` → `long` | Change type to `long` |
| `GLTFAccessor.sparse_count` | `int` → `long` | Change type to `long` |
| `GLTFAccessor.sparse_indices_byte_offset` | `int` → `long` | Change type to `long` |
| `GLTFAccessor.sparse_values_byte_offset` | `int` → `long` | Change type to `long` |
| `GLTFBufferView.byte_length` | `int` → `long` | Change type to `long` |
| `GLTFBufferView.byte_offset` | `int` → `long` | Change type to `long` |
| `GLTFBufferView.byte_stride` | `int` → `long` | Change type to `long` |
| `RichTextLabel.add_image` / `update_image` | `size_in_percent` split → `width_in_percent` + `height_in_percent` | Use two separate params |
| `EditorExportPlatformExtension._get_option_icon` | Returns `ImageTexture` → `Texture2D` | Change return type |
| OpenXR API extensions | `OpenXRExtensionWrapperExtension` → `OpenXRExtensionWrapper` | Update type references |
| XR Binding/Profile editors | API types moved from Core → Editor; wrap in `#if TOOLS` | Add `#if TOOLS` guards |

### C# Binary-Incompatible

- `Node.get_rpc_config` → `get_node_rpc_config` (source-compatible for C#)
- `RenderingServer.instance_reset_physics_interpolation` — **removed**
- `RenderingServer.instance_set_interpolated` — **removed**

### Behavior Changes

- **TileMapLayer**: `get_coords_for_body_rid()` returns different values with default physics partitioning
- **Resource.duplicate(true)**: Deep copy now only duplicates internal resources; use `duplicate_deep(DEEP_DUPLICATE_ALL)` for old behavior
- **Jolt Physics**: `areas_detect_static_bodies` project setting removed; Area3D-static body overlap always reported
- **Android C# exports**: Must target **.NET 9** (other platforms can stay on .NET 8)
- **Quaternion(Vector3, Vector3)**: Now correctly represents shortest arc between two vectors

---

## 4.3 → 4.4 Breaking Changes

Minimal C# breaking changes. 4.4 was near the LLM training cutoff — most APIs
should be familiar to the model. Key additions include typed dictionaries,
improved C# signal support, and build system refinements.
