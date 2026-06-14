//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.UI;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 界面。
    ///
    /// 继承 Node 并实现 IUIForm 接口，作为 UI 窗体的包装器。
    /// 实际的 UI 控件（Control）是其第一个子节点。
    ///
    /// 架构说明：
    /// UIForm 是框架与 UI 控件之间的桥梁。
    /// 核心框架通过 IUIForm 接口管理生命周期，
    /// UIForm 将所有生命周期调用委托给 UIFormLogic（纯 C# 逻辑类）。
    ///
    /// <code>
    /// UIForm (Node, IUIForm)          ← 框架管理层
    ///   └── [Control from PackedScene] ← 实际的 UI 控件
    ///         └── UIFormLogic          ← 用户逻辑（纯 C# 类）
    /// </code>
    ///
    /// 对标 UGF 中的 UIForm (MonoBehaviour, IUIForm)。
    /// 方法体级别对齐 UGF 的实现逻辑。
    /// </summary>
    public sealed partial class UIForm : Node, IUIForm
    {
        /// <summary>界面序列编号。</summary>
        private int m_SerialId;

        /// <summary>界面资源名称。</summary>
        private string m_UIFormAssetName;

        /// <summary>界面所属的界面组。</summary>
        private IUIGroup m_UIGroup;

        /// <summary>界面在界面组中的深度。</summary>
        private int m_DepthInUIGroup;

        /// <summary>是否暂停被覆盖的界面。</summary>
        private bool m_PauseCoveredUIForm;

        /// <summary>界面逻辑实例。</summary>
        private UIFormLogic m_UIFormLogic;

        /// <summary>
        /// 获取界面序列编号。
        /// </summary>
        public int SerialId => m_SerialId;

        /// <summary>
        /// 获取界面资源名称。
        /// </summary>
        public string UIFormAssetName => m_UIFormAssetName;

        /// <summary>
        /// 获取界面实例。
        ///
        /// 返回实际的 UI 控件节点（第一个子节点）。
        /// 对标 UGF 中 UIForm.Handle 返回 gameObject。
        /// </summary>
        public object Handle => GetChild(0);

        /// <summary>
        /// 获取界面所属的界面组。
        /// </summary>
        public IUIGroup UIGroup => m_UIGroup;

        /// <summary>
        /// 获取界面深度。
        /// </summary>
        public int DepthInUIGroup => m_DepthInUIGroup;

        /// <summary>
        /// 获取是否暂停被覆盖的界面。
        /// </summary>
        public bool PauseCoveredUIForm => m_PauseCoveredUIForm;

        /// <summary>
        /// 获取界面逻辑。
        /// </summary>
        public UIFormLogic Logic => m_UIFormLogic;

        /// <summary>
        /// 设置界面逻辑实例。
        ///
        /// 由 DefaultUIFormHelper.CreateUIForm 在创建 UIForm 时调用。
        /// 仅在首次创建时调用一次，对象池复用时不会重新设置。
        /// </summary>
        /// <param name="logic">要设置的界面逻辑实例。</param>
        internal void SetUIFormLogic(UIFormLogic logic)
        {
            m_UIFormLogic = logic;
            if (m_UIFormLogic != null)
            {
                m_UIFormLogic.InternalSetUIForm(this);
            }
        }

        /// <summary>
        /// 初始化界面。
        ///
        /// 对标 UGF UIForm.OnInit，方法体级别对齐。
        /// isNewInstance 为 true 时才初始化 UIFormLogic（对象池复用时跳过）。
        /// </summary>
        /// <param name="serialId">界面序列编号。</param>
        /// <param name="uiFormAssetName">界面资源名称。</param>
        /// <param name="uiGroup">界面所处的界面组。</param>
        /// <param name="pauseCoveredUIForm">是否暂停被覆盖的界面。</param>
        /// <param name="isNewInstance">是否是新实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup,
            bool pauseCoveredUIForm, bool isNewInstance, object userData)
        {
            m_SerialId = serialId;
            m_UIFormAssetName = uiFormAssetName;
            m_UIGroup = uiGroup;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = pauseCoveredUIForm;

            // 对象池复用时跳过 UIFormLogic 初始化
            // 与 UGF 行为一致：!isNewInstance 直接 return
            if (!isNewInstance)
            {
                return;
            }

            // 检查 UIFormLogic 是否已设置（由 DefaultUIFormHelper 在 CreateUIForm 中设置）
            if (m_UIFormLogic == null)
            {
                Log.Error("UI form '{0}' can not get UI form logic.", uiFormAssetName);
                return;
            }

            try
            {
                m_UIFormLogic.OnInit(userData);
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnInit with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面回收。
        ///
        /// 对标 UGF UIForm.OnRecycle。
        /// </summary>
        public void OnRecycle()
        {
            if (m_UIFormLogic != null)
            {
                try
                {
                    m_UIFormLogic.OnRecycle();
                }
                catch (Exception exception)
                {
                    Log.Error("UI form '[{0}]{1}' OnRecycle with exception '{2}'.",
                        m_SerialId, m_UIFormAssetName, exception);
                }
            }

            // 重置状态字段，但保留 UIFormLogic 引用（对象池复用）
            m_SerialId = 0;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = true;
        }

        /// <summary>
        /// 界面打开。
        /// 对标 UGF UIForm.OnOpen。
        /// </summary>
        public void OnOpen(object userData)
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnOpen(userData);
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnOpen with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面关闭。
        /// 对标 UGF UIForm.OnClose。
        /// </summary>
        public void OnClose(bool isShutdown, object userData)
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnClose(isShutdown, userData);
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnClose with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面暂停。
        /// 对标 UGF UIForm.OnPause。
        /// </summary>
        public void OnPause()
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnPause();
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnPause with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面暂停恢复。
        /// 对标 UGF UIForm.OnResume。
        /// </summary>
        public void OnResume()
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnResume();
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnResume with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面遮挡。
        /// 对标 UGF UIForm.OnCover。
        /// </summary>
        public void OnCover()
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnCover();
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnCover with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面遮挡恢复。
        /// 对标 UGF UIForm.OnReveal。
        /// </summary>
        public void OnReveal()
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnReveal();
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnReveal with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面重新获得焦点。
        /// 对标 UGF UIForm.OnRefocus。
        /// </summary>
        public void OnRefocus(object userData)
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnRefocus(userData);
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnRefocus with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面轮询。
        /// 对标 UGF UIForm.OnUpdate。
        /// </summary>
        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnUpdate(elapseSeconds, realElapseSeconds);
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnUpdate with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }

        /// <summary>
        /// 界面深度改变。
        /// 对标 UGF UIForm.OnDepthChanged。
        /// </summary>
        public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            m_DepthInUIGroup = depthInUIGroup;

            if (m_UIFormLogic == null) return;

            try
            {
                m_UIFormLogic.OnDepthChanged(uiGroupDepth, depthInUIGroup);
            }
            catch (Exception exception)
            {
                Log.Error("UI form '[{0}]{1}' OnDepthChanged with exception '{2}'.",
                    m_SerialId, m_UIFormAssetName, exception);
            }
        }
    }
}
