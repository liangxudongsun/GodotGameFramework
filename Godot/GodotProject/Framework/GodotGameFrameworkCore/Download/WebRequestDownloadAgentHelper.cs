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

                if (!response.IsSuccessStatusCode)
                {
                    // 服务器不支持 Range 但返回 200 OK → 从头下载
                    if (fromPosition.HasValue && response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        // 通知 DownloadAgent 放弃续传，从 0 开始（通过删除 .download 文件重新发起任务）
                        FireError(false, $"Server does not support Range request, will restart: {downloadUri}");
                        return;
                    }

                    FireError(false, $"HTTP {(int)response.StatusCode}: {downloadUri}");
                    return;
                }

                long totalDownloaded = 0;
                using var responseStream = await response.Content.ReadAsStreamAsync();
                byte[] buffer = new byte[BufferSize];
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    // 通知 DownloadAgent 写入磁盘
                    var bytesArgs = DownloadAgentHelperUpdateBytesEventArgs.Create(buffer, 0, bytesRead);
                    DownloadAgentHelperUpdateBytes?.Invoke(this, bytesArgs);
                    ReferencePool.Release(bytesArgs);

                    // 通知 DownloadAgent 更新下载进度
                    var lengthArgs = DownloadAgentHelperUpdateLengthEventArgs.Create(bytesRead);
                    DownloadAgentHelperUpdateLength?.Invoke(this, lengthArgs);
                    ReferencePool.Release(lengthArgs);

                    totalDownloaded += bytesRead;
                }

                // 下载完成
                var completeArgs = DownloadAgentHelperCompleteEventArgs.Create(totalDownloaded);
                DownloadAgentHelperComplete?.Invoke(this, completeArgs);
                ReferencePool.Release(completeArgs);
            }
            catch (OperationCanceledException)
            {
                FireError(true, "Download cancelled or timed out.");
            }
            catch (HttpRequestException ex)
            {
                FireError(true, $"HTTP request error: {ex.Message}");
            }
            catch (Exception ex)
            {
                FireError(true, $"Download error: {ex.Message}");
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

        private void FireError(bool deleteDownloading, string errorMessage)
        {
            var args = DownloadAgentHelperErrorEventArgs.Create(deleteDownloading, errorMessage);
            DownloadAgentHelperError?.Invoke(this, args);
            ReferencePool.Release(args);
        }
    }
}
