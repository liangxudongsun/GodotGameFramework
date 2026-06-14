//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using Godot;

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
    /// 对应 Unity 版本中的 DefaultLogHelper。
    /// </summary>
    public class DefaultLogHelper : GameFrameworkLog.ILogHelper
    {
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
                    GD.PushWarning(message.ToString());
                    break;

                case GameFrameworkLogLevel.Error:
                    GD.PushError(message.ToString());
                    break;

                case GameFrameworkLogLevel.Fatal:
                    GD.PushError($"[FATAL] {message}");
                    break;

                default:
                    GD.PushError($"[UNKNOWN LOG LEVEL] {message}");
                    break;
            }
        }
    }
}
