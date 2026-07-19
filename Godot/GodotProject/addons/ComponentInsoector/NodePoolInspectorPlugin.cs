#if TOOLS
using GameFramework;
using Godot;
using Godot.Collections;
using GodotGameFramework.NodePool;
using System;
using System.Collections.Generic;

namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class NodePoolInspectorPlugin : EditorInspectorPlugin
    {
        private const string ScanRoot = "res://TheGame/";

        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith($"NodePoolConfig.cs");
            }
            return false;
        }

        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            DrawInfoLabel(@object);
            DrawActionButtons(@object);
        }

        // ── UI ──

        private void DrawInfoLabel(GodotObject @object)
        {
            var entriesVar = @object.Get("Entries");
            int count = entriesVar.VariantType == Variant.Type.Array
                ? entriesVar.AsGodotArray<GodotObject>().Count
                : 0;

            var label = new Label();
            label.Text = $"池化场景: {count} 个";
            label.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.5f));
            AddCustomControl(label);
        }

        private void DrawActionButtons(GodotObject @object)
        {
            var hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 32);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var scanBtn = new Button();
            scanBtn.Text = "Scan IPoolable Scenes";
            scanBtn.TooltipText = "扫描 res://TheGame/ 下所有 .tscn，查找实现了 IPoolable 的场景";
            scanBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(scanBtn);

            var clearBtn = new Button();
            clearBtn.Text = "Clear";
            clearBtn.TooltipText = "清空所有条目";
            clearBtn.AddThemeColorOverride("font_color", Colors.OrangeRed);
            hbox.AddChild(clearBtn);

            AddCustomControl(hbox);

            scanBtn.Pressed += () => OnScanPressed(@object);
            clearBtn.Pressed += () =>
            {
                ShowConfirmDialog("确定清空所有条目？", () =>
                {
                    @object.Set("Entries", Variant.CreateFrom(new Array<GodotObject>()));
                    SaveConfig(@object);
                    GD.Print("[NodePoolInspector] 已清空。");
                });
            };
        }

        // ── 扫描 ──

        /// <summary>缓存：类名 → 类全名。从程序集中反射 IPoolable 类型，仅做一次。</summary>
        private static System.Collections.Generic.Dictionary<string, string> s_PoolableTypeMap;

        private void OnScanPressed(GodotObject @object)
        {
            GD.Print("[NodePoolInspector] 开始扫描 IPoolable 场景...");

            // 一次性反射所有 IPoolable 类型，构建 类名→全名 映射
            BuildPoolableTypeMap();

            if (s_PoolableTypeMap == null || s_PoolableTypeMap.Count == 0)
            {
                GD.PushWarning("[NodePoolInspector] 未找到任何实现 IPoolable 的 C# 类型。");
                return;
            }

            GD.Print($"[NodePoolInspector] 程序集中找到 {s_PoolableTypeMap.Count} 个 IPoolable 类型: {string.Join(", ", s_PoolableTypeMap.Keys)}");

            // 读取旧 Entries
            var oldEntries = ReadEntriesArray(@object);

            // 资源路径 → 旧 PoolEntry
            var existing = new System.Collections.Generic.Dictionary<string, GodotObject>();
            foreach (var entry in oldEntries)
            {
                if (entry == null) continue;
                var scene = ReadPoolEntryScene(entry);
                if (scene != null && !string.IsNullOrEmpty(scene.ResourcePath))
                    existing[scene.ResourcePath] = entry;
            }

            var scanned = new HashSet<string>();
            var newEntries = new Array<GodotObject>();
            var tscnFiles = new List<string>();
            NodeUtility.CollectionFilesByExtension(ScanRoot, ".tscn", tscnFiles);

            GD.Print($"[NodePoolInspector] 找到 {tscnFiles.Count} 个 .tscn 文件");

            int found = 0;
            foreach (var filePath in tscnFiles)
            {
                scanned.Add(filePath);

                string className = NodeUtility.GetSceneScriptClassName(filePath);
                if (className == null) continue;
                if (!s_PoolableTypeMap.ContainsKey(className)) continue;

                found++;
                GD.Print($"  [NodePoolInspector] ✓ {filePath} ({className})");

                if (existing.TryGetValue(filePath, out var oldEntry))
                {
                    var scene = ResourceLoader.Load<PackedScene>(filePath);
                    if (scene != null)
                        oldEntry.Set("Scene", scene);
                    newEntries.Add(oldEntry);
                }
                else
                {
                    newEntries.Add(new PoolEntry
                    {
                        Scene = filePath
                    });
                }
            }

            // 保留扫描范围外的条目
            foreach (var oldEntry in oldEntries)
            {
                if (oldEntry == null) continue;
                var scene = ReadPoolEntryScene(oldEntry);
                if (scene != null && !string.IsNullOrEmpty(scene.ResourcePath)
                    && !scanned.Contains(scene.ResourcePath))
                    newEntries.Add(oldEntry);
            }

            @object.Set("Entries", Variant.CreateFrom(newEntries));
            SaveConfig(@object);
            GD.Print($"[NodePoolInspector] 扫描完成: {found} 个 IPoolable 场景");
        }

        /// <summary>
        /// 反射程序集，构建所有 IPoolable 实现类的 类名→全名 映射。
        /// </summary>
        private static void BuildPoolableTypeMap()
        {
            if (s_PoolableTypeMap != null) return;

            s_PoolableTypeMap = new System.Collections.Generic.Dictionary<string, string>();
            try
            {
                System.Type[] types = Utility.Assembly.GetAssignableFormTypes(typeof(IPoolable));
                foreach (var t in types)
                {
                    if (t.IsAbstract || t.IsInterface) continue;
                    if (!typeof(Godot.Node).IsAssignableFrom(t)) continue; // 只处理 Godot Node 子类
                    s_PoolableTypeMap[t.Name] = t.FullName ?? t.Name;
                }
            }
            catch (Exception ex)
            {
                GD.PushError($"[NodePoolInspector] 反射 IPoolable 类型失败: {ex.Message}");
            }
        }

        // ── Godot 属性读写 ──

        private static Array<GodotObject> ReadEntriesArray(GodotObject @object)
        {
            var v = @object.Get("Entries");
            return v.VariantType == Variant.Type.Array
                ? v.AsGodotArray<GodotObject>()
                : new Array<GodotObject>();
        }

        private static PackedScene ReadPoolEntryScene(GodotObject entry)
        {
            var v = entry.Get("Scene");
            if (v.VariantType == Variant.Type.Object)
                return v.AsGodotObject() as PackedScene;
            return null;
        }

        // ── 保存 ──

        private static void SaveConfig(GodotObject @object)
        {
            var res = @object as Godot.Resource;
            if (res == null || string.IsNullOrEmpty(res.ResourcePath))
            {
                GD.PushWarning("[NodePoolInspector] 无法获取资源路径，请在 Inspector 中选中 .tres 文件后重试。");
                return;
            }

            Error err = ResourceSaver.Save(res, res.ResourcePath);
            if (err != Error.Ok)
                GD.PushError($"[NodePoolInspector] 保存失败 ({err}): {res.ResourcePath}");
        }

        // ── 对话框 ──

        private static void ShowConfirmDialog(string message, System.Action onConfirm)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = "确认";
            dialog.DialogText = message;
            dialog.Confirmed += () => { onConfirm(); dialog.QueueFree(); };
            dialog.Canceled += () => dialog.QueueFree();
            EditorInterface.Singleton.GetBaseControl().AddChild(dialog);
            dialog.PopupCentered();
        }
    }
}
#endif
