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
    /// 默认版本号辅助器。
    /// </summary>
    public class DefaultVersionHelper : GameFramework.Version.IVersionHelper
    {
        /// <summary>
        /// 获取游戏版本号。
        /// </summary>
        public string GameVersion
        {
            get
            {
                // 尝试从项目设置中读取版本号
                // project.godot 中 [application] config/version = "1.0.0"
                string version = ProjectSettings.GetSetting("application/config/version").AsString();
                return string.IsNullOrEmpty(version) ? "0.1.0" : version;
            }
        }

        /// <summary>
        /// 获取内部游戏版本号（整数）。
        ///
        /// 可用于版本比较，数字越大版本越新。
        /// 默认返回 0，可按需修改。
        /// </summary>
        public int InternalGameVersion
        {
            get
            {
                return 0;
            }
        }
    }
}
