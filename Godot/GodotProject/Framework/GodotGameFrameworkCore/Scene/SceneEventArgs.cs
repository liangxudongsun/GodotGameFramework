using GameFramework;
using GameFramework.Event;
using Godot;

namespace GodotGameFramework.Scene
{
    /// <summary>加载场景成功事件。</summary>
    public sealed class LoadSceneSuccessEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(LoadSceneSuccessEventArgs).GetHashCode();

        public override int Id => EventId;

        public string SceneAssetPath { get; private set; }
        public Node SceneInstance { get; private set; }

        public static LoadSceneSuccessEventArgs Create(string assetPath, Node instance)
        {
            LoadSceneSuccessEventArgs e = ReferencePool.Acquire<LoadSceneSuccessEventArgs>();
            e.SceneAssetPath = assetPath;
            e.SceneInstance = instance;
            return e;
        }

        public override void Clear()
        {
            SceneAssetPath = null;
            SceneInstance = null;
        }
    }

    /// <summary>加载场景失败事件。</summary>
    public sealed class LoadSceneFailureEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(LoadSceneFailureEventArgs).GetHashCode();

        public override int Id => EventId;

        public string SceneAssetPath { get; private set; }
        public string ErrorMessage { get; private set; }

        public static LoadSceneFailureEventArgs Create(string assetPath, string errorMessage)
        {
            LoadSceneFailureEventArgs e = ReferencePool.Acquire<LoadSceneFailureEventArgs>();
            e.SceneAssetPath = assetPath;
            e.ErrorMessage = errorMessage;
            return e;
        }

        public override void Clear()
        {
            SceneAssetPath = null;
            ErrorMessage = null;
        }
    }

    /// <summary>卸载场景成功事件。</summary>
    public sealed class UnloadSceneSuccessEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(UnloadSceneSuccessEventArgs).GetHashCode();

        public override int Id => EventId;

        public string SceneAssetPath { get; private set; }

        public static UnloadSceneSuccessEventArgs Create(string assetPath)
        {
            UnloadSceneSuccessEventArgs e = ReferencePool.Acquire<UnloadSceneSuccessEventArgs>();
            e.SceneAssetPath = assetPath;
            return e;
        }

        public override void Clear()
        {
            SceneAssetPath = null;
        }
    }
}
