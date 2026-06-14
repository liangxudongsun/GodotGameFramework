//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.ObjectPool;
using GameFramework.UI;

namespace GodotGameFramework
{
    /// <summary>
    /// 界面实例对象。
    ///
    /// 继承自 ObjectBase，用于对象池管理 UI 窗体实例。
    /// 当 UI 窗体被关闭时，实例对象会被归还到池中等待复用，
    /// 而不是直接销毁。当池满或实例过期时，才真正释放节点。
    ///
    /// 工作流程：
    /// 1. OpenUIForm → 从池中 Spawn（如果有可用实例）
    /// 2. CloseUIForm → Unspawn 回池（不销毁）
    /// 3. 池容量/过期触发 Release → QueueFree 真正销毁
    ///
    /// 对标 EntityInstanceObject，完全复用相同模式。
    /// 对应 UGF 中 UIManager.UIFormInstanceObject（internal 类）。
    /// </summary>
    public sealed class UIFormInstanceObject : ObjectBase
    {
        /// <summary>
        /// 界面资源（PackedScene）。
        /// 用于在池释放时传递给 UIFormHelper.ReleaseUIForm。
        /// </summary>
        private object m_UIFormAsset;

        /// <summary>
        /// 界面辅助器。
        /// 用于在池释放时调用 ReleaseUIForm。
        /// </summary>
        private IUIFormHelper m_UIFormHelper;

        /// <summary>
        /// 创建界面实例对象。
        /// UGF 风格：使用 ReferencePool 获取实例，避免 GC。
        /// </summary>
        /// <param name="name">界面资源名称（作为池中的键）。</param>
        /// <param name="uiFormAsset">界面资源（PackedScene）。</param>
        /// <param name="uiFormInstance">界面实例（Node）。</param>
        /// <param name="uiFormHelper">界面辅助器。</param>
        /// <returns>创建的界面实例对象。</returns>
        public static UIFormInstanceObject Create(string name, object uiFormAsset,
            object uiFormInstance, IUIFormHelper uiFormHelper)
        {
            if (uiFormAsset == null)
            {
                throw new GameFramework.GameFrameworkException("UI form asset is invalid.");
            }

            if (uiFormHelper == null)
            {
                throw new GameFramework.GameFrameworkException("UI form helper is invalid.");
            }

            UIFormInstanceObject uiFormInstanceObject = ReferencePool.Acquire<UIFormInstanceObject>();
            uiFormInstanceObject.Initialize(name, uiFormInstance);
            uiFormInstanceObject.m_UIFormAsset = uiFormAsset;
            uiFormInstanceObject.m_UIFormHelper = uiFormHelper;
            return uiFormInstanceObject;
        }

        /// <summary>
        /// 清理界面实例对象。
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            m_UIFormAsset = null;
            m_UIFormHelper = null;
        }

        /// <summary>
        /// 释放界面实例对象。
        /// 当对象池决定释放此对象时调用（池满或过期）。
        /// 调用 UIFormHelper.ReleaseUIForm 真正销毁节点。
        /// </summary>
        /// <param name="isShutdown">是否是关闭时释放。</param>
        protected internal override void Release(bool isShutdown)
        {
            m_UIFormHelper.ReleaseUIForm(m_UIFormAsset, Target);
        }
    }
}
