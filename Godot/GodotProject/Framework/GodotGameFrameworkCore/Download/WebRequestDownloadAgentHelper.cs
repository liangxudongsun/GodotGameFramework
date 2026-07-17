using GameFramework;
using GameFramework.Download;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace GodotGameFramework.Download
{
    /// <summary>
    /// 基于 .NET HttpClient 的下载代理辅助器。
    /// 使用流式下载，内存仅占一个缓冲区（64KB）。
    /// 支持 HTTP Range 断点续传。
    /// </summary>
    public partial class WebRequestDownloadAgentHelper : DownloadAgentHelperBase
    {
        public override event EventHandler<DownloadAgentHelperUpdateBytesEventArgs> DownloadAgentHelperUpdateBytes;
        public override event EventHandler<DownloadAgentHelperUpdateLengthEventArgs> DownloadAgentHelperUpdateLength;
        public override event EventHandler<DownloadAgentHelperCompleteEventArgs> DownloadAgentHelperComplete;
        public override event EventHandler<DownloadAgentHelperErrorEventArgs> DownloadAgentHelperError;
        private readonly HttpClient m_HttpClient;
        private CancellationTokenSource m_CancellationTokenSource;
        private bool m_Busy = false;
        private const int BufferSize = 65536; // 64KB

        public WebRequestDownloadAgentHelper()
        {
            m_HttpClient = new HttpClient();
            m_HttpClient.Timeout = TimeSpan.FromMilliseconds(System.Threading.Timeout.Infinite); // 由 DownloadManager 的 Timeout 机制控制
        }

        /// <summary>
        /// 通过下载代理辅助器下载指定地址的数据。
        /// </summary>
        /// <param name="downloadUri">下载地址。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void Download(string downloadUri, object userData)
        {
            StartDownload(downloadUri, null, null, userData);
        }

        /// <summary>
        /// 通过下载代理辅助器下载指定地址的数据（断点续传）。
        /// </summary>
        /// <param name="downloadUri">下载地址。</param>
        /// <param name="fromPosition">下载数据起始位置。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void Download(string downloadUri, long fromPosition, object userData)
        {
            StartDownload(downloadUri, fromPosition, null, userData);
        }

        /// <summary>
        /// 通过下载代理辅助器下载指定地址的数据（指定范围）。
        /// </summary>
        /// <param name="downloadUri">下载地址。</param>
        /// <param name="fromPosition">下载数据起始位置。</param>
        /// <param name="toPosition">下载数据结束位置。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void Download(string downloadUri, long fromPosition, long toPosition, object userData)
        {
            StartDownload(downloadUri, fromPosition, toPosition, userData);
        }

        private void StartDownload(string downloadUri, long? fromPosition, long? toPosition, object userData)
        {
            if (m_Busy)
            {
                FireError(true, "Web request download agent helper is busy.");
                return;
            }

            m_Busy = true;
            m_CancellationTokenSource = new CancellationTokenSource();
            _ = DownloadAsync(downloadUri, fromPosition, toPosition, m_CancellationTokenSource.Token);
        }

        private async Task DownloadAsync(string downloadUri, long? fromPosition, long? toPosition, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUri);

                if (fromPosition.HasValue)
                {
                    if (toPosition.HasValue)
                        request.Headers.Range = new RangeHeaderValue(fromPosition.Value, toPosition.Value);
                    else
                        request.Headers.Range = new RangeHeaderValue(fromPosition.Value, null);
                }

                using var response = await m_HttpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // 416：本地 .download 已越界，删除后从头下载；其余错误保留断点文件以便续传重试
                    bool deleteDownloading = response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable;
                    FireError(deleteDownloading, $"HTTP {(int)response.StatusCode}: {downloadUri}");
                    return;
                }

                // 续传请求必须返回 206 Partial Content。服务器不支持 Range 时会返回 200 全量内容，
                // 继续追加写会损坏文件 → 删除 .download 后整个任务从头下载。
                if (fromPosition.HasValue && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                {
                    FireError(true, $"Server does not support Range request, will restart: {downloadUri}");
                    return;
                }

                long totalDownloaded = 0;
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                byte[] buffer = new byte[BufferSize];
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    // Reset() 主动取消后不允许再发任何事件（agent 可能已被回收或复用）
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    // 通知 DownloadAgent 写入磁盘
                    var bytesArgs = DownloadAgentHelperUpdateBytesEventArgs.Create(buffer, 0, bytesRead);
                    DownloadAgentHelperUpdateBytes?.Invoke(this, bytesArgs);
                    ReferencePool.Release(bytesArgs);

                    // 事件处理器内部失败（如写盘异常）会调用 Reset()，此后不允许再发事件
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    // 通知 DownloadAgent 更新下载进度
                    var lengthArgs = DownloadAgentHelperUpdateLengthEventArgs.Create(bytesRead);
                    DownloadAgentHelperUpdateLength?.Invoke(this, lengthArgs);
                    ReferencePool.Release(lengthArgs);

                    totalDownloaded += bytesRead;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 下载完成
                var completeArgs = DownloadAgentHelperCompleteEventArgs.Create(totalDownloaded);
                DownloadAgentHelperComplete?.Invoke(this, completeArgs);
                ReferencePool.Release(completeArgs);
            }
            catch (OperationCanceledException)
            {
                // Reset() 主动取消属于正常终止（超时、移除任务、agent 复用），管理器已处理完毕，不发事件。
            }
            catch (HttpRequestException ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // 网络错误保留 .download 断点文件，重试时续传
                    FireError(false, $"HTTP request error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    FireError(false, $"Download error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 重置下载代理辅助器。
        /// </summary>
        public override void Reset()
        {
            m_Busy = false;
            if (m_CancellationTokenSource != null)
            {
                m_CancellationTokenSource.Cancel();
                m_CancellationTokenSource.Dispose();
                m_CancellationTokenSource = null;
            }
        }

        public override void _ExitTree()
        {
            Reset();
            m_HttpClient.Dispose();
            base._ExitTree();
        }

        private void FireError(bool deleteDownloading, string errorMessage)
        {
            var args = DownloadAgentHelperErrorEventArgs.Create(deleteDownloading, errorMessage);
            DownloadAgentHelperError?.Invoke(this, args);
            ReferencePool.Release(args);
        }
    }
}
