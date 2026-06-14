//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 游戏框架组件抽象基类。
    /// </summary>
    public abstract partial class GameFrameworkComponent : GodotComponent
    {
        public override void OnInit()
        {
            base.OnInit();
            GameEntry.RegisterComponent(this);
        }

    }
}
