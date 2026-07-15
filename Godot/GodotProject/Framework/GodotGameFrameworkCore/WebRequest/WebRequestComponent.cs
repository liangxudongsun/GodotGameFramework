using GameFramework;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFramework.Web;

/// <summary>
/// Web 请求组件，提供基于 Godot HttpRequest 的 HTTP 通信能力。
/// 支持同步 fire-and-forget 和异步 Task 两种调用方式，
/// 结果同时通过 EventComponent 事件和 Task 返回。
/// </summary>
public partial class WebRequestComponent : GameFrameworkComponent
{
    /// <summary>
    /// 默认请求超时时间（秒）。
    /// </summary>
    private const float DefaultTimeout = 30f;
    private sealed class PendingRequest
    {
        public string Url;
        public TaskCompletionSource<WebRequestCompleteEventArgs> Tcs;
        public float Timeout;
        public double Elapsed;
    }

    private readonly Dictionary<WebRequestAgent, PendingRequest> m_PendingRequests = new();
    private EventComponent m_EventComponent;

    public override void OnInit()
    {
        base.OnInit();
        m_EventComponent = GameEntry.GetComponent<EventComponent>();
    }

    /// <summary>
    /// 每帧轮询，仅负责超时检测。响应处理完全由信号驱动。
    /// </summary>
    public override void OnUpdate(double delta)
    {
        base.OnUpdate(delta);

        if (m_PendingRequests.Count == 0)
            return;

        List<WebRequestAgent> timedOut = null;

        foreach (var (agent, info) in m_PendingRequests)
        {
            if (info.Timeout <= 0f)
                continue;

            info.Elapsed += delta;
            if (info.Elapsed >= info.Timeout)
            {
                timedOut ??= new List<WebRequestAgent>();
                timedOut.Add(agent);
            }
        }

        if (timedOut == null)
            return;

        foreach (var agent in timedOut)
        {
            if (!m_PendingRequests.TryGetValue(agent, out var info))
                continue;

            m_PendingRequests.Remove(agent);

            // 取消底层 HTTP 请求
            agent.CancelRequest();

            Log.Warning("[WebRequestComponent] Request timeout: {0}", info.Url);

            // 超时约定：Result = -1, ResponseCode = 0
            var pooledArgs = WebRequestCompleteEventArgs.Create(info.Url, -1, 0, null, null);
            m_EventComponent?.Fire(this, pooledArgs);

            if (info.Tcs != null)
            {
                var tcsArgs = new WebRequestCompleteEventArgs(info.Url, -1, 0, null, null);
                info.Tcs.TrySetResult(tcsArgs);
            }

            agent.QueueFree();
        }
    }

    // ──────────────────────────────────────
    //  事件驱动 API（fire-and-forget，结果通过 EventComponent 获取）
    // ──────────────────────────────────────

    /// <summary>
    /// 发送一个 GET 请求，结果通过 EventComponent 的 WebRequestCompleteEventArgs 事件获取。
    /// 适合多请求、集中处理的场景。
    /// </summary>
    /// <param name="url">请求地址。</param>
    public void SendRequest(string url)
    {
        if (!ValidateUrl(url))
            return;
        CreateAndSend(url, postData: null, DefaultTimeout, tcs: null);
    }

    /// <summary>
    /// 发送一个 GET 请求，指定超时时间。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="timeout">超时时间（秒），0 或负数表示不超时。</param>
    public void SendRequest(string url, float timeout)
    {
        if (!ValidateUrl(url))
            return;
        CreateAndSend(url, postData: null, timeout, tcs: null);
    }

    // ──────────────────────────────────────
    //  异步 API（返回 Task，结果也同时通过事件推送）
    // ──────────────────────────────────────

    /// <summary>
    /// 发送一个 GET 请求，异步等待结果。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <returns>包含响应数据的结果。</returns>
    public Task<WebRequestCompleteEventArgs> SendRequestAsync(string url)
    {
        return SendRequestAsync(url, DefaultTimeout);
    }

