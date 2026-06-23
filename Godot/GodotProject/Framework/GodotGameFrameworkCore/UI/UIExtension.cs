using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameConfig;

namespace GodotGameFramework.UI
{
    public static class UIExtension
    {
        public static TLogic GetUIForm<TLogic>(this UIComponent uiComponent, int serialId)
            where TLogic : UIFormLogic
        {
            UIForm uiForm = uiComponent.GetUIForm(serialId);
            return uiForm?.Logic as TLogic;
        }

        public static TLogic GetUIForm<TLogic>(this UIComponent uiComponent, string uiFormAssetName)
            where TLogic : UIFormLogic
        {
            UIForm uiForm = uiComponent.GetUIForm(uiFormAssetName);
            return uiForm?.Logic as TLogic;
        }

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

        public static void CloseUIForm(this UIComponent uiComponent, string uiFormAssetName, object userData = null)
        {
            UIForm uiForm = uiComponent.GetUIForm(uiFormAssetName);
            if (uiForm != null)
            {
                uiComponent.CloseUIForm(uiForm, userData);
            }
        }

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

        public static UIForm GetTopUIForm(this UIComponent uiComponent)
        {
            UIForm[] uiForms = uiComponent.GetAllLoadedUIForms();
            if (uiForms.Length == 0) return null;

            // 所有已加载的 UI 都已被 Refresh 算法排序，
            // 最后一个即为最顶层（深度最大）
            return uiForms[uiForms.Length - 1];
        }

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
        /// <summary>
        /// 异步打开UI表单，返回UIFormLogic
        /// </summary>
        /// <param name="uiComponent"></param>
        /// <param name="formId"></param>
        /// <param name="userData"></param>
        /// <returns></returns>
        public static async Task<UIFormLogic> OpenUIFormAsync(this UIComponent uiComponent, UIFormId formId, object userData = null)
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
            var uIForm = await uiComponent.OpenUIFormAsync(formConfig.AssetPath, formConfig.UIGroupName, userData);
            return (uIForm as UIForm)?.Logic;
        }
        /// <summary>
        /// 异步打开UI表单，泛型返回UIFormLogic
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uiComponent"></param>
        /// <param name="formId"></param>
        /// <param name="userData"></param>
        /// <returns></returns>
        public static async Task<T> OpenUIFormAsync<T>(this UIComponent uiComponent, UIFormId formId, object userData = null) where T : UIFormLogic
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
            var uIForm = await uiComponent.OpenUIFormAsync(formConfig.AssetPath, formConfig.UIGroupName, userData);
            return (uIForm as UIForm)?.Logic as T;
        }
    }
}
