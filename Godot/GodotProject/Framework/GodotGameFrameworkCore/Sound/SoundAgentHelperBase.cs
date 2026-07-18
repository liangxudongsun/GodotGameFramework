//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Sound;
using Godot;
using System;

namespace GodotGameFramework.Sound
{
    public abstract partial class SoundAgentHelperBase : GodotComponent, ISoundAgentHelper
    {
        public abstract bool IsPlaying { get; }

        public abstract float Length { get; }

        public abstract float Time { get; set; }
        public abstract bool Mute { get; set; }
        [Obsolete("Godot的音频是否循环取决于资源导入设置")]
        public bool Loop { get; set; }
        public abstract int Priority { get; set; }
        public abstract float Volume { get; set; }
        public abstract float Pitch { get; set; }
        public abstract float PanStereo { get; set; }
        public abstract float SpatialBlend { get; set; }
        public abstract float MaxDistance { get; set; }
        public abstract float DopplerLevel { get; set; }

        public abstract AudioStreamPlayer AudioStreamPlayer { get; set; }
        public abstract event EventHandler<ResetSoundAgentEventArgs> ResetSoundAgent;

        public abstract void Pause(float fadeOutSeconds);

        public abstract void Play(float fadeInSeconds);

        public abstract void Reset();

        public abstract void Resume(float fadeInSeconds);

        public abstract bool SetSoundAsset(object soundAsset);

        public abstract void Stop(float fadeOutSeconds);
        public abstract void SetAudioStreamPlayer(AudioStreamPlayer audioStreamPlayer);

    }
}
