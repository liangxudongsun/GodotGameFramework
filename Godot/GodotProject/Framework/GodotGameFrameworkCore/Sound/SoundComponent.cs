//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Sound;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 声音组件。
    ///
    /// 这是音频管理系统的封装组件，提供通过框架播放/停止/暂停/恢复声音的能力。
    /// 支持声音组管理、优先级抢占、音量/速率/循环控制。
    ///
    /// 架构说明：
    /// GGF 的 SoundComponent 采用与 EntityComponent、UIComponent 相同的策略 — 绕过核心
    /// SoundManager 的 PlaySound 管道（因为核心 SoundManager.PlaySound 内部调用
    /// ResourceManager.LoadAsset，需要版本列表），直接使用 ResourceComponent 加载
    /// AudioStream 资源，自行管理声音生命周期。
    ///
    /// SoundComponent 在内部实现了完整的音频管理功能，包括：
    /// - 声音组管理（Name → SoundGroup，使用 Node 容器）
    /// - 优先级抢占算法（从核心 SoundManager.SoundGroup.PlaySound 方法体级别移植）
    /// - 声音代理管理（每个 Agent 对应一个 AudioStreamPlayer）
    /// - Mute/Volume 级联（组级静音/音量自动刷新所有 Agent）
    /// - 异步加载状态追踪（加载中/待释放）
    ///
    /// 场景树结构：
    /// <code>
    /// GGFEntry
    ///   └── SoundComponent
    ///       └── DefaultSoundGroupHelper "Sound Group - Music"  (2 agents)
    ///           ├── AudioStreamPlayer "Agent 0"
    ///           └── AudioStreamPlayer "Agent 1"
    ///       └── DefaultSoundGroupHelper "Sound Group - SFX"    (8 agents)
    ///       └── DefaultSoundGroupHelper "Sound Group - UI"     (4 agents)
    /// </code>
    ///
    /// 使用方式：
    /// <code>
    /// SoundComponent soundComp = GF.Sound;
    ///
    /// // 播放背景音乐（循环）
    /// int bgmId = soundComp.PlaySound("res://Audio/BGM.mp3", "Music");
    ///
    /// // 播放音效
    /// PlaySoundParams sfxParams = PlaySoundParams.Create();
    /// sfxParams.VolumeInSoundGroup = 0.8f;
    /// soundComp.PlaySound("res://Audio/Click.wav", "SFX", sfxParams);
    ///
    /// // 停止背景音乐（带淡出）
    /// soundComp.StopSound(bgmId, 1f);
    ///
    /// // 暂停所有声音
    /// soundComp.PauseAllLoadedSounds();
    ///
    /// // 恢复所有声音
    /// soundComp.ResumeAllLoadedSounds();
    /// </code>
    ///
    /// 对应 Unity 版本中的 SoundComponent。
    /// </summary>
    public sealed partial class SoundComponent : GameFrameworkComponent
    {
        // ================================================================
        //  内部类型 - 声音代理
        // ================================================================

        /// <summary>
        /// 声音代理（内部类）。
        ///
        /// 封装一个 DefaultSoundAgentHelper，代表一个可播放声音的"槽位"。
        /// 每个声音组包含多个 Agent，同一时间每个 Agent 只能播放一个声音。
        ///
        /// 核心职责：
        /// 1. 追踪当前播放请求的序列编号（SerialId）
        /// 2. 维护组内静音/音量（MuteInSoundGroup / VolumeInSoundGroup）
        /// 3. 委托播放控制给 DefaultSoundAgentHelper
        /// 4. 实现 Mute/Volume 级联计算（组级 × 个体级）
        /// 5. 订阅 ResetSoundAgent 事件，自然播放完成时清除序列编号
        ///
        /// 从核心 SoundManager.SoundAgent 方法体级别移植，但去掉了 ISoundAgent 接口。
        /// </summary>
        private sealed class SoundAgent
        {
            /// <summary>所属的声音组。</summary>
            private readonly SoundGroup m_SoundGroup;

            /// <summary>底层的声音代理辅助器（封装 AudioStreamPlayer）。</summary>
            private readonly DefaultSoundAgentHelper m_Helper;

            /// <summary>当前播放请求的序列编号（0 表示空闲）。</summary>
            private int m_SerialId;

            /// <summary>设置声音资源的时间（用于优先级抢占时选择最早的 Agent）。</summary>
            private DateTime m_SetSoundAssetTime;

            /// <summary>在声音组内是否静音。</summary>
            private bool m_MuteInSoundGroup;

            /// <summary>在声音组内的音量系数。</summary>
            private float m_VolumeInSoundGroup;

            /// <summary>
            /// 初始化声音代理的新实例。
            /// </summary>
            public SoundAgent(SoundGroup soundGroup, DefaultSoundAgentHelper helper)
            {
                m_SoundGroup = soundGroup;
                m_Helper = helper;
                m_Helper.ResetSoundAgent += OnResetSoundAgent;
                m_SerialId = 0;
                Reset();
            }

            /// <summary>获取或设置序列编号。</summary>
            public int SerialId { get { return m_SerialId; } set { m_SerialId = value; } }

            /// <summary>获取当前是否正在播放。</summary>
            public bool IsPlaying { get { return m_Helper.IsPlaying; } }

            /// <summary>获取声音长度。</summary>
            public float Length { get { return m_Helper.Length; } }

            /// <summary>获取或设置播放位置。</summary>
            public float Time { get { return m_Helper.Time; } set { m_Helper.Time = value; } }

            /// <summary>
            /// 获取最终静音状态（组级 OR 个体级）。
            /// 由 RefreshMute() 计算后应用到 Helper。
            /// </summary>
            public bool Mute { get { return m_Helper.Mute; } }

            /// <summary>获取最终音量（组级 × 个体级）。</summary>
            public float Volume { get { return m_Helper.Volume; } }

            /// <summary>
            /// 获取或设置在声音组内是否静音。
            /// 设置后自动调用 RefreshMute() 重新计算最终静音状态。
            /// </summary>
            public bool MuteInSoundGroup
            {
                get { return m_MuteInSoundGroup; }
                set { m_MuteInSoundGroup = value; RefreshMute(); }
            }

            /// <summary>获取或设置是否循环播放。</summary>
            public bool Loop { get { return m_Helper.Loop; } set { m_Helper.Loop = value; } }

            /// <summary>获取或设置优先级。</summary>
            public int Priority { get { return m_Helper.Priority; } set { m_Helper.Priority = value; } }

            /// <summary>
            /// 获取或设置在声音组内的音量系数。
            /// 设置后自动调用 RefreshVolume() 重新计算最终音量。
            /// </summary>
            public float VolumeInSoundGroup
            {
                get { return m_VolumeInSoundGroup; }
                set { m_VolumeInSoundGroup = value; RefreshVolume(); }
            }

            /// <summary>获取或设置音调。</summary>
            public float Pitch { get { return m_Helper.Pitch; } set { m_Helper.Pitch = value; } }

            /// <summary>获取或设置立体声声相。</summary>
            public float PanStereo { get { return m_Helper.PanStereo; } set { m_Helper.PanStereo = value; } }

            /// <summary>获取或设置空间混合量。</summary>
            public float SpatialBlend { get { return m_Helper.SpatialBlend; } set { m_Helper.SpatialBlend = value; } }

            /// <summary>获取或设置最大距离。</summary>
            public float MaxDistance { get { return m_Helper.MaxDistance; } set { m_Helper.MaxDistance = value; } }

            /// <summary>获取或设置多普勒等级。</summary>
            public float DopplerLevel { get { return m_Helper.DopplerLevel; } set { m_Helper.DopplerLevel = value; } }

            /// <summary>获取底层 Helper。</summary>
            public DefaultSoundAgentHelper Helper { get { return m_Helper; } }

            /// <summary>获取设置声音资源的时间（用于优先级抢占排序）。</summary>
            public DateTime SetSoundAssetTime { get { return m_SetSoundAssetTime; } }

            /// <summary>播放声音。</summary>
            public void Play(float fadeInSeconds) { m_Helper.Play(fadeInSeconds); }

            /// <summary>停止播放。</summary>
            public void Stop(float fadeOutSeconds) { m_Helper.Stop(fadeOutSeconds); }

            /// <summary>暂停播放。</summary>
            public void Pause(float fadeOutSeconds) { m_Helper.Pause(fadeOutSeconds); }

            /// <summary>恢复播放。</summary>
            public void Resume(float fadeInSeconds) { m_Helper.Resume(fadeInSeconds); }

            /// <summary>
            /// 设置声音资源。
            /// 先调用 Reset() 清理上一个资源，再设置新资源。
            /// </summary>
            internal bool SetSoundAsset(object soundAsset)
            {
                Reset();
                m_SetSoundAssetTime = DateTime.UtcNow;
                return m_Helper.SetSoundAsset(soundAsset);
            }

            /// <summary>
            /// 刷新静音状态。
            /// 最终静音 = 组级静音 OR 个体级静音。
            /// 从核心 SoundManager.SoundAgent.RefreshMute() 方法体级别移植。
            /// </summary>
            internal void RefreshMute()
            {
                m_Helper.Mute = m_SoundGroup.Mute || m_MuteInSoundGroup;
            }

            /// <summary>
            /// 刷新音量。
            /// 最终音量 = 组级音量 × 个体级音量系数。
            /// 从核心 SoundManager.SoundAgent.RefreshVolume() 方法体级别移植。
            /// </summary>
            internal void RefreshVolume()
            {
                m_Helper.Volume = m_SoundGroup.Volume * m_VolumeInSoundGroup;
            }

            /// <summary>
            /// 重置声音代理到默认状态。
            /// 从核心 SoundManager.SoundAgent.Reset() 方法体级别移植。
            /// </summary>
            public void Reset()
            {
                m_SetSoundAssetTime = DateTime.MinValue;
                m_SerialId = 0;
                Time = Constant.DefaultTime;
                MuteInSoundGroup = Constant.DefaultMute;
                Loop = Constant.DefaultLoop;
                Priority = Constant.DefaultPriority;
                VolumeInSoundGroup = Constant.DefaultVolume;
                Pitch = Constant.DefaultPitch;
                PanStereo = Constant.DefaultPanStereo;
                SpatialBlend = Constant.DefaultSpatialBlend;
                MaxDistance = Constant.DefaultMaxDistance;
                DopplerLevel = Constant.DefaultDopplerLevel;
                m_Helper.Reset();
            }

            /// <summary>
            /// ResetSoundAgent 事件处理。
            /// 当声音自然播放完成时（Helper 的 Finished 信号触发），清除序列编号，
            /// 使该 Agent 空闲可被新声音使用。
            /// </summary>
            private void OnResetSoundAgent(object sender, ResetSoundAgentEventArgs e)
            {
                m_SerialId = 0;
            }
        }

        // ================================================================
        //  内部类型 - 声音组
        // ================================================================

        /// <summary>
        /// 声音组（内部类）。
        ///
        /// 管理一组 SoundAgent，提供优先级抢占算法。
        /// 每个声音组有独立的静音/音量控制，变更时级联刷新所有 Agent。
        ///
        /// 优先级抢占算法（从核心 SoundManager.SoundGroup.PlaySound 方法体级别移植）：
        /// 1. 优先选择空闲的 Agent（未在播放）
        /// 2. 如果所有 Agent 都在播放，选择优先级最低的 Agent 抢占
        /// 3. 如果有相同优先级的 Agent，选择最早设置的（播放时间最长的）
        /// 4. 如果 AvoidBeingReplacedBySamePriority=true，不抢占相同优先级
        /// 5. 如果没有可抢占的 Agent，返回 IgnoredDueToLowPriority
        /// </summary>
        private sealed class SoundGroup
        {
            /// <summary>声音组名称。</summary>
            private readonly string m_Name;

            /// <summary>声音组辅助器（Node 容器）。</summary>
            private readonly DefaultSoundGroupHelper m_Helper;

            /// <summary>该组内的所有声音代理列表。</summary>
            private readonly List<SoundAgent> m_SoundAgents;

            /// <summary>是否避免被同优先级声音替换。</summary>
            private bool m_AvoidBeingReplacedBySamePriority;

            /// <summary>声音组是否静音。</summary>
            private bool m_Mute;

            /// <summary>声音组音量（0-1）。</summary>
            private float m_Volume;

            /// <summary>
            /// 初始化声音组的新实例。
            /// </summary>
            public SoundGroup(string name, DefaultSoundGroupHelper helper, bool avoidBeingReplacedBySamePriority)
            {
                m_Name = name;
                m_Helper = helper;
                m_SoundAgents = new List<SoundAgent>();
                m_AvoidBeingReplacedBySamePriority = avoidBeingReplacedBySamePriority;
                m_Mute = false;
                m_Volume = 1f;
            }

            /// <summary>获取声音组名称。</summary>
            public string Name { get { return m_Name; } }

            /// <summary>获取声音代理数量。</summary>
            public int SoundAgentCount { get { return m_SoundAgents.Count; } }

            /// <summary>获取声音组辅助器。</summary>
            public DefaultSoundGroupHelper Helper { get { return m_Helper; } }

            /// <summary>
            /// 获取或设置是否避免被同优先级声音替换。
            /// </summary>
            public bool AvoidBeingReplacedBySamePriority
            {
                get { return m_AvoidBeingReplacedBySamePriority; }
                set { m_AvoidBeingReplacedBySamePriority = value; }
            }

            /// <summary>
            /// 获取或设置声音组是否静音。
            /// 设置后自动刷新所有 Agent 的静音状态。
            /// 从核心 SoundManager.SoundGroup.Mute setter 方法体级别移植。
            /// </summary>
            public bool Mute
            {
                get { return m_Mute; }
                set
                {
                    m_Mute = value;
                    foreach (SoundAgent soundAgent in m_SoundAgents)
                    {
                        soundAgent.RefreshMute();
                    }
                }
            }

            /// <summary>
            /// 获取或设置声音组音量。
            /// 设置后自动刷新所有 Agent 的音量。
            /// 从核心 SoundManager.SoundGroup.Volume setter 方法体级别移植。
            /// </summary>
            public float Volume
            {
                get { return m_Volume; }
                set
                {
                    m_Volume = value;
                    foreach (SoundAgent soundAgent in m_SoundAgents)
                    {
                        soundAgent.RefreshVolume();
                    }
                }
            }

            /// <summary>
            /// 添加声音代理。
            /// </summary>
            public void AddSoundAgent(DefaultSoundAgentHelper agentHelper)
            {
                m_SoundAgents.Add(new SoundAgent(this, agentHelper));
            }

            /// <summary>
            /// 播放声音（优先级抢占算法）。
            ///
            /// 这是声音系统最核心的方法，决定由哪个 Agent 来播放新的声音。
            /// 从核心 SoundManager.SoundGroup.PlaySound() 方法体级别移植。
            ///
            /// 算法流程：
            /// 1. 遍历所有 Agent，寻找空闲的（未在播放的）→ 立即使用
            /// 2. 如果没有空闲的，寻找优先级更低的 Agent → 候选抢占
            /// 3. 如果有相同优先级的（且允许替换），选择最早设置的 → 候选抢占
            /// 4. 在所有候选中选择优先级最低的（或最早的）→ 抢占
            /// 5. 如果没有候选 → 返回 IgnoredDueToLowPriority
            /// </summary>
            /// <param name="serialId">播放请求的序列编号。</param>
            /// <param name="soundAsset">声音资源（AudioStream）。</param>
            /// <param name="playSoundParams">播放参数。</param>
            /// <param name="errorCode">输出错误码（如果失败）。</param>
            /// <returns>用于播放的 Agent，失败返回 null。</returns>
            public SoundAgent PlaySound(int serialId, object soundAsset, PlaySoundParams playSoundParams, out PlaySoundErrorCode? errorCode)
            {
                errorCode = null;
                SoundAgent candidateAgent = null;

                // 遍历所有 Agent，寻找最佳候选
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (!soundAgent.IsPlaying)
                    {
                        // 找到空闲 Agent，直接使用（最优选择）
                        candidateAgent = soundAgent;
                        break;
                    }

                    if (soundAgent.Priority < playSoundParams.Priority)
                    {
                        // 当前 Agent 优先级更低，可以作为抢占候选
                        if (candidateAgent == null || soundAgent.Priority < candidateAgent.Priority)
                        {
                            candidateAgent = soundAgent;
                        }
                    }
                    else if (!m_AvoidBeingReplacedBySamePriority && soundAgent.Priority == playSoundParams.Priority)
                    {
                        // 同优先级且允许替换，选择最早设置的（播放时间最长的优先被抢占）
                        if (candidateAgent == null || soundAgent.SetSoundAssetTime < candidateAgent.SetSoundAssetTime)
                        {
                            candidateAgent = soundAgent;
                        }
                    }
                }

                // 没有可用的 Agent
                if (candidateAgent == null)
                {
                    errorCode = PlaySoundErrorCode.IgnoredDueToLowPriority;
                    return null;
                }

                // 设置声音资源到候选 Agent
                if (!candidateAgent.SetSoundAsset(soundAsset))
                {
                    errorCode = PlaySoundErrorCode.SetSoundAssetFailure;
                    return null;
                }

                // 设置所有播放参数
                candidateAgent.SerialId = serialId;
                candidateAgent.Time = playSoundParams.Time;
                candidateAgent.MuteInSoundGroup = playSoundParams.MuteInSoundGroup;
                candidateAgent.Loop = playSoundParams.Loop;
                candidateAgent.Priority = playSoundParams.Priority;
                candidateAgent.VolumeInSoundGroup = playSoundParams.VolumeInSoundGroup;
                candidateAgent.Pitch = playSoundParams.Pitch;
                candidateAgent.PanStereo = playSoundParams.PanStereo;
                candidateAgent.SpatialBlend = playSoundParams.SpatialBlend;
                candidateAgent.MaxDistance = playSoundParams.MaxDistance;
                candidateAgent.DopplerLevel = playSoundParams.DopplerLevel;

                // 开始播放
                candidateAgent.Play(playSoundParams.FadeInSeconds);
                return candidateAgent;
            }

            /// <summary>
            /// 停止指定序列编号的声音。
            /// </summary>
            public bool StopSound(int serialId, float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != serialId)
                    {
                        continue;
                    }

                    soundAgent.Stop(fadeOutSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 暂停指定序列编号的声音。
            /// </summary>
            public bool PauseSound(int serialId, float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != serialId)
                    {
                        continue;
                    }

                    soundAgent.Pause(fadeOutSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 恢复指定序列编号的声音。
            /// </summary>
            public bool ResumeSound(int serialId, float fadeInSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != serialId)
                    {
                        continue;
                    }

                    soundAgent.Resume(fadeInSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 停止该组内所有正在播放的声音。
            /// </summary>
            public void StopAllLoadedSounds(float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.IsPlaying)
                    {
                        soundAgent.Stop(fadeOutSeconds);
                    }
                }
            }

            /// <summary>
            /// 暂停该组内所有正在播放的声音。
            /// </summary>
            public void PauseAllLoadedSounds(float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.IsPlaying)
                    {
                        soundAgent.Pause(fadeOutSeconds);
                    }
                }
            }

            /// <summary>
            /// 恢复该组内所有已暂停的声音。
            /// </summary>
            public void ResumeAllPausedSounds(float fadeInSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != 0 && !soundAgent.IsPlaying)
                    {
                        soundAgent.Resume(fadeInSeconds);
                    }
                }
            }
        }

        // ================================================================
        //  内部类型 - 异步播放信息
        // ================================================================

        /// <summary>
        /// 播放声音信息（内部类，IReference）。
        ///
        /// 当 PlaySound 发起异步加载时，创建此对象作为加载上下文。
        /// 加载完成后，使用此信息将声音资源分配给合适的 Agent。
        /// 使用 ReferencePool 管理以减少 GC 压力。
        ///
        /// 对应核心 SoundManager.PlaySoundInfo（内部类）。
        /// </summary>
        private sealed class PlaySoundInfo : IReference
        {
            /// <summary>播放请求的序列编号。</summary>
            private int m_SerialId;

            /// <summary>目标声音组。</summary>
            private SoundGroup m_SoundGroup;

            /// <summary>播放参数。</summary>
            private PlaySoundParams m_PlaySoundParams;

            /// <summary>用户自定义数据。</summary>
            private object m_UserData;

            public PlaySoundInfo()
            {
                m_SerialId = 0;
                m_SoundGroup = null;
                m_PlaySoundParams = null;
                m_UserData = null;
            }

            /// <summary>获取序列编号。</summary>
            public int SerialId { get { return m_SerialId; } }

            /// <summary>获取声音组。</summary>
            public SoundGroup SoundGroup { get { return m_SoundGroup; } }

            /// <summary>获取播放参数。</summary>
            public PlaySoundParams PlaySoundParams { get { return m_PlaySoundParams; } }

            /// <summary>获取用户数据。</summary>
            public object UserData { get { return m_UserData; } }

            /// <summary>
            /// 创建播放声音信息（从 ReferencePool 获取）。
            /// </summary>
            public static PlaySoundInfo Create(int serialId, SoundGroup soundGroup,
                PlaySoundParams playSoundParams, object userData)
            {
                PlaySoundInfo playSoundInfo = ReferencePool.Acquire<PlaySoundInfo>();
                playSoundInfo.m_SerialId = serialId;
                playSoundInfo.m_SoundGroup = soundGroup;
                playSoundInfo.m_PlaySoundParams = playSoundParams;
                playSoundInfo.m_UserData = userData;
                return playSoundInfo;
            }

            /// <summary>
            /// 清理播放声音信息。
            /// </summary>
            public void Clear()
            {
                m_SerialId = 0;
                m_SoundGroup = null;
                m_PlaySoundParams = null;
                m_UserData = null;
            }
        }

        // ================================================================
        //  字段
        // ================================================================

        /// <summary>
        /// 所有声音组的字典（组名 → SoundGroup）。
        /// 使用 Ordinal 比较器以提高性能。
        /// </summary>
        private Dictionary<string, SoundGroup> m_SoundGroups;

        /// <summary>
        /// 正在异步加载的声音序列编号列表。
        /// </summary>
        private List<int> m_SoundsBeingLoaded;

        /// <summary>
        /// 加载完成后需要立即释放的声音序列编号集合。
        /// 当 StopSound 被调用时，如果声音仍在加载中，将其加入此集合。
        /// 加载完成后检测到此标记，立即释放资源而不播放。
        /// </summary>
        private HashSet<int> m_SoundsToReleaseOnLoad;

        /// <summary>
        /// 序列编号生成器。
        /// 每次 PlaySound 时递增，确保每个播放请求有唯一标识。
        /// </summary>
        private int m_Serial;

        /// <summary>
        /// 组件是否已关闭。
        /// </summary>
        private bool m_IsShutdown;

        /// <summary>
        /// 事件组件引用（懒加载）。
        /// </summary>
        private EventComponent m_EventComponent;

        // ================================================================
        //  [Export] 配置
        // ================================================================

        /// <summary>
        /// 声音组名称数组。
        /// 在 Godot Inspector 中配置，例如 ["Music", "SFX", "UI"]。
        /// 如果为空或未设置，将使用默认声音组。
        /// </summary>
        [Export]
        private string[] m_SoundGroupNames = null;

        /// <summary>
        /// 每个声音组的 Agent 数量。
        /// Agent 数量决定了该组同时能播放多少个声音。
        /// 例如 [2, 8, 4] 表示 Music 组 2 个并发、SFX 组 8 个并发、UI 组 4 个并发。
        /// </summary>
        [Export]
        private int[] m_SoundGroupAgentCounts = null;

        /// <summary>
        /// 每个声音组是否避免被同优先级声音替换。
        /// Music 组通常设为 true（避免被同优先级音效抢占）。
        /// 注意：Godot 4.x 不支持 [Export] bool[]（GD0102），所以通过名称判断：
        /// 名为 "Music" 的组默认避免被同优先级替换，其他组不避免。
        /// </summary>

        // ================================================================
        //  公共属性
        // ================================================================

        /// <summary>
        /// 获取声音组数量。
        /// </summary>
        public int SoundGroupCount
        {
            get { return m_SoundGroups.Count; }
        }

        // ================================================================
        //  生命周期
        // ================================================================

        /// <summary>
        /// 节点初始化回调。
        /// 创建默认或用户配置的声音组。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_SoundGroups = new Dictionary<string, SoundGroup>(StringComparer.Ordinal);
            m_SoundsBeingLoaded = new List<int>();
            m_SoundsToReleaseOnLoad = new HashSet<int>();
            m_Serial = 0;
            m_IsShutdown = false;

            // 如果用户在 Inspector 中配置了声音组，使用用户配置
            // 否则创建默认声音组
            if (m_SoundGroupNames != null && m_SoundGroupNames.Length > 0)
            {
                for (int i = 0; i < m_SoundGroupNames.Length; i++)
                {
                    if (string.IsNullOrEmpty(m_SoundGroupNames[i]))
                    {
                        continue;
                    }

                    int agentCount = (m_SoundGroupAgentCounts != null && i < m_SoundGroupAgentCounts.Length)
                        ? m_SoundGroupAgentCounts[i] : 8;

                    // 名为 "Music" 的组默认避免被同优先级替换
                    bool avoidReplaced = string.Equals(m_SoundGroupNames[i], "Music", StringComparison.Ordinal);

                    if (!AddSoundGroup(m_SoundGroupNames[i], agentCount, avoidReplaced))
                    {
                        Log.Warning("Add sound group '{0}' failure.", m_SoundGroupNames[i]);
                    }
                }
            }
            else
            {
                // 创建默认声音组
                // Music: 2个并发槽位，避免被同优先级替换（BGM 通常优先级高）
                AddSoundGroup("Music", 2, true);
                // SFX: 8个并发槽位（音效可能同时播放多个）
                AddSoundGroup("SFX", 8, false);
                // UI: 4个并发槽位（UI 音效如按钮点击）
                AddSoundGroup("UI", 4, false);
            }
        }

        /// <summary>
        /// 节点退出场景树时调用。
        /// 停止所有声音并标记为已关闭。
        /// </summary>
        public override void _ExitTree()
        {
            m_IsShutdown = true;
            StopAllLoadedSounds();
            base._ExitTree();
        }

        // ================================================================
        //  声音组管理
        // ================================================================

        /// <summary>
        /// 是否存在指定声音组。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasSoundGroup(string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                return false;
            }

            return m_SoundGroups.ContainsKey(soundGroupName);
        }

        /// <summary>
        /// 增加声音组。
        ///
        /// 创建一个 DefaultSoundGroupHelper 容器节点和指定数量的 Agent（AudioStreamPlayer）。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="soundAgentHelperCount">该组的 Agent 数量（同时可播放的声音数）。</param>
        /// <param name="avoidBeingReplacedBySamePriority">是否避免被同优先级声音替换。</param>
        /// <returns>是否添加成功。</returns>
        public bool AddSoundGroup(string soundGroupName, int soundAgentHelperCount,
            bool avoidBeingReplacedBySamePriority = false)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                Log.Error("Sound group name is invalid.");
                return false;
            }

            if (m_SoundGroups.ContainsKey(soundGroupName))
            {
                Log.Warning("Sound group '{0}' is already exist.", soundGroupName);
                return false;
            }

            if (soundAgentHelperCount <= 0)
            {
                Log.Error("Sound agent helper count is invalid.");
                return false;
            }

            // 创建组容器节点
            DefaultSoundGroupHelper groupHelper = new DefaultSoundGroupHelper();
            groupHelper.Name = Utility.Text.Format("Sound Group - {0}", soundGroupName);
            AddChild(groupHelper);

            // 创建内部声音组
            SoundGroup soundGroup = new SoundGroup(soundGroupName, groupHelper, avoidBeingReplacedBySamePriority);

            // 创建 Agent（每个 Agent 对应一个 AudioStreamPlayer 节点）
            for (int i = 0; i < soundAgentHelperCount; i++)
            {
                // 创建 AudioStreamPlayer 节点
                AudioStreamPlayer audioPlayer = new AudioStreamPlayer();
                audioPlayer.Name = Utility.Text.Format("Agent {0}", i);
                groupHelper.AddChild(audioPlayer);

                // 创建 Agent Helper（包装 AudioStreamPlayer）
                DefaultSoundAgentHelper agentHelper = new DefaultSoundAgentHelper(audioPlayer);
                soundGroup.AddSoundAgent(agentHelper);
            }

            m_SoundGroups.Add(soundGroupName, soundGroup);
            return true;
        }

        // ================================================================
        //  播放控制
        // ================================================================

        /// <summary>
        /// 播放声音（最简形式）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径（如 "res://Audio/BGM.mp3"）。</param>
        /// <param name="soundGroupName">声音组名称（如 "Music"、"SFX"）。</param>
        /// <returns>声音的序列编号，用于后续控制（停止/暂停/恢复）。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName)
        {
            return PlaySound(soundAssetName, soundGroupName, 0, null, null);
        }

        /// <summary>
        /// 播放声音（指定优先级）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="priority">加载优先级。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority)
        {
            return PlaySound(soundAssetName, soundGroupName, priority, null, null);
        }

        /// <summary>
        /// 播放声音（指定播放参数）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="playSoundParams">播放参数（音量、循环、淡入等）。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams)
        {
            return PlaySound(soundAssetName, soundGroupName, 0, playSoundParams, null);
        }

        /// <summary>
        /// 播放声音（携带用户数据）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, object userData)
        {
            return PlaySound(soundAssetName, soundGroupName, 0, null, userData);
        }

        /// <summary>
        /// 播放声音（指定优先级和播放参数）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="playSoundParams">播放参数。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority, PlaySoundParams playSoundParams)
        {
            return PlaySound(soundAssetName, soundGroupName, priority, playSoundParams, null);
        }

        /// <summary>
        /// 播放声音（指定优先级和用户数据）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority, object userData)
        {
            return PlaySound(soundAssetName, soundGroupName, priority, null, userData);
        }

        /// <summary>
        /// 播放声音（指定播放参数和用户数据）。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="playSoundParams">播放参数。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, object userData)
        {
            return PlaySound(soundAssetName, soundGroupName, 0, playSoundParams, userData);
        }

        /// <summary>
        /// 播放声音（完整参数版本 — 主方法）。
        ///
        /// 这是所有 PlaySound 重载的最终汇聚方法，负责：
        /// 1. 参数验证
        /// 2. 生成唯一序列编号
        /// 3. 尝试同步加载 AudioStream（如果已在 Godot 缓存中则瞬间完成）
        /// 4. 同步加载失败则走异步加载路径
        /// 5. 通过声音组的优先级抢占算法找到 Agent 并播放
        ///
        /// 对应核心 ISoundManager.PlaySound() 的完整版本。
        /// </summary>
        /// <param name="soundAssetName">声音资源路径。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="priority">加载优先级（暂未使用，保留接口兼容性）。</param>
        /// <param name="playSoundParams">播放参数（为 null 时使用默认值）。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音的序列编号。</returns>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority,
            PlaySoundParams playSoundParams, object userData)
        {
            if (m_IsShutdown)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(soundAssetName))
            {
                throw new GameFrameworkException("Sound asset name is invalid.");
            }

            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new GameFrameworkException("Sound group name is invalid.");
            }

            // 如果未提供播放参数，从引用池创建默认参数
            if (playSoundParams == null)
            {
                playSoundParams = PlaySoundParams.Create();
            }

            // 生成唯一序列编号
            int serialId = ++m_Serial;

            // 查找目标声音组
            SoundGroup soundGroup = GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                throw new GameFrameworkException(
                    Utility.Text.Format("Sound group '{0}' is not exist.", soundGroupName));
            }

            if (soundGroup.SoundAgentCount <= 0)
            {
                throw new GameFrameworkException(
                    Utility.Text.Format("Sound group '{0}' has no sound agent.", soundGroupName));
            }

            // 获取 ResourceComponent 用于加载音频资源
            ResourceComponent resourceComp = GF.Resource;
            if (resourceComp == null)
            {
                throw new GameFrameworkException("Resource component is invalid.");
            }

            // 先尝试同步加载（如果资源已在 Godot 缓存中，此操作瞬间完成）
            AudioStream audioStream = resourceComp.LoadAsset<AudioStream>(soundAssetName);
            if (audioStream != null)
            {
                // 同步加载成功，直接播放
                PlaySoundErrorCode? errorCode = null;
                SoundAgent agent = soundGroup.PlaySound(serialId, audioStream, playSoundParams, out errorCode);

                // 释放引用池中的播放参数（如果是从池中获取的）
                if (playSoundParams.Referenced)
                {
                    ReferencePool.Release(playSoundParams);
                }

                if (errorCode.HasValue)
                {
                    Log.Info("Play sound '{0}' in group '{1}' failure, error code '{2}'.",
                        soundAssetName, soundGroupName, errorCode.Value);
                }

                return serialId;
            }

            // 同步加载失败，走异步加载路径
            m_SoundsBeingLoaded.Add(serialId);
            PlaySoundInfo playInfo = PlaySoundInfo.Create(serialId, soundGroup, playSoundParams, userData);

            resourceComp.LoadAssetAsync(soundAssetName, typeof(AudioStream),
                asset => OnLoadSoundSuccess(serialId, soundAssetName, playInfo, asset),
                errorMsg => OnLoadSoundFailure(serialId, soundAssetName, soundGroupName, playInfo, errorMsg)
            );

            return serialId;
        }

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        /// <param name="serialId">要停止的声音序列编号。</param>
        /// <returns>是否停止成功。</returns>
        public bool StopSound(int serialId)
        {
            return StopSound(serialId, Constant.DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 停止播放声音（带淡出）。
        ///
        /// 如果声音仍在加载中，将其标记为"加载完成后释放"，不会播放。
        /// </summary>
        /// <param name="serialId">要停止的声音序列编号。</param>
        /// <param name="fadeOutSeconds">淡出时间（秒）。</param>
        /// <returns>是否停止成功。</returns>
        public bool StopSound(int serialId, float fadeOutSeconds)
        {
            // 如果声音正在加载中，标记为待释放
            if (m_SoundsBeingLoaded.Remove(serialId))
            {
                m_SoundsToReleaseOnLoad.Add(serialId);
                return true;
            }

            // 在所有声音组中查找并停止
            foreach (KeyValuePair<string, SoundGroup> pair in m_SoundGroups)
            {
                if (pair.Value.StopSound(serialId, fadeOutSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 停止所有已加载的声音（立即停止）。
        /// </summary>
        public void StopAllLoadedSounds()
        {
            StopAllLoadedSounds(Constant.DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 停止所有已加载的声音（带淡出）。
        /// </summary>
        /// <param name="fadeOutSeconds">淡出时间（秒）。</param>
        public void StopAllLoadedSounds(float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> pair in m_SoundGroups)
            {
                pair.Value.StopAllLoadedSounds(fadeOutSeconds);
            }
        }

        /// <summary>
        /// 停止所有正在加载的声音。
        /// 加载完成后这些声音会被立即释放而不播放。
        /// </summary>
        public void StopAllLoadingSounds()
        {
            foreach (int serialId in m_SoundsBeingLoaded)
            {
                m_SoundsToReleaseOnLoad.Add(serialId);
            }

            m_SoundsBeingLoaded.Clear();
        }

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        /// <param name="serialId">要暂停的声音序列编号。</param>
        /// <returns>是否暂停成功。</returns>
        public bool PauseSound(int serialId)
        {
            return PauseSound(serialId, Constant.DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 暂停播放声音（带淡出）。
        /// </summary>
        /// <param name="serialId">要暂停的声音序列编号。</param>
        /// <param name="fadeOutSeconds">淡出时间（秒）。</param>
        /// <returns>是否暂停成功。</returns>
        public bool PauseSound(int serialId, float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> pair in m_SoundGroups)
            {
                if (pair.Value.PauseSound(serialId, fadeOutSeconds))
                {
                    return true;
                }
            }

            Log.Warning("Pause sound '{0}' failure, not found.", serialId);
            return false;
        }

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        /// <param name="serialId">要恢复的声音序列编号。</param>
        /// <returns>是否恢复成功。</returns>
        public bool ResumeSound(int serialId)
        {
            return ResumeSound(serialId, Constant.DefaultFadeInSeconds);
        }

        /// <summary>
        /// 恢复播放声音（带淡入）。
        /// </summary>
        /// <param name="serialId">要恢复的声音序列编号。</param>
        /// <param name="fadeInSeconds">淡入时间（秒）。</param>
        /// <returns>是否恢复成功。</returns>
        public bool ResumeSound(int serialId, float fadeInSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> pair in m_SoundGroups)
            {
                if (pair.Value.ResumeSound(serialId, fadeInSeconds))
                {
                    return true;
                }
            }

            Log.Warning("Resume sound '{0}' failure, not found.", serialId);
            return false;
        }

        /// <summary>
        /// 暂停所有已加载的声音。
        /// </summary>
        public void PauseAllLoadedSounds()
        {
            PauseAllLoadedSounds(Constant.DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 暂停所有已加载的声音（带淡出）。
        /// </summary>
        /// <param name="fadeOutSeconds">淡出时间（秒）。</param>
        public void PauseAllLoadedSounds(float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> pair in m_SoundGroups)
            {
                pair.Value.PauseAllLoadedSounds(fadeOutSeconds);
            }
        }

        /// <summary>
        /// 恢复所有已暂停的声音。
        /// </summary>
        public void ResumeAllPausedSounds()
        {
            ResumeAllPausedSounds(Constant.DefaultFadeInSeconds);
        }

        /// <summary>
        /// 恢复所有已暂停的声音（带淡入）。
        /// </summary>
        /// <param name="fadeInSeconds">淡入时间（秒）。</param>
        public void ResumeAllPausedSounds(float fadeInSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> pair in m_SoundGroups)
            {
                pair.Value.ResumeAllPausedSounds(fadeInSeconds);
            }
        }

        // ================================================================
        //  加载状态查询
        // ================================================================

        /// <summary>
        /// 是否正在加载声音。
        /// </summary>
        /// <param name="serialId">声音序列编号。</param>
        /// <returns>是否正在加载。</returns>
        public bool IsLoadingSound(int serialId)
        {
            return m_SoundsBeingLoaded.Contains(serialId);
        }

        /// <summary>
        /// 获取所有正在加载声音的序列编号。
        /// </summary>
        /// <returns>所有正在加载声音的序列编号数组。</returns>
        public int[] GetAllLoadingSoundSerialIds()
        {
            return m_SoundsBeingLoaded.ToArray();
        }

        // ================================================================
        //  声音组音量/静音控制
        // ================================================================

        /// <summary>
        /// 设置指定声音组的音量。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="volume">音量值（0-1）。</param>
        public void SetSoundGroupVolume(string soundGroupName, float volume)
        {
            SoundGroup soundGroup = GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                Log.Warning("Set sound group '{0}' volume failure, group not exist.", soundGroupName);
                return;
            }

            soundGroup.Volume = volume;
        }

        /// <summary>
        /// 设置指定声音组是否静音。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="mute">是否静音。</param>
        public void SetSoundGroupMute(string soundGroupName, bool mute)
        {
            SoundGroup soundGroup = GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                Log.Warning("Set sound group '{0}' mute failure, group not exist.", soundGroupName);
                return;
            }

            soundGroup.Mute = mute;
        }

        // ================================================================
        //  私有方法 - 异步加载回调
        // ================================================================

        /// <summary>
        /// 声音资源加载成功回调。
        ///
        /// 当 ResourceComponent 完成异步加载 AudioStream 后调用此方法。
        /// 处理流程：
        /// 1. 检查是否在"待释放"列表中（StopSound 在加载中调用过）
        /// 2. 如果是，释放资源并返回
        /// 3. 如果不是，通过声音组的抢占算法分配 Agent 并播放
        /// </summary>
        private void OnLoadSoundSuccess(int serialId, string soundAssetName, PlaySoundInfo playInfo, object asset)
        {
            // 检查是否已被标记为待释放
            if (m_SoundsToReleaseOnLoad.Remove(serialId))
            {
                m_SoundsBeingLoaded.Remove(serialId);
                ReferencePool.Release(playInfo);
                return;
            }

            // 从加载列表中移除
            m_SoundsBeingLoaded.Remove(serialId);

            // 通过声音组的优先级抢占算法分配 Agent 并播放
            PlaySoundErrorCode? errorCode = null;
            SoundAgent agent = playInfo.SoundGroup.PlaySound(serialId, asset, playInfo.PlaySoundParams, out errorCode);

            // 释放引用池对象
            if (playInfo.PlaySoundParams != null && playInfo.PlaySoundParams.Referenced)
            {
                ReferencePool.Release(playInfo.PlaySoundParams);
            }
            ReferencePool.Release(playInfo);

            if (errorCode.HasValue)
            {
                Log.Info("Play sound '{0}' in group '{1}' failure, error code '{2}'.",
                    soundAssetName, playInfo.SoundGroup.Name, errorCode.Value);
            }
        }

        /// <summary>
        /// 声音资源加载失败回调。
        /// </summary>
        private void OnLoadSoundFailure(int serialId, string soundAssetName, string soundGroupName,
            PlaySoundInfo playInfo, string errorMessage)
        {
            // 检查是否已被标记为待释放
            if (m_SoundsToReleaseOnLoad.Remove(serialId))
            {
                m_SoundsBeingLoaded.Remove(serialId);
                if (playInfo.PlaySoundParams != null && playInfo.PlaySoundParams.Referenced)
                {
                    ReferencePool.Release(playInfo.PlaySoundParams);
                }
                ReferencePool.Release(playInfo);
                return;
            }

            // 从加载列表中移除
            m_SoundsBeingLoaded.Remove(serialId);

            Log.Warning("Play sound failure, asset name '{0}', error message '{1}'.",
                soundAssetName, errorMessage);

            // 释放引用池对象
            if (playInfo.PlaySoundParams != null && playInfo.PlaySoundParams.Referenced)
            {
                ReferencePool.Release(playInfo.PlaySoundParams);
            }
            ReferencePool.Release(playInfo);
        }

        // ================================================================
        //  私有方法 - 辅助
        // ================================================================

        /// <summary>
        /// 获取内部声音组。
        /// </summary>
        private SoundGroup GetSoundGroup(string soundGroupName)
        {
            SoundGroup soundGroup = null;
            m_SoundGroups.TryGetValue(soundGroupName, out soundGroup);
            return soundGroup;
        }

        /// <summary>
        /// 获取事件组件（懒加载）。
        /// 避免在 _Ready 中依赖其他组件的初始化顺序。
        /// </summary>
        private EventComponent GetEventComponent()
        {
            if (m_EventComponent == null)
            {
                m_EventComponent = GF.Event;
            }
            return m_EventComponent;
        }
    }
}
