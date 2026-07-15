//------------------------------------------------------------
// 更新检测流程
// 连接服务器检测版本更新，有更新则下载补丁包
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GameFramework;
using GameFramework.Procedure;
using GameFramework.Resource;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Json;
using GodotGameFramework.Web;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 更新检测流程。
/// </summary>
public class ProcedureUpdate : ProcedureBase
{
    public static readonly string ExeDir = OS.HasFeature("editor") ? $"{ProjectSettings.GlobalizePath("res://")}" + "../../Godot" : System.IO.Path.GetDirectoryName(OS.GetExecutablePath());
    /// <summary>
    /// 状态初始化。
    /// </summary>
    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);
    }

    /// <summary>
    /// 进入流程：检测服务器更新。
    /// </summary>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        if (GF.Resource.ResourceMode == ResourceMode.Package)
        {
            ChangeState<ProcedurePrelode>(procedureOwner);
            Log.Info("[ProcedureUpdate] Package 模式，跳过更新检测。");
            return;
        }
        Log.Info("[ProcedureUpdate] 开始检测更新...");

        string remoteUrl = GF.Resource.UpdateSettingRes?.RemoteUrl;
        if (string.IsNullOrEmpty(remoteUrl))
        {
            Log.Warning("[ProcedureUpdate] 未配置 RemoteUrl，跳过更新检测。");
            SkipToNext(procedureOwner);
            return;
        }

        // ── 1. 请求服务器版本文件 ──
        string versionUrl = $"{remoteUrl.TrimEnd('/')}/{ResourceManager.GameFrameworkVersionData}";
        Log.Info("[ProcedureUpdate] 请求版本文件: {0}", versionUrl);

        var result = await GF.WebRequest.SendRequestAsync(versionUrl);
        if (!IsHttpSuccess(result))
        {
            Log.Warning("[ProcedureUpdate] 获取服务器版本失败，使用本地版本。");
            SkipToNext(procedureOwner);
            return;
        }

        PackVersionList serverVersion;
        if (!TryParseVersionJson(result.Body, out serverVersion))
        {
            SkipToNext(procedureOwner);
            return;
        }

        Log.Info("[ProcedureUpdate] 服务器版本: {0}, 包含 {1} 个子包",
            serverVersion.Version, serverVersion.Packs?.Length ?? 0);

        // ── 2. 与本地版本比对 ──
        var localVersion = GF.Resource.GetPackVersionList();
        var packsToDownload = FindPacksToUpdate(serverVersion, localVersion, remoteUrl);

        // ── 3. 下载更新的包 ──
        if (packsToDownload.Count > 0)
        {
            Log.Info("[ProcedureUpdate] 共 {0} 个包需要下载，开始下载...", packsToDownload.Count);
            int downloaded = await DownloadPacks(packsToDownload);
            Log.Info("[ProcedureUpdate] 下载完成: {0}/{1}", downloaded, packsToDownload.Count);
        }
        else
        {
            Log.Info("[ProcedureUpdate] 所有包已是最新，无需下载。");
        }

        // ── 4. 保存版本文件 & 加载子包 ──
        if (packsToDownload.Count > 0 || localVersion == null ||
            (localVersion != null && localVersion.Version != serverVersion.Version))
        {
            await EasySave.SaveInUserAsync(serverVersion, ResourceManager.GameFrameworkVersionData);
            Log.Info("[ProcedureUpdate] 版本文件已保存。");
        }

        LoadDownloadedPacks(serverVersion);

        ChangeState<ProcedurePrelode>(procedureOwner);
    }

    /// <summary>HTTP 响应是否成功（200 OK 且有 Body）。</summary>
    private static bool IsHttpSuccess(WebRequestCompleteEventArgs result)
    {
        if (result == null) return false;
        // 超时
        if (result.Result == -1 && result.ResponseCode == 0) return false;
        // HTTP 非 200 或 Godot Error 非 Ok
        if (result.ResponseCode != 200 || result.Result != (long)Error.Ok) return false;
        // 空 Body
        if (result.Body == null || result.Body.Length == 0) return false;
        return true;
    }

    /// <summary>解析版本 JSON，失败返回 false。</summary>
    private static bool TryParseVersionJson(byte[] body, out PackVersionList version)
    {
        version = null;
        try
        {
            string json = Encoding.UTF8.GetString(body);
            version = Utility.Json.ToObject<PackVersionList>(json);
            return version != null;
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureUpdate] JSON 解析失败: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 比对本机与服务器版本，找出需要下载的包。
    /// 返回待下载列表（含下载 URL）。
    /// </summary>
    private static List<(Pack Pack, string Url)> FindPacksToUpdate(
        PackVersionList server, PackVersionList local, string remoteUrl)
    {
        var toDownload = new List<(Pack, string)>();

        if (server?.Packs == null || server.Packs.Length == 0)
            return toDownload;

        // 本地版本 → 字典，O(1) 查找
        var localDict = new Dictionary<string, Pack>();
        if (local?.Packs != null)
        {
            foreach (var lp in local.Packs)
                localDict[lp.Name] = lp;
        }

        foreach (var sp in server.Packs)
        {
            // 构造下载 URL：优先用 Pack 自带的 Url，否则拼接
            string url = sp.Url;
            if (string.IsNullOrEmpty(url))
                url = $"{remoteUrl.TrimEnd('/')}/{sp.Name}.pck";

            if (!localDict.TryGetValue(sp.Name, out var lp))
            {
                // 新包 — 本地没有
                Log.Info("[ProcedureUpdate] 发现新包: {0}", sp.Name);
                toDownload.Add((sp, url));
            }
            else if (lp.Hash != sp.Hash || lp.Size != sp.Size)
            {
                // 已有包但内容变化
                Log.Info("[ProcedureUpdate] 包有更新: {0} (Hash: {1}→{2}, Size: {3}→{4})",
                    sp.Name, lp.Hash, sp.Hash, lp.Size, sp.Size);
                toDownload.Add((sp, url));
            }
            else
            {
                Log.Info("[ProcedureUpdate] 包无变化: {0}", sp.Name);
            }
        }

        return toDownload;
    }

    /// <summary>
    /// 下载所有待更新包，返回成功下载数量。
    /// </summary>
    private static async Task<int> DownloadPacks(List<(Pack Pack, string Url)> packs)
    {
        int downloaded = 0;
        string subpackDir = Path.Combine(ExeDir, ResourceManager.SubPack);

        if (!Directory.Exists(subpackDir))
            Directory.CreateDirectory(subpackDir);

        foreach (var (pack, url) in packs)
        {
            Log.Info("[ProcedureUpdate] 下载中: {0} ← {1}", pack.Name, url);

            var result = await GF.WebRequest.SendRequestAsync(url);
            if (!IsHttpSuccess(result))
            {
                Log.Error("[ProcedureUpdate] 下载失败: {0} (HTTP {1})", pack.Name, result?.ResponseCode);
                continue;
            }

            string savePath = Path.Combine(subpackDir, pack.Name + ".pck");
            try
            {
                await File.WriteAllBytesAsync(savePath, result.Body);
                Log.Info("[ProcedureUpdate] 保存成功: {0} ({1} bytes)", savePath, result.Body.Length);
                downloaded++;
            }
            catch (Exception ex)
            {
                Log.Error("[ProcedureUpdate] 写入文件失败: {0} - {1}", savePath, ex.Message);
            }
        }

        return downloaded;
    }

    /// <summary>
    /// 从 user://subpackages/ 加载已下载的 .pck 子包。
    /// </summary>
    private static void LoadDownloadedPacks(PackVersionList version)
    {
        if (version?.Packs == null || version.Packs.Length == 0)
            return;

        string subpackDir = Path.Combine(ExeDir, ResourceManager.SubPack);
        int loaded = 0;

        foreach (var pack in version.Packs)
        {
            string packPath = Path.Combine(subpackDir, pack.Name + ".pck");
            if (!File.Exists(packPath))
            {
                Log.Warning("[ProcedureUpdate] 子包不存在: {0}", packPath);
                continue;
            }

            if (ProjectSettings.LoadResourcePack(packPath))
            {
                loaded++;
                Log.Info("[ProcedureUpdate] 子包加载成功: {0}", packPath);
            }
            else
            {
                Log.Warning("[ProcedureUpdate] 子包加载失败: {0}", packPath);
            }
        }

        Log.Info("[ProcedureUpdate] 子包加载完成: {0}/{1}", loaded, version.Packs.Length);
    }

    /// <summary>跳过更新，直接进入下一个流程。</summary>
    private void SkipToNext(ProcedureOwner procedureOwner)
    {
        // 如果有本地版本，仍然加载本地包
        var local = GF.Resource.GetPackVersionList();
        if (local != null)
            LoadDownloadedPacks(local);

        ChangeState<ProcedurePrelode>(procedureOwner);
    }

    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }
}
