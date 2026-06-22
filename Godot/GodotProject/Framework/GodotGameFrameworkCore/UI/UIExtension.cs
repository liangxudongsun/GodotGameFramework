//------------------------------------------------------------
// UIExtension - 界面组件便捷扩展方法
// 提供 UIComponent 的常用便捷方法，简化游戏代码编写。
//
// 使用方式：
//   GF.UI.GetUIForm<MainMenuForm>(serialId);
//   GF.UI.CloseUIForms("Popup");
//   GF.UI.GetTopUIForm();
//
// 对应 UGF 参考项目中的 UIExtension（游戏特定的 Toast、动画、
// UIView 枚举、坐标转换等逻辑不移植，只保留通用便捷方法）。
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig;

namespace GodotGameFramework.UI
{
    /// <summary>
    /// 界面组件扩展方法。
    ///
    /// 提供 UIComponent 的常用便捷方法，
    /// 包括通过 UIFormLogic 类型获取 UI、关闭指定组所有 UI、获取顶层 UI 等。
    ///
    /// </summary>
    public static class UIExtension
    {
        /// <summary>
        /// 通过序列号获取 UIFormLogic 子类。
        ///
        /// 等价于：
        /// <code>
        /// UIForm uiForm = comp.GetUIForm(serialId);
        /// MainMenuForm logic = uiForm?.Logic as MainMenuForm;
        /// </code>
        /// </summary>
        /// <typeparam name="TLogic">UIFormLogic 子类类型。</typeparam>
        /// <param name="uiComponent">界面组件。</param>
        /// <param name="serialId">界面序列号。</param>
        /// <returns>对应的 UIFormLogic 实例，未找到返回 null。</returns>
        public static TLogic GetUIForm<TLogic>(this UIComponent uiComponent, int serialId)
            where TLogic : UIFormLogic
        {
            UIForm uiForm = uiComponent.GetUIForm(serialId);
            return uiForm?.Logic as TLogic;
        }

        /// <summary>
        /// 通过界面资源名获取第一个匹配的 UIFormLogic 子类。
        ///
        /// 等价于：
        /// <code>
        /// UIForm uiForm = comp.GetUIForm(assetName);
        /// MainMenuForm logic = uiForm?.Logic as MainMenuForm;
        /// </code>
        /// </summary>
        /// <typeparam name="TLogic">UIFormLogic 子类类型。</typeparam>
        /// <param name="uiComponent">界面组件。</param>
        /// <param name="uiFormAssetName">界面资源路径。</param>
        /// <returns>对应的 UIFormLogic 实例，未找到返回 null。</returns>
        public static TLogic GetUIForm<TLogic>(this UIComponent uiComponent, string uiFormAssetName)
            where TLogic : UIFormLogic
        {
            UIForm uiForm = uiComponent.GetUIForm(uiFormAssetName);
            return uiForm?.Logic as TLogic;
        }

