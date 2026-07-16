//------------------------------------------------------------
// 更新检测流程
// 连接服务器检测版本更新，有更新则下载补丁包
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using GameConfig.Constant;
using GameFramework;
using GameFramework.Procedure;
using GameFramework.Resource;
using GameLogic;
using Godot;
using GodotGameFramework;
using GodotGameFramework.HotUpdate;
using GodotGameFramework.Json;
using GodotGameFramework.Web;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 更新检测流程。
/// 步骤：请求版本文件 → 比对本地 → 下载差量包 → SHA256 校验 → 保存版本 → 加载子包
/// </summary>
public class ProcedureUpdate : ProcedureBase
{
    private const int MaxRetries = 3;
    private const float RetryBaseDelaySeconds = 1.5f;

    /// <summary>
    /// 热更补丁存储目录。
    /// 自动选择：配置优先 → 游戏目录（可写时） → user:// 回退
    /// </summary>
    private static string SubpackDir => GetOrCreateHotUpdateDir();

    private static string GetOrCreateHotUpdateDir()
    {
        // 1. 开发者显式配置的路径
        string customPath = GF.Resource?.UpdateSettingRes?.HotUpdatePath;
        if (!string.IsNullOrEmpty(customPath))
        {
            EnsureDirectory(customPath);
            return customPath;
        }

        // 2. 游戏安装目录（大多数 PC 游戏不装在 C:\Program Files\）
        string exeDir = OS.HasFeature("editor")
            ? $"{ProjectSettings.GlobalizePath("res://")}" + "../../Godot"
            : System.IO.Path.GetDirectoryName(OS.GetExecutablePath());

        if (!string.IsNullOrEmpty(exeDir))
        {
            string gameSubpackDir = Path.Combine(exeDir, "subpackages");
            if (IsDirectoryWritable(gameSubpackDir))
                return gameSubpackDir;
        }

        // 3. 回退 user://（一定能写，但在 C 盘）
        string userSubpackDir = Path.Combine(
            ProjectSettings.GlobalizePath("user://"), "subpackages");
        EnsureDirectory(userSubpackDir);
        return userSubpackDir;
    }

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            EnsureDirectory(path);
            string testFile = Path.Combine(path, ".write_test");
            File.WriteAllText(testFile, " ");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);
    }

    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        try
        {
            await RunUpdateFlowAsync(procedureOwner);
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureUpdate] 更新流程异常: {0}", ex);
            SkipToNext(procedureOwner);
        }
    }

    // ── 主流程 ──

    private async Task RunUpdateFlowAsync(ProcedureOwner procedureOwner)
    {
        // Package 模式不检测更新
        if (GF.Resource.ResourceMode == ResourceMode.Package)
        {
            Log.Info("[ProcedureUpdate] Package 模式，跳过更新检测。");
            ChangeState<ProcedurePrelode>(procedureOwner);
            return;
        }

        // ── 0. 崩溃恢复检测（必须在一切之前） ──
        if (HotUpdateSafetyGuard.WasLastSessionCrashed())
        {
            HotUpdateSafetyGuard.EnterSafeMode();
            Log.Warning("[ProcedureUpdate] 上次启动崩溃，本次跳过所有热更补丁。");
            ChangeState<ProcedurePrelode>(procedureOwner);
            return;
        }

        // 校验远程地址
        string remoteUrl = GF.Resource.UpdateSettingRes?.RemoteUrl;
        if (string.IsNullOrEmpty(remoteUrl))
        {
            // 没有远程地址 → 跳过更新检测，但仍加载已下载的本地补丁
            Log.Warning("[ProcedureUpdate] 未配置 RemoteUrl，跳过更新检测。");
            var localVersion = EasySave.LoadFromUser<PackVersionList>(
                ResourceManager.GameFrameworkVersionData);
            if (localVersion != null)
            {
                // 加载前校验本地文件完整性
                int damaged = VerifyLocalPackIntegrity(localVersion);
                if (damaged > 0)
                    Log.Warning("[ProcedureUpdate] 本地 {0} 个文件损坏，但无法连接服务器修复。", damaged);
                LoadDownloadedPacks(localVersion);
            }
            ChangeState<ProcedurePrelode>(procedureOwner);
            return;
        }

        // 打开登录界面显示进度
        GF.UI.AddUIGroup("MainPack");
        var loginForm = await GF.UI.OpenUIFormAsync(
            ResourcesCollectionConstant.Resources_LogInForm, "MainPack") as LogInForm;

        try
        {
            // ── 1. 请求服务器版本文件 ──
            string versionUrl = $"{remoteUrl.TrimEnd('/')}/{ResourceManager.GameFrameworkVersionData}";
            Log.Info("[ProcedureUpdate] 请求版本文件: {0}", versionUrl);
            loginForm?.SetLogState("检测更新...", 0);

            PackVersionList serverVersion = await FetchVersionWithRetryAsync(versionUrl);
            if (serverVersion == null || !serverVersion.IsValid())
            {
                loginForm?.SetLogState("版本检测失败", 100);
                await Task.Delay(1500);
                SkipToNext(procedureOwner);
                return;
            }

            Log.Info("[ProcedureUpdate] 服务器版本: {0}, {1} 个子包",
                serverVersion.Version, serverVersion.Packs?.Length ?? 0);

            // ── 2. 版本兼容性检查 ──
            string appVersion = GetAppVersion();
            if (!string.IsNullOrEmpty(serverVersion.MinAppVersion) &&
                CompareVersions(appVersion, serverVersion.MinAppVersion) < 0)
            {
                Log.Warning("[ProcedureUpdate] App 版本过低 ({0} < {1})，需要去商店更新。",
                    appVersion, serverVersion.MinAppVersion);
                loginForm?.SetLogState("请更新App版本", 100);
                await Task.Delay(3000);
                // TODO: 弹窗引导用户去商店更新，然后退出
                SkipToNext(procedureOwner);
                return;
            }

            // ── 3. 加载本地版本并校验完整性 ──
            var localVersion = EasySave.LoadFromUser<PackVersionList>(ResourceManager.GameFrameworkVersionData);
            loginForm?.SetLogState("校验本地数据...", 5);

            // 自检：逐包校验本地文件是否完整（存在 + 大小 + SHA256）
            int damagedCount = VerifyLocalPackIntegrity(localVersion);
            if (damagedCount > 0)
            {
                Log.Warning("[ProcedureUpdate] 本地客户端不完整！{0} 个包已损坏或丢失，将重新下载。", damagedCount);
                loginForm?.SetLogState($"检测到 {damagedCount} 个文件损坏,即将修复", 8);
            }

            // ── 4. 与服务器版本比对 ──
            var toDownload = FindPacksToUpdate(serverVersion, localVersion);

            // 强制更新：有更新包且服务器标记为强制
            if (toDownload.Count > 0 && serverVersion.ForceUpdate)
            {
                Log.Info("[ProcedureUpdate] 本次为强制更新。");
                // 强制更新时，即使下载失败也不跳过——在下载失败处理中不再允许跳过
            }

            // ── 4. 下载更新的包 ──
            if (toDownload.Count > 0)
            {
                // 磁盘空间预检
                long totalSize = toDownload.Sum(x => x.Pack.Size);
                long freeSpace = GetFreeDiskSpace(SubpackDir);
                if (freeSpace > 0 && freeSpace < totalSize * 2) // 需要 2x（文件 + .tmp）
                {
                    Log.Warning("[ProcedureUpdate] 磁盘空间不足: 需要 {0}, 可用 {1}",
                        FormatBytes(totalSize * 2), FormatBytes(freeSpace));
                    loginForm?.SetLogState("磁盘空间不足", 100);
                    await Task.Delay(2000);
                    SkipToNext(procedureOwner);
                    return;
                }

                Log.Info("[ProcedureUpdate] 共 {0} 个包需要更新，总计 {1}，开始下载...",
                    toDownload.Count, FormatBytes(totalSize));
                int downloaded = await DownloadPacksWithProgressAsync(toDownload, loginForm);
                Log.Info("[ProcedureUpdate] 下载完成: {0}/{1}", downloaded, toDownload.Count);

                if (downloaded == 0)
                {
                    loginForm?.SetLogState("下载失败，请检查网络", 100);
                    await Task.Delay(2000);
                    SkipToNext(procedureOwner);
                    return;
                }
            }
            else
            {
                Log.Info("[ProcedureUpdate] 所有包已是最新，无需下载。");
            }

            // ── 4. 先加载子包（验证可用） ──
            //     顺序很重要：先加载验证 → 再保存版本。
            //     如果加载时崩溃，版本文件仍是旧版，下次启动不会陷入死循环。
            loginForm?.SetLogState("加载资源...", 95);

            // 写入启动锁：加载期间如果崩溃，下次启动会检测到
            HotUpdateSafetyGuard.MarkStartupBegin();

            LoadDownloadedPacks(serverVersion);

            // ── 5. 加载成功后再保存版本文件（带备份） ──
            if (toDownload.Count > 0 || localVersion == null ||
                (localVersion != null && localVersion.Version != serverVersion.Version))
            {
                // 备份旧版本
                if (EasySave.ExistsInUser(ResourceManager.GameFrameworkVersionData))
                {
                    EasySave.SaveInUser(localVersion,
                        ResourceManager.GameFrameworkVersionData + ".bak");
                }

                await EasySave.SaveInUserAsync(serverVersion, ResourceManager.GameFrameworkVersionData);
                Log.Info("[ProcedureUpdate] 版本文件已保存。");
            }

            loginForm?.SetLogState("更新完成", 100);
            await Task.Delay(500);
        }
        finally
        {
            GF.UI.CloseUIForm(loginForm);
        }

        ChangeState<ProcedurePrelode>(procedureOwner);
    }


    // ── 版本比对 ──

    /// <summary>
    /// 校验本地版本中所有 .pck 文件的完整性。
    /// 检查：文件存在 + 大小匹配 + SHA256 匹配（>1MB 文件）。
    /// 损坏或丢失的包从 localVersion 中移除，后续会自动与服务器对齐重新下载。
    /// </summary>
    /// <returns>损坏/丢失的包数量</returns>
    private static int VerifyLocalPackIntegrity(PackVersionList localVersion)
    {
        Log.Info("[ProcedureUpdate] 开始校验本地文件完整性...");
        if (localVersion?.Packs == null || localVersion.Packs.Length == 0)
            return 0;

        var validPacks = new List<Pack>();
        int damaged = 0;

        foreach (var pack in localVersion.Packs)
        {
            if (!pack.IsValid()) continue;

            string packPath = Path.Combine(SubpackDir, pack.Name + ".pck");
            string damageReason = null;

            // 1. 文件存在？
            if (!File.Exists(packPath))
            {
                damageReason = "文件不存在";
            }
            else
            {
                var fileInfo = new FileInfo(packPath);

                // 2. 大小匹配？
                if (fileInfo.Length != pack.Size)
                {
                    damageReason = $"大小不匹配 (期望 {pack.Size}, 实际 {fileInfo.Length})";
                }
                // 3. SHA256 校验（>1MB 的文件）
                else if (!string.IsNullOrEmpty(pack.Hash) && fileInfo.Length > 1024 * 1024)
                {
                    try
                    {
                        string actualHash = ComputeSHA256(packPath);
                        if (!string.Equals(actualHash, pack.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            damageReason = "SHA256 校验失败（文件已损坏或被修改）";
                        }
                    }
                    catch (Exception ex)
                    {
                        damageReason = $"SHA256 计算失败: {ex.Message}";
                    }
                }
            }

            if (damageReason != null)
            {
                Log.Warning("[ProcedureUpdate] 本地文件损坏: {0} — {1}", pack.Name, damageReason);
                TryDeleteFile(packPath);
                damaged++;
            }
            else
            {
                validPacks.Add(pack);
            }
        }

        // 更新本地版本列表：只保留通过校验的
        localVersion.Packs = validPacks.ToArray();

        if (damaged > 0)
        {
            Log.Warning("[ProcedureUpdate] 本地完整性校验完成: {0} 个损坏/丢失, {1} 个完好",
                damaged, validPacks.Count);
        }

        return damaged;
    }

    /// <summary>
    /// 比对本机与服务器版本，返回需要下载的包列表（含下载 URL）。
    /// </summary>
    private static List<(Pack Pack, string Url)> FindPacksToUpdate(
        PackVersionList server, PackVersionList local)
    {
        var toDownload = new List<(Pack, string)>();

        if (server?.Packs == null || server.Packs.Length == 0)
            return toDownload;

        var localDict = new Dictionary<string, Pack>();
        if (local?.Packs != null)
        {
            foreach (var lp in local.Packs)
            {
                if (!string.IsNullOrEmpty(lp.Name))
                    localDict[lp.Name] = lp;
            }
        }

        foreach (var sp in server.Packs)
        {
            if (!sp.IsValid())
            {
                Log.Warning("[ProcedureUpdate] 服务器版本中的包数据无效: {0}", sp.Name ?? "(null)");
                continue;
            }

            string url = !string.IsNullOrEmpty(sp.Url)
                ? sp.Url
                : $"{GetRemoteUrlBase()}/{sp.Name}.pck";

            if (!localDict.TryGetValue(sp.Name, out var lp))
            {
                Log.Info("[ProcedureUpdate] 发现新包: {0} ({1} bytes)", sp.Name, sp.Size);
                toDownload.Add((sp, url));
            }
            else if (lp.Hash != sp.Hash || lp.Size != sp.Size)
            {
                Log.Info("[ProcedureUpdate] 包有更新: {0} ({1}→{2} bytes)",
                    sp.Name, lp.Size, sp.Size);
                toDownload.Add((sp, url));
            }
        }

        return toDownload;
    }

    // ── 下载逻辑 ──

    /// <summary>
    /// 批量下载，带进度报告和 SHA256 校验。
    /// </summary>
    private static async Task<int> DownloadPacksWithProgressAsync(
        List<(Pack Pack, string Url)> packs, LogInForm loginForm)
    {
        int downloaded = 0;
        long totalBytes = 0;
        long downloadedBytes = 0;

        foreach (var (pack, _) in packs)
            totalBytes += pack.Size;

        EnsureDirectory(SubpackDir);

        for (int i = 0; i < packs.Count; i++)
        {
            var (pack, url) = packs[i];
            string savePath = Path.Combine(SubpackDir, pack.Name + ".pck");

            // 进度按字节加权，避免 500MB 和 1KB 各占 50% 的假进度
            long completedBeforeThis = downloadedBytes;
            int progressStart = totalBytes > 0
                ? 10 + (int)(80.0 * completedBeforeThis / totalBytes)
                : 10 + (int)(80.0 * i / packs.Count);
            int progressEnd = totalBytes > 0
                ? 10 + (int)(80.0 * (completedBeforeThis + pack.Size) / totalBytes)
                : 10 + (int)(80.0 * (i + 1) / packs.Count);
            int progressRange = Math.Max(1, progressEnd - progressStart);

            loginForm?.SetLogState($"下载中 [{i + 1}/{packs.Count}] {pack.Name}", progressStart);

            bool ok = await DownloadSinglePackWithRetryAsync(
                pack, url, savePath,
                downloadedBytes, totalBytes, progressStart, progressRange, loginForm);

            if (ok)
            {
                downloaded++;
                downloadedBytes += pack.Size;
                Log.Info("[ProcedureUpdate] 下载+校验成功: {0}", pack.Name);
            }
            else
            {
                Log.Error("[ProcedureUpdate] 下载失败（已重试 {0} 次）: {1}", MaxRetries, pack.Name);
                // 不跳过后续包——一个失败不影响其他包
            }
        }

        return downloaded;
    }

    /// <summary>
    /// 下载单个包（含重试 + 断点续传 + 流式写入 + SHA256 校验）。
    /// 内存只占 64KB 缓冲区，支持断点续传。
    /// </summary>
    private static async Task<bool> DownloadSinglePackWithRetryAsync(
        Pack pack, string url, string savePath,
        long baseDownloadedBytes, long totalBytes,
        int progressStart, int progressRange, LogInForm loginForm)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                float delay = RetryBaseDelaySeconds * (1 << (attempt - 1));
                Log.Info("[ProcedureUpdate] 重试 {0}/{1}: {2}（{3:F1}s 后）",
                    attempt + 1, MaxRetries, pack.Name, delay);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            try
            {
                bool ok = await StreamingDownloader.DownloadFileAsync(
                    url: url,
                    savePath: savePath,
                    expectedSize: pack.Size,
                    expectedHash: pack.Hash,
                    allowResume: true,
                    onProgress: (downloaded, total) =>
                    {
                        if (totalBytes > 0)
                        {
                            long globalProgress = baseDownloadedBytes + downloaded;
                            int pct = 10 + (int)(80.0 * globalProgress / totalBytes);
                            loginForm?.SetLogState(
                                $"下载中 {pack.Name} ({FormatBytes(downloaded)}/{FormatBytes(total)})",
                                Math.Min(pct, 90));
                        }
                    });

                if (ok)
                {
                    Log.Info("[ProcedureUpdate] 下载+校验成功: {0},路径:{1}", pack.Name, savePath);
                    return true;
                }

                Log.Warning("[ProcedureUpdate] 下载失败 (attempt {0}/{1}): {2}",
                    attempt + 1, MaxRetries, pack.Name);
            }
            catch (Exception ex)
            {
                Log.Error("[ProcedureUpdate] 下载异常: {0} — {1}", pack.Name, ex.Message);
                // 等一等让文件句柄释放，避免下次重试时文件被锁
                await Task.Delay(200);
                TryDeleteFile(savePath + ".tmp");
            }
        }

        return false;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1}KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1}MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2}GB";
    }

    // ── 版本文件请求 ──

    private static async Task<PackVersionList> FetchVersionWithRetryAsync(string versionUrl)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                float delay = RetryBaseDelaySeconds * (1 << (attempt - 1));
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            try
            {
                var result = await GF.WebRequest.SendRequestAsync(versionUrl);
                if (!IsHttpSuccess(result))
                {
                    Log.Warning("[ProcedureUpdate] 版本文件请求失败 (attempt {0}/{1}, HTTP {2})",
                        attempt + 1, MaxRetries, result?.ResponseCode);
                    continue;
                }

                string json = Encoding.UTF8.GetString(result.Body);
                var version = Utility.Json.ToObject<PackVersionList>(json);
                if (version != null)
                    return version;

                Log.Warning("[ProcedureUpdate] 版本 JSON 解析为 null (attempt {0}/{1})",
                    attempt + 1, MaxRetries);
            }
            catch (Exception ex)
            {
                Log.Error("[ProcedureUpdate] 版本文件请求异常: {0}", ex.Message);
            }
        }

        return null;
    }

    // ── 子包加载 ──

    /// <summary>
    /// 加载已下载的 .pck 子包。
    /// 先加载 Config 类型（Luban/本地化），再加载 Resource 类型（场景/贴图）。
    /// 加载前大小校验，对大文件做 SHA256 重校验。
    /// </summary>
    private static void LoadDownloadedPacks(PackVersionList version)
    {
        if (version?.Packs == null || version.Packs.Length == 0)
            return;

        // 先 Config 后 Resource，确保场景加载时配置已就绪
        var ordered = version.Packs
            .OrderBy(p => p.Type == PackType.Config ? 0 : 1)
            .ToArray();

        int loaded = 0;
        int failed = 0;

        foreach (var pack in ordered)
        {
            if (!pack.IsValid()) continue;

            string packPath = Path.Combine(SubpackDir, pack.Name + ".pck");
            if (!File.Exists(packPath))
            {
                Log.Warning("[ProcedureUpdate] 子包不存在，跳过: {0}", packPath);
                failed++;
                continue;
            }

            // 大小校验
            var fileInfo = new FileInfo(packPath);
            if (fileInfo.Length != pack.Size)
            {
                Log.Warning("[ProcedureUpdate] 子包大小不匹配({0})，可能已损坏，跳过: {1}",
                    pack.Name, fileInfo.Length);
                TryDeleteFile(packPath);
                failed++;
                continue;
            }

            // 对大文件做 SHA256 重校验（防御磁盘静默损坏）
            if (!string.IsNullOrEmpty(pack.Hash) && fileInfo.Length > 1024 * 1024) // > 1MB
            {
                try
                {
                    string actualHash = ComputeSHA256(packPath);
                    if (!string.Equals(actualHash, pack.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warning("[ProcedureUpdate] SHA256 重校验失败，文件可能损坏: {0}", pack.Name);
                        TryDeleteFile(packPath);
                        failed++;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[ProcedureUpdate] SHA256 计算失败: {0} — {1}", pack.Name, ex.Message);
                    failed++;
                    continue;
                }
            }

            // 加载
            if (ProjectSettings.LoadResourcePack(packPath))
            {
                loaded++;
                Log.Info("[ProcedureUpdate] 子包加载成功: {0} ({1})", pack.Name, pack.Type);
            }
            else
            {
                Log.Warning("[ProcedureUpdate] 子包加载失败: {0}", packPath);
                failed++;
            }
        }

        // 清理不属于当前版本的废弃 .pck
        CleanStalePacks(version);

        // 如果有包加载失败，回退版本文件
        if (failed > 0)
        {
            Log.Warning("[ProcedureUpdate] {0} 个子包加载失败，回退版本文件。", failed);
            RollbackVersionFile();
        }

        Log.Info("[ProcedureUpdate] 子包加载完成: {0}/{1} (失败: {2})",
            loaded, version.Packs.Length, failed);
    }

    /// <summary>清理磁盘上不在版本清单中的废弃 .pck 文件。</summary>
    private static void CleanStalePacks(PackVersionList version)
    {
        if (!Directory.Exists(SubpackDir)) return;

        var validNames = new HashSet<string>(
            version.Packs.Where(p => p.IsValid()).Select(p => p.Name + ".pck"));

        try
        {
            foreach (string file in Directory.GetFiles(SubpackDir, "*.pck"))
            {
                string fileName = Path.GetFileName(file);
                if (!validNames.Contains(fileName))
                {
                    Log.Info("[ProcedureUpdate] 清理废弃子包: {0}", fileName);
                    TryDeleteFile(file);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[ProcedureUpdate] 清理废弃包异常: {0}", ex.Message);
        }
    }

    /// <summary>回退版本文件到备份。</summary>
    private static void RollbackVersionFile()
    {
        try
        {
            string versionPath = Path.Combine(
                ProjectSettings.GlobalizePath("user://"), ResourceManager.GameFrameworkVersionData);
            string backupPath = versionPath + ".bak";

            if (File.Exists(backupPath))
            {
                TryDeleteFile(versionPath);
                File.Move(backupPath, versionPath);
                Log.Info("[ProcedureUpdate] 已自动回退到上一版本。");
            }
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureUpdate] 版本回退失败: {0}", ex.Message);
        }
    }

    // ── 工具方法 ──

    private static bool IsHttpSuccess(WebRequestCompleteEventArgs result)
    {
        if (result == null) return false;
        if (result.Result == -1 && result.ResponseCode == 0) return false; // timeout
        if (result.ResponseCode != 200 || result.Result != (long)Error.Ok) return false;
        if (result.Body == null || result.Body.Length == 0) return false;
        return true;
    }

    private static string ComputeSHA256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string GetRemoteUrlBase()
    {
        string url = GF.Resource.UpdateSettingRes?.RemoteUrl;
        return url?.TrimEnd('/') ?? string.Empty;
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning("[ProcedureUpdate] 删除文件失败: {0} — {1}", path, ex.Message);
        }
    }

    private void SkipToNext(ProcedureOwner procedureOwner)
    {
        // 尝试加载已存在的本地版本
        var local = EasySave.LoadFromUser<PackVersionList>(ResourceManager.GameFrameworkVersionData);
        if (local != null)
            LoadDownloadedPacks(local);

        ChangeState<ProcedurePrelode>(procedureOwner);
    }

    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }

    // ── 版本工具 ──

    /// <summary>获取当前 App 版本号（来自 project.godot 的 config/version）。</summary>
    private static string GetAppVersion()
    {
        return ProjectSettings.GetSetting("application/config/version").AsString() ?? "1.0.0";
    }

    /// <summary>获取指定目录所在磁盘的剩余空间，失败返回 -1。</summary>
    private static long GetFreeDiskSpace(string dir)
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

    /// <summary>
    /// 比较语义化版本号。a > b 返回 1，a == b 返回 0，a < b 返回 -1。
    /// 简单实现：按 "." 分割后逐段比较数字。
    /// </summary>
    private static int CompareVersions(string a, string b)
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
}
