//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Entity;

namespace GodotGameFramework.Entity
{
    public abstract partial class EntityHelperBase : GodotComponent, IEntityHelper
    {
        public abstract IEntity CreateEntity(object entityInstance, IEntityGroup entityGroup, object userData);
        public abstract object InstantiateEntity(object entityAsset);

        public abstract void ReleaseEntity(object entityAsset, object entityInstance);
    }
}