        /// <summary>
        /// 通过界面资源名获取所有匹配的 UIFormLogic 子类列表。
        ///
        /// 同一资源名的 UI 可能被多次打开（如多个弹窗实例），
        /// 此方法返回所有实例的 UIFormLogic。
        /// </summary>
        /// <typeparam name="TLogic">UIFormLogic 子类类型。</typeparam>
        /// <param name="uiComponent">界面组件。</param>
        /// <param name="uiFormAssetName">界面资源路径。</param>
        /// <returns>匹配的 UIFormLogic 实例列表。</returns>
        public static List<TLogic> GetAllUIForms<TLogic>(this UIComponent uiComponent, string uiFormAssetName)
            where TLogic : UIFormLogic
        {
            List<TLogic> result = new List<TLogic>();
            UIForm[] uiForms = uiComponent.GetUIForms(uiFormAssetName);
            for (int i = 0; i < uiForms.Length; i++)
            {
                if (uiForms[i]?.Logic is TLogic logic)
                {
                    result.Add(logic);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取所有已加载 UI 中指定类型的 UIFormLogic 列表。
        ///
        /// 适用于需要遍历所有同类型 UI 的场景（如关闭所有弹窗）。
        /// </summary>
        /// <typeparam name="TLogic">UIFormLogic 子类类型。</typeparam>
        /// <param name="uiComponent">界面组件。</param>
        /// <returns>匹配的 UIFormLogic 实例列表。</returns>
        public static List<TLogic> GetAllUIForms<TLogic>(this UIComponent uiComponent)
            where TLogic : UIFormLogic
        {
            List<TLogic> result = new List<TLogic>();
            UIForm[] uiForms = uiComponent.GetAllLoadedUIForms();
            for (int i = 0; i < uiForms.Length; i++)
            {
                if (uiForms[i]?.Logic is TLogic logic)
                {
                    result.Add(logic);
                }
            }

            return result;
        }

        /// <summary>
        /// 检查指定类型的 UI 是否存在。
        ///
        /// 通过 UIFormLogic 类型匹配，而非序列号或资源名。
        /// </summary>
        /// <typeparam name="TLogic">UIFormLogic 子类类型。</typeparam>
        /// <param name="uiComponent">界面组件。</param>
        /// <param name="uiFormAssetName">界面资源路径。</param>
        /// <returns>是否存在指定类型的 UI。</returns>
        public static bool HasUIForm<TLogic>(this UIComponent uiComponent, string uiFormAssetName)
            where TLogic : UIFormLogic
        {
            UIForm[] uiForms = uiComponent.GetUIForms(uiFormAssetName);
            for (int i = 0; i < uiForms.Length; i++)
            {
                if (uiForms[i]?.Logic is TLogic)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 通过界面资源名关闭 UI。
        ///
        /// 关闭第一个匹配资源名的 UI 窗体。
        /// </summary>
        /// <param name="uiComponent">界面组件。</param>
        /// <param name="uiFormAssetName">界面资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        public static void CloseUIForm(this UIComponent uiComponent, string uiFormAssetName, object userData = null)
        {
            UIForm uiForm = uiComponent.GetUIForm(uiFormAssetName);
            if (uiForm != null)
            {
                uiComponent.CloseUIForm(uiForm, userData);
            }
        }

        /// <summary>
        /// 关闭指定界面组中的所有已加载 UI。
        ///
        /// 适用于切换场景、清理界面等批量操作。
        /// </summary>
        /// <param name="uiComponent">界面组件。</param>
        /// <param name="uiGroupName">界面组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        public static void CloseUIForms(this UIComponent uiComponent, string uiGroupName, object userData = null)
        {
            UIForm[] uiForms = uiComponent.GetAllLoadedUIForms();
            for (int i = 0; i < uiForms.Length; i++)
            {
                if (uiForms[i] != null && uiComponent.IsValidUIForm(uiForms[i]))
                {
                    if (uiForms[i].UIGroup?.Name == uiGroupName)
                    {
                        uiComponent.CloseUIForm(uiForms[i], userData);
                    }
                }
            }
        }

        /// <summary>
        /// 获取最顶层的 UI 窗体。
        ///
        /// "最顶层"指在所有 UI 组中深度值最大的已打开 UI 窗体。
        /// 适用于需要在最顶层 UI 之上显示提示信息的场景。
        /// </summary>
        /// <param name="uiComponent">界面组件。</param>
        /// <returns>最顶层的 UIForm，无 UI 返回 null。</returns>
        public static UIForm GetTopUIForm(this UIComponent uiComponent)
        {
            UIForm[] uiForms = uiComponent.GetAllLoadedUIForms();
            if (uiForms.Length == 0) return null;

            // 所有已加载的 UI 都已被 Refresh 算法排序，
            // 最后一个即为最顶层（深度最大）
            return uiForms[uiForms.Length - 1];
        }

        /// <summary>
        /// 获取最顶层的 UIFormLogic 子类。
        ///
        /// 等价于 GetTopUIForm() 的类型安全版本。
        /// </summary>
        /// <typeparam name="TLogic">UIFormLogic 子类类型。</typeparam>
        /// <param name="uiComponent">界面组件。</param>
        /// <returns>最顶层的 UIFormLogic 实例，不匹配或无 UI 返回 null。</returns>
        public static TLogic GetTopUIForm<TLogic>(this UIComponent uiComponent)
            where TLogic : UIFormLogic
        {
            UIForm topForm = uiComponent.GetTopUIForm();
            return topForm?.Logic as TLogic;
        }

        public static int OpenUIForm(this UIComponent uiComponent, string uiFormAssetName, string uiGroupName, int priority, bool pauseCoveredUIForm, object userData)
        {
            return uiComponent.OpenUIForm(uiFormAssetName, uiGroupName, priority, pauseCoveredUIForm, userData);
        }
        public static int OpenUIForm(this UIComponent uiComponent, string uiFormAssetName, string uiGroupName, object userData = null)
        {
            return uiComponent.OpenUIForm(uiFormAssetName, uiGroupName, userData);
        }
        public static int OpenUIForm(this UIComponent uiComponent, UIFormId formId, object userData = null)
        {
            if (GF.DataTable?.TbUIFormConfig?.DataList == null)
            {
                throw new Exception("UIFormConfig data table is not available.");
            }
            UIFormConfig formConfig = GF.DataTable.TbUIFormConfig.DataList.FirstOrDefault(x => x.UIFormId == formId);
            if (formConfig == null)
            {
                throw new Exception($"找不到UIFormId:{formId}的配置");
            }
            return uiComponent.OpenUIForm(formConfig.AssetPath, formConfig.UIGroupName, userData);
        }
        // TODO: Async OpenUIForm requires AddOpenUIFormTask support on UIComponent
        // to hook into OpenUIFormSuccess/OpenUIFormFailure events and complete the TCS.
        // public static Task<UIForm> OpenUIForm(this UIComponent uiComponent, UIFormId formId, string uiGroupName, object userData = null)
        // { ... }
    }
}
