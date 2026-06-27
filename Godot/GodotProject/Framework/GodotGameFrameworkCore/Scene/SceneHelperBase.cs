using GameFramework.Scene;

namespace GodotGameFramework.Scene
{
    /// <summary>
    /// 场景辅助器抽象基类。桥接 ISceneHelper 到 Godot 组件系统。
    /// </summary>
    public abstract partial class SceneHelperBase : GodotComponent, ISceneHelper
    {
        /// <summary>
        /// 实例化场景。
        /// </summary>
        /// <param name="sceneAsset">场景资源（PackedScene）。</param>
        /// <returns>实例化后的场景节点。</returns>
        public abstract object InstantiateScene(object sceneAsset);

        /// <summary>
        /// 释放场景。
        /// </summary>
        /// <param name="sceneAsset">场景资源。</param>
        /// <param name="sceneInstance">场景实例。</param>
        public abstract void ReleaseScene(object sceneAsset, object sceneInstance);
    }
}
