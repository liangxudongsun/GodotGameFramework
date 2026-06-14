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
    /// 基础组件。
    ///
    /// 这是 GGF 框架中最核心的组件，负责：
    /// 1. 初始化各种 Helper（日志、文本格式化、版本信息）
    /// 2. 管理 Godot 引擎的帧率设置
    /// 3. 驱动核心框架的 Update 循环（由 GGFEntry 负责调用）
    /// 4. 管理框架的关闭流程
    ///
    /// 对应 Unity 版本中的 BaseComponent。
    ///
    /// 初始化顺序：
    /// 1. _Ready() 被调用
    /// 2. 初始化 TextHelper（字符串格式化工具）
    /// 3. 初始化 VersionHelper（版本信息）
    /// 4. 初始化 LogHelper（日志系统）
    /// 5. 输出框架版本信息
    /// 6. 设置帧率
    /// </summary>
    public sealed partial class BaseComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 帧率设置。默认 60 帧。
        /// </summary>
        private int m_FrameRate = 60;

        /// <summary>
        /// 游戏速度。
        /// </summary>
        private float m_GameSpeed = 1f;

        /// <summary>
        /// 框架是否已经关闭。
        /// </summary>
        private bool m_Shutdown = false;

        /// <summary>
        /// 暂停前保存的游戏速度。
        /// </summary>
        private float m_GameSpeedBeforePause = 1f;

        /// <summary>
        /// 获取或设置游戏帧率。
        /// 直接映射到 Godot 的 Engine.MaxFps。
        /// </summary>
        public int FrameRate
        {
            get => m_FrameRate;
            set
            {
                m_FrameRate = value;
                Engine.MaxFps = value;
            }
        }

        /// <summary>
        /// 获取或设置游戏速度。
        /// 对齐 UGF 的 GameSpeed 实现，维护 m_GameSpeed 备份字段避免浮点精度问题。
        /// 对应 Unity 的 Time.timeScale。
        /// </summary>
        public float GameSpeed
        {
            get
            {
                return m_GameSpeed;
            }
            set
            {
                Engine.TimeScale = m_GameSpeed = value >= 0f ? value : 0f;
            }
        }

        /// <summary>
        /// 获取游戏是否暂停。
        /// </summary>
        public bool IsGamePaused
        {
            get
            {
                return m_GameSpeed <= 0f;
            }
        }

        /// <summary>
        /// 获取是否正常游戏速度。
        /// </summary>
        public bool IsNormalGameSpeed
        {
            get
            {
                return m_GameSpeed == 1f;
            }
        }

        /// <summary>
        /// 暂停游戏。
        /// </summary>
        public void PauseGame()
        {
            if (IsGamePaused)
            {
                return;
            }

            m_GameSpeedBeforePause = GameSpeed;
            GameSpeed = 0f;
        }

        /// <summary>
        /// 恢复游戏。
        /// </summary>
        public void ResumeGame()
        {
            if (!IsGamePaused)
            {
                return;
            }

            GameSpeed = m_GameSpeedBeforePause;
        }

        /// <summary>
        /// 重置为正常游戏速度。
        /// </summary>
        public void ResetNormalGameSpeed()
        {
            if (IsNormalGameSpeed)
            {
                return;
            }

            GameSpeed = 1f;
        }

        /// <summary>
        /// 节点初始化回调。
        /// 在这里完成框架的初始化工作。
        ///
        /// 注意：由于 GGFComponent._Ready() 会先被调用（基类），
        /// 所以组件已经注册到 GGFEntry 中了。
        /// </summary>
        public override void _Ready()
        {
            // 先调用基类的 _Ready，完成组件注册
            base._Ready();

            // 按顺序初始化各个 Helper
            // 注意：LogHelper 必须最后初始化，因为前面的初始化可能需要日志输出
            InitTextHelper();
            InitVersionHelper();
            InitLogHelper();

            // 输出框架版本信息
            Log.Info("Game Framework Version: {0}", GameFramework.Version.GameFrameworkVersion);
            Log.Info("Game Version: {0} ({1})", GameFramework.Version.GameVersion,
                GameFramework.Version.InternalGameVersion);
            Log.Info("Godot Engine Version: {0}", Engine.GetVersionInfo()["string"].AsString());

            // 设置帧率和游戏速度
            Engine.MaxFps = m_FrameRate;
            Engine.TimeScale = m_GameSpeed;
        }

        /// <summary>
        /// 节点被销毁时调用。
        /// 触发核心框架的 Shutdown 流程。
        /// </summary>
        public override void _Notification(int what)
        {
            // NOTIFICATION_PREDELETE 在节点即将被删除前发送
            if (what == NotificationPredelete)
            {
                Shutdown();
            }
        }

        /// <summary>
        /// 执行框架关闭。
        /// 清理核心框架中的所有模块，释放资源。
        /// </summary>
        internal void Shutdown()
        {
            if (m_Shutdown)
            {
                return;
            }

            m_Shutdown = true;
            Log.Info("Shutdown Game Framework...");
            GameFrameworkEntry.Shutdown();
        }

        /// <summary>
        /// 初始化文本格式化 Helper。
        ///
        /// TextHelper 用于优化 string.Format 的性能，
        /// 通过 StringBuilder 缓存减少内存分配。
        /// 如果不设置，核心框架会使用默认的 string.Format。
        /// </summary>
        private void InitTextHelper()
        {
            try
            {
                Utility.Text.SetTextHelper(new DefaultTextHelper());
            }
            catch (Exception exception)
            {
                Log.Error("Can not create text helper instance with exception '{0}'.", exception);
            }
        }

        /// <summary>
        /// 初始化版本信息 Helper。
        ///
        /// VersionHelper 提供游戏版本号信息。
        /// 在 Godot 中，版本号可以从项目设置中读取。
        /// </summary>
        private void InitVersionHelper()
        {
            try
            {
                GameFramework.Version.SetVersionHelper(new DefaultVersionHelper());
            }
            catch (Exception exception)
            {
                Log.Fatal("Can not create version helper instance with exception '{0}'.", exception);
            }
        }

        /// <summary>
        /// 初始化日志 Helper。
        ///
        /// LogHelper 将框架的日志输出桥接到 Godot 的 GD.Print 系统。
        /// 支持 Debug/Info/Warning/Error/Fatal 五个级别。
        /// </summary>
        private void InitLogHelper()
        {
            try
            {
                GameFrameworkLog.SetLogHelper(new DefaultLogHelper());
            }
            catch (Exception exception)
            {
                Log.Fatal("Can not create log helper instance with exception '{0}'.", exception);
            }
        }
    }
}
