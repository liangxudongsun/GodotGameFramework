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

namespace GodotGameFramework
{
    /// <summary>
    /// 默认声音代理辅助器。
    ///
    /// 封装 Godot 的 AudioStreamPlayer，实现核心框架的 ISoundAgentHelper 接口。
    /// 每个实例对应一个 AudioStreamPlayer 节点，负责单个声音的播放控制。
    ///
    /// 核心功能：
    /// 1. 播放/停止/暂停/恢复（含淡入淡出支持）
    /// 2. 音量（线性 0-1 ↔ 分贝 VolumeDb 转换）
    /// 3. 静音（AudioStreamPlayer 无原生 Mute，通过 VolumeDb=-80f 实现）
    /// 4. 循环播放（通过 AudioStreamLoopable.LoopMode 控制）
    /// 5. 播放速率（映射到 PitchScale）
    /// 6. 自然播放完成检测（连接 Finished 信号）
    /// 7. 淡入淡出（使用 Godot Tween API）
    ///
    /// Godot 4.x 适配要点：
    /// - AudioStreamPlayer.VolumeDb 是分贝值，需要 Mathf.LinearToDb/DbToLinear 转换
    /// - AudioStreamPlayer 没有原生 Mute 属性，通过设 VolumeDb=-80f 实现
    /// - AudioStreamPlayer 没有 Pause/Resume 方法，需要保存播放位置后 Stop，恢复时 Seek+Play
    /// - AudioStreamPlayer 不支持空间音频（PanStereo/SpatialBlend/MaxDistance/DopplerLevel 存储但 no-op）
    /// - 自然完成通过 Finished 信号检测（比 _Process 轮询更高效）
    ///
    /// 对应 Unity 版本中的 DefaultSoundAgentHelper（MonoBehaviour 包装 AudioSource）。
    /// </summary>
    public sealed class DefaultSoundAgentHelper : ISoundAgentHelper
    {
        // ================================================================
        //  常量
        // ================================================================

        /// <summary>
        /// 静音时的分贝值。
        /// -80db 在人耳中基本听不到，等效于 Unity AudioSource.mute = true。
        /// </summary>
        private const float MuteVolumeDb = -80f;

        // ================================================================
        //  私有字段
        // ================================================================

        /// <summary>
        /// 被封装的 AudioStreamPlayer 节点。
        /// 每个 Agent 对应一个独立的 AudioStreamPlayer 实例。
        /// </summary>
        private AudioStreamPlayer m_AudioStreamPlayer;

        /// <summary>
        /// 静音状态。
        /// AudioStreamPlayer 没有原生 Mute 属性，需要自己维护。
        /// 当 Mute=true 时，ApplyVolume() 会将 VolumeDb 设为 -80f。
        /// </summary>
        private bool m_Mute;

        /// <summary>
        /// 音量值（线性 0-1）。
        /// 对应 UGF AudioSource.volume 和核心框架的 Volume 属性。
        /// 实际应用到 AudioStreamPlayer 时会通过 Mathf.LinearToDb 转换为分贝。
        /// </summary>
        private float m_Volume;

        /// <summary>
        /// 优先级。
        /// 对应 UGF AudioSource.priority（经过 128-value 反转）。
        /// 在 GGF 中直接存储核心框架的优先级值（0=最低），不做反转。
        /// </summary>
        private int m_Priority;

        /// <summary>
        /// 立体声声相（-1 到 1）。
        /// AudioStreamPlayer 不支持此属性，仅存储以保持接口兼容。
        /// </summary>
        private float m_PanStereo;

        /// <summary>
        /// 空间混合量（0=2D，1=3D）。
        /// AudioStreamPlayer 不支持此属性，仅存储以保持接口兼容。
        /// 如需空间音频，可使用 AudioStreamPlayer2D 或 AudioStreamPlayer3D。
        /// </summary>
        private float m_SpatialBlend;

        /// <summary>
        /// 最大距离（3D 空间音频参数）。
        /// AudioStreamPlayer 不支持此属性，仅存储以保持接口兼容。
        /// </summary>
        private float m_MaxDistance;

        /// <summary>
        /// 多普勒效应等级（3D 空间音频参数）。
        /// AudioStreamPlayer 不支持此属性，仅存储以保持接口兼容。
        /// </summary>
        private float m_DopplerLevel;

