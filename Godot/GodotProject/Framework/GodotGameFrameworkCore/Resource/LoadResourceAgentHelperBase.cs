//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.FileSystem;
using GameFramework.Resource;
using System;

namespace GodotGameFramework.Resource
{
    public abstract partial class LoadResourceAgentHelperBase : GodotComponent, ILoadResourceAgentHelper
    {
        public abstract event EventHandler<LoadResourceAgentHelperUpdateEventArgs> LoadResourceAgentHelperUpdate;
        public abstract event EventHandler<LoadResourceAgentHelperReadFileCompleteEventArgs> LoadResourceAgentHelperReadFileComplete;
        public abstract event EventHandler<LoadResourceAgentHelperReadBytesCompleteEventArgs> LoadResourceAgentHelperReadBytesComplete;
        public abstract event EventHandler<LoadResourceAgentHelperParseBytesCompleteEventArgs> LoadResourceAgentHelperParseBytesComplete;
        public abstract event EventHandler<LoadResourceAgentHelperLoadCompleteEventArgs> LoadResourceAgentHelperLoadComplete;
        public abstract event EventHandler<LoadResourceAgentHelperErrorEventArgs> LoadResourceAgentHelperError;

        public abstract void LoadAsset(object resource, string assetName, Type assetType, bool isScene);

        public abstract void ParseBytes(byte[] bytes);

        public abstract void ReadBytes(string fullPath);

        public abstract void ReadBytes(IFileSystem fileSystem, string name);

        public abstract void ReadFile(string fullPath);

        public abstract void ReadFile(IFileSystem fileSystem, string name);

        public abstract void Reset();

    }
}
