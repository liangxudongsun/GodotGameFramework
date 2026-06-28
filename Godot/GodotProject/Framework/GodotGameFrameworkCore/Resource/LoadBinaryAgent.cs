using System;

namespace GameFramework.Resource
{
    internal sealed class LoadBinaryAgent : ITaskAgent<LoadBinaryTask>
    {
        public LoadBinaryTask Task { get; private set; }
        private string m_LoadingPath;
        private byte[] m_ResultData;
        private string m_Error;

        public void Initialize() { }
        public void Shutdown() { }

        public void Reset()
        {
            m_LoadingPath = null;
            m_ResultData = null;
            m_Error = null;
            if (Task != null) { ReferencePool.Release(Task); Task = null; }
        }

        public StartTaskStatus Start(LoadBinaryTask task)
        {
            Task = task;
            m_LoadingPath = task.Path;
            m_ResultData = null;
            m_Error = null;

            // 后台线程读取文件（用 System.IO 避免 Godot API 线程安全问题）
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (System.IO.File.Exists(m_LoadingPath))
                        m_ResultData = System.IO.File.ReadAllBytes(m_LoadingPath);
                    else
                        m_Error = Utility.Text.Format("File '{0}' does not exist.", m_LoadingPath);
                }
                catch (Exception ex) { m_Error = ex.Message; }
            });

            return StartTaskStatus.CanResume;
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (m_LoadingPath == null || Task == null) return;
            if (m_ResultData == null && m_Error == null) return; // 后台线程未完成

            if (m_Error != null)
            {
                Task.Callbacks.LoadBinaryFailureCallback?.Invoke(
                    Task.Path, LoadResourceStatus.AssetError, m_Error, Task.UserData);
            }
            else
            {
                Task.Callbacks.LoadBinarySuccessCallback?.Invoke(
                    Task.Path, m_ResultData, 0f, Task.UserData);
            }

            Task.Done = true;
            m_LoadingPath = null;
        }
    }
}