        /// <summary>
        /// 暂停时保存的播放位置（秒）。
        /// Godot 的 AudioStreamPlayer 没有 Pause() 方法，
        /// 需要在暂停时记录当前位置，恢复时从该位置继续播放。
        /// </summary>
        private float m_PausedPosition;

        /// <summary>
        /// 是否处于暂停状态。
        /// 用于区分"已暂停"和"已停止"两种非播放状态。
        /// </summary>
        private bool m_IsPaused;

        /// <summary>
        /// 当前活跃的淡入淡出 Tween。
        /// 用于在新的 Play/Stop/Pause/Resume 调用时取消上一个未完成的淡入淡出。
        /// </summary>
        private Tween m_FadeTween;

        /// <summary>
        /// 是否正在淡出中。
        /// 淡出过程中，Finished 信号不应触发 ResetSoundAgent（避免重复触发）。
        /// </summary>
        private bool m_IsFadingOut;

        /// <summary>
        /// 重置声音代理事件。
        /// 当声音自然播放完成（非循环声音播到末尾）时触发。
        /// SoundComponent 订阅此事件来回收 Agent。
        /// 对应 UGF 中 DefaultSoundAgentHelper.Update() 检测自然完成后触发的事件。
        /// </summary>
        private event EventHandler<ResetSoundAgentEventArgs> m_ResetSoundAgent;

        // ================================================================
        //  构造函数
        // ================================================================

        /// <summary>
        /// 初始化默认声音代理辅助器的新实例。
        ///
        /// 接收一个 AudioStreamPlayer 节点作为底层播放器。
        /// 该 AudioStreamPlayer 应该已经添加到场景树中（作为 DefaultSoundGroupHelper 的子节点）。
        /// </summary>
        /// <param name="audioStreamPlayer">要封装的 AudioStreamPlayer 节点。</param>
        public DefaultSoundAgentHelper(AudioStreamPlayer audioStreamPlayer)
        {
            if (audioStreamPlayer == null)
            {
                throw new GameFrameworkException("Audio stream player is invalid.");
            }

            m_AudioStreamPlayer = audioStreamPlayer;
            m_Mute = false;
            m_Volume = 1f;
            m_Priority = 0;
            m_PanStereo = 0f;
            m_SpatialBlend = 0f;
            m_MaxDistance = 100f;
            m_DopplerLevel = 1f;
            m_PausedPosition = 0f;
            m_IsPaused = false;
            m_FadeTween = null;
            m_IsFadingOut = false;

            // 连接 AudioStreamPlayer 的 Finished 信号，用于检测自然播放完成
            // 当非循环声音播放到末尾时，Godot 会自动触发此信号
            m_AudioStreamPlayer.Finished += OnAudioStreamPlayerFinished;
        }

        // ================================================================
        //  ISoundAgentHelper 属性实现
        // ================================================================

        /// <summary>
        /// 获取当前是否正在播放。
        /// 直接委托给 AudioStreamPlayer.Playing。
        /// </summary>
        public bool IsPlaying
        {
            get { return m_AudioStreamPlayer.Playing; }
        }

        /// <summary>
        /// 获取声音长度（秒）。
        ///
        /// Godot 的 AudioStream 基类没有 Length 属性，
        /// 需要根据具体子类型获取：
        /// - AudioStreamWav → GetLength()
        /// - AudioStreamOggVorbis → GetLength()
        /// - AudioStreamMP3 → GetLength()
        /// 其他类型返回 0。
        /// </summary>
        public float Length
        {
            get
            {
                AudioStream stream = m_AudioStreamPlayer.Stream;
                if (stream == null)
                {
                    return 0f;
                }

                // 尝试转型到具体的流类型获取长度
                if (stream is AudioStreamWav wav)
                {
                    return (float)wav.GetLength();
                }
                if (stream is AudioStreamOggVorbis ogg)
                {
                    return (float)ogg.GetLength();
                }
                if (stream is AudioStreamMP3 mp3)
                {
                    return (float)mp3.GetLength();
                }

                return 0f;
            }
        }

        /// <summary>
        /// 获取或设置播放位置（秒）。
        /// 委托给 AudioStreamPlayer 的 GetPlaybackPosition()/Seek()。
        /// </summary>
        public float Time
        {
            get { return m_AudioStreamPlayer.GetPlaybackPosition(); }
            set { m_AudioStreamPlayer.Seek(value); }
        }

