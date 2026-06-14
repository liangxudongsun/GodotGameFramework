//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 辅助器创建器相关的实用函数。
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// 创建辅助器。
        /// </summary>
        /// <typeparam name="T">要创建的辅助器类型。</typeparam>
        /// <param name="helperTypeName">要创建的辅助器类型名称。</param>
        /// <param name="customHelper">若要创建的辅助器类型为空时，使用的自定义辅助器类型。</param>
        /// <returns>创建的辅助器。</returns>
        public static T CreateHelper<T>(string helperTypeName, T customHelper) where T : Node
        {
            return CreateHelper(helperTypeName, customHelper, 0);
        }

        /// <summary>
        /// 创建辅助器。
        /// </summary>
        /// <typeparam name="T">要创建的辅助器类型。</typeparam>
        /// <param name="helperTypeName">要创建的辅助器类型名称。</param>
        /// <param name="customHelper">若要创建的辅助器类型为空时，使用的自定义辅助器类型。</param>
        /// <param name="index">要创建的辅助器索引。</param>
        /// <returns>创建的辅助器。</returns>
        public static T CreateHelper<T>(string helperTypeName, T customHelper, int index) where T : Node
        {
            T helper = null;
            if (!string.IsNullOrEmpty(helperTypeName))
            {
                System.Type helperType = Utility.Assembly.GetType(helperTypeName);
                if (helperType == null)
                {
                    Log.Warning("Can not find helper type '{0}'.", helperTypeName);
                    return null;
                }

                if (!typeof(T).IsAssignableFrom(helperType))
                {
                    Log.Warning("Type '{0}' is not assignable from '{1}'.", typeof(T).FullName, helperType.FullName);
                    return null;
                }

                helper = Activator.CreateInstance(helperType) as T;
                if (helper == null)
                {
                    Log.Warning($"Failed to create instance of type '{helperTypeName}'.");
                    return null;
                }
            }
            else if (customHelper == null)
            {
                Log.Warning("You must set custom helper with '{0}' type first.", typeof(T).FullName);
                return null;
            }
            else if (customHelper.IsInsideTree())
            {
                // 已在场景树中：index>0则克隆，index=0直接复用原实例
                helper = index > 0 ? customHelper.Duplicate() as T : customHelper;
            }
            else
            {
                helper = customHelper.Duplicate() as T;
            }
            helper.Name = typeof(T).Name;
            return helper;
        }
    }
}
