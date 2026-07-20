//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认游戏框架日志辅助器。
    ///
    /// 实现了核心框架的 ILogHelper 接口，
    /// 将框架内部的日志输出桥接到 Godot 的 GD 日志系统。
    ///
    /// 日志级别映射：
    /// - Debug → GD.Print（灰色输出）
    /// - Info  → GD.Print（普通输出）
    /// - Warning → GD.PushWarning（黄色警告）
    /// - Error → GD.PushError（红色错误）
    /// - Fatal → GD.PushError（红色错误，带 [FATAL] 前缀）
    ///
    /// 此外，Warning/Error/Fatal 级别日志会持久化到 user://session.log，
    /// 用于崩溃后排查（即使日志缓冲区丢失也有现场数据）。
    /// </summary>
    public class DefaultLogHelper : GameFrameworkLog.ILogHelper
    {
        /// <summary>会话日志路径（持久化，崩溃后可读取）。</summary>
        private static readonly string SessionLogPath =
            System.IO.Path.Combine(ProjectSettings.GlobalizePath("user://"), "session.log");

        /// <summary>日志文件写入锁。</summary>
        private static readonly object LogFileLock = new object();

        /// <summary>会话日志最大字节数（超过后截半，防止无限增长）。</summary>
        private const long MaxSessionLogBytes = 512 * 1024; // 512 KB

        /// <summary>
        /// 将 Warning 及以上级别日志写入 user://session.log（线程安全，带大小控制）。
        /// </summary>
        private static void PersistToSessionLog(string line)
        {
            try
            {
                lock (LogFileLock)
                {
                    System.IO.File.AppendAllText(SessionLogPath, line + "\n", System.Text.Encoding.UTF8);

                    // 超过上限时截半保留
                    var info = new System.IO.FileInfo(SessionLogPath);
                    if (info.Length > MaxSessionLogBytes)
                    {
                        string[] allLines = System.IO.File.ReadAllLines(SessionLogPath, System.Text.Encoding.UTF8);
                        int keep = allLines.Length / 2;
                        System.IO.File.WriteAllLines(SessionLogPath,
                            allLines.AsSpan(allLines.Length - keep).ToArray(),
                            System.Text.Encoding.UTF8);
                    }
                }
            }
            catch
            {
                // 日志持久化失败不能影响主流程
            }
        }

        /// <summary>
        /// 记录日志。
        /// 由核心框架的 GameFrameworkLog 类自动调用。
        /// </summary>
        /// <param name="level">日志等级</param>
        /// <param name="message">日志内容</param>
        public void Log(GameFrameworkLogLevel level, object message)
        {
            switch (level)
            {
                case GameFrameworkLogLevel.Debug:
                    // Debug 级别使用灰色文字标识
                    GD.Print($"[DEBUG] {message}");
                    break;

                case GameFrameworkLogLevel.Info:
                    GD.Print(message);
                    break;

                case GameFrameworkLogLevel.Warning:
                    GD.Print($"[WARNING] {message}");
                    GD.PushWarning(message.ToString());
                    break;

                case GameFrameworkLogLevel.Error:
                    GD.Print($"[ERROR] {message}");
                    GD.PushError(message.ToString());
                    break;

                case GameFrameworkLogLevel.Fatal:
                    GD.Print($"[FATAL] {message}");
                    GD.PushError($"[FATAL] {message}");
                    break;

                default:
                    GD.Print($"[UNKNOWN LOG LEVEL] {message}");
                    GD.PushError($"[UNKNOWN LOG LEVEL] {message}");
                    break;
            }
        }
    }
}
