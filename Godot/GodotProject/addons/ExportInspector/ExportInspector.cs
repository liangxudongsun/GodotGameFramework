#if TOOLS
using GameConfig.Constant;
using GameFramework;
using Godot;
using GameFramework.Resource;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GodotGameFramework.Editor
{
    /// <summary>
    /// AB 包导出管理面板 —— 可视化编辑所有 AssetBundle 资源，
    /// 一键切换启用/导出状态，选择导出目标文件夹，快速打包。
    /// </summary>
    [Tool]
    public partial class ExportInspector : EditorPlugin
    {
        private sealed class BundleInfo
        {
            public string Path;           // res:// 资源路径
            public string FolderPath;     // 所在文件夹
            public string Name;
            public bool Enabled;
            public bool ExportEnabled;
            public bool PackExternalDeps;
            public bool ExportOnlyImported;
            public int ResourceCount;
        }
        private const string EditorSettingExportFolder = "godot_asset_bundle/export_folder";

        private Control _dockPanel;
        private Tree _bundleTree;
        private Button _btnRefresh;
        private Button _btnExportFolder;
        private Button _btnExportAll;
        private LineEdit _txtExportFolder;
        private TreeItem _treeRoot;

        // ── 数据 ────────────────────────────────────────
        private string _exportFolder = "";
        private readonly Dictionary<string, Godot.Resource> _bundles = new();

        // ═══════════════════════════════════════════════════════════
        //  生命周期
        // ═══════════════════════════════════════════════════════════

        public override void _EnterTree()
        {
            LoadExportFolderPreference();
            BuildUI();
#pragma warning disable CS0618 // 保留兼容旧 API，Godot 4.6 仍可用
            AddControlToDock(DockSlot.RightUl, _dockPanel);
#pragma warning restore CS0618
            RefreshBundleList();
        }

        public override void _ExitTree()
        {
#pragma warning disable CS0618
            RemoveControlFromDocks(_dockPanel);
#pragma warning restore CS0618
            _dockPanel?.QueueFree();
        }

        // ═══════════════════════════════════════════════════════════
        //  UI 构建
        // ═══════════════════════════════════════════════════════════

        private void BuildUI()
        {
            // ── 根容器 ──
            _dockPanel = new MarginContainer
            {
                Name = "ExportInspectorDock",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };

            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _dockPanel.AddChild(vbox);

            // ── 标题 ──
            var titleLabel = new Label
            {
                Text = "AssetBundle 导出管理",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(titleLabel);

            vbox.AddChild(new HSeparator());

            // ── 导出文件夹选择行 ──
            var folderRow = new HBoxContainer();
            folderRow.AddChild(new Label { Text = "导出目录:" });

            _txtExportFolder = new LineEdit
            {
                Text = _exportFolder,
                PlaceholderText = "选择或输入导出目标文件夹...",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Editable = false,
            };
            folderRow.AddChild(_txtExportFolder);

            _btnExportFolder = new Button { Text = "选择..." };
            _btnExportFolder.Pressed += OnSelectExportFolderPressed;
            folderRow.AddChild(_btnExportFolder);

            vbox.AddChild(folderRow);

            // ── 操作按钮行 ──
            var buttonRow = new HBoxContainer();

            _btnRefresh = new Button { Text = "刷新列表" };
            _btnRefresh.Pressed += OnRefreshPressed;
            buttonRow.AddChild(_btnRefresh);

            _btnExportAll = new Button { Text = "导出所有 AB 包" };
            _btnExportAll.Pressed += OnExportAllPressed;
            buttonRow.AddChild(_btnExportAll);

            vbox.AddChild(buttonRow);

            vbox.AddChild(new HSeparator());

            // ── Bundle 列表 (Tree) ──
            _bundleTree = new Tree
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                Columns = 6,
                AllowReselect = true,
            };
            _bundleTree.SetColumnTitle(0, "包名");
            _bundleTree.SetColumnTitle(1, "启用");
            _bundleTree.SetColumnTitle(2, "导出");
            _bundleTree.SetColumnTitle(3, "外部依赖");
            _bundleTree.SetColumnTitle(4, "仅产物");
            _bundleTree.SetColumnTitle(5, "资源数");

            // 列宽
            _bundleTree.SetColumnExpand(0, true);
            _bundleTree.SetColumnExpand(1, false);
            _bundleTree.SetColumnExpand(2, false);
            _bundleTree.SetColumnExpand(3, false);
            _bundleTree.SetColumnExpand(4, false);
            _bundleTree.SetColumnExpand(5, false);
            _bundleTree.SetColumnCustomMinimumWidth(0, 120);
            _bundleTree.SetColumnCustomMinimumWidth(1, 44);
            _bundleTree.SetColumnCustomMinimumWidth(2, 44);
            _bundleTree.SetColumnCustomMinimumWidth(3, 72);
            _bundleTree.SetColumnCustomMinimumWidth(4, 48);
            _bundleTree.SetColumnCustomMinimumWidth(5, 56);

            _bundleTree.ItemEdited += OnTreeItemEdited;
            _bundleTree.ButtonClicked += OnTreeButtonClicked;

            _treeRoot = _bundleTree.CreateItem();
            _treeRoot.SetText(0, "扫描中...");

            vbox.AddChild(_bundleTree);
        }

        // ═══════════════════════════════════════════════════════════
        //  核心逻辑
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 递归扫描 res:// 下所有 .tres/.res，找到 AssetBundle 资源。
        /// </summary>
        private void RefreshBundleList()
        {
            _bundles.Clear();
            _bundleTree.Clear();
            _treeRoot = _bundleTree.CreateItem();

            ScanDirectoryForBundles("res://");

            if (_bundles.Count == 0)
            {
                _treeRoot.SetText(0, "未发现 AssetBundle 资源");
                SetCellSpan(_treeRoot, 0, 6);
                return;
            }

            _treeRoot.SetText(0, $"共 {_bundles.Count} 个资源包");

            int enabledCount = 0;
            int exportCount = 0;

            foreach (var kvp in _bundles.OrderBy(k => k.Key))
            {
                var path = kvp.Key;
                var res = kvp.Value;
                var info = BuildBundleInfo(path, res);

                var item = _bundleTree.CreateItem(_treeRoot);
                item.SetMetadata(0, path);

                // 列 0: 包名
                item.SetText(0, info.Name);

                // 列 1: Enabled (checkbox)
                item.SetEditable(1, true);
                item.SetCellMode(1, TreeItem.TreeCellMode.Check);
                item.SetChecked(1, info.Enabled);

                // 列 2: Export Enabled (checkbox)
                item.SetEditable(2, true);
                item.SetCellMode(2, TreeItem.TreeCellMode.Check);
                item.SetChecked(2, info.ExportEnabled);

                // 列 3: Pack External Deps (checkbox)
                item.SetEditable(3, true);
                item.SetCellMode(3, TreeItem.TreeCellMode.Check);
                item.SetChecked(3, info.PackExternalDeps);

                // 列 4: Export Only Imported (checkbox)
                item.SetEditable(4, true);
                item.SetCellMode(4, TreeItem.TreeCellMode.Check);
                item.SetChecked(4, info.ExportOnlyImported);
                item.SetTooltipText(4, "勾选=仅导出 Godot 导入产物(.ctex/.fontdata/.sample)，不包含源文件，体积更小");

                // 列 5: 资源数
                item.SetText(5, info.ResourceCount.ToString());
                item.SetTooltipText(0, info.FolderPath);

                if (info.Enabled) enabledCount++;
                if (info.ExportEnabled) exportCount++;
            }

        }

        /// <summary>递归搜索目录中所有 AssetBundle</summary>
        private void ScanDirectoryForBundles(string dirPath)
        {
            var dir = DirAccess.Open(dirPath);
            if (dir == null) return;

            dir.ListDirBegin();
            var fileName = dir.GetNext();

            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.StartsWith('.'))
                {
                    fileName = dir.GetNext();
                    continue;
                }

                var fullPath = dirPath.PathJoin(fileName);

                if (dir.CurrentIsDir())
                {
                    ScanDirectoryForBundles(fullPath);
                }
                else
                {
                    TryAddBundle(fullPath);
                }

                fileName = dir.GetNext();
            }

            dir.ListDirEnd();
        }

        /// <summary>尝试将文件作为 AssetBundle 加载</summary>
        private void TryAddBundle(string path)
        {
            var ext = Path.GetExtension(path)?.ToLower();
            if (ext != ".tres" && ext != ".res") return;

            var resource = ResourceLoader.Load(path);
            if (resource == null) return;

            // AssetBundle 是 GDScript class_name，通过脚本的 global_name 判断
            var script = resource.GetScript().As<Script>();
            if (script == null) return;

            var globalName = script.GetGlobalName();
            if (string.IsNullOrEmpty(globalName) || !globalName.ToString().Contains("AssetBundle")) return;

            if (!_bundles.ContainsKey(path))
            {
                _bundles[path] = resource;
            }
        }

        /// <summary>从 Resource 读取 Bundle 信息</summary>
        private BundleInfo BuildBundleInfo(string path, Godot.Resource res)
        {
            string folderPath = path.GetBaseDir();

            var info = new BundleInfo
            {
                Path = path,
                FolderPath = folderPath,
                Name = Path.GetFileNameWithoutExtension(path),
                Enabled = res.Get("enabled").AsBool(),
                ExportEnabled = res.Get("export_enabled").AsBool(),
                PackExternalDeps = res.Get("pack_external_dependencies").AsBool(),
                ExportOnlyImported = res.Get("export_only_imported").AsBool(),
            };

            // 统计文件夹内资源数量
            info.ResourceCount = CountResourcesInFolder(folderPath);

            return info;
        }

        /// <summary>统计文件夹内非脚本资源数量</summary>
        private int CountResourcesInFolder(string folderPath)
        {
            int count = 0;
            var dir = DirAccess.Open(folderPath);
            if (dir == null) return 0;

            dir.ListDirBegin();
            var fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!fileName.StartsWith('.'))
                {
                    var fullPath = folderPath.PathJoin(fileName);
                    if (dir.CurrentIsDir())
                    {
                        count += CountResourcesInFolder(fullPath);
                    }
                    else
                    {
                        var ext = Path.GetExtension(fileName)?.ToLower();
                        if (ext is ".tscn" or ".scn" or ".png" or ".jpg" or ".ogg" or ".wav"
                            or ".mp3" or ".glb" or ".gltf" or ".gdshader" or ".tres" or ".res"
                            or ".svg" or ".ttf" or ".otf")
                        {
                            count++;
                        }
                    }
                }

                fileName = dir.GetNext();
            }

            dir.ListDirEnd();
            return count;
        }

        // ═══════════════════════════════════════════════════════════
        //  事件处理
        // ═══════════════════════════════════════════════════════════

        /// <summary>Tree 单元格编辑（checkbox 切换）</summary>
        private void OnTreeItemEdited()
        {
            var editedItem = _bundleTree.GetEdited();
            if (editedItem == null) return;

            var path = editedItem.GetMetadata(0).AsString();
            if (string.IsNullOrEmpty(path) || !_bundles.TryGetValue(path, out var res)) return;

            var column = _bundleTree.GetEditedColumn();

            switch (column)
            {
                case 1: // Enabled
                    res.Set("enabled", editedItem.IsChecked(1));
                    break;
                case 2: // Export Enabled
                    res.Set("export_enabled", editedItem.IsChecked(2));
                    break;
                case 3: // Pack External Deps
                    res.Set("pack_external_dependencies", editedItem.IsChecked(3));
                    break;
                case 4: // Export Only Imported
                    res.Set("export_only_imported", editedItem.IsChecked(4));
                    break;
            }

            // 保存修改到 .tres 文件
            var saveErr = ResourceSaver.Save(res, path);
            if (saveErr != Error.Ok)
            {
                GD.PrintErr($"保存失败: {path} (Error: {saveErr})");
            }
            else
            {
                GD.Print($"已保存: {Path.GetFileNameWithoutExtension(path)}");
            }

            RefreshBundleList();
        }

        /// <summary>Tree 按钮点击（预留扩展）</summary>
        private void OnTreeButtonClicked(TreeItem item, long column, long id, long mouseButton)
        {
            // 可扩展：点击包名跳转到文件夹等
        }

        private void OnRefreshPressed()
        {
            GD.Print("正在扫描...");
            RefreshBundleList();
        }

        /// <summary>选择导出文件夹</summary>
        private void OnSelectExportFolderPressed()
        {
            var dialog = new EditorFileDialog
            {
                Title = "选择 AB 包导出目录",
                FileMode = EditorFileDialog.FileModeEnum.OpenDir,
                Access = EditorFileDialog.AccessEnum.Filesystem,
            };

            dialog.DirSelected += (dir) =>
            {
                _exportFolder = dir;
                _txtExportFolder.Text = dir;
                SaveExportFolderPreference();
                GD.Print($"导出目录已设为: {dir}");
            };

            EditorInterface.Singleton.GetBaseControl().AddChild(dialog);
            dialog.PopupCenteredRatio(0.6f);
        }

        /// <summary>一键导出所有启用的 AB 包</summary>
        private void OnExportAllPressed()
        {
            var toExport = new List<BundleInfo>();

            foreach (var kvp in _bundles)
            {
                var info = BuildBundleInfo(kvp.Key, kvp.Value);
                if (info.Enabled && info.ExportEnabled)
                {
                    toExport.Add(info);
                }
            }

            if (toExport.Count == 0)
            {
                GD.Print("没有需要导出的 AB 包（请确保 Enabled 和 Export Enabled 都已勾选）。");
                return;
            }

            var exportDir = _exportFolder;
            if (string.IsNullOrEmpty(exportDir))
            {
                exportDir = ProjectSettings.GlobalizePath("res://").PathJoin("exported_bundles");
            }

            // 确保导出目录存在
            var absDir = Path.Combine(ProjectSettings.GlobalizePath(exportDir), DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            if (!absDir.StartsWith("res://") && !Directory.Exists(absDir))
            {
                Directory.CreateDirectory(absDir);
            }

            int success = 0;
            try
            {
                foreach (var bundle in toExport)
                {
                    ExportSingleBundle(bundle, absDir);
                    success++;
                }
                CreatePackVersionFile(absDir, success);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ExportInspector] 导出异常: {ex}");
            }
        }

        /// <summary>使用 PCKPacker 打包单个 Bundle</summary>
        private bool ExportSingleBundle(BundleInfo bundle, string exportDir)
        {
            var packageName = bundle.Name + ".pck";
            var packagePath = Path.Combine(exportDir, packageName);

            try
            {
                var packer = new PckPacker();
                var err = packer.PckStart(packagePath);
                if (err != Error.Ok)
                {
                    GD.PrintErr($"[ExportInspector] 无法创建 .pck: {packagePath} (Error: {err})");
                    return false;
                }

                PackDirectoryContents(packer, bundle.FolderPath, bundle);

                err = packer.Flush();
                if (err != Error.Ok)
                {
                    GD.PrintErr($"[ExportInspector] 写入 .pck 失败: {packagePath} (Error: {err})");
                    return false;
                }

                GD.Print($"[ExportInspector] 导出成功: {packagePath}");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ExportInspector] 导出异常 ({bundle.Name}): {ex.Message}");
                return false;
            }
        }

        /// <summary>递归将目录内容添加到 PCKPacker</summary>
        private static void PackDirectoryContents(PckPacker packer, string dirPath, BundleInfo bundle)
        {
            var dir = DirAccess.Open(dirPath);
            if (dir == null) return;

            dir.ListDirBegin();
            var fileName = dir.GetNext();

            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.StartsWith('.') || fileName.EndsWith(".gd.uid"))
                {
                    fileName = dir.GetNext();
                    continue;
                }

                var fullPath = dirPath.PathJoin(fileName);

                if (dir.CurrentIsDir())
                {
                    PackDirectoryContents(packer, fullPath, bundle);
                }
                else
                {
                    var ext = Path.GetExtension(fileName)?.ToLower();

                    // 跳过 AssetBundle 资源文件本身（元数据，不是实际资源）
                    if (ext is ".tres" or ".res")
                    {
                        var resCheck = ResourceLoader.Load(fullPath);
                        if (resCheck != null)
                        {
                            var script = resCheck.GetScript().As<Script>();
                            if (script != null &&
                                script.GetGlobalName().ToString().Contains("AssetBundle"))
                            {
                                fileName = dir.GetNext();
                                continue; // 跳过 AssetBundle 标记文件
                            }
                        }
                    }

                    // 打包文件及其 .import
                    PackFileWithImport(packer, fullPath, bundle.ExportOnlyImported);
                }

                fileName = dir.GetNext();
            }

            dir.ListDirEnd();
        }

        /// <summary>打包文件及其 .import 依赖，包括 Godot 导入产物</summary>
        private static void PackFileWithImport(PckPacker packer, string filePath, bool exportOnlyImported = false)
        {
            var globalPath = ProjectSettings.GlobalizePath(filePath);
            if (!File.Exists(globalPath)) return;

            // 打包 .import 文件（如果存在）
            var importPath = filePath + ".import";
            var globalImportPath = ProjectSettings.GlobalizePath(importPath);
            bool hasImport = File.Exists(globalImportPath);

            if (hasImport)
            {
                if (!exportOnlyImported)
                {
                    // 完整模式：打包源文件
                    var err = packer.AddFile(filePath, globalPath);
                    if (err != Error.Ok)
                    {
                        GD.PushWarning($"[ExportInspector] 无法添加文件到包: {filePath}");
                    }
                }

                // 打包 .import 文件
                var errImport = packer.AddFile(importPath, globalImportPath);
                if (errImport != Error.Ok)
                {
                    GD.PushWarning($"[ExportInspector] 无法添加 .import 文件到包: {importPath}");
                }

                // 解析 .import，将 Godot 导入产物（.ctex / .fontdata / .sample 等）也打进子包
                PackImportedResource(packer, globalImportPath);
            }
            else
            {
                // 无 .import 文件 → 直接打包源文件（如 .tscn、.tres、.glb）
                var err = packer.AddFile(filePath, globalPath);
                if (err != Error.Ok)
                {
                    GD.PushWarning($"[ExportInspector] 无法添加文件到包: {filePath}");
                }
            }
        }

        /// <summary>解析 .import 文件中 dest_files 列出的导入产物，打包进 .pck</summary>
        private static void PackImportedResource(PckPacker packer, string importFilePath)
        {
            try
            {
                var lines = File.ReadAllLines(importFilePath);
                string destFilesLine = null;
                string pathLine = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("dest_files="))
                    {
                        destFilesLine = trimmed;
                    }
                    else if (trimmed.StartsWith("path=") && pathLine == null)
                    {
                        // 只取 [remap] 段的 path（第一个出现的 path=）
                        pathLine = trimmed;
                    }
                }

                // 优先用 dest_files（JSON 数组，可包含多个路径）
                string[] destPaths = null;
                if (destFilesLine != null)
                {
                    var json = destFilesLine.Substring("dest_files=".Length);
                    destPaths = System.Text.Json.JsonSerializer.Deserialize<string[]>(json);
                }

                // 回退：用 path= 字段（单路径）
                if ((destPaths == null || destPaths.Length == 0) && pathLine != null)
                {
                    var firstQuote = pathLine.IndexOf('"');
                    var lastQuote = pathLine.LastIndexOf('"');
                    if (firstQuote > 0 && lastQuote > firstQuote)
                    {
                        var singlePath = pathLine.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        destPaths = new[] { singlePath };
                    }
                }

                if (destPaths == null || destPaths.Length == 0) return;

                foreach (var resPath in destPaths)
                {
                    var globalDestPath = ProjectSettings.GlobalizePath(resPath);
                    if (File.Exists(globalDestPath))
                    {
                        var addErr = packer.AddFile(resPath, globalDestPath);
                        if (addErr != Error.Ok)
                        {
                            GD.PushWarning($"[ExportInspector] 无法添加导入产物到包: {resPath}");
                        }
                    }
                    else
                    {
                        GD.PushWarning($"[ExportInspector] 导入产物不存在，跳过: {resPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[ExportInspector] 解析 .import 文件失败: {importFilePath}, {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  偏好设置持久化
        // ═══════════════════════════════════════════════════════════

        private void LoadExportFolderPreference()
        {
            if (EditorInterface.Singleton.GetEditorSettings()
                    .HasSetting(EditorSettingExportFolder))
            {
                _exportFolder = (string)EditorInterface.Singleton
                    .GetEditorSettings()
                    .GetSetting(EditorSettingExportFolder);
            }
        }

        private void SaveExportFolderPreference()
        {
            EditorInterface.Singleton.GetEditorSettings()
                .SetSetting(EditorSettingExportFolder, _exportFolder);
        }


        private void CreatePackVersionFile(string exportDir, int num)
        {
            if (num == 0)
            {
                GD.Print("[ExportInspector] 没有成功导出的包，跳过版本文件生成。");
                return;
            }

            var blist = _bundles.ToList();
            var packs = new Pack[num];
            int idx = 0;

            for (int i = 0; i < blist.Count; i++)
            {
                var bundle = blist[i];
                var info = BuildBundleInfo(bundle.Key, bundle.Value);
                if (!info.Enabled || !info.ExportEnabled) continue;

                var pckPath = Path.Combine(exportDir, info.Name + ".pck");
                var fileInfo = new FileInfo(pckPath);

                packs[idx] = new Pack
                {
                    Name = info.Name,
                    Size = fileInfo.Exists ? (int)fileInfo.Length : 0,
                    Hash = fileInfo.Exists ? ComputeFileHash(pckPath) : 0,
                    Url = "http://localhost",  // TODO: 替换为实际 CDN 地址
                };
                idx++;
            }

            var versionList = new PackVersionList("1.0.0", packs);
            string json = JsonConvert.SerializeObject(versionList, Formatting.Indented);
            string filePath = Path.Combine(exportDir, ResourceManager.GameFrameworkVersionData);
            File.WriteAllText(filePath, json);
            GD.Print($"[ExportInspector] 版本文件已生成: {filePath}");
        }

        /// <summary>基于文件内容计算的 MD5 哈希（取前 4 字节转 int）</summary>
        private static int ComputeFileHash(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = md5.ComputeHash(stream);
            return BitConverter.ToInt32(hashBytes, 0);
        }


        private static void SetCellSpan(TreeItem item, int column, int span)
        {
            for (int i = 1; i < span; i++)
            {
                item.SetText(column + i, "");
            }
        }
    }
}
#endif
