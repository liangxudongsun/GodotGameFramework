using GameFramework;
using GameFramework.Download;
using Godot;
using GodotGameFramework.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GodotGameFramework.Download
{
    /// <summary>
    /// 下载组件。
    /// 封装 GameFramework 的 IDownloadManager，提供 Godot 环境下的下载功能。
    /// </summary>
    public partial class DownloadComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 下载任务。
        /// </summary>
        private sealed class DownloadFileOperation
        {
            public TaskCompletionSource<bool> Tcs;
            public long ExpectedSize;
            public string ExpectedHash;
            public Action<long, long> OnProgress;
            public CancellationTokenRegistration Ctr;
        }
        private const int DefaultPriority = 0;
        private const int OneMegaBytes = 1024 * 1024;

        private IDownloadManager m_DownloadManager;
        private EventComponent m_EventComponent;

        [Export]
        private Node m_InstanceRoot;

        [Export]
        private string m_DownloadAgentHelperTypeName = "GodotGameFramework.Download.WebRequestDownloadAgentHelper";
        private DownloadAgentHelperBase m_DownloadAgentHelper = null;

        [Export]
        private int m_DownloadAgentHelperCount = 3;

        [Export]
        private float m_Timeout = 30f;

        [Export]
        private int m_FlushSize = OneMegaBytes;
        /// <summary>
        /// 获取或设置下载是否被暂停。
        /// </summary>
        public bool Paused
        {
            get => m_DownloadManager.Paused;
            set => m_DownloadManager.Paused = value;
        }

        /// <summary>
        /// 获取下载代理总数量。
        /// </summary>
        public int TotalAgentCount => m_DownloadManager.TotalAgentCount;

        /// <summary>
        /// 获取可用下载代理数量。
        /// </summary>
        public int FreeAgentCount => m_DownloadManager.FreeAgentCount;

        /// <summary>
        /// 获取工作中下载代理数量。
        /// </summary>
        public int WorkingAgentCount => m_DownloadManager.WorkingAgentCount;

        /// <summary>
        /// 获取等待下载任务数量。
        /// </summary>
        public int WaitingTaskCount => m_DownloadManager.WaitingTaskCount;

        /// <summary>
        /// 获取或设置下载超时时长，以秒为单位。
        /// </summary>
        public float Timeout
        {
            get => m_DownloadManager.Timeout;
            set
            {
                m_Timeout = value;
                m_DownloadManager.Timeout = value;
            }
        }

        /// <summary>
        /// 获取或设置将缓冲区写入磁盘的临界大小，仅当开启断点续传时有效。
        /// </summary>
        public int FlushSize
        {
            get => m_DownloadManager.FlushSize;
            set
            {
                m_FlushSize = value;
                m_DownloadManager.FlushSize = value;
            }
        }

        /// <summary>
        /// 获取当前下载速度。
        /// </summary>
        public float CurrentSpeed => m_DownloadManager.CurrentSpeed;


        private readonly Dictionary<int, DownloadFileOperation> m_FileOperations = new();

        public override void OnInit()
        {
            base.OnInit();
            m_DownloadManager = GameFrameworkEntry.GetModule<IDownloadManager>();
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            if (m_InstanceRoot == null)
            {
                m_InstanceRoot = new Node();
                m_InstanceRoot.Name = "InstanceRoot";
                AddChild(m_InstanceRoot);
            }
            // 设置参数
            m_DownloadManager.DownloadStart += OnDownloadStart;
            m_DownloadManager.DownloadUpdate += OnDownloadUpdate;
            m_DownloadManager.DownloadSuccess += OnDownloadSuccess;
            m_DownloadManager.DownloadFailure += OnDownloadFailure;
            m_DownloadManager.Timeout = m_Timeout;
            m_DownloadManager.FlushSize = m_FlushSize;

            // 创建下载代理辅助器
            for (int i = 0; i < m_DownloadAgentHelperCount; i++)
            {
                if (Create(m_DownloadAgentHelperTypeName) is DownloadAgentHelperBase helper)
                {
                    // Godot 节点名不允许 '.' 等字符，不能直接用完整类型名
                    helper.Name = helper.GetType().Name + i;
                    m_InstanceRoot.AddChild(helper);
                    m_DownloadManager.AddDownloadAgentHelper(helper);
                }
            }

            Log.Info("[DownloadComponent] Initialized. Agent count: {0}, Timeout: {1}s, FlushSize: {2}",
                m_DownloadAgentHelperCount, m_Timeout, m_FlushSize);
        }



        public override void OnExitTree()
        {
            // 未完成的 awaiter 全部按失败完结（RemoveAllDownloads 不触发事件）
            foreach (var op in m_FileOperations.Values)
            {
                op.Ctr.Dispose();
                op.Tcs.TrySetResult(false);
            }
            m_FileOperations.Clear();

            if (m_DownloadManager != null)
            {
                m_DownloadManager.RemoveAllDownloads();
                m_DownloadManager.DownloadStart -= OnDownloadStart;
                m_DownloadManager.DownloadUpdate -= OnDownloadUpdate;
                m_DownloadManager.DownloadSuccess -= OnDownloadSuccess;
                m_DownloadManager.DownloadFailure -= OnDownloadFailure;
                m_DownloadManager = null;
            }
            base.OnExitTree();
        }



        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="tag">下载任务的标签。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, string tag)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, tag);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="priority">下载任务的优先级。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, int priority)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, priority);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, object userData)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, userData);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="tag">下载任务的标签。</param>
        /// <param name="priority">下载任务的优先级。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, string tag, int priority)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, tag, priority);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="tag">下载任务的标签。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, string tag, object userData)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, tag, userData);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="priority">下载任务的优先级。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, int priority, object userData)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, priority, userData);
        }

        /// <summary>
        /// 增加下载任务。
        /// </summary>
        /// <param name="downloadPath">下载后存放路径。</param>
        /// <param name="downloadUri">原始下载地址。</param>
        /// <param name="tag">下载任务的标签。</param>
        /// <param name="priority">下载任务的优先级。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>新增下载任务的序列编号。</returns>
        public int AddDownload(string downloadPath, string downloadUri, string tag, int priority, object userData)
        {
            return m_DownloadManager.AddDownload(EasySave.GlobalizeDownloadPath(downloadPath), downloadUri, tag, priority, userData);
        }

        /// <summary>
        /// 根据下载任务的序列编号移除下载任务。
        /// </summary>
        /// <param name="serialId">要移除下载任务的序列编号。</param>
        /// <returns>是否移除下载任务成功。</returns>
        public bool RemoveDownload(int serialId)
        {
            return m_DownloadManager.RemoveDownload(serialId);
        }

        /// <summary>
        /// 根据下载任务的标签移除下载任务。
        /// </summary>
        /// <param name="tag">要移除下载任务的标签。</param>
        /// <returns>移除下载任务的数量。</returns>
        public int RemoveDownloads(string tag)
        {
            return m_DownloadManager.RemoveDownloads(tag);
        }

        /// <summary>
        /// 移除所有下载任务。
        /// </summary>
        /// <returns>移除下载任务的数量。</returns>
        public int RemoveAllDownloads()
        {
            return m_DownloadManager.RemoveAllDownloads();
        }

        /// <summary>
        /// 根据下载任务的序列编号获取下载任务的信息。
        /// </summary>
        /// <param name="serialId">要获取信息的下载任务的序列编号。</param>
        /// <returns>下载任务的信息。</returns>
        public TaskInfo GetDownloadInfo(int serialId)
        {
            return m_DownloadManager.GetDownloadInfo(serialId);
        }

        /// <summary>
        /// 根据下载任务的标签获取下载任务的信息。
        /// </summary>
        /// <param name="tag">要获取信息的下载任务的标签。</param>
        /// <returns>下载任务的信息。</returns>
        public TaskInfo[] GetDownloadInfos(string tag)
        {
            return m_DownloadManager.GetDownloadInfos(tag);
        }

        /// <summary>
        /// 获取所有下载任务的信息。
        /// </summary>
        /// <returns>所有下载任务的信息。</returns>
        public TaskInfo[] GetAllDownloadInfos()
        {
            return m_DownloadManager.GetAllDownloadInfos();
        }

        /// <summary>
        /// 下载文件到磁盘并等待结果
        /// </summary>
        /// <param name="downloadUri"></param>
        /// <param name="downloadPath"></param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        public Task<bool> DownloadFileAsync(string downloadUri, string downloadPath, Action<long, long> onProgress = null)
        {
            return DownloadFileAsync(downloadUri, downloadPath, 0L, null, onProgress);
        }
        /// <summary>
        /// 下载文件到磁盘并等待结果（含大小 / SHA256 校验）。
        /// 任何失败 / 取消都返回 false
        /// 断点续传由 .download 机制自动生效：失败保留断点文件，重试自动续传。
        /// </summary>
        /// <param name="downloadUri">下载地址。</param>
        /// <param name="downloadPath">下载后存放路径（支持 user:// 等 Godot 虚拟路径）。</param>
        /// <param name="expectedSize">期望文件大小（大于 0 时校验，通常来自版本清单）。</param>
        /// <param name="expectedHash">期望 SHA256 hex（非空时校验）。</param>
        /// <param name="onProgress">进度回调 (已下载字节, 总字节)，主线程调用。</param>
        /// <param name="cancellationToken">取消令牌，取消后移除下载任务并返回 false。</param>
        /// <returns>true = 下载成功且校验通过。</returns>
        public Task<bool> DownloadFileAsync(string downloadUri, string downloadPath, long expectedSize = 0L, string expectedHash = null, Action<long, long> onProgress = null, CancellationToken cancellationToken = default)
        {
            var op = new DownloadFileOperation
            {
                Tcs = new TaskCompletionSource<bool>(),
                ExpectedSize = expectedSize,
                ExpectedHash = expectedHash,
                OnProgress = onProgress,
            };

            int serialId = AddDownload(downloadPath, downloadUri);
            m_FileOperations.Add(serialId, op);

            if (cancellationToken.CanBeCanceled)
            {
                op.Ctr = cancellationToken.Register(() =>
                {
                    // 回调可能来自任意线程：先完结 TCS，RemoveDownload 派发回主线程。
                    // TaskPool 移除任务不会触发 Failure 事件，必须在这里自己完结。
                    if (op.Tcs.TrySetResult(false))
                    {
                        Callable.From(() =>
                        {
                            m_FileOperations.Remove(serialId);
                            m_DownloadManager?.RemoveDownload(serialId);
                        }).CallDeferred();
                    }
                });
            }

            return op.Tcs.Task;
        }
        /// <summary>
        /// 完成时验证下载任务
        /// </summary>
        /// <param name="op"></param>
        /// <param name="downloadPath"></param>
        /// <param name="currentLength"></param>
        /// <returns></returns>
        private async Task VerifyAndCompleteAsync(DownloadFileOperation op, string downloadPath, long currentLength)
        {
            try
            {
                if (op.ExpectedSize > 0L && currentLength != op.ExpectedSize)
                {
                    Log.Warning("[DownloadComponent] 文件大小不匹配: '{0}' 期望 {1}, 实际 {2}",
                        downloadPath, op.ExpectedSize, currentLength);
                    EasySave.TryDelete(downloadPath);
                    op.Tcs.TrySetResult(false);
                    return;
                }

                if (!string.IsNullOrEmpty(op.ExpectedHash))
                {
                    // 大文件哈希放线程池，避免卡主线程
                    string actualHash = await Task.Run(() => EasySave.ComputeSHA256(downloadPath));
                    if (!string.Equals(actualHash, op.ExpectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warning("[DownloadComponent] SHA256 校验失败: '{0}'", downloadPath);
                        EasySave.TryDelete(downloadPath);
                        op.Tcs.TrySetResult(false);
                        return;
                    }
                }

                op.Tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Log.Warning("[DownloadComponent] 下载校验异常: '{0}' — {1}", downloadPath, ex.Message);
                EasySave.TryDelete(downloadPath);
                op.Tcs.TrySetResult(false);
            }
        }





        private void OnDownloadFailure(object sender, GameFramework.Download.DownloadFailureEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadFailureEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.ErrorMessage, e.UserData));
            if (m_FileOperations.TryGetValue(e.SerialId, out var op))
            {
                m_FileOperations.Remove(e.SerialId);
                op.Ctr.Dispose();
                op.Tcs.TrySetResult(false);
            }
        }


        private void OnDownloadSuccess(object sender, GameFramework.Download.DownloadSuccessEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadSuccessEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.CurrentLength, e.UserData));
            if (m_FileOperations.TryGetValue(e.SerialId, out var op))
            {
                m_FileOperations.Remove(e.SerialId);
                op.Ctr.Dispose();
                // 事件 args 是池化对象，异步校验前把所需字段作为参数传出（不可持有 e）
                _ = VerifyAndCompleteAsync(op, e.DownloadPath, e.CurrentLength);
            }
        }


        private void OnDownloadUpdate(object sender, GameFramework.Download.DownloadUpdateEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadUpdateEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.CurrentLength, e.UserData));
            if (m_FileOperations.TryGetValue(e.SerialId, out var op))
            {
                op.OnProgress?.Invoke(e.CurrentLength, op.ExpectedSize > 0L ? op.ExpectedSize : e.CurrentLength);
            }
        }


        private void OnDownloadStart(object sender, GameFramework.Download.DownloadStartEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadStartEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.CurrentLength, e.UserData));
        }
    }
}
