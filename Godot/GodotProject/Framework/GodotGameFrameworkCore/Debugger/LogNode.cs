using GameFramework;
using System;

namespace GodotGameFramework.Debugger;

/// <summary>
/// 日志记录结点（引用池复用）。
/// </summary>
public sealed class LogNode : IReference
{
    private DateTime m_LogTime;
    private ulong m_LogFrameCount;
    private GameFrameworkLogLevel m_LogLevel;
    private string m_LogMessage;
    private string m_StackTrack;

    /// <summary>
    /// 初始化日志记录结点的新实例。
    /// </summary>
    public LogNode()
    {
        m_LogTime = default;
        m_LogFrameCount = 0;
        m_LogLevel = GameFrameworkLogLevel.Debug;
        m_LogMessage = null;
        m_StackTrack = null;
    }

    /// <summary>
    /// 获取日志时间。
    /// </summary>
    public DateTime LogTime => m_LogTime;

    /// <summary>
    /// 获取日志帧计数。
    /// </summary>
    public ulong LogFrameCount => m_LogFrameCount;

    /// <summary>
    /// 获取日志类型。
    /// </summary>
    public GameFrameworkLogLevel LogLevel => m_LogLevel;

    /// <summary>
    /// 获取日志内容。
    /// </summary>
    public string LogMessage => m_LogMessage;

    /// <summary>
    /// 获取日志堆栈信息。
    /// </summary>
    public string StackTrack => m_StackTrack;

    /// <summary>
    /// 创建日志记录结点。
    /// </summary>
    /// <param name="logLevel">日志类型。</param>
    /// <param name="logMessage">日志内容。</param>
    /// <param name="stackTrack">日志堆栈信息。</param>
    /// <returns>创建的日志记录结点。</returns>
    public static LogNode Create(GameFrameworkLogLevel logLevel, string logMessage, string stackTrack)
    {
        LogNode logNode = ReferencePool.Acquire<LogNode>();
        logNode.m_LogTime = DateTime.Now;
        logNode.m_LogFrameCount = Godot.Engine.GetProcessFrames();
        logNode.m_LogLevel = logLevel;
        logNode.m_LogMessage = logMessage;
        logNode.m_StackTrack = stackTrack;
        return logNode;
    }

    /// <summary>
    /// 清理日志记录结点。
    /// </summary>
    public void Clear()
    {
        m_LogTime = default;
        m_LogFrameCount = 0;
        m_LogLevel = GameFrameworkLogLevel.Debug;
        m_LogMessage = null;
        m_StackTrack = null;
    }
}
