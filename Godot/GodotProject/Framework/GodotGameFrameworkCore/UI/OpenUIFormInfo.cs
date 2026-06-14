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
    /// 打开界面信息。
    ///
    /// 用于 OpenUIForm 方法的内部数据传递，
    /// 携带 UIFormLogic 类型、加载序列号、用户数据等信息。
    ///
    /// 对标 Entity 系统中的 ShowEntityInfo。
    /// 对齐 UGF: 实现 IReference 接口，使用 ReferencePool 管理，避免 GC 分配。
    /// </summary>
    public class OpenUIFormInfo : IReference
    {
        /// <summary>
        /// 获取或设置用户指定的 UIFormLogic 类型。
        /// 如果为 null，则不创建 UIFormLogic 实例。
        /// </summary>
        public Type UIFormLogicType { get; set; }

        /// <summary>
        /// 获取或设置透传给 UIFormLogic.OnInit 和 OnOpen 的用户数据。
        /// </summary>
        public object UserData { get; set; }

        /// <summary>
        /// 获取或设置加载序列号。
        /// 用于标识一次异步加载请求。
        /// </summary>
        public int SerialId { get; set; }

        /// <summary>
        /// 获取或设置界面组名称。
        /// </summary>
        public string UIGroupName { get; set; }

        /// <summary>
        /// 获取或设置是否暂停被覆盖的界面。
        /// </summary>
        public bool PauseCoveredUIForm { get; set; }

        /// <summary>
        /// 获取或设置加载开始时间（真实流逝时间）。
        /// 用于计算异步加载耗时。
        /// </summary>
        public float StartTime { get; set; }

        /// <summary>
        /// 初始化 OpenUIFormInfo 的新实例。
        /// </summary>
        public OpenUIFormInfo()
        {
            UIFormLogicType = null;
            UserData = null;
            SerialId = 0;
            UIGroupName = null;
            PauseCoveredUIForm = false;
            StartTime = 0f;
        }

        /// <summary>
        /// 创建打开界面信息。
        /// UGF 风格：从引用池获取实例，避免 GC。
        /// </summary>
        /// <param name="uiFormLogicType">UIFormLogic 类型。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>打开界面信息实例。</returns>
        public static OpenUIFormInfo Create(Type uiFormLogicType, object userData)
        {
            OpenUIFormInfo openUIFormInfo = ReferencePool.Acquire<OpenUIFormInfo>();
            openUIFormInfo.UIFormLogicType = uiFormLogicType;
            openUIFormInfo.UserData = userData;
            return openUIFormInfo;
        }

        /// <summary>
        /// 清理打开界面信息。
        /// IReference.Clear 实现，重置所有字段。
        /// </summary>
        public void Clear()
        {
            UIFormLogicType = null;
            UserData = null;
            SerialId = 0;
            UIGroupName = null;
            PauseCoveredUIForm = false;
            StartTime = 0f;
        }
    }
}
