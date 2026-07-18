namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 帧率计数器（对齐 UGF FpsCounter）。
    /// </summary>
    private sealed class FpsCounter
    {
        private float m_UpdateInterval;
        private float m_CurrentFps;
        private int m_Frames;
        private float m_Accumulator;
        private float m_TimeLeft;

        public FpsCounter(float updateInterval)
        {
            if (updateInterval <= 0f)
            {
                updateInterval = 0.5f;
            }

            m_UpdateInterval = updateInterval;
            Reset();
        }

        /// <summary>
        /// 获取或设置刷新间隔（秒）。
        /// </summary>
        public float UpdateInterval
        {
            get => m_UpdateInterval;
            set
            {
                if (value <= 0f)
                {
                    return;
                }

                m_UpdateInterval = value;
                Reset();
            }
        }

        /// <summary>
        /// 获取当前帧率。
        /// </summary>
        public float CurrentFps => m_CurrentFps;

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            m_Frames++;
            m_Accumulator += realElapseSeconds;
            m_TimeLeft -= realElapseSeconds;

            if (m_TimeLeft <= 0f)
            {
                m_CurrentFps = m_Accumulator > 0f ? m_Frames / m_Accumulator : 0f;
                m_Frames = 0;
                m_Accumulator = 0f;
                m_TimeLeft += m_UpdateInterval;
            }
        }

        public void Reset()
        {
            m_CurrentFps = 0f;
            m_Frames = 0;
            m_Accumulator = 0f;
            m_TimeLeft = 0f;
        }
    }
}
