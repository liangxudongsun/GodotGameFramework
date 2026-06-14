//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 显示实体信息。
    ///
    /// 用于 ShowEntity 方法的内部数据传递，
    /// 携带 EntityLogic 类型、加载序列号、实体 ID 等信息。
    ///
    /// 对齐 UGF: 实现 IReference 接口，使用 ReferencePool 管理，
    /// 避免 GC 分配。通过 static Create() 工厂方法获取实例。
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
        /// 获取或设置加载序列号。
        /// 用于标识一次异步加载请求，支持取消操作。
        /// </summary>
        public int SerialId { get; set; }

        /// <summary>
        /// 获取或设置实体编号。
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// 获取或设置实体组名称。
        /// </summary>
        public string EntityGroupName { get; set; }

        /// <summary>
        /// 获取或设置加载开始时间（真实流逝时间）。
        /// 用于计算异步加载耗时。
        /// </summary>
        public float StartTime { get; set; }

        /// <summary>
        /// 初始化 ShowEntityInfo 的新实例。
        /// </summary>
        public ShowEntityInfo()
        {
            EntityLogicType = null;
            UserData = null;
            SerialId = 0;
            EntityId = 0;
            EntityGroupName = null;
            StartTime = 0f;
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
            SerialId = 0;
            EntityId = 0;
            EntityGroupName = null;
            StartTime = 0f;
        }
    }
}
