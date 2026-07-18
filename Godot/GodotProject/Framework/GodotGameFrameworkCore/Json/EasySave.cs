using Godot;
using System;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace GodotGameFramework.Json;

/// <summary>
/// 轻量级 JSON 持久化工具。
/// 所有路径在使用前通过 ProjectSettings.GlobalizePath 解析。
/// 注意：Godot 文件 API（FileAccess）不是线程安全的，
/// 因此异步方法内部使用纯 .NET 的 StreamWriter/StreamReader + Task.Run。
/// </summary>
public static class EasySave
{
    private static readonly string s_UserDir = ProjectSettings.GlobalizePath("user://");
    private static readonly string s_ProjectDir = ProjectSettings.GlobalizePath("res://");

    // ──────────────────────────
    //  同步方法（主线程安全）
    // ──────────────────────────

    public static bool TrySave<T>(T data, string filePath)
    {
        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            writer.Write(json);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 保存失败: {filePath} — {ex.Message}");
            return false;
        }
    }

    public static T LoadOrDefault<T>(string filePath) where T : class, new()
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
            string json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 加载失败: {filePath} — {ex.Message}");
            return null;
        }
    }

    public static bool TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 删除失败: {filePath} — {ex.Message}");
            return false;
        }
    }

    public static bool FileExists(string filePath) => File.Exists(filePath);

    // ──────────────────────────
    //  user:// 便捷方法
    // ──────────────────────────

    public static bool SaveInUser<T>(T data, string fileName) =>
        TrySave(data, Path.Combine(s_UserDir, fileName));

    public static T LoadFromUser<T>(string fileName) where T : class, new() =>
        LoadOrDefault<T>(Path.Combine(s_UserDir, fileName));

    public static bool DeleteInUser(string fileName) =>
        TryDelete(Path.Combine(s_UserDir, fileName));

    public static bool ExistsInUser(string fileName) =>
        File.Exists(Path.Combine(s_UserDir, fileName));

    // ──────────────────────────
    //  res:// 便捷方法
    // ──────────────────────────

    public static bool SaveInProject<T>(T data, string fileName) =>
        TrySave(data, Path.Combine(s_ProjectDir, fileName));

    public static T LoadFromProject<T>(string fileName) where T : class, new() =>
        LoadOrDefault<T>(Path.Combine(s_ProjectDir, fileName));

    public static bool DeleteInProject(string fileName) =>
        TryDelete(Path.Combine(s_ProjectDir, fileName));

    // ──────────────────────────
    //  异步方法（用于非 Godot 线程的调用场景）
    // ──────────────────────────

    public static async Task<bool> SaveInUserAsync<T>(T data, string fileName)
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            await Task.Run(() =>
            {
                using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
                writer.Write(json);
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步保存失败: {path} — {ex.Message}");
            return false;
        }
    }

    public static async Task<T> LoadFromUserAsync<T>(string fileName) where T : class, new()
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(path)) return null;
                using var reader = new StreamReader(path, System.Text.Encoding.UTF8);
                string json = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(json);
            });
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步加载失败: {path} — {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DeleteInUserAsync(string fileName)
    {
        string path = Path.Combine(s_UserDir, fileName);
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(path))
                    File.Delete(path);
            });
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[EasySave] 异步删除失败: {path} — {ex.Message}");
            return false;
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
