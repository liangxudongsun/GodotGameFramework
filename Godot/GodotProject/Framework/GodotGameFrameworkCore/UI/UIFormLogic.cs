//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.ObjectPool;
using GameFramework.UI;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 界面逻辑基类。
    ///
    /// 纯 C# 抽象类（非 Node），供用户继承编写 UI 窗体逻辑。
    /// 与 EntityLogic 设计模式一致：不继承 Node，通过 Owner 属性
    /// 持有 UIForm 引用来访问实际的 Godot 节点。
    ///
    /// 用户继承此类来编写具体的 UI 逻辑，例如：
    /// <code>
    /// public class MainMenuForm : UIFormLogic
    /// {
    ///     private Button m_StartButton;
    ///
    ///     protected internal override void OnInit(object userData)
    ///     {
    ///         base.OnInit(userData);
    ///         m_StartButton = CachedControl.GetNode&lt;Button&gt;("StartButton");
    ///         m_StartButton.Pressed += OnStartButtonPressed;
    ///     }
    ///
    ///     protected internal override void OnOpen(object userData)
    ///     {
    ///         base.OnOpen(userData);
    ///         // 界面打开后的逻辑
    ///     }
    /// }
    /// </code>
    ///
    /// 状态管理：
    /// - Available: 标记界面是否处于打开状态（OnOpen → true, OnClose → false）
    /// - Visible: 标记界面是否可见（OnPause → false, OnResume → true）
    ///
    /// UIItem 支持：
    /// 提供 SpawnItem/UnspawnItem/UnspawnAllItems 便捷方法，
    /// 用于管理 UIForm 内部子元素的对象池。
    /// OnRecycle 时自动清理所有 UIItem。
    ///
    /// 对标 UGF 中的 UIFormLogic (MonoBehaviour)。
    /// </summary>
    public abstract class UIFormLogic
    {
        /// <summary>界面是否可用（处于打开状态）。</summary>
        private bool m_Available;

        /// <summary>界面是否可见。</summary>
        private bool m_Visible;

        /// <summary>所属的 UIForm 实例。</summary>
        private UIForm m_UIForm;

        /// <summary>
        /// UIItem 对象池集合。
        /// Key: 对象池 ID（基于 PackedScene 路径生成）
        /// Value: 该类型的对象池实例
        ///
        /// OnRecycle 时自动释放所有池。
        /// </summary>
        private readonly Dictionary<string, IObjectPool<UIItemInstanceObject>> m_ItemPools = new();

        /// <summary>
        /// 获取界面。
        /// </summary>
        public UIForm UIForm
        {
            get { return m_UIForm; }
        }

        /// <summary>
        /// 获取或设置界面名称。
        /// </summary>
        public string Name
        {
            get { return m_UIForm?.Name; }
            set { if (m_UIForm != null) m_UIForm.Name = value; }
        }

        /// <summary>
        /// 获取界面是否可用。
        ///
        /// Available 在 OnOpen 时设为 true，OnClose 时设为 false。
        /// 只有 Available 为 true 时才能设置 Visible。
        /// </summary>
        public bool Available
        {
            get { return m_Available; }
        }

        /// <summary>
        /// 获取或设置界面是否可见。
        ///
        /// Visible 控制实际的节点可见性。
        /// 当界面被暂停时 Visible 自动设为 false，恢复时设为 true。
        /// </summary>
        public bool Visible
        {
            get { return m_Available && m_Visible; }
            set
            {
                if (!m_Available)
                {
                    Log.Warning("UI form '{0}' is not available.", Name);
                    return;
                }

                if (m_Visible == value)
                {
                    return;
                }

                m_Visible = value;
                InternalSetVisible(value);
            }
        }

        /// <summary>
        /// 获取已缓存的 Control 节点。
        ///
        /// 返回 UIForm 的第一个子节点（实际的 UI 控件）。
        /// 对标 UGF UIFormLogic.CachedTransform。
        /// </summary>
        public Control CachedControl
        {
            get
            {
                if (m_UIForm == null || m_UIForm.GetChildCount() <= 0)
                {
                    return null;
                }

                return m_UIForm.GetChild(0) as Control;
            }
        }

        private List<UIStringKey> m_UIStringKeys;
        public List<UIStringKey> UIStringKeys
        {
            get
            {
                if (m_UIStringKeys == null)
                {
                    m_UIStringKeys = CachedControl.GetChildren<UIStringKey>();
                }
                return m_UIStringKeys;
            }
        }

        /// <summary>
        /// 内部方法：设置 UIForm 引用。
        /// 由 UIForm.SetUIFormLogic 调用，建立双向关联。
        /// </summary>
        /// <param name="uiForm">所属的 UIForm 实例。</param>
        internal void InternalSetUIForm(UIForm uiForm)
        {
            m_UIForm = uiForm;
        }

        /// <summary>
        /// 界面初始化。
        ///
        /// 仅在首次创建时调用一次（对象池复用时跳过）。
        /// 用户应在此方法中获取子节点引用、绑定事件等。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnInit(object userData)
        {

        }

        /// <summary>
        /// 界面回收。
        ///
        /// 界面被关闭后调用，用于清理运行时状态。
        /// 注意：不要在此方法中释放子节点引用（对象池复用需要保留）。
        /// 框架会自动清理所有 UIItem 对象池。
        /// </summary>
        protected internal virtual void OnRecycle()
        {
        }

        /// <summary>
        /// 界面打开。
        ///
        /// 每次打开界面时调用（包括对象池复用后重新打开）。
        /// 基类实现：设置 Available=true，Visible=true。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnOpen(object userData)
        {
            m_Available = true;
            Visible = true;
            UIStringKeys.ForEach(key => key.SetValue());
        }

        /// <summary>
        /// 界面关闭。
        ///
        /// 基类实现：Visible=false，Available=false。
        /// </summary>
        /// <param name="isShutdown">是否是关闭界面管理器时触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnClose(bool isShutdown, object userData)
        {
            Visible = false;
            m_Available = false;
        }

        /// <summary>
        /// 界面暂停。
        ///
        /// 当界面被同一组中 PauseCoveredUIForm=true 的窗体覆盖时调用。
        /// 基类实现：Visible=false。
        /// </summary>
        protected internal virtual void OnPause()
        {
            Visible = false;
        }

        /// <summary>
        /// 界面暂停恢复。
        ///
        /// 当覆盖在上的窗体被关闭后调用。
        /// 基类实现：Visible=true。
        /// </summary>
        protected internal virtual void OnResume()
        {
            Visible = true;
        }

        /// <summary>
        /// 界面遮挡。
        ///
        /// 当同一组中有新窗体打开在该窗体之上时调用。
        /// 基类实现为空，用户可覆盖以实现自定义遮挡效果。
        /// </summary>
        protected internal virtual void OnCover()
        {
        }

        /// <summary>
        /// 界面遮挡恢复。
        ///
        /// 当覆盖在上的窗体被关闭后调用。
        /// 基类实现为空，用户可覆盖以实现自定义恢复效果。
        /// </summary>
        protected internal virtual void OnReveal()
        {
        }

        /// <summary>
        /// 界面重新获得焦点。
        ///
        /// 当窗体被 RefocusUIForm 移动到组的最顶部时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected internal virtual void OnRefocus(object userData)
        {
        }

        /// <summary>
        /// 界面轮询。
        ///
        /// 仅在界面处于未暂停状态时调用。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 界面深度改变。
        ///
        /// 当 UI 组的 Refresh 操作重新计算深度时调用。
        /// </summary>
        /// <param name="uiGroupDepth">界面组深度。</param>
        /// <param name="depthInUIGroup">界面在界面组中的深度。</param>
        protected internal virtual void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
        }

        /// <summary>
        /// 设置界面的可见性。
        ///
        /// 默认实现：控制 CachedControl 的 Visible 属性。
        /// 用户可覆盖此方法实现自定义可见性效果（如渐隐渐现）。
        /// </summary>
        /// <param name="visible">界面的可见性。</param>
        protected virtual void InternalSetVisible(bool visible)
        {
            Control control = CachedControl;
            if (control != null)
            {
                control.Visible = visible;
            }
        }

        // ================================================================
        //  UIItem 对象池管理
        // ================================================================

        /// <summary>
        /// 从对象池获取一个 UIItem 实例（带逻辑类）。
        ///
        /// 如果池中有可用实例则直接复用，否则从 PackedScene 实例化新节点
        /// 并创建 TLogic 逻辑实例。
        ///
        /// 对标 UGF UIFormBase.SpawnItem&lt;T&gt;()。
        /// </summary>
        /// <typeparam name="TLogic">UIItemBase 子类类型。</typeparam>
        /// <param name="itemScene">UIItem 的 PackedScene 资源。</param>
        /// <param name="container">UIItem 实例化后的父容器节点。</param>
        /// <param name="autoReleaseInterval">对象池自动释放间隔（秒）。</param>
        /// <param name="capacity">对象池容量。</param>
        /// <param name="expireTime">对象过期时间（秒）。</param>
        /// <returns>UIItem 实例对象。</returns>
        protected UIItemInstanceObject SpawnItem<TLogic>(PackedScene itemScene, Node container,
            float autoReleaseInterval = 5f, int capacity = 50, float expireTime = 50f)
            where TLogic : UIItemBase
        {
            UIItemInstanceObject spawn = SpawnItem(itemScene, container,
                autoReleaseInterval, capacity, expireTime);
            if (spawn == null)
            {
                return null;
            }

            // 仅在新创建时（池中无可用实例）才创建逻辑类
            if (spawn.ItemLogic == null)
            {
                try
                {
                    TLogic logic = (TLogic)Activator.CreateInstance(typeof(TLogic));
                    logic.InternalSetOwner(spawn);
                    logic.OnInit();
                    spawn.InternalSetItemLogic(logic);
                }
                catch (System.Exception exception)
                {
                    Log.Error("Create UI item logic '{0}' with exception '{1}'.",
                        typeof(TLogic).FullName, exception);
                }
            }

            return spawn;
        }

        /// <summary>
        /// 从对象池获取一个 UIItem 实例（不带逻辑类）。
        ///
        /// 仅实例化 PackedScene 节点，不创建 UIItemBase 逻辑类。
        /// 适用于不需要逻辑的简单 UI 子元素复用场景。
        ///
        /// 如果池中有可用实例则直接复用，否则从 PackedScene 实例化新节点。
        /// 新实例会自动添加到 container 节点下。
        ///
        /// 对标 UGF UIFormBase.SpawnItem&lt;T&gt;()。
        /// </summary>
        /// <param name="itemScene">UIItem 的 PackedScene 资源。</param>
        /// <param name="container">UIItem 实例化后的父容器节点。</param>
        /// <param name="autoReleaseInterval">对象池自动释放间隔（秒）。</param>
        /// <param name="capacity">对象池容量。</param>
        /// <param name="expireTime">对象过期时间（秒）。</param>
        /// <returns>UIItem 实例对象。</returns>
        protected UIItemInstanceObject SpawnItem(PackedScene itemScene, Node container,
            float autoReleaseInterval = 5f, int capacity = 50, float expireTime = 50f)
        {
            if (itemScene == null)
            {
                Log.Error("UI item scene is invalid.");
                return null;
            }

            if (container == null)
            {
                Log.Error("UI item container is invalid.");
                return null;
            }

            string poolId = GetItemPoolId(itemScene);
            IObjectPool<UIItemInstanceObject> pool;

            // 尝试获取已有的对象池
            if (m_ItemPools.TryGetValue(poolId, out pool))
            {
                pool.AutoReleaseInterval = autoReleaseInterval;
                pool.Capacity = capacity;
                pool.ExpireTime = expireTime;
            }
            else
            {
                // 创建新的单次生成对象池
                pool = GameFrameworkEntry.GetModule<IObjectPoolManager>()
                    .CreateSingleSpawnObjectPool<UIItemInstanceObject>(
                        poolId, autoReleaseInterval, capacity, expireTime, 0);
                m_ItemPools[poolId] = pool;
            }

            // 从池中 Spawn（必须传 poolId 匹配注册时的 Name）
            UIItemInstanceObject spawn = pool.Spawn(poolId);
            if (spawn == null)
            {
                // 池中没有可用实例，从 PackedScene 实例化
                Node itemInstance = itemScene.Instantiate<Node>();
                container.AddChild(itemInstance);

                spawn = UIItemInstanceObject.Create(poolId, itemInstance);
                pool.Register(spawn, true);
            }

            return spawn;
        }

        /// <summary>
        /// 从对象池回收一个 UIItem 实例。
        ///
        /// 对标 UGF UIFormBase.UnspawnItem&lt;T&gt;()。
        /// </summary>
        /// <param name="itemScene">UIItem 的 PackedScene 资源。</param>
        /// <param name="itemObject">要回收的 UIItem 实例对象。</param>
        protected void UnspawnItem(PackedScene itemScene, UIItemInstanceObject itemObject)
        {
            if (itemScene == null || itemObject == null)
            {
                return;
            }

            string poolId = GetItemPoolId(itemScene);
            if (!m_ItemPools.TryGetValue(poolId, out IObjectPool<UIItemInstanceObject> pool))
            {
                return;
            }

            pool.Unspawn(itemObject);
        }

        /// <summary>
        /// 回收指定类型的所有 UIItem 实例。
        ///
        /// 对标 UGF UIFormBase.UnspawnAllItem&lt;T&gt;()。
        /// </summary>
        /// <param name="itemScene">UIItem 的 PackedScene 资源。</param>
        protected void UnspawnAllItems(PackedScene itemScene)
        {
            if (itemScene == null)
            {
                return;
            }

            string poolId = GetItemPoolId(itemScene);
            if (!m_ItemPools.TryGetValue(poolId, out IObjectPool<UIItemInstanceObject> pool))
            {
                return;
            }

            pool.ReleaseAllUnused();
            pool.UnspawnAll();
        }

        /// <summary>
        /// 回收所有 UIItem 实例并销毁对象池。
        ///
        /// 在 UIForm 关闭时自动调用。
        /// </summary>
        private void DestroyAllItemPools()
        {
            IObjectPoolManager objectPoolManager = GameFrameworkEntry.GetModule<IObjectPoolManager>();
            if (objectPoolManager == null)
            {
                return;
            }

            foreach (var kvp in m_ItemPools)
            {
                kvp.Value.ReleaseAllUnused();
                kvp.Value.UnspawnAll();
                objectPoolManager.DestroyObjectPool<UIItemInstanceObject>(kvp.Key);
            }

            m_ItemPools.Clear();
        }

        /// <summary>
        /// 生成对象池 ID。
        /// 基于 UIForm 的序列号和 PackedScene 的资源路径生成唯一键。
        /// </summary>
        private string GetItemPoolId(PackedScene itemScene)
        {
            // 使用资源路径作为池 ID，确保同一 PackedScene 共享同一个池
            return itemScene.ResourcePath;
        }

        /// <summary>
        /// 内部方法：回收所有 UIItem。
        /// 由 UIComponent 在 OnRecycle 之前调用。
        /// </summary>
        internal void InternalRecycleItems()
        {
            DestroyAllItemPools();
        }
    }
}
