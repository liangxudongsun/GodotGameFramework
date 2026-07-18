//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

namespace GameFramework.Resource
{
    /// <summary>
    /// 加载场景回调函数集。
    /// 注：Godot 资源系统自动处理依赖，不需要 Unity 式的 DependencyAsset 回调，已移除。
    /// </summary>
    public sealed class LoadSceneCallbacks
    {
        private readonly LoadSceneSuccessCallback m_LoadSceneSuccessCallback;
        private readonly LoadSceneFailureCallback m_LoadSceneFailureCallback;
        private readonly LoadSceneUpdateCallback m_LoadSceneUpdateCallback;

        public LoadSceneCallbacks(LoadSceneSuccessCallback loadSceneSuccessCallback)
            : this(loadSceneSuccessCallback, null, null)
        {
        }

        public LoadSceneCallbacks(LoadSceneSuccessCallback loadSceneSuccessCallback, LoadSceneFailureCallback loadSceneFailureCallback)
            : this(loadSceneSuccessCallback, loadSceneFailureCallback, null)
        {
        }

        public LoadSceneCallbacks(LoadSceneSuccessCallback loadSceneSuccessCallback, LoadSceneUpdateCallback loadSceneUpdateCallback)
            : this(loadSceneSuccessCallback, null, loadSceneUpdateCallback)
        {
        }

        public LoadSceneCallbacks(LoadSceneSuccessCallback loadSceneSuccessCallback, LoadSceneFailureCallback loadSceneFailureCallback, LoadSceneUpdateCallback loadSceneUpdateCallback)
        {
            m_LoadSceneSuccessCallback = loadSceneSuccessCallback ?? throw new GameFrameworkException("Load scene success callback is invalid.");
            m_LoadSceneFailureCallback = loadSceneFailureCallback;
            m_LoadSceneUpdateCallback = loadSceneUpdateCallback;
        }

        public LoadSceneSuccessCallback LoadSceneSuccessCallback => m_LoadSceneSuccessCallback;
        public LoadSceneFailureCallback LoadSceneFailureCallback => m_LoadSceneFailureCallback;
        public LoadSceneUpdateCallback LoadSceneUpdateCallback => m_LoadSceneUpdateCallback;
    }
}
