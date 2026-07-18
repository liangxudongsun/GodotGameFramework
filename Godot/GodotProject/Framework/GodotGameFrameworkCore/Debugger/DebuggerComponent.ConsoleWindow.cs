using GameFramework;
using GameFramework.Debugger;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 控制台调试器窗口（对齐 UGF ConsoleWindow）。
    /// 捕获框架日志，支持级别过滤、锁定滚动、行选中查看堆栈与复制。
    /// </summary>
    public sealed class ConsoleWindow : IDebuggerWindow
    {
        private const string SettingLockScroll = "Debugger.Console.LockScroll";
        private const string SettingDebugFilter = "Debugger.Console.DebugFilter";
        private const string SettingInfoFilter = "Debugger.Console.InfoFilter";
        private const string SettingWarningFilter = "Debugger.Console.WarningFilter";
        private const string SettingErrorFilter = "Debugger.Console.ErrorFilter";
        private const string SettingFatalFilter = "Debugger.Console.FatalFilter";

        private readonly Queue<LogNode> m_LogNodes = new Queue<LogNode>();
        private readonly Queue<LogNode> m_PendingLogNodes = new Queue<LogNode>();
        private readonly object m_PendingLock = new object();

        private DebuggerComponent m_Component;
        private LogNode m_SelectedNode;
        private int m_MaxLine = 100;
        private bool m_LockScroll = true;
        private bool m_DebugFilter = true;
        private bool m_InfoFilter = true;
        private bool m_WarningFilter = true;
        private bool m_ErrorFilter = true;
        private bool m_FatalFilter = true;

        /// <summary>
        /// 获取或设置最大日志行数。
        /// </summary>
        public int MaxLine
        {
            get => m_MaxLine;
            set => m_MaxLine = Math.Max(1, value);
        }

        /// <summary>
        /// 获取或设置是否锁定滚动（自动滚动到最新日志）。
        /// </summary>
        public bool LockScroll
        {
            get => m_LockScroll;
            set
            {
                m_LockScroll = value;
                SaveBoolSetting(SettingLockScroll, value);
            }
        }

        public bool DebugFilter
        {
            get => m_DebugFilter;
            set
            {
                m_DebugFilter = value;
                SaveBoolSetting(SettingDebugFilter, value);
            }
        }

        public bool InfoFilter
        {
            get => m_InfoFilter;
            set
            {
                m_InfoFilter = value;
                SaveBoolSetting(SettingInfoFilter, value);
            }
        }

        public bool WarningFilter
        {
            get => m_WarningFilter;
            set
            {
                m_WarningFilter = value;
                SaveBoolSetting(SettingWarningFilter, value);
            }
        }

        public bool ErrorFilter
        {
            get => m_ErrorFilter;
            set
            {
                m_ErrorFilter = value;
                SaveBoolSetting(SettingErrorFilter, value);
            }
        }

        public bool FatalFilter
        {
            get => m_FatalFilter;
            set
            {
                m_FatalFilter = value;
                SaveBoolSetting(SettingFatalFilter, value);
            }
        }

        public void Initialize(params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                m_Component = args[0] as DebuggerComponent;
            }

            m_Component ??= GameEntry.GetComponent<DebuggerComponent>();
            LoadSettings();
            DefaultLogHelper.LogMessageReceived += OnLogMessageReceived;
        }

        public void Shutdown()
        {
            DefaultLogHelper.LogMessageReceived -= OnLogMessageReceived;
            Clear();
        }

        public void OnEnter()
        {
        }

        public void OnLeave()
        {
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            Pump();
        }

        public void OnDraw()
        {
            Pump();
            RefreshCounts(out int debugCount, out int infoCount, out int warningCount, out int errorCount, out int fatalCount);

            DebuggerDraw draw = m_Component.Draw;
            draw.ScrollFollowing = m_LockScroll && m_SelectedNode == null;

            // ---- 工具条 ----
            draw.Button("Clear All", Clear);
            draw.Toggle(m_LockScroll, "Lock Scroll", value => LockScroll = value);
            draw.Toggle(m_DebugFilter, $"Debug ({debugCount})", value => DebugFilter = value);
            draw.Toggle(m_InfoFilter, $"Info ({infoCount})", value => InfoFilter = value);
            draw.Toggle(m_WarningFilter, $"Warning ({warningCount})", value => WarningFilter = value);
            draw.Toggle(m_ErrorFilter, $"Error ({errorCount})", value => ErrorFilter = value);
            draw.Toggle(m_FatalFilter, $"Fatal ({fatalCount})", value => FatalFilter = value);
            draw.NewLine();
            draw.Separator();

            // ---- 日志行 ----
            foreach (LogNode logNode in m_LogNodes)
            {
                if (!IsPassedFilter(logNode.LogLevel))
                {
                    continue;
                }

                bool selected = logNode == m_SelectedNode;
                string line = $"[{logNode.LogTime:HH:mm:ss.fff}][{logNode.LogFrameCount}] {logNode.LogMessage}";
                string inner = Utility.Text.Format(
                    "{0}[color={1}]{2}[/color]{3}",
                    selected ? "[bgcolor=#2A3A50]" : string.Empty,
                    GetLogLevelColor(logNode.LogLevel),
                    DebuggerDraw.Esc(line),
                    selected ? "[/bgcolor]" : string.Empty);

                LogNode capturedNode = logNode;
                draw.Link(inner, () => m_SelectedNode = m_SelectedNode == capturedNode ? null : capturedNode);
                draw.NewLine();
            }

            // ---- 选中详情 ----
            if (m_SelectedNode != null)
            {
                draw.Separator();
                draw.Button("Copy", () =>
                {
                    string stack = string.IsNullOrEmpty(m_SelectedNode?.StackTrack) ? string.Empty : "\n" + m_SelectedNode.StackTrack;
                    DisplayServer.ClipboardSet(m_SelectedNode?.LogMessage + stack);
                });
                draw.Button("Deselect", () => m_SelectedNode = null);
                draw.NewLine();
                draw.AppendRaw(Utility.Text.Format(
                    "[color={0}]{1}[/color]\n",
                    GetLogLevelColor(m_SelectedNode.LogLevel),
                    DebuggerDraw.Esc(m_SelectedNode.LogMessage)));
                if (!string.IsNullOrEmpty(m_SelectedNode.StackTrack))
                {
                    draw.AppendRaw(Utility.Text.Format("[color=#8899AA]{0}[/color]\n", DebuggerDraw.Esc(m_SelectedNode.StackTrack)));
                }
            }
        }

        /// <summary>
        /// 获取各级别日志数量（供 FPS 图标变色等使用）。
        /// </summary>
        public void GetLogCounts(out int debugCount, out int infoCount, out int warningCount, out int errorCount, out int fatalCount)
        {
            Pump();
            RefreshCounts(out debugCount, out infoCount, out warningCount, out errorCount, out fatalCount);
        }

        /// <summary>
        /// 清空全部日志。
        /// </summary>
        public void Clear()
        {
            m_SelectedNode = null;
            lock (m_PendingLock)
            {
                while (m_PendingLogNodes.Count > 0)
                {
                    ReferencePool.Release(m_PendingLogNodes.Dequeue());
                }
            }

            while (m_LogNodes.Count > 0)
            {
                ReferencePool.Release(m_LogNodes.Dequeue());
            }
        }

        private void OnLogMessageReceived(GameFrameworkLogLevel level, string message, string stackTrack)
        {
            // 日志可能来自任意线程，先入暂存队列，主线程再消费
            LogNode logNode = LogNode.Create(level, message, stackTrack);
            lock (m_PendingLock)
            {
                m_PendingLogNodes.Enqueue(logNode);
            }
        }

        private void Pump()
        {
            lock (m_PendingLock)
            {
                while (m_PendingLogNodes.Count > 0)
                {
                    m_LogNodes.Enqueue(m_PendingLogNodes.Dequeue());
                }
            }

            while (m_LogNodes.Count > m_MaxLine)
            {
                LogNode dropped = m_LogNodes.Dequeue();
                if (dropped == m_SelectedNode)
                {
                    m_SelectedNode = null;
                }

                ReferencePool.Release(dropped);
            }
        }

        private void RefreshCounts(out int debugCount, out int infoCount, out int warningCount, out int errorCount, out int fatalCount)
        {
            debugCount = infoCount = warningCount = errorCount = fatalCount = 0;
            foreach (LogNode logNode in m_LogNodes)
            {
                switch (logNode.LogLevel)
                {
                    case GameFrameworkLogLevel.Debug:
                        debugCount++;
                        break;
                    case GameFrameworkLogLevel.Info:
                        infoCount++;
                        break;
                    case GameFrameworkLogLevel.Warning:
                        warningCount++;
                        break;
                    case GameFrameworkLogLevel.Error:
                        errorCount++;
                        break;
                    case GameFrameworkLogLevel.Fatal:
                        fatalCount++;
                        break;
                }
            }
        }

        private bool IsPassedFilter(GameFrameworkLogLevel level)
        {
            return level switch
            {
                GameFrameworkLogLevel.Debug => m_DebugFilter,
                GameFrameworkLogLevel.Info => m_InfoFilter,
                GameFrameworkLogLevel.Warning => m_WarningFilter,
                GameFrameworkLogLevel.Error => m_ErrorFilter,
                GameFrameworkLogLevel.Fatal => m_FatalFilter,
                _ => true,
            };
        }

        private static string GetLogLevelColor(GameFrameworkLogLevel level)
        {
            return level switch
            {
                GameFrameworkLogLevel.Debug => "#888888",
                GameFrameworkLogLevel.Info => "#FFFFFF",
                GameFrameworkLogLevel.Warning => "#FFFF00",
                GameFrameworkLogLevel.Error => "#FF5050",
                GameFrameworkLogLevel.Fatal => "#FF00FF",
                _ => "#FFFFFF",
            };
        }

        private void LoadSettings()
        {
            try
            {
                var setting = GF.Setting;
                if (setting == null)
                {
                    return;
                }

                m_LockScroll = setting.GetBool(SettingLockScroll, true);
                m_DebugFilter = setting.GetBool(SettingDebugFilter, true);
                m_InfoFilter = setting.GetBool(SettingInfoFilter, true);
                m_WarningFilter = setting.GetBool(SettingWarningFilter, true);
                m_ErrorFilter = setting.GetBool(SettingErrorFilter, true);
                m_FatalFilter = setting.GetBool(SettingFatalFilter, true);
            }
            catch (Exception)
            {
                // 设置组件不可用时使用默认值
            }
        }

        private static void SaveBoolSetting(string key, bool value)
        {
            try
            {
                var setting = GF.Setting;
                if (setting == null)
                {
                    return;
                }

                setting.SetBool(key, value);
                setting.Save();
            }
            catch (Exception)
            {
            }
        }
    }
}
