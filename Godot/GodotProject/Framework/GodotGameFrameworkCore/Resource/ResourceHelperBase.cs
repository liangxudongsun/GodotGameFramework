//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Resource;

namespace GodotGameFramework.Resource
{
    public abstract partial class ResourceHelperBase : GameFrameworkComponent, IResourceHelper
    {
        public abstract void LoadBytes(string fileUri, LoadBytesCallbacks loadBytesCallbacks, object userData);

        public abstract void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks, object userData);

        public abstract void Release(object objectToRelease);
    }
}
