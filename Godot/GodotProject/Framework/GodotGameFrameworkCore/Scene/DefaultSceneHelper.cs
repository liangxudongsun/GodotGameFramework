using Godot;

namespace GodotGameFramework.Scene
{
    /// <summary>
    /// 默认场景辅助器。
    /// InstantiateScene：将 PackedScene 实例化为 Godot Node。
    /// ReleaseScene：通过 QueueFree 释放场景节点。
    /// </summary>
    public partial class DefaultSceneHelper : SceneHelperBase
    {
        public override object InstantiateScene(object sceneAsset)
        {
            if (sceneAsset is PackedScene packedScene)
            {
                return packedScene.Instantiate();
            }

            Log.Warning("Scene asset is not a PackedScene: {0}.",
                sceneAsset?.GetType().Name ?? "null");
            return null;
        }

        public override void ReleaseScene(object sceneAsset, object sceneInstance)
        {
            if (sceneInstance is Node node)
            {
                node.QueueFree();
            }
        }
    }
}
