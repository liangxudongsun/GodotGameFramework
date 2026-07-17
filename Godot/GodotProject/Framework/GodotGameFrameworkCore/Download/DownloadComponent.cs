using GameFramework;
using GameFramework.Download;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework.Download
{
    /// <summary>
    /// 下载组件。
    /// 封装 GameFramework 的 IDownloadManager，提供 Godot 环境下的下载功能。
    /// </summary>
    public partial class DownloadComponent : GameFrameworkComponent
    {
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
                    helper.Name = m_DownloadAgentHelperTypeName + i;
                    m_InstanceRoot.AddChild(helper);
                    m_DownloadManager.AddDownloadAgentHelper(helper);
                }
            }

            Log.Info("[DownloadComponent] Initialized. Agent count: {0}, Timeout: {1}s, FlushSize: {2}",
                m_DownloadAgentHelperCount, m_Timeout, m_FlushSize);
        }



        public override void OnExitTree()
        {
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, tag);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, priority);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, userData);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, tag, priority);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, tag, userData);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, priority, userData);
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
            return m_DownloadManager.AddDownload(downloadPath, downloadUri, tag, priority, userData);
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


        private void OnDownloadFailure(object sender, GameFramework.Download.DownloadFailureEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadFailureEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.ErrorMessage, e.UserData));
        }


        private void OnDownloadSuccess(object sender, GameFramework.Download.DownloadSuccessEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadSuccessEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.CurrentLength, e.UserData));
        }


        private void OnDownloadUpdate(object sender, GameFramework.Download.DownloadUpdateEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadUpdateEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.CurrentLength, e.UserData));
        }


        private void OnDownloadStart(object sender, GameFramework.Download.DownloadStartEventArgs e)
        {
            m_EventComponent.Fire(this, DownloadStartEventArgs.Create(e.SerialId, e.DownloadPath, e.DownloadUri, e.CurrentLength, e.UserData));
        }
    }
}
