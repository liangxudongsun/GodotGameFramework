//------------------------------------------------------------
// SoundExtension - 音频组件便捷扩展方法
// 提供 SoundComponent 的常用便捷方法，简化音频播放代码。
//
// 使用方式：
//   GF.Sound.PlayBGM("res://Audio/bgm.mp3");
//   GF.Sound.PlaySFX("res://Audio/click.wav");
//   GF.Sound.PlayUISound("res://Audio/ui_click.wav");
//   GF.Sound.StopBGM(1.0f);
//
// 对应 UGF 参考项目中的 SoundExtension（PlayBGM/PlaySound/PlayEffect，
// 振动和冷却逻辑属于游戏特定逻辑，不移植）。
//------------------------------------------------------------

namespace GodotGameFramework
{
    /// <summary>
    /// 音频组件扩展方法。
    ///
    /// 提供 SoundComponent 的常用便捷方法，
    /// 包括按声音组快捷播放 BGM/SFX/UI 音效。
    ///
    /// 对应 Unity 版本中游戏项目的 SoundExtension。
    /// </summary>
    public static class SoundExtension
    {
        /// <summary>
        /// 默认背景音乐组名称。
        /// 与 SoundComponent 初始化时创建的默认组名一致。
        /// </summary>
        private const string DefaultMusicGroup = "Music";

        /// <summary>
        /// 默认音效组名称。
        /// 与 SoundComponent 初始化时创建的默认组名一致。
        /// </summary>
        private const string DefaultSfxGroup = "SFX";

        /// <summary>
        /// 默认 UI 音效组名称。
        /// 与 SoundComponent 初始化时创建的默认组名一致。
        /// </summary>
        private const string DefaultUiGroup = "UI";

        /// <summary>
        /// 播放背景音乐（BGM）。
        ///
        /// 使用 Music 组播放，优先级为 0（默认）。
        /// Music 组默认配置为 2 个 Agent 且避免被同优先级替换。
        ///
        /// <code>
        /// GF.Sound.PlayBGM("res://Audio/background.mp3");
        /// </code>
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="soundAssetName">音频资源路径（res:// 协议）。</param>
        /// <returns>声音序列号，可用于后续停止/暂停操作。</returns>
        public static int PlayBGM(this SoundComponent soundComponent, string soundAssetName)
        {
            return soundComponent.PlaySound(soundAssetName, DefaultMusicGroup);
        }

        /// <summary>
        /// 播放背景音乐（BGM），带用户自定义数据。
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="soundAssetName">音频资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音序列号。</returns>
        public static int PlayBGM(this SoundComponent soundComponent, string soundAssetName, object userData)
        {
            return soundComponent.PlaySound(soundAssetName, DefaultMusicGroup, 0, userData);
        }

        /// <summary>
        /// 播放音效（SFX）。
        ///
        /// 使用 SFX 组播放，优先级为 0（默认）。
        /// SFX 组默认配置为 8 个 Agent，适合同时播放多个音效。
        ///
        /// <code>
        /// GF.Sound.PlaySFX("res://Audio/explosion.wav");
        /// </code>
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="soundAssetName">音频资源路径。</param>
        /// <returns>声音序列号。</returns>
        public static int PlaySFX(this SoundComponent soundComponent, string soundAssetName)
        {
            return soundComponent.PlaySound(soundAssetName, DefaultSfxGroup);
        }

        /// <summary>
        /// 播放音效（SFX），带用户自定义数据。
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="soundAssetName">音频资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音序列号。</returns>
        public static int PlaySFX(this SoundComponent soundComponent, string soundAssetName, object userData)
        {
            return soundComponent.PlaySound(soundAssetName, DefaultSfxGroup, 0, userData);
        }

        /// <summary>
        /// 播放 UI 音效。
        ///
        /// 使用 UI 组播放，优先级为 0（默认）。
        /// UI 组默认配置为 4 个 Agent，适合按钮点击等 UI 交互音效。
        ///
        /// <code>
        /// GF.Sound.PlayUISound("res://Audio/ui_click.wav");
        /// </code>
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="soundAssetName">音频资源路径。</param>
        /// <returns>声音序列号。</returns>
        public static int PlayUISound(this SoundComponent soundComponent, string soundAssetName)
        {
            return soundComponent.PlaySound(soundAssetName, DefaultUiGroup);
        }

        /// <summary>
        /// 播放 UI 音效，带用户自定义数据。
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="soundAssetName">音频资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>声音序列号。</returns>
        public static int PlayUISound(this SoundComponent soundComponent, string soundAssetName, object userData)
        {
            return soundComponent.PlaySound(soundAssetName, DefaultUiGroup, 0, userData);
        }

        /// <summary>
        /// 停止所有背景音乐。
        ///
        /// 通过隐藏 Music 组实现，组内所有 Agent 的声音都会被停止。
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        public static void StopBGM(this SoundComponent soundComponent)
        {
            soundComponent.StopAllLoadedSounds();
        }

        /// <summary>
        /// 淡出停止所有背景音乐。
        ///
        /// 通过逐渐降低音量实现平滑过渡的停止效果。
        /// </summary>
        /// <param name="soundComponent">音频组件。</param>
        /// <param name="fadeOutSeconds">淡出时长（秒）。</param>
        public static void StopBGM(this SoundComponent soundComponent, float fadeOutSeconds)
        {
            soundComponent.StopAllLoadedSounds(fadeOutSeconds);
        }
    }
}