        /// <summary>
        /// 获取或设置是否静音。
        ///
        /// Godot 的 AudioStreamPlayer 没有原生 Mute 属性。
        /// 实现方式：静音时将 VolumeDb 设为 -80db（人耳几乎听不到）。
        /// </summary>
        public bool Mute
        {
            get { return m_Mute; }
            set
            {
                m_Mute = value;
                ApplyVolume();
            }
        }

        /// <summary>
        /// 获取或设置是否循环播放。
        ///
        /// Godot 中循环播放是通过 AudioStream 资源对象的属性控制的，
        /// 而不是 AudioStreamPlayer 本身。不同子类型的 API 不同：
        /// - AudioStreamWav: LoopMode 枚举 (Forward/Disabled/PingPong)
        /// - AudioStreamOggVorbis: Loop bool
        /// - AudioStreamMP3: Loop bool
        ///
        /// 注意：不是所有 AudioStream 子类型都支持循环。
        /// 常见的 AudioStreamWav、AudioStreamOggVorbis、AudioStreamMP3 都支持。
        /// </summary>
        public bool Loop
        {
            get
            {
                if (m_AudioStreamPlayer.Stream is AudioStreamWav wav)
                {
                    return wav.LoopMode != AudioStreamWav.LoopModeEnum.Disabled;
                }
                if (m_AudioStreamPlayer.Stream is AudioStreamOggVorbis ogg)
                {
                    return ogg.Loop;
                }
                if (m_AudioStreamPlayer.Stream is AudioStreamMP3 mp3)
                {
                    return mp3.Loop;
                }
                return false;
            }
            set
            {
                if (m_AudioStreamPlayer.Stream is AudioStreamWav wav)
                {
                    wav.LoopMode = value ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
                }
                else if (m_AudioStreamPlayer.Stream is AudioStreamOggVorbis ogg)
                {
                    ogg.Loop = value;
                }
                else if (m_AudioStreamPlayer.Stream is AudioStreamMP3 mp3)
                {
                    mp3.Loop = value;
                }
            }
        }

        /// <summary>
        /// 获取或设置声音优先级。
        ///
        /// 在 UGF 中，AudioSource.priority 是 0=最高、256=最低，
        /// 核心框架做了 128-value 的反转使其 0=最低。
        /// 在 GGF 中，Priority 直接使用核心框架的语义（0=最低），不做反转。
        /// 此值仅在 SoundComponent 的优先级抢占算法中使用。
        /// </summary>
        public int Priority
        {
            get { return m_Priority; }
            set { m_Priority = value; }
        }

        /// <summary>
        /// 获取或设置音量大小（线性 0-1）。
        ///
        /// UGF 的 AudioSource.volume 是线性值（0-1）。
        /// Godot 的 AudioStreamPlayer.VolumeDb 是分贝值。
        /// 通过 Mathf.LinearToDb() 进行转换。
        /// </summary>
        public float Volume
        {
            get { return m_Volume; }
            set
            {
                m_Volume = Mathf.Clamp(value, 0f, 1f);
                ApplyVolume();
            }
        }

        /// <summary>
        /// 获取或设置声音音调（播放速率）。
        /// 映射到 AudioStreamPlayer.PitchScale（1.0=原始速率）。
        /// </summary>
        public float Pitch
        {
            get { return m_AudioStreamPlayer.PitchScale; }
            set { m_AudioStreamPlayer.PitchScale = Mathf.Clamp(value, 0.01f, 4f); }
        }

        /// <summary>
        /// 获取或设置声音立体声声相（-1 到 1）。
        /// AudioStreamPlayer 不支持此属性，仅存储。
        /// 如需立体声声相，可使用 AudioStreamPlayer2D（PanningStrength）。
        /// </summary>
        public float PanStereo
        {
            get { return m_PanStereo; }
            set { m_PanStereo = value; }
        }

        /// <summary>
        /// 获取或设置声音空间混合量（0=纯2D，1=纯3D）。
        /// AudioStreamPlayer 不支持此属性，仅存储。
        /// 如需空间混合，可使用 AudioStreamPlayer2D 或 AudioStreamPlayer3D。
        /// </summary>
        public float SpatialBlend
        {
            get { return m_SpatialBlend; }
            set { m_SpatialBlend = value; }
        }

        /// <summary>
        /// 获取或设置声音最大距离（3D 空间音频参数）。
        /// AudioStreamPlayer 不支持此属性，仅存储。
        /// </summary>
        public float MaxDistance
        {
            get { return m_MaxDistance; }
            set { m_MaxDistance = value; }
        }