    /// <summary>
    /// 发送一个 GET 请求，异步等待结果，指定超时时间。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="timeout">超时时间（秒），0 或负数表示不超时。</param>
    /// <returns>包含响应数据的结果。</returns>
    public Task<WebRequestCompleteEventArgs> SendRequestAsync(string url, float timeout)
    {
        if (!ValidateUrl(url))
            return Task.FromResult<WebRequestCompleteEventArgs>(null);

        var tcs = new TaskCompletionSource<WebRequestCompleteEventArgs>();
        CreateAndSend(url, postData: null, timeout, tcs);
        return tcs.Task;
    }

    /// <summary>
    /// 发送一个 POST 请求，异步等待结果。
    /// </summary>
    /// <param name="url">请求地址。</param>
    /// <param name="postData">POST 请求体数据。</param>
    /// <param name="timeout">超时时间（秒），0 或负数表示不超时。</param>
    /// <returns>包含响应数据的结果。</returns>
    public Task<WebRequestCompleteEventArgs> SendRequestAsync(string url, byte[] postData, float timeout = DefaultTimeout)
    {
        if (!ValidateUrl(url))
            return Task.FromResult<WebRequestCompleteEventArgs>(null);
        if (postData == null)
        {
            Log.Error("[WebRequestComponent] SendRequestAsync: postData is null.");
            return Task.FromResult<WebRequestCompleteEventArgs>(null);
        }

        var tcs = new TaskCompletionSource<WebRequestCompleteEventArgs>();
        string body = System.Text.Encoding.UTF8.GetString(postData);
        CreateAndSend(url, body, timeout, tcs);
        return tcs.Task;
    }

    /// <summary>
    /// 请求创建与发送
    /// </summary>
    private void CreateAndSend(string url, string postData, float timeout, TaskCompletionSource<WebRequestCompleteEventArgs> tcs)
    {
        var agent = new WebRequestAgent();
        AddChild(agent);

        // 防止信号重复触发（belt-and-suspenders）
        bool completed = false;

        agent.RequestCompleted += (result, responseCode, headers, body) =>
        {
            if (completed)
                return;
            completed = true;

            m_PendingRequests.Remove(agent);

            var pooledArgs = WebRequestCompleteEventArgs.Create(url, result, responseCode, headers, body);
            m_EventComponent?.Fire(this, pooledArgs);

            if (tcs != null)
            {
                var tcsArgs = new WebRequestCompleteEventArgs(url, result, responseCode, headers, body);
                tcs.TrySetResult(tcsArgs);
            }

            Log.Info("[WebRequestComponent] Request completed: {0}, response code: {1}", url, responseCode);

            agent.QueueFree();
        };

        // 注册到追踪字典，用于超时检测
        m_PendingRequests[agent] = new PendingRequest
        {
            Url = url,
            Tcs = tcs,
            Timeout = timeout > 0f ? timeout : 0f,
            Elapsed = 0.0
        };

        // 发起 HTTP 请求
        Error reqError;
        if (postData != null)
            reqError = agent.Request(url, null, HttpClient.Method.Post, postData);
        else
            reqError = agent.Request(url);

        if (reqError != Error.Ok)
        {
            Log.Error("[WebRequestComponent] Failed to initiate request to {0}: {1}", url, reqError);

            m_PendingRequests.Remove(agent);

            var failArgs = new WebRequestCompleteEventArgs(url, (long)reqError, 0, null, null);
            m_EventComponent?.Fire(this, failArgs);
            tcs?.TrySetResult(failArgs);

            agent.QueueFree();
        }
        else
        {
            Log.Info("[WebRequestComponent] Request sent: {0}", url);
        }
    }

    private static bool ValidateUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Log.Error("[WebRequestComponent] SendRequest: url is null or empty.");
            return false;
        }
        return true;
    }
}
