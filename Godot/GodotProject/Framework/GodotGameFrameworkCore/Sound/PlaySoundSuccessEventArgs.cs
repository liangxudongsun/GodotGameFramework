//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Entity;
using GameFramework.Event;
using GameFramework.Sound;

namespace GodotGameFramework.Sound
{
    /// <summary>
    /// 播放声音成功事件（Godot 全局事件）。
    /// </summary>
    public sealed class PlaySoundSuccessEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(PlaySoundSuccessEventArgs).GetHashCode();

        public PlaySoundSuccessEventArgs()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundGroupName = null;
            Duration = 0f;
            BindingEntity = null;
            UserData = null;
        }

        public override int Id => EventId;

        /// <summary>获取声音的序列编号。</summary>
        public int SerialId { get; private set; }

        /// <summary>获取声音资源名称。</summary>
        public string SoundAssetName { get; private set; }

        /// <summary>获取声音组名称。</summary>
        public string SoundGroupName { get; private set; }

        /// <summary>获取加载持续时间。</summary>
        public float Duration { get; private set; }

        /// <summary>获取声音绑定的实体。</summary>
        public IEntity BindingEntity { get; private set; }

        /// <summary>获取用户自定义数据。</summary>
        public object UserData { get; private set; }

        /// <summary>
        /// 创建播放声音成功事件。
        /// </summary>
        public static PlaySoundSuccessEventArgs Create(GameFramework.Sound.PlaySoundSuccessEventArgs e)
        {
            PlaySoundInfo playSoundInfo = (PlaySoundInfo)e.UserData;
            PlaySoundSuccessEventArgs args = ReferencePool.Acquire<PlaySoundSuccessEventArgs>();
            args.SerialId = e.SerialId;
            args.SoundAssetName = e.SoundAssetName;
            args.SoundGroupName = e.SoundAgent?.SoundGroup?.Name;
            args.Duration = e.Duration;
            args.BindingEntity = playSoundInfo?.BindingEntity;
            args.UserData = playSoundInfo?.UserData;
            return args;
        }

        public override void Clear()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundGroupName = null;
            Duration = 0f;
            BindingEntity = null;
            UserData = null;
        }
    }
}
