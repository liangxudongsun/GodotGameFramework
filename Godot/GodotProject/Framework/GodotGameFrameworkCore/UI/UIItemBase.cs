//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 界面项逻辑基类。
    ///
    /// 纯 C# 抽象类（非 Node），供用户继承编写 UI 子元素逻辑。
    /// 典型场景：列表项、选项卡、背包格子等需要动态复用的 UI 元素。
    ///
    /// 与 UIFormLogic 的区别：
    /// - UIFormLogic: 整个 UI 窗口的逻辑，由框架管理生命周期
    /// - UIItemBase: 窗口内部的子元素逻辑，由 UIFormLogic 通过对象池管理
    ///
    /// 使用方式：
    /// <code>
    /// public class ShopItem : UIItemBase
    /// {
    ///     private TextureRect m_Icon;
    ///     private Label m_PriceLabel;
    ///
    ///     protected override void OnInit()
    ///     {
    ///         base.OnInit();
    ///         m_Icon = CachedNode.GetNode&lt;TextureRect&gt;("Icon");
    ///         m_PriceLabel = CachedNode.GetNode&lt;Label&gt;("Price");
    ///     }
    ///
    ///     public void SetData(string iconPath, int price)
    ///     {
    ///         m_Icon.Texture = GD.Load&lt;Texture2D&gt;(iconPath);
    ///         m_PriceLabel.Text = price.ToString();
    ///     }
    /// }
    /// </code>
    ///
    /// 对标 UGF 测试项目中的 UIItemBase（MonoBehaviour → 纯 C# 类）。
    /// </summary>
    public abstract class UIItemBase
    {
        /// <summary>所属的 UIItemInstanceObject。</summary>
        private UIItemInstanceObject m_Owner;

        /// <summary>
        /// 获取所属的 UIItemInstanceObject。
        /// </summary>
        public UIItemInstanceObject Owner
        {
            get { return m_Owner; }
        }

        /// <summary>
        /// 获取已缓存的节点。
        ///
        /// 返回 UIItemInstanceObject.Target（实际的 Godot 节点）。
        /// </summary>
        public Node CachedNode
        {
            get { return m_Owner?.Target as Node; }
        }

        /// <summary>
        /// 内部方法：设置所属的 UIItemInstanceObject。
        /// </summary>
        /// <param name="owner">所属的 UIItemInstanceObject。</param>
        internal void InternalSetOwner(UIItemInstanceObject owner)
        {
            m_Owner = owner;
        }

        /// <summary>
        /// 界面项初始化。
        ///
        /// 在 UIItem 首次被创建并注册到对象池时调用。
        /// 用户应在此方法中获取子节点引用。
        /// </summary>
        protected internal virtual void OnInit()
        {
        }

        /// <summary>
        /// 界面项回收。
        ///
        /// 在 UIItem 从对象池中被驱逐（真正销毁）前调用。
        /// 用于清理运行时状态。
        /// </summary>
        protected internal virtual void OnRecycle()
        {
        }
    }
}