        /// <summary>
        /// 获取或设置声音多普勒效应等级。
        /// AudioStreamPlayer 不支持此属性，仅存储。
        /// </summary>
        public float DopplerLevel
        {
            get { return m_DopplerLevel; }
            set { m_DopplerLevel = value; }
        }

        /// <summary>
        /// 重置声音代理事件。
        ///
        /// 当声音自然播放完成时（非循环声音播到末尾），触发此事件。
        /// SoundComponent 通过此事件得知 Agent 已空闲，可分配给新的播放请求。
        ///
        /// 对应 UGF 中 DefaultSoundAgentHelper.Update() 中的逻辑：
        /// 检测到 !IsPlaying && clip != null 时触发 ResetSoundAgent。
        /// GGF 改用 Finished 信号实现，更高效。
        /// </summary>
        public event EventHandler<ResetSoundAgentEventArgs> ResetSoundAgent
        {
            add { m_ResetSoundAgent += value; }
            remove { m_ResetSoundAgent -= value; }
        }

        // ================================================================
        //  ISoundAgentHelper 方法实现
        // ================================================================

        /// <summary>
        /// 播放声音。
        ///
        /// 调用 AudioStreamPlayer.Play() 开始播放。
        /// 如果指定了淡入时间（fadeInSeconds > 0），先从静音开始播放，
        /// 然后通过 Tween 渐变到目标音量。
        ///
        /// 对应 UGF DefaultSoundAgentHelper.Play(float fadeInSeconds)。
        /// </summary>
        /// <param name="fadeInSeconds">声音淡入时间，以秒为单位。</param>
        public void Play(float fadeInSeconds)
        {
            // 取消任何进行中的淡入淡出
            KillFadeTween();

            m_IsPaused = false;

            // 开始播放
            m_AudioStreamPlayer.Play();

            // 如果需要淡入
            if (fadeInSeconds > 0f)
            {
                FadeIn(fadeInSeconds);
            }
        }

        /// <summary>
        /// 停止播放声音。
        ///
        /// 如果指定了淡出时间，先通过 Tween 将音量渐变到静音，完成后再停止。
        /// 如果没有淡出或节点不在场景树中，立即停止。
        ///
        /// 停止后重置播放位置到开头，并清除暂停状态。
        ///
        /// 对应 UGF DefaultSoundAgentHelper.Stop(float fadeOutSeconds)。
        /// </summary>
        /// <param name="fadeOutSeconds">声音淡出时间，以秒为单位。</param>
        public void Stop(float fadeOutSeconds)
        {
            // 如果既不在播放也不在暂停，无需操作
            if (!m_AudioStreamPlayer.Playing && !m_IsPaused)
            {
                return;
            }

            // 取消任何进行中的淡入淡出
            KillFadeTween();

            // 如果需要淡出且当前正在播放
            if (fadeOutSeconds > 0f && m_AudioStreamPlayer.Playing)
            {
                FadeOut(fadeOutSeconds, onComplete: () =>
                {
                    // 淡出完成后停止播放
                    m_AudioStreamPlayer.Stop();
                    m_IsPaused = false;
                });
            }
            else
            {
                // 立即停止
                m_AudioStreamPlayer.Stop();
                m_IsPaused = false;
            }
        }

        /// <summary>
        /// 暂停播放声音。
        ///
        /// Godot 的 AudioStreamPlayer 没有 Pause() 方法，需要手动实现：
        /// 1. 保存当前播放位置（GetPlaybackPosition）
        /// 2. 停止播放（Stop）
        /// 3. 标记为暂停状态
        ///
        /// 如果指定了淡出时间，先渐变到静音再停止。
        /// 恢复时通过 Resume() 从保存的位置继续播放。
        ///
        /// 对应 UGF DefaultSoundAgentHelper.Pause(float fadeOutSeconds)。
        /// </summary>
        /// <param name="fadeOutSeconds">声音淡出时间，以秒为单位。</param>
        public void Pause(float fadeOutSeconds)
        {
            // 如果不在播放或已经暂停，无需操作
            if (!m_AudioStreamPlayer.Playing || m_IsPaused)
            {
                return;
            }

            // 保存当前播放位置，用于恢复时定位
            m_PausedPosition = m_AudioStreamPlayer.GetPlaybackPosition();
            m_IsPaused = true;

            // 取消任何进行中的淡入淡出
            KillFadeTween();

            if (fadeOutSeconds > 0f)
            {
                // 淡出后停止
                FadeOut(fadeOutSeconds, onComplete: () =>
                {
                    m_AudioStreamPlayer.Stop();
                });
            }
            else
            {
                // 立即停止
                m_AudioStreamPlayer.Stop();
            }
        }

