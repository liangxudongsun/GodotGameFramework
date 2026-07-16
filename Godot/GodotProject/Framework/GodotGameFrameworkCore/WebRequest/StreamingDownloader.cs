using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace GodotGameFramework.Web
{
    /// <summary>
    /// 流式文件下载器。
    /// 边下载边写盘，内存只占一个缓冲区（默认 64KB）。
    /// 支持断点续传（HTTP Range）。
    /// </summary>
    public static class StreamingDownloader
    {
        private const int BufferSize = 65536; // 64KB

        private static System.Net.Http.HttpClient s_Client;

        private static System.Net.Http.HttpClient Client =>
            s_Client ??= new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        /// <summary>
        /// 流式下载文件到磁盘。
        /// </summary>
        /// <param name="url">下载地址</param>
        /// <param name="savePath">最终保存路径</param>
        /// <param name="expectedSize">期望文件大小（来自版本清单，用于进度和校验）</param>
        /// <param name="expectedHash">期望 SHA256 hex（非空时下载后校验）</param>
        /// <param name="allowResume">是否支持断点续传</param>
        /// <param name="onProgress">进度回调 (已下载字节, 总字节)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>true = 下载成功且校验通过</returns>
        public static async Task<bool> DownloadFileAsync(
            string url,
            string savePath,
            long expectedSize,
            string expectedHash = null,
            bool allowResume = true,
            Action<long, long> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            string tempPath = savePath + ".tmp";
            long existingBytes = 0;

            try
            {
                // 断点续传：检查已存在的 .tmp
                if (allowResume && File.Exists(tempPath))
                {
                    var tempInfo = new FileInfo(tempPath);
                    if (tempInfo.Length > 0 && tempInfo.Length < expectedSize)
                    {
                        existingBytes = tempInfo.Length;
                        GD.Print($"[StreamingDownloader] 断点续传: {existingBytes}/{expectedSize} bytes");
                    }
                    else
                    {
                        // 文件已完成或损坏 → 重来
                        TryDelete(tempPath);
                        existingBytes = 0;
                    }
                }
                else
                {
                    TryDelete(tempPath);
                }

                // 发起 HTTP 请求
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existingBytes > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(existingBytes, null);
                }

                using var response = await Client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                // 检查响应
                if (existingBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    // 206 = 断点续传正常
                }
                else if (existingBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // 服务器不支持 Range → 从头下载
                    existingBytes = 0;
                    TryDelete(tempPath);
                }
                else if (!response.IsSuccessStatusCode)
                {
                    GD.PrintErr($"[StreamingDownloader] HTTP {(int)response.StatusCode}: {url}");
                    return false;
                }

                long totalSize = existingBytes +
                    (response.Content.Headers.ContentLength ?? expectedSize - existingBytes);

                // 流式写入磁盘（FileShare.Read 避免杀毒软件/重试时的文件锁冲突）
                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = OpenFileForWrite(tempPath, existingBytes > 0);

                byte[] buffer = new byte[BufferSize];
                long totalRead = existingBytes;
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(
                    buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;
                    onProgress?.Invoke(totalRead, totalSize);
                }

                await fileStream.FlushAsync(cancellationToken);
                // 强制关闭流，防止后续重试时文件被锁
                fileStream.Close();

                // 大小校验
                if (totalRead != expectedSize)
                {
                    GD.PrintErr($"[StreamingDownloader] 文件大小不匹配: 期望 {expectedSize}, 实际 {totalRead}");
                    TryDelete(tempPath);
                    return false;
                }

                // SHA256 校验
                if (!string.IsNullOrEmpty(expectedHash))
                {
                    string actualHash = ComputeSHA256(tempPath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        GD.PrintErr($"[StreamingDownloader] SHA256 校验失败");
                        TryDelete(tempPath);
                        return false;
                    }
                }

                // 原子重命名
                TryDelete(savePath);
                File.Move(tempPath, savePath);
                return true;
            }
            catch (OperationCanceledException)
            {
                GD.Print("[StreamingDownloader] 下载已取消。");
                return false;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[StreamingDownloader] 下载异常: {ex.Message}");
                return false;
            }
        }

        private static string ComputeSHA256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// 以写入模式打开文件。
        /// 使用 FileShare.Read（而非 None）防止杀毒软件/上次流残留导致锁冲突。
        /// 内置 3 次重试处理瞬时锁（杀毒软件扫描）。
        /// </summary>
        private static FileStream OpenFileForWrite(string path, bool append)
        {
            const int maxAttempts = 3;
            const int retryDelayMs = 100;

            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    return new FileStream(
                        path,
                        append ? System.IO.FileMode.Append : System.IO.FileMode.Create,
                        System.IO.FileAccess.Write,
                        FileShare.Read, // 允许读共享，避免杀软锁冲突
                        BufferSize,
                        useAsync: true);
                }
                catch (IOException) when (i < maxAttempts - 1)
                {
                    // 文件被锁（杀毒/上次流未释放）→ 等一小会重试
                    System.Threading.Thread.Sleep(retryDelayMs * (i + 1));
                }
            }

            // 最后一次尝试，失败直接抛
            return new FileStream(
                path,
                append ? System.IO.FileMode.Append : System.IO.FileMode.Create,
                System.IO.FileAccess.Write,
                FileShare.Read,
                BufferSize,
                useAsync: true);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
