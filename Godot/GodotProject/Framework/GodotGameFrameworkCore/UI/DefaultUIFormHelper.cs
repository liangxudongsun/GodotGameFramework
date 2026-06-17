//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.UI;
using Godot;
using System;

namespace GodotGameFramework.UI
{
    /// <summary>
    /// 默认界面辅助器。
    ///
    /// 实现 IUIFormHelper 接口，负责 UI 窗体的实例化、创建和释放。
    /// 将 Godot 的 PackedScene/Node API 与核心框架的引擎无关接口桥接。
    ///
    /// 对标 UGF 中的 DefaultUIFormHelper。
    ///
    /// 三个核心方法：
    /// 1. InstantiateUIForm: PackedScene.Instantiate() 实例化场景
    /// 2. CreateUIForm: 创建 UIForm(Node) 包装器，挂载到 UI 组容器
    /// 3. ReleaseUIForm: Node.QueueFree() 销毁节点
    /// </summary>
    public partial class DefaultUIFormHelper : UIFormHelperBase, IUIFormHelper
    {
        /// <summary>
        /// 实例化界面。
        ///
        /// 将 PackedScene 资源实例化为 Godot Node。
        /// 对标 UGF: Object.Instantiate((Object)uiFormAsset)
        /// </summary>
        /// <param name="uiFormAsset">要实例化的界面资源（PackedScene）。</param>
        /// <returns>实例化后的界面（Node）。</returns>
        public override object InstantiateUIForm(object uiFormAsset)
        {
            PackedScene packedScene = (PackedScene)uiFormAsset;
            return packedScene.Instantiate();
        }

        /// <summary>
        /// 创建界面。
        ///
        /// 将实例化的场景节点包装为 UIForm(Node)，并挂载到 UI 组容器下。
        ///
        /// 对标 UGF DefaultUIFormHelper.CreateUIForm 的流程：
        /// 1. 创建 UIForm Node
        /// 2. 将 Control 实例添加为 UIForm 的子节点
        /// 3. 将 UIForm 添加到 UI 组的 CanvasLayer 容器下
        /// 4. 如果有 UIFormLogic 类型信息，创建并关联
        /// 5. 返回 UIForm (IUIForm)
        /// </summary>
        /// <param name="uiFormInstance">界面实例（从 PackedScene 实例化的 Node）。</param>
        /// <param name="uiGroup">界面所属的界面组。</param>
        /// <param name="userData">用户自定义数据（包含 UIFormLogic 类型信息）。</param>
        /// <returns>创建的界面。</returns>
        public override IUIForm CreateUIForm(object uiFormInstance, IUIGroup uiGroup, object userData)
        {
            // 创建 UIForm 包装器节点
            if (uiFormInstance is UIFormLogic uiFormLogic)
            {
                UIForm uiForm = new UIForm();
                UIFormLogic instance = uiFormLogic;
                if (instance == null)
                {
                    Log.Error("UI form instance is invalid.");
                    return null;
                }

                // 获取 UI 组的辅助器节点（CanvasLayer）作为容器
                Node groupContainer = ((Node)uiGroup.Helper);
                if (groupContainer == null)
                {
                    Log.Error("UI group helper is invalid.");
                    return null;
                }


                uiForm.Name = string.IsNullOrEmpty(instance.Name) ? "UIForm_" + instance.GetType().Name : "UIForm_" + instance.Name;

                // 将 Control 实例添加为 UIForm 的子节点
                uiForm.AddChild(instance);

                // 将 UIForm 添加到 UI 组容器下
                groupContainer.AddChild(uiForm);

                uiForm.SetUIFormLogic(instance);
                instance.Name = $"UIFormLogic_{instance.GetType().Name}";
                return uiForm;
            }
            else
            {
                Log.Error("{0} is not UIFormLogic type.", uiFormInstance);
                return null;
            }
        }

        /// <summary>
        /// 释放界面。
        ///
        /// 销毁 UIForm 节点及其所有子节点。
        /// </summary>
        /// <param name="uiFormAsset">要释放的界面资源。</param>
        /// <param name="uiFormInstance">要释放的界面实例。</param>
        public override void ReleaseUIForm(object uiFormAsset, object uiFormInstance)
        {
            Node node = uiFormInstance as Node;
            if (node == null)
            {
                return;
            }

            // 从父节点移除并标记为待删除
            // QueueFree 会在当前帧结束后安全地删除节点
            node.QueueFree();
        }
    }
}
