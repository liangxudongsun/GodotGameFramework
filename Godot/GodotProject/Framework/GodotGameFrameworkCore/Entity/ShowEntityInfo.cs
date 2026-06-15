//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System;

namespace GodotGameFramework.Entity
{
    /// <summary>
    /// 显示实体信息。
    ///
    /// 用于 ShowEntity&lt;T&gt; 泛型方法的内部数据传递，
    /// 携带 EntityLogic 类型和用户数据。
    /// DefaultEntityHelper 通过 EntityLogicType 创建对应的 EntityLogic 实例，
    /// Entity.OnInit 解包 UserData 传给 EntityLogic.OnInit。
    ///
    /// 异步加载的序列号、取消等由核心 IEntityManager 内部管理，
    /// ShowEntityInfo 不再需要关心。
    ///
    /// 对齐 UGF: 实现 IReference 接口，使用 ReferencePool 管理。
    /// </summary>
    public class ShowEntityInfo : IReference
    {
        /// <summary>
        /// 获取或设置用户指定的 EntityLogic 类型。
        /// 如果为 null，则不创建 EntityLogic 实例。
        /// </summary>
        public Type EntityLogicType { get; set; }

        /// <summary>
        /// 获取或设置透传给 EntityLogic.OnInit 和 OnShow 的用户数据。
        /// </summary>
        public object UserData { get; set; }

        /// <summary>
        /// 初始化 ShowEntityInfo 的新实例。
        /// </summary>
        public ShowEntityInfo()
        {
            EntityLogicType = null;
            UserData = null;
        }

        /// <summary>
        /// 创建显示实体信息。
        /// UGF 风格：从引用池获取实例，避免 GC。
        /// </summary>
        /// <param name="entityLogicType">EntityLogic 类型。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示实体信息实例。</returns>
        public static ShowEntityInfo Create(Type entityLogicType, object userData)
        {
            ShowEntityInfo showEntityInfo = ReferencePool.Acquire<ShowEntityInfo>();
            showEntityInfo.EntityLogicType = entityLogicType;
            showEntityInfo.UserData = userData;
            return showEntityInfo;
        }

        /// <summary>
        /// 清理显示实体信息。
        /// IReference.Clear 实现，重置所有字段。
        /// </summary>
        public void Clear()
        {
            EntityLogicType = null;
            UserData = null;
        }
    }
}