        /// <summary>
        /// 恢复播放声音。
        ///
        /// 从暂停时保存的位置继续播放：
        /// 1. 定位到保存的播放位置（Seek）
        /// 2. 开始播放（Play）
        /// 3. 清除暂停状态
        ///
        /// 如果指定了淡入时间，先从静音开始播放，然后渐变到目标音量。
        ///
        /// 对应 UGF DefaultSoundAgentHelper.Resume(float fadeInSeconds)。
        /// </summary>
        /// <param name="fadeInSeconds">声音淡入时间，以秒为单位。</param>
        public void Resume(float fadeInSeconds)
        {
            // 如果不在暂停状态，无需操作
            if (!m_IsPaused)
            {
                return;
            }

            // 取消任何进行中的淡入淡出
            KillFadeTween();

            // 从暂停位置恢复播放
            m_AudioStreamPlayer.Play(m_PausedPosition);
            m_IsPaused = false;

            // 如果需要淡入
            if (fadeInSeconds > 0f)
            {
                FadeIn(fadeInSeconds);
            }
        }

        /// <summary>
        /// 重置声音代理辅助器。
        ///
        /// 将所有状态恢复到默认值：
        /// - 停止播放
        /// - 清除音频资源（Stream = null）
        /// - 重置音量、音调、静音等参数到默认值
        /// - 清除淡入淡出状态
        ///
        /// 注意：不重新订阅 Finished 信号（因为 AudioStreamPlayer 节点是复用的）。
        ///
        /// 对应 UGF DefaultSoundAgentHelper.Reset()。
        /// </summary>
        public void Reset()
        {
            // 取消进行中的淡入淡出
            KillFadeTween();

            // 停止播放并清除资源
            m_AudioStreamPlayer.Stop();
            m_AudioStreamPlayer.Stream = null;

            // 重置所有参数到默认值
            m_Mute = false;
            m_Volume = 1f;
            m_AudioStreamPlayer.VolumeDb = 0f;
            m_AudioStreamPlayer.PitchScale = 1f;
            m_Priority = 0;
            m_PanStereo = 0f;
            m_SpatialBlend = 0f;
            m_MaxDistance = 100f;
            m_DopplerLevel = 1f;
            m_PausedPosition = 0f;
            m_IsPaused = false;
            m_IsFadingOut = false;
        }

        /// <summary>
        /// 设置声音资源。
        ///
        /// 将 AudioStream 资源赋值给底层 AudioStreamPlayer。
        /// 如果传入的对象不是 AudioStream 类型，返回 false。
        ///
        /// 对应 UGF DefaultSoundAgentHelper.SetSoundAsset(object soundAsset)
        /// 中将 AudioClip 赋值给 AudioSource.clip 的逻辑。
        /// </summary>
        /// <param name="soundAsset">声音资源（必须是 AudioStream 类型）。</param>
        /// <returns>是否设置成功。</returns>
        public bool SetSoundAsset(object soundAsset)
        {
            AudioStream audioStream = soundAsset as AudioStream;
            if (audioStream == null)
            {
                return false;
            }

            m_AudioStreamPlayer.Stream = audioStream;
            return true;
        }

        // ================================================================
        //  私有方法 - 音量控制
        // ================================================================

        /// <summary>
        /// 应用当前音量到 AudioStreamPlayer。
        ///
        /// 根据静音状态决定最终的分贝值：
        /// - 静音时：VolumeDb = -80f（人耳几乎听不到）
        /// - 非静音时：VolumeDb = Mathf.LinearToDb(m_Volume)
        ///
        /// 此方法在 Mute 或 Volume 属性变化时自动调用。
        /// </summary>
        private void ApplyVolume()
        {
            if (m_Mute)
            {
                m_AudioStreamPlayer.VolumeDb = MuteVolumeDb;
            }
            else
            {
                m_AudioStreamPlayer.VolumeDb = Mathf.LinearToDb(m_Volume);
            }
        }

        // ================================================================
        //  私有方法 - 淡入淡出（Godot Tween）
        // ================================================================

