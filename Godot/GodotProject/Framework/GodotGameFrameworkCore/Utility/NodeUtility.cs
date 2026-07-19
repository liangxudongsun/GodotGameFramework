using GameFramework.Resource;
using Godot;
using GodotGameFramework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

public static partial class NodeUtility
{
    public static object InstantiatePack(object asset)
    {
        if (asset is PackedScene)
        {
            return (asset as PackedScene).Instantiate();
        }
        Log.Error("NodeUtility.InstantiatePack: asset is not PackedScene");
        return null;
    }

    public static void ReleaseNode(object node)
    {
        if (node is Node)
        {
            (node as Node).QueueFree();
        }
    }
    /// <summary>
    /// 获取场景脚本类名
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetSceneScriptClassName(string path)
    {
        PackedScene scene;
        try { scene = ResourceLoader.Load<PackedScene>(path); }
        catch { return null; }
        if (scene == null) return null;

        var state = scene.GetState();
        for (int i = 0; i < state.GetNodePropertyCount(0); i++)
        {
            if (state.GetNodePropertyName(0, i) != "script")
                continue;

            var script = state.GetNodePropertyValue(0, i).AsGodotObject();
            if (script is not CSharpScript csScript)
                continue;

            // 从脚本路径提取类名: "res://.../DamagePop.cs" → "DamagePop"
            string scriptPath = csScript.ResourcePath;
            if (string.IsNullOrEmpty(scriptPath)) continue;

            string fileName = scriptPath.GetFile();
            if (!fileName.EndsWith(".cs")) continue;

            return fileName.Substring(0, fileName.Length - 3);
        }

        return null;
    }
    public static string GetSceneScriptClassName(PackedScene scene)
    {
        if (scene == null) return null;

        var state = scene.GetState();
        for (int i = 0; i < state.GetNodePropertyCount(0); i++)
        {
            if (state.GetNodePropertyName(0, i) != "script")
                continue;

            var script = state.GetNodePropertyValue(0, i).AsGodotObject();
            if (script is not CSharpScript csScript)
                continue;

            // 从脚本路径提取类名: "res://.../DamagePop.cs" → "DamagePop"
            string scriptPath = csScript.ResourcePath;
            if (string.IsNullOrEmpty(scriptPath)) continue;

            string fileName = scriptPath.GetFile();
            if (!fileName.EndsWith(".cs")) continue;

            return fileName.Substring(0, fileName.Length - 3);
        }

        return null;
    }
    public static string GetNodeScriptClassName(Node node)
    {
        if (node == null) return null;

        var script = node.GetScript().AsGodotObject();
        if (script is not CSharpScript csScript)
            return null;

        // 从脚本路径提取类名: "res://.../DamagePop.cs" → "DamagePop"
        string scriptPath = csScript.ResourcePath;
        if (string.IsNullOrEmpty(scriptPath)) return null;
        string fileName = scriptPath.GetFile();
        if (!fileName.EndsWith(".cs")) return null;

        return fileName.Substring(0, fileName.Length - 3);
    }
    /// <summary>
    /// 获得指定文件夹下所有指定后缀的文件路径
    /// </summary>
    /// <param name="dirPath"></param>
    /// <param name="extension"></param>
    /// <param name="results"></param>
    public static void CollectionFilesByExtension(string dirPath, string extension, List<string> results)
    {
        using var dir = DirAccess.Open(dirPath);
        if (dir == null) return;

        dir.ListDirBegin();
        string fileName;
        while ((fileName = dir.GetNext()) != "")
        {
            if (fileName == "." || fileName == "..") continue;
            if (fileName.EndsWith(extension))
                results.Add(dirPath + fileName);
            else if (dir.CurrentIsDir())
                CollectionFilesByExtension(dirPath + fileName + "/", extension, results);
        }
        dir.ListDirEnd();
    }
    /// <summary>
    /// 加载并校验版本清单。JSON 截断 / 字段缺失 / 包数据无效 → 返回 null + 记录具体原因。
    /// </summary>
    public static PackVersionList LoadAndValidateVersionList(string fileName)
    {
        try
        {
            string path = Path.Combine(ProjectSettings.GlobalizePath("user://"), fileName);
            if (!File.Exists(path))
            {
                GD.Print($"[EasySave] 版本文件不存在: {path}");
                return null;
            }

            using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
            string json = reader.ReadToEnd();

            // 基本的 JSON 截断检测
            if (string.IsNullOrEmpty(json) || json.Length < 2)
            {
                GD.PrintErr($"[EasySave] 版本文件为空或截断 (长度={json?.Length ?? 0}): {path}");
                return null;
            }

            if (!json.TrimStart().StartsWith("{") || !json.TrimEnd().EndsWith("}"))
            {
                GD.PrintErr($"[EasySave] 版本文件 JSON 格式损坏，缺少花括号: {path}");
                return null;
            }

            var version = JsonConvert.DeserializeObject<PackVersionList>(json);
            if (version == null)
            {
                GD.PrintErr($"[EasySave] 版本文件 JSON 反序列化返回 null: {path}");
                return null;
            }

            if (!version.Validate(out string error))
            {
                GD.PrintErr($"[EasySave] 版本文件数据校验失败: {error}. 文件={path}");
                return null;
            }

            return version;
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            GD.PrintErr($"[EasySave] 版本文件 JSON 解析异常: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 版本文件加载异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 计算文件的 SHA256 哈希值
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public static string ComputeSHA256(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = System.IO.File.OpenRead(filePath);
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }
    /// <summary>
    /// 将 Godot 虚拟路径（user:// 或 res://）转换为绝对路径。
    /// DownloadManager 内部使用 System.IO 写文件，无法识别 Godot 虚拟路径。
    /// </summary>
    public static string GlobalizeDownloadPath(string downloadPath)
    {
        if (downloadPath != null && (downloadPath.StartsWith("user://") || downloadPath.StartsWith("res://")))
        {
            return ProjectSettings.GlobalizePath(downloadPath);
        }

        return downloadPath;
    }
    /// <summary>
    /// 比较语义化版本号。a > b 返回 1，a == b 返回 0，a < b 返回 -1。
    /// 简单实现：按 "." 分割后逐段比较数字。
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
        if (string.IsNullOrEmpty(a)) return -1;
        if (string.IsNullOrEmpty(b)) return 1;

        string[] aParts = a.Split('.');
        string[] bParts = b.Split('.');
        int maxLen = Math.Max(aParts.Length, bParts.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int aNum = i < aParts.Length && int.TryParse(aParts[i], out int va) ? va : 0;
            int bNum = i < bParts.Length && int.TryParse(bParts[i], out int vb) ? vb : 0;
            if (aNum > bNum) return 1;
            if (aNum < bNum) return -1;
        }
        return 0;
    }
    /// <summary>获取当前 App 版本号（来自 project.godot 的 config/version）。</summary>
    public static string GetAppVersion()
    {
        return ProjectSettings.GetSetting("application/config/version").AsString() ?? "1.0.0";
    }

    /// <summary>获取指定目录所在磁盘的剩余空间，失败返回 -1。</summary>
    public static long GetFreeDiskSpace(string dir)
    {
        try
        {
            string root = Path.GetPathRoot(dir);
            if (string.IsNullOrEmpty(root)) return -1;
            var driveInfo = new DriveInfo(root);
            return driveInfo.IsReady ? driveInfo.AvailableFreeSpace : -1;
        }
        catch
        {
            return -1;
        }
    }
}
