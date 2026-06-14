//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Setting;
using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认游戏设置辅助器。
    ///
    /// 基于 Godot 的 ConfigFile 实现的 ISettingHelper。
    /// ConfigFile 是 Godot 提供的 INI 风格配置文件读写类，
    /// 支持分节（Section）、键值对（Key-Value）的持久化存储。
    ///
    /// 存储方案：
    /// - 文件路径：user://settings.cfg（Godot 的用户数据目录）
    /// - 文件格式：Godot ConfigFile 格式（类似 INI）
    /// - 所有配置项存储在名为 "Settings" 的 Section 下
    ///
    /// 对象序列化：
    /// - GetObject/SetObject 使用 System.Text.Json 进行 JSON 序列化
    /// - 这是 .NET 8 内置的 JSON 库，无需额外依赖
    ///
    /// 对应 Unity 版本中的 DefaultSettingHelper（使用自定义二进制序列化）。
    /// </summary>
    public class DefaultSettingHelper : ISettingHelper
    {
        /// <summary>
        /// 配置文件中的 Section 名称。
        /// Godot ConfigFile 支持分节存储，这里使用固定 Section 名。
        /// </summary>
        private const string SectionName = "Settings";

        /// <summary>
        /// 配置文件的存储路径。
        /// user:// 是 Godot 的用户持久化数据目录：
        /// - Windows: %APPDATA%/Godot/app_userdata/[项目名]/
        /// - Linux: ~/.local/share/godot/app_userdata/[项目名]/
        /// - macOS: ~/Library/Application Support/Godot/app_userdata/[项目名]/
        /// </summary>
        private readonly string m_FilePath = "user://settings.cfg";

        /// <summary>
        /// Godot ConfigFile 实例，用于读写配置。
        /// </summary>
        private readonly ConfigFile m_ConfigFile = new();

        /// <summary>
        /// 获取游戏配置项数量。
        /// </summary>
        public int Count
        {
            get
            {
                return m_ConfigFile.GetSectionKeys(SectionName).Length;
            }
        }

        /// <summary>
        /// 加载游戏配置。
        ///
        /// 从 user://settings.cfg 读取配置数据。
        /// 如果文件不存在，返回 true（使用空配置）。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public bool Load()
        {
            try
            {
                Error err = m_ConfigFile.Load(m_FilePath);
                // 文件不存在不算失败，只是使用空配置
                if (err == Error.FileNotFound)
                {
                    return true;
                }

                if (err != Error.Ok)
                {
                    Log.Warning("Load settings failure with error '{0}'.", err);
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Load settings failure with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 保存游戏配置。
        ///
        /// 将配置数据写入 user://settings.cfg。
        /// Godot 会自动创建必要的目录结构。
        /// </summary>
        /// <returns>是否保存成功。</returns>
        public bool Save()
        {
            try
            {
                Error err = m_ConfigFile.Save(m_FilePath);
                if (err != Error.Ok)
                {
                    Log.Warning("Save settings failure with error '{0}'.", err);
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Save settings failure with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 获取所有游戏配置项的名称。
        /// </summary>
        /// <returns>所有配置项名称数组。</returns>
        public string[] GetAllSettingNames()
        {
            return m_ConfigFile.GetSectionKeys(SectionName);
        }

        /// <summary>
        /// 获取所有游戏配置项的名称。
        /// </summary>
        /// <param name="results">输出所有配置项名称到此列表。</param>
        public void GetAllSettingNames(List<string> results)
        {
            if (results == null)
            {
                throw new GameFrameworkException("Results is invalid.");
            }

            results.Clear();
            string[] keys = m_ConfigFile.GetSectionKeys(SectionName);
            results.AddRange(keys);
        }

        /// <summary>
        /// 检查是否存在指定配置项。
        /// </summary>
        /// <param name="settingName">配置项名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasSetting(string settingName)
        {
            return m_ConfigFile.HasSectionKey(SectionName, settingName);
        }

        /// <summary>
        /// 移除指定配置项。
        /// </summary>
        /// <param name="settingName">要移除的配置项名称。</param>
        /// <returns>是否移除成功。</returns>
        public bool RemoveSetting(string settingName)
        {
            if (!HasSetting(settingName))
            {
                return false;
            }

            m_ConfigFile.EraseSectionKey(SectionName, settingName);
            return true;
        }

        /// <summary>
        /// 清空所有配置项。
        /// </summary>
        public void RemoveAllSettings()
        {
            // 删除整个 Section
            if (m_ConfigFile.HasSection(SectionName))
            {
                string[] keys = m_ConfigFile.GetSectionKeys(SectionName);
                foreach (string key in keys)
                {
                    m_ConfigFile.EraseSectionKey(SectionName, key);
                }
            }
        }

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        public bool GetBool(string settingName)
        {
            return m_ConfigFile.GetValue(SectionName, settingName).AsBool();
        }

        /// <summary>
        /// 读取布尔值（带默认值）。
        /// </summary>
        public bool GetBool(string settingName, bool defaultValue)
        {
            if (!HasSetting(settingName))
            {
                return defaultValue;
            }

            return GetBool(settingName);
        }

        /// <summary>
        /// 写入布尔值。
        /// </summary>
        public void SetBool(string settingName, bool value)
        {
            m_ConfigFile.SetValue(SectionName, settingName, value);
        }

        /// <summary>
        /// 读取整数值。
        /// </summary>
        public int GetInt(string settingName)
        {
            return (int)m_ConfigFile.GetValue(SectionName, settingName).AsInt64();
        }

        /// <summary>
        /// 读取整数值（带默认值）。
        /// </summary>
        public int GetInt(string settingName, int defaultValue)
        {
            if (!HasSetting(settingName))
            {
                return defaultValue;
            }

            return GetInt(settingName);
        }

        /// <summary>
        /// 写入整数值。
        /// </summary>
        public void SetInt(string settingName, int value)
        {
            m_ConfigFile.SetValue(SectionName, settingName, value);
        }

        /// <summary>
        /// 读取浮点数值。
        /// </summary>
        public float GetFloat(string settingName)
        {
            return (float)m_ConfigFile.GetValue(SectionName, settingName).AsDouble();
        }

        /// <summary>
        /// 读取浮点数值（带默认值）。
        /// </summary>
        public float GetFloat(string settingName, float defaultValue)
        {
            if (!HasSetting(settingName))
            {
                return defaultValue;
            }

            return GetFloat(settingName);
        }

        /// <summary>
        /// 写入浮点数值。
        /// </summary>
        public void SetFloat(string settingName, float value)
        {
            m_ConfigFile.SetValue(SectionName, settingName, value);
        }

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        public string GetString(string settingName)
        {
            return m_ConfigFile.GetValue(SectionName, settingName).AsString();
        }

        /// <summary>
        /// 读取字符串值（带默认值）。
        /// </summary>
        public string GetString(string settingName, string defaultValue)
        {
            if (!HasSetting(settingName))
            {
                return defaultValue;
            }

            return GetString(settingName);
        }

        /// <summary>
        /// 写入字符串值。
        /// </summary>
        public void SetString(string settingName, string value)
        {
            m_ConfigFile.SetValue(SectionName, settingName, value);
        }

        /// <summary>
        /// 读取对象（使用 JSON 反序列化）。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="settingName">配置项名称。</param>
        /// <returns>反序列化后的对象。</returns>
        public T GetObject<T>(string settingName)
        {
            string json = GetString(settingName);
            return JsonSerializer.Deserialize<T>(json);
        }

        /// <summary>
        /// 读取对象（使用 JSON 反序列化）。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        /// <param name="settingName">配置项名称。</param>
        /// <returns>反序列化后的对象。</returns>
        public object GetObject(Type objectType, string settingName)
        {
            string json = GetString(settingName);
            return JsonSerializer.Deserialize(json, objectType);
        }

        /// <summary>
        /// 读取对象（带默认值）。
        /// </summary>
        public T GetObject<T>(string settingName, T defaultObj)
        {
            if (!HasSetting(settingName))
            {
                return defaultObj;
            }

            return GetObject<T>(settingName);
        }

        /// <summary>
        /// 读取对象（带默认值）。
        /// </summary>
        public object GetObject(Type objectType, string settingName, object defaultObj)
        {
            if (!HasSetting(settingName))
            {
                return defaultObj;
            }

            return GetObject(objectType, settingName);
        }

        /// <summary>
        /// 写入对象（使用 JSON 序列化）。
        /// </summary>
        public void SetObject<T>(string settingName, T obj)
        {
            SetString(settingName, JsonSerializer.Serialize(obj));
        }

        /// <summary>
        /// 写入对象（使用 JSON 序列化）。
        /// </summary>
        public void SetObject(string settingName, object obj)
        {
            SetString(settingName, JsonSerializer.Serialize(obj));
        }
    }
}
