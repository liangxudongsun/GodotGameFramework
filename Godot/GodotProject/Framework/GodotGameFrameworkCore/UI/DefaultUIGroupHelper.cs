//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.UI;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认界面组辅助器。
    ///
    /// 继承 CanvasLayer，作为 UI 组的容器节点。
    /// 每个 UI 组对应一个 CanvasLayer，通过 Layer 属性控制渲染深度。
    ///
    /// 在 Godot 中，CanvasLayer 创建独立的渲染层，
    /// 不同 CanvasLayer 的 Layer 值决定了它们的绘制顺序：
    /// - Layer 值越大，越后绘制（显示在最上面）
    /// - 同一 CanvasLayer 内的子节点按树顺序绘制
    ///
    /// 场景树结构：
    /// <code>
    /// UIComponent
    ///   └── CanvasLayer "UI Root"
    ///       ├── DefaultUIGroupHelper "Background" (Layer=0)
    ///       ├── DefaultUIGroupHelper "Normal" (Layer=1)
    ///       ├── DefaultUIGroupHelper "Popup" (Layer=2)
    ///       └── DefaultUIGroupHelper "Tips" (Layer=3)
    /// </code>
    ///
    /// 对标 UGF 中的 DefaultUIGroupHelper。
    /// UGF 默认实现为空（因为 Unity 中 Canvas.sortingOrder 由其他方式控制），
    /// GGF 中实际设置 CanvasLayer.Layer。
    /// </summary>
    public sealed partial class DefaultUIGroupHelper : UIGroupHelperBase, IUIGroupHelper
    {
        /// <summary>
        /// 设置界面组深度。
        ///
        /// 在 Godot 中直接映射为 CanvasLayer.Layer 属性。
        /// Layer 值越大，该组中的所有 UI 窗体渲染在越上层。
        /// </summary>
        /// <param name="depth">界面组深度。</param>
        public override void SetDepth(int depth)
        {
            Layer = depth;
        }
    }
}
