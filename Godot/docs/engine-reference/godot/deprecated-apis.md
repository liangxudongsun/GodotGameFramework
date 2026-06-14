# Godot — Deprecated APIs (Post-Cutoff)

"Don't use X → Use Y" reference for Godot 4.4 through 4.6.2.
Last verified: 2026-06-13

---

## Removed APIs (do not use — compilation error)

| Removed API | Version | Replacement |
|-------------|---------|-------------|
| `EditorFileDialog.add_side_menu()` | 4.6 | Use `FileDialog` directly |
| `RenderingServer.instance_reset_physics_interpolation()` | 4.5 | No direct replacement |
| `RenderingServer.instance_set_interpolated()` | 4.5 | No direct replacement |

---

## Renamed APIs (old name → new name)

| Old Name | New Name | Version |
|----------|----------|---------|
| `JSONRPC.set_scope()` | `JSONRPC.set_method()` | 4.5 |
| `RenderingDevice.Features.Address` | `RenderingDevice.Features.BufferDeviceAddress` | 4.5 |
| `Node.get_rpc_config()` | `Node.get_node_rpc_config()` | 4.5 |
| `SpringBoneSimulator3D.BoneDirection` | `SkeletonModifier3D.BoneDirection` | 4.6 |
| `SpringBoneSimulator3D.RotationAxis` | `SkeletonModifier3D.RotationAxis` | 4.6 |

---

## Type Changes (same name, different type)

| API | Old Type | New Type | Version |
|-----|----------|----------|---------|
| `AnimationPlayer.assigned_animation` | `String` | `StringName` | 4.6 |
| `AnimationPlayer.autoplay` | `String` | `StringName` | 4.6 |
| `AnimationPlayer.current_animation` | `String` | `StringName` | 4.6 |
| `AnimationPlayer.get_queue()` return | `PackedStringArray` | `StringName[]` | 4.6 |
| `EditorExportPreset.get_script_export_mode()` return | `int` | `EditorExportPreset.ScriptExportMode` | 4.6 |
| `GLTFAccessor.byte_offset` | `int` | `long` | 4.5 |
| `GLTFAccessor.component_type` | `int` | `GLTFComponentType` (enum) | 4.5 |
| `GLTFAccessor.count` | `int` | `long` | 4.5 |
| `GLTFAccessor.sparse_count` | `int` | `long` | 4.5 |
| `GLTFAccessor.sparse_indices_byte_offset` | `int` | `long` | 4.5 |
| `GLTFAccessor.sparse_values_byte_offset` | `int` | `long` | 4.5 |
| `GLTFBufferView.byte_length` | `int` | `long` | 4.5 |
| `GLTFBufferView.byte_offset` | `int` | `long` | 4.5 |
| `GLTFBufferView.byte_stride` | `int` | `long` | 4.5 |
| `EditorExportPlatformExtension._get_option_icon` return | `ImageTexture` | `Texture2D` | 4.5 |
| OpenXR `extension` params | `OpenXRExtensionWrapperExtension` | `OpenXRExtensionWrapper` | 4.5 |

---

## Signature Changes (new/changed parameters)

| API | Change | Version |
|-----|--------|---------|
| `OpenXRExtensionWrapper._get_requested_extensions` | New `xr_version` parameter | 4.6 |
| `RichTextLabel.add_image()` | `size_in_percent` split into `width_in_percent` + `height_in_percent` | 4.5 |
| `RichTextLabel.update_image()` | `size_in_percent` split into `width_in_percent` + `height_in_percent` | 4.5 |
| `FileAccess.get_as_text()` | `skip_cr` parameter removed | 4.6 |
| Various `draw_*` methods (CanvasItem, Font, TextServer) | New optional `oversampling` parameter | 4.5 |

---

## Behavior Changes (same API, different result)

| API | Change | Version |
|-----|--------|---------|
| `Resource.duplicate(true)` | Now only duplicates internal resources; external sub-resources not duplicated | 4.5 |
| `TileMapLayer.get_coords_for_body_rid()` | Different results with default physics partitioning | 4.5 |
| `C# StringExtensions.PathJoin()` | Empty/separator-starting paths no longer add extra separators | 4.5 |
| `C# StringExtensions.GetExtension()` | No extension → empty string (was original string) | 4.5 |
| `C# Quaternion(Vector3, Vector3)` | Now correctly represents shortest arc | 4.5 |
| Jolt: Area3D-static body overlap | Always reported regardless of project setting | 4.5 |
| Android C# exports | Must target .NET 9 | 4.5 |
| `MeshInstance3D.skeleton` default path | `NodePath("..")` → `NodePath("")` | 4.6 |
