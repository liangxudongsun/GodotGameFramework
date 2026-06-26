using GameFramework.UI;
using Godot;
using GodotGameFramework.UI;
using System;
using System.Collections.Generic;
namespace GodotGameFramework
{
    /// <summary>
    /// 界面
    /// </summary>
    [GlobalClass]
    public abstract partial class ControlUIForm : Control, IUIForm
    {
        /// <summary>
        /// 界面序列编号。
        /// </summary>
        private int m_SerialId;

        /// <summary>
        /// 界面资源名称。
        /// </summary>
        private string m_UIFormAssetName;

        /// <summary>
        /// 界面所属的界面组。
        /// </summary>
        private IUIGroup m_UIGroup;

        /// <summary>
        /// 界面在界面组中的深度。
        /// </summary>
        private int m_DepthInUIGroup;

        /// <summary
        /// >是否暂停被覆盖的界面。
        /// </summary>
        private bool m_PauseCoveredUIForm;

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
        /// </summary>
        public object Handle => this;

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

        private List<IStringKey> m_UIStringKeys;
        public List<IStringKey> UIStringKeys
        {
            get
            {
                if (m_UIStringKeys == null)
                {
                    m_UIStringKeys = this.FindChildrenOfType<IStringKey>() ?? new List<IStringKey>();
                }
                return m_UIStringKeys;
            }
        }
        /// <summary>
        /// 初始化界面。
        /// </summary>
        /// <param name="serialId">界面序列编号。</param>
        /// <param name="uiFormAssetName">界面资源名称。</param>
        /// <param name="uiGroup">界面所处的界面组。</param>
        /// <param name="pauseCoveredUIForm">是否暂停被覆盖的界面。</param>
        /// <param name="isNewInstance">是否是新实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
        {
            m_SerialId = serialId;
            m_UIFormAssetName = uiFormAssetName;
            m_UIGroup = uiGroup;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = pauseCoveredUIForm;
            UIStringKeys.ForEach(key => key.SetValue());
        }

        /// <summary>
        /// 界面回收。
        ///
        /// </summary>
        public virtual void OnRecycle()
        {
            m_SerialId = 0;
            m_DepthInUIGroup = 0;
            m_PauseCoveredUIForm = true;
            Visible = false;
        }

        /// <summary>
        /// 界面打开。
        /// </summary>
        public virtual void OnOpen(object userData)
        {
            Visible = true;
        }

        /// <summary>
        /// 界面关闭。
        /// </summary>
        public virtual void OnClose(bool isShutdown, object userData)
        {
            Visible = false;
        }

        /// <summary>
        /// 界面暂停。
        /// </summary>
        public virtual void OnPause()
        {

        }

        /// <summary>
        /// 界面暂停恢复。
        /// </summary>
        public virtual void OnResume()
        {

        }

        /// <summary>
        /// 界面遮挡。
        /// </summary>
        public virtual void OnCover()
        {

        }

        /// <summary>
        /// 界面遮挡恢复。
        /// </summary>
        public virtual void OnReveal()
        {

        }

        /// <summary>
        /// 界面重新获得焦点。
        /// </summary>
        public virtual void OnRefocus(object userData)
        {

        }

        /// <summary>
        /// 界面轮询。
        /// </summary>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {

        }

        /// <summary>
        /// 界面深度改变。
        /// </summary>
        public virtual void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            m_DepthInUIGroup = depthInUIGroup;
        }

        public void Close()
        {
            GF.UI.CloseUIForm(this);
        }
    }
}
