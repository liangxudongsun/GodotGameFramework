namespace GameFramework.Scene
{
    /// <summary>
    /// 场景辅助器接口。
    /// </summary>
    public interface ISceneHelper
    {
        /// <summary>
        /// 实例化场景。
        /// </summary>
        /// <param name="sceneAsset">要实例化的场景资源（PackedScene）。</param>
        /// <returns>实例化后的场景（Node）。</returns>
        object InstantiateScene(object sceneAsset);

        /// <summary>
        /// 释放场景。
        /// </summary>
        /// <param name="sceneAsset">要释放的场景资源。</param>
        /// <param name="sceneInstance">要释放的场景实例。</param>
        void ReleaseScene(object sceneAsset, object sceneInstance);
    }
}
