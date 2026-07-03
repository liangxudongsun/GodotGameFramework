//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Resource;
using GameFramework.Sound;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework.Sound
{
    /// <summary>
    /// 声音组件。
    /// </summary>
    public sealed partial class SoundComponent : GameFrameworkComponent
    {
        public static class Parameters
        {
            public static readonly string SoundHelper = "m_SoundHelperTypeName";
            public static readonly string SoundGroupHelper = "m_SoundGroupHelperTypeName";
            public static readonly string SoundAgentHelper = "m_SoundAgentHelperTypeName";
        }
        /// <summary>
        /// 默认背景音乐组名称。
        /// </summary>
        public const string DefaultMusicGroup = "Music";

        /// <summary>
        /// 默认音效组名称。
        /// </summary>
        public const string DefaultSfxGroup = "SFX";

        /// <summary>
        /// 默认 UI 音效组名称。
        /// </summary>
        public const string DefaultUiGroup = "UI";

        private ISoundManager m_SoundManager = null;
        private SoundHelperBase m_SoundHelper = null;
        private EventComponent m_EventComponent = null;

        [Export] private bool m_EnablePlaySoundSuccessEvent = true;
        [Export] private bool m_EnablePlaySoundFailureEvent = true;
        [Export] private bool m_EnablePlaySoundUpdateEvent = false;
        [Export] private bool m_EnablePlaySoundDependencyAssetEvent = false;
        [Export] private string m_SoundHelperTypeName = "GodotGameFramework.Sound.DefaultSoundHelper";
        [Export] private string m_SoundGroupHelperTypeName = "GodotGameFramework.Sound.DefaultSoundGroupHelper";
        [Export] private string m_SoundAgentHelperTypeName = "GodotGameFramework.Sound.DefaultSoundAgentHelper";
        [Export]
        public SoundGroupRes SoundGroupRes;

        public int SoundGroupCount => m_SoundManager.SoundGroupCount;

        public override void OnInit()
        {
            base.OnInit();

            m_SoundManager = GameFrameworkEntry.GetModule<ISoundManager>();
            if (m_SoundManager == null) { Log.Fatal("Sound manager is invalid."); return; }
            m_EventComponent = GameEntry.GetComponent<EventComponent>();

            if (m_EnablePlaySoundSuccessEvent) m_SoundManager.PlaySoundSuccess += OnPlaySoundSuccess;
            m_SoundManager.PlaySoundFailure += OnPlaySoundFailure;
            if (m_EnablePlaySoundUpdateEvent) m_SoundManager.PlaySoundUpdate += OnPlaySoundUpdate;
            if (m_EnablePlaySoundDependencyAssetEvent) m_SoundManager.PlaySoundDependencyAsset += OnPlaySoundDependencyAsset;

            m_SoundManager.SetResourceManager(GameFrameworkEntry.GetModule<GameFramework.Resource.IResourceManager>());
            SoundHelperBase soundHelper = Helper.CreateHelper(m_SoundHelperTypeName, m_SoundHelper);
            if (soundHelper == null) { Log.Fatal("Can not create sound helper."); return; }
            m_SoundHelper = soundHelper;
            m_SoundHelper.Name = m_SoundHelperTypeName;
            m_SoundManager.SetSoundHelper(soundHelper);
            AddChild(m_SoundHelper);

        }


        // ================================================================
        //  声音组管理
        // ================================================================


        /// <summary>是否存在指定声音组。</summary>
        public bool HasSoundGroup(string soundGroupName) => m_SoundManager.HasSoundGroup(soundGroupName);

        /// <summary>获取指定声音组。</summary>
        public ISoundGroup GetSoundGroup(string soundGroupName) => m_SoundManager.GetSoundGroup(soundGroupName);

        /// <summary>获取所有声音组。</summary>
        public ISoundGroup[] GetAllSoundGroups() => m_SoundManager.GetAllSoundGroups();

        /// <summary>增加声音组。创建组容器节点和指定数量的 AudioStreamPlayer。</summary>
        public bool AddSoundGroup(string soundGroupName, int agentCount,
            bool avoidBeingReplacedBySamePriority = false)
        {
            if (string.IsNullOrEmpty(soundGroupName)) { Log.Error("Sound group name is invalid."); return false; }
            if (m_SoundManager.HasSoundGroup(soundGroupName)) { Log.Warning("Sound group '{0}' already exists.", soundGroupName); return false; }
            if (agentCount <= 0) { Log.Error("Agent count must be > 0."); return false; }

            SoundGroupHelperBase groupHelper = Create(m_SoundGroupHelperTypeName) as SoundGroupHelperBase;
            groupHelper.Name = $"Sound Group - {soundGroupName}";
            AddChild(groupHelper);

            if (!m_SoundManager.AddSoundGroup(soundGroupName, avoidBeingReplacedBySamePriority, false, 1f, groupHelper))
            {
                RemoveChild(groupHelper);
                groupHelper.QueueFree();
                return false;
            }

            for (int i = 0; i < agentCount; i++)
            {
                var audioPlayer = new AudioStreamPlayer();
                audioPlayer.Name = $"Agent {i}";
                SoundAgentHelperBase agentHelper = (SoundAgentHelperBase)Create(m_SoundAgentHelperTypeName);
                switch (soundGroupName)
                {
                    case DefaultMusicGroup:
                        audioPlayer.ProcessMode = AudioStreamPlayer.ProcessModeEnum.Always;
                        audioPlayer.Bus = DefaultMusicGroup;
                        break;
                    case DefaultSfxGroup:
                        audioPlayer.Bus = DefaultSfxGroup;
                        break;
                    case DefaultUiGroup:
                        audioPlayer.Bus = DefaultUiGroup;
                        break;
                }
                groupHelper.AddChild(audioPlayer);

                if (agentHelper == null)
                {
                    Log.Error("Can not create sound agent helper.");
                    continue;
                }
                agentHelper.Name = $"Agent Helper {i}";
                agentHelper.AudioStreamPlayer = audioPlayer;
                agentHelper.SetAudioStreamPlayer(audioPlayer);
                audioPlayer.AddChild(agentHelper);
                m_SoundManager.AddSoundAgentHelper(soundGroupName, agentHelper);
            }

            return true;
        }

        // ================================================================
        //  播放控制
        // ================================================================

        /// <summary>播放声音。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName);

        /// <summary>播放声音，指定优先级。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, priority);

        /// <summary>播放声音，指定播放参数。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, playSoundParams);

        /// <summary>播放声音，指定用户数据。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName, object userData) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, userData);

        /// <summary>播放声音，指定优先级和播放参数。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority,
            PlaySoundParams playSoundParams) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, priority, playSoundParams, null);

        /// <summary>播放声音，指定优先级和用户数据。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority,
            object userData) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, priority, userData);

        /// <summary>播放声音，指定播放参数和用户数据。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName,
            PlaySoundParams playSoundParams, object userData) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, playSoundParams, userData);

        /// <summary>播放声音（完整参数）。</summary>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority,
            PlaySoundParams playSoundParams, object userData) =>
            m_SoundManager.PlaySound(soundAssetName, soundGroupName, priority, playSoundParams, userData);

        /// <summary>停止声音。</summary>
        public bool StopSound(int serialId) => m_SoundManager.StopSound(serialId);

        /// <summary>停止声音，带淡出。</summary>
        public bool StopSound(int serialId, float fadeOutSeconds) =>
            m_SoundManager.StopSound(serialId, fadeOutSeconds);

        /// <summary>停止所有已加载的声音。</summary>
        public void StopAllLoadedSounds() => m_SoundManager.StopAllLoadedSounds();

        /// <summary>停止所有已加载的声音，带淡出。</summary>
        public void StopAllLoadedSounds(float fadeOutSeconds) =>
            m_SoundManager.StopAllLoadedSounds(fadeOutSeconds);

        /// <summary>停止所有正在加载的声音。</summary>
        public void StopAllLoadingSounds() => m_SoundManager.StopAllLoadingSounds();

        /// <summary>暂停声音。</summary>
        public void PauseSound(int serialId) => m_SoundManager.PauseSound(serialId);

        /// <summary>暂停声音，带淡出。</summary>
        public void PauseSound(int serialId, float fadeOutSeconds) =>
            m_SoundManager.PauseSound(serialId, fadeOutSeconds);

        /// <summary>恢复声音。</summary>
        public void ResumeSound(int serialId) => m_SoundManager.ResumeSound(serialId);

        /// <summary>恢复声音，带淡入。</summary>
        public void ResumeSound(int serialId, float fadeInSeconds) =>
            m_SoundManager.ResumeSound(serialId, fadeInSeconds);

        // ================================================================
        //  加载状态查询
        // ================================================================

        /// <summary>是否正在加载声音。</summary>
        public bool IsLoadingSound(int serialId) => m_SoundManager.IsLoadingSound(serialId);

        /// <summary>获取所有正在加载声音的序列编号。</summary>
        public int[] GetAllLoadingSoundSerialIds() => m_SoundManager.GetAllLoadingSoundSerialIds();

        // ================================================================
        //  声音组控制
        // ================================================================

        /// <summary>设置指定声音组的音量。</summary>
        public void SetSoundGroupVolume(string soundGroupName, float volume)
        {
            var group = m_SoundManager.GetSoundGroup(soundGroupName);
            if (group != null) group.Volume = volume;
            else Log.Warning("Sound group '{0}' not found.", soundGroupName);
        }

        /// <summary>设置指定声音组是否静音。</summary>
        public void SetSoundGroupMute(string soundGroupName, bool mute)
        {
            var group = m_SoundManager.GetSoundGroup(soundGroupName);
            if (group != null) group.Mute = mute;
            else Log.Warning("Sound group '{0}' not found.", soundGroupName);
        }

        // ================================================================
        //  事件处理
        // ================================================================

        private void OnPlaySoundSuccess(object sender, GameFramework.Sound.PlaySoundSuccessEventArgs e)
        {
            // PlaySoundInfo playSoundInfo = (PlaySoundInfo)e.UserData;
            // if (playSoundInfo != null)
            // {
            //     SoundAgentHelperBase soundAgentHelper = (SoundAgentHelperBase)e.SoundAgent.Helper;
            //     if (playSoundInfo.BindingEntity != null)
            //     {
            //         soundAgentHelper.SetBindingEntity(playSoundInfo.BindingEntity);
            //     }
            //     else
            //     {
            //         soundAgentHelper.SetWorldPosition(playSoundInfo.WorldPosition);
            //     }
            // }

            // m_EventComponent.Fire(this, PlaySoundSuccessEventArgs.Create(e));
        }

        private void OnPlaySoundFailure(object sender, GameFramework.Sound.PlaySoundFailureEventArgs e)
        {
            Log.Warning("Play sound failure, asset '{0}', group '{1}', msg '{2}'.",
                e.SoundAssetName, e.SoundGroupName, e.ErrorMessage);
        }

        private void OnPlaySoundUpdate(object sender, GameFramework.Sound.PlaySoundUpdateEventArgs e)
        {
            m_EventComponent.Fire(this, PlaySoundUpdateEventArgs.Create(e));
        }

        private void OnPlaySoundDependencyAsset(object sender, GameFramework.Sound.PlaySoundDependencyAssetEventArgs e)
        {
            // PlaySoundDependencyAssetEventArgs 继承自 GameFrameworkEventArgs，无法通过 EventComponent 转发
        }
    }
}
