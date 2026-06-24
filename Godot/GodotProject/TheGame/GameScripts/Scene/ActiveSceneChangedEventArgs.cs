using GameFramework;
using GameFramework.Event;
using Godot;

namespace GodotGameFramework.Scene
{
    /// <summary>
    /// 激活场景切换事件。
    /// </summary>
    public sealed class ActiveSceneChangedEventArgs : GameEventArgs
    {
        /// <summary>
        /// 激活场景切换事件编号。
        /// </summary>
        public static readonly int EventId = typeof(ActiveSceneChangedEventArgs).GetHashCode();

        /// <summary>
        /// 初始化激活场景切换事件的新实例。
        /// </summary>
        public ActiveSceneChangedEventArgs()
        {
            LastActiveScene = null;
            ActiveScene = null;
        }

        /// <summary>
        /// 获取激活场景切换事件编号。
        /// </summary>
        public override int Id
        {
            get
            {
                return EventId;
            }
        }

        /// <summary>
        /// 获取之前激活的场景。
        /// </summary>
        public Node LastActiveScene
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取当前激活的场景。
        /// </summary>
        public Node ActiveScene
        {
            get;
            private set;
        }

        /// <summary>
        /// 创建激活场景切换事件。
        /// </summary>
        /// <param name="lastActiveScene">之前激活的场景。</param>
        /// <param name="activeScene">当前激活的场景。</param>
        /// <returns>创建的激活场景切换事件。</returns>
        public static ActiveSceneChangedEventArgs Create(Node lastActiveScene, Node activeScene)
        {
            ActiveSceneChangedEventArgs e = ReferencePool.Acquire<ActiveSceneChangedEventArgs>();
            e.LastActiveScene = lastActiveScene;
            e.ActiveScene = activeScene;
            return e;
        }

        /// <summary>
        /// 清理激活场景切换事件。
        /// </summary>
        public override void Clear()
        {
            LastActiveScene = null;
            ActiveScene = null;
        }
    }
}