        /// <summary>
        /// 淡入效果。
        ///
        /// 将音量从静音（-80db）渐变到当前目标音量。
        /// 使用 Godot 的 Tween API 实现，比手动 _Process 跟踪更高效。
        ///
        /// 对应 UGF 中的 FadeToVolume(AudioSource, volume, fadeInSeconds) 协程。
        /// </summary>
        /// <param name="duration">淡入持续时间（秒）。</param>
        private void FadeIn(float duration)
        {
            KillFadeTween();

            // 先设为静音
            m_AudioStreamPlayer.VolumeDb = MuteVolumeDb;

            // 获取 SceneTree 来创建 Tween
            SceneTree tree = m_AudioStreamPlayer.GetTree();
            if (tree == null)
            {
                // 节点不在场景树中，直接设为目标音量
                ApplyVolume();
                return;
            }

            // 计算目标分贝值
            float targetDb = m_Mute ? MuteVolumeDb : Mathf.LinearToDb(m_Volume);

            // 创建 Tween 动画：从 -80db 渐变到目标分贝值
            m_FadeTween = tree.CreateTween();
            m_FadeTween.TweenProperty(m_AudioStreamPlayer, "volume_db", targetDb, duration);
            m_FadeTween.TweenCallback(Callable.From(() =>
            {
                m_FadeTween = null;
            }));
        }

        /// <summary>
        /// 淡出效果。
        ///
        /// 将音量从当前值渐变到静音（-80db），完成后执行回调。
        ///
        /// 对应 UGF 中的 FadeToVolume(AudioSource, 0f, fadeOutSeconds) 协程。
        /// </summary>
        /// <param name="duration">淡出持续时间（秒）。</param>
        /// <param name="onComplete">淡出完成后的回调。</param>
        private void FadeOut(float duration, Action onComplete = null)
        {
            KillFadeTween();

            m_IsFadingOut = true;

            // 获取 SceneTree 来创建 Tween
            SceneTree tree = m_AudioStreamPlayer.GetTree();
            if (tree == null)
            {
                // 节点不在场景树中，直接静音并执行回调
                m_IsFadingOut = false;
                onComplete?.Invoke();
                return;
            }

            // 创建 Tween 动画：从当前分贝值渐变到 -80db
            m_FadeTween = tree.CreateTween();
            m_FadeTween.TweenProperty(m_AudioStreamPlayer, "volume_db", MuteVolumeDb, duration);
            m_FadeTween.TweenCallback(Callable.From(() =>
            {
                m_IsFadingOut = false;
                m_FadeTween = null;
                onComplete?.Invoke();
            }));
        }

        /// <summary>
        /// 终止当前进行中的淡入淡出动画。
        ///
        /// 在新的 Play/Stop/Pause/Resume 调用时，需要先终止上一个未完成的动画。
        /// 对应 UGF 中的 StopAllCoroutines()。
        /// </summary>
        private void KillFadeTween()
        {
            if (m_FadeTween != null)
            {
                m_FadeTween.Kill();
                m_FadeTween = null;
            }
            m_IsFadingOut = false;
        }

        // ================================================================
        //  私有方法 - 信号处理
        // ================================================================

        /// <summary>
        /// AudioStreamPlayer 播放完成信号处理。
        ///
        /// 当非循环声音自然播放到末尾时，Godot 自动触发 Finished 信号。
        /// 此时需要通知 SoundComponent 该 Agent 已空闲，可以分配给新的播放请求。
        ///
        /// 注意事项：
        /// - 循环声音不会触发此信号
        /// - 淡出过程中不应触发（避免 Stop 时重复处理）
        /// - 暂停状态下不应触发
        ///
        /// 对应 UGF DefaultSoundAgentHelper.Update() 中检测自然完成的逻辑：
        /// <code>
        /// if (!m_ApplicationPauseFlag && !IsPlaying && m_AudioSource.clip != null)
        /// {
        ///     // 触发 ResetSoundAgent 事件
        /// }
        /// </code>
        /// GGF 使用信号代替轮询，更高效。
        /// </summary>
        private void OnAudioStreamPlayerFinished()
        {
            // 淡出过程中不触发（Stop 的淡出完成会自行处理）
            if (m_IsFadingOut)
            {
                return;
            }

            // 暂停状态下不触发
            if (m_IsPaused)
            {
                return;
            }

            // 通知订阅者：Agent 已空闲
            if (m_ResetSoundAgent != null)
            {
                ResetSoundAgentEventArgs resetArgs = ResetSoundAgentEventArgs.Create();
                m_ResetSoundAgent(this, resetArgs);
                ReferencePool.Release(resetArgs);
            }
        }
    }
}
