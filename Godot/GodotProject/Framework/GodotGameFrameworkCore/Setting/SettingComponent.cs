//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Setting;
using System;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 游戏设置组件。
    ///
    /// 这是游戏持久化设置系统的封装组件，透传核心层的 ISettingManager。
    /// 设置数据会被保存到用户的本地文件系统中（user://settings.cfg）。
    ///
    /// 使用场景：
    /// - 保存玩家的偏好设置（音量、画质、语言等）
    /// - 保存游戏进度标记（已通过的关卡等）
    /// - 保存玩家自定义键位配置
    ///
    /// 使用方式：
    /// <code>
    /// SettingComponent setting = GF.Setting;
    ///
    /// // 写入设置
    /// setting.SetFloat("MusicVolume", 0.8f);
    /// setting.SetBool("Fullscreen", true);
    /// setting.SetString("PlayerName", "Hero");
    ///
    /// // 读取设置（带默认值）
    /// float volume = setting.GetFloat("MusicVolume", 1.0f);
    /// bool fullscreen = setting.GetBool("Fullscreen", false);
    ///
    /// // 保存到磁盘
    /// setting.Save();
    /// </code>
    ///
    /// 初始化流程：
    /// 1. _Ready() 中获取核心层 ISettingManager
    /// 2. 创建 DefaultSettingHelper 并设置到 Manager
    /// 3. CallDeferred 延迟加载配置文件（确保所有组件就绪）
    ///
    /// 对应 Unity 版本中的 SettingComponent。
    /// </summary>
    public sealed partial class SettingComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的设置管理器实例。
        /// </summary>
        private ISettingManager m_SettingManager = null;

        /// <summary>
        /// 获取游戏配置项数量。
        /// </summary>
        public int Count => m_SettingManager.Count;

        /// <summary>
        /// 节点初始化回调。
        /// 获取核心层 ISettingManager，创建 Helper 并初始化。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_SettingManager = GameFrameworkEntry.GetModule<ISettingManager>();
            if (m_SettingManager == null)
            {
                Log.Fatal("Setting manager is invalid.");
                return;
            }

            // 创建默认的设置辅助器并设置到管理器
            // DefaultSettingHelper 使用 Godot ConfigFile 实现持久化
            DefaultSettingHelper settingHelper = new DefaultSettingHelper();
            m_SettingManager.SetSettingHelper(settingHelper);

            // 延迟加载配置文件，确保所有组件都已完成初始化
            CallDeferred(MethodName.LoadSettings);
        }

        /// <summary>
        /// 延迟加载配置文件。
        /// </summary>
        private void LoadSettings()
        {
            if (!m_SettingManager.Load())
            {
                Log.Warning("Load settings failure.");
            }
        }

        /// <summary>
        /// 保存游戏配置到磁盘。
        /// </summary>
        public void Save()
        {
            m_SettingManager.Save();
        }

        /// <summary>
        /// 获取所有游戏配置项的名称。
        /// </summary>
        /// <returns>所有配置项名称数组。</returns>
        public string[] GetAllSettingNames()
        {
            return m_SettingManager.GetAllSettingNames();
        }

        /// <summary>
        /// 获取所有游戏配置项的名称。
        /// </summary>
        /// <param name="results">输出所有配置项名称到此列表。</param>
        public void GetAllSettingNames(List<string> results)
        {
            m_SettingManager.GetAllSettingNames(results);
        }

        /// <summary>
        /// 检查是否存在指定配置项。
        /// </summary>
        /// <param name="settingName">配置项名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasSetting(string settingName)
        {
            return m_SettingManager.HasSetting(settingName);
        }

        /// <summary>
        /// 移除指定配置项。
        /// </summary>
        /// <param name="settingName">要移除的配置项名称。</param>
        public void RemoveSetting(string settingName)
        {
            m_SettingManager.RemoveSetting(settingName);
        }

        /// <summary>
        /// 清空所有配置项。
        /// </summary>
        public void RemoveAllSettings()
        {
            m_SettingManager.RemoveAllSettings();
        }

        /// <summary>
        /// 读取布尔值。
        /// </summary>
        public bool GetBool(string settingName)
        {
            return m_SettingManager.GetBool(settingName);
        }

        /// <summary>
        /// 读取布尔值（带默认值）。
        /// </summary>
        public bool GetBool(string settingName, bool defaultValue)
        {
            return m_SettingManager.GetBool(settingName, defaultValue);
        }

        /// <summary>
        /// 写入布尔值。
        /// </summary>
        public void SetBool(string settingName, bool value)
        {
            m_SettingManager.SetBool(settingName, value);
        }

        /// <summary>
        /// 读取整数值。
        /// </summary>
        public int GetInt(string settingName)
        {
            return m_SettingManager.GetInt(settingName);
        }

        /// <summary>
        /// 读取整数值（带默认值）。
        /// </summary>
        public int GetInt(string settingName, int defaultValue)
        {
            return m_SettingManager.GetInt(settingName, defaultValue);
        }

        /// <summary>
        /// 写入整数值。
        /// </summary>
        public void SetInt(string settingName, int value)
        {
            m_SettingManager.SetInt(settingName, value);
        }

        /// <summary>
        /// 读取浮点数值。
        /// </summary>
        public float GetFloat(string settingName)
        {
            return m_SettingManager.GetFloat(settingName);
        }

        /// <summary>
        /// 读取浮点数值（带默认值）。
        /// </summary>
        public float GetFloat(string settingName, float defaultValue)
        {
            return m_SettingManager.GetFloat(settingName, defaultValue);
        }

        /// <summary>
        /// 写入浮点数值。
        /// </summary>
        public void SetFloat(string settingName, float value)
        {
            m_SettingManager.SetFloat(settingName, value);
        }

        /// <summary>
        /// 读取字符串值。
        /// </summary>
        public string GetString(string settingName)
        {
            return m_SettingManager.GetString(settingName);
        }

        /// <summary>
        /// 读取字符串值（带默认值）。
        /// </summary>
        public string GetString(string settingName, string defaultValue)
        {
            return m_SettingManager.GetString(settingName, defaultValue);
        }

        /// <summary>
        /// 写入字符串值。
        /// </summary>
        public void SetString(string settingName, string value)
        {
            m_SettingManager.SetString(settingName, value);
        }

        /// <summary>
        /// 读取对象（通过 JSON 反序列化）。
        /// </summary>
        public T GetObject<T>(string settingName)
        {
            return m_SettingManager.GetObject<T>(settingName);
        }

        /// <summary>
        /// 读取对象（通过 JSON 反序列化）。
        /// </summary>
        public object GetObject(Type objectType, string settingName)
        {
            return m_SettingManager.GetObject(objectType, settingName);
        }

        /// <summary>
        /// 读取对象（带默认值）。
        /// </summary>
        public T GetObject<T>(string settingName, T defaultObj)
        {
            return m_SettingManager.GetObject(settingName, defaultObj);
        }

        /// <summary>
        /// 读取对象（带默认值）。
        /// </summary>
        public object GetObject(Type objectType, string settingName, object defaultObj)
        {
            return m_SettingManager.GetObject(objectType, settingName, defaultObj);
        }

        /// <summary>
        /// 写入对象（通过 JSON 序列化）。
        /// </summary>
        public void SetObject<T>(string settingName, T obj)
        {
            m_SettingManager.SetObject(settingName, obj);
        }

        /// <summary>
        /// 写入对象（通过 JSON 序列化）。
        /// </summary>
        public void SetObject(string settingName, object obj)
        {
            m_SettingManager.SetObject(settingName, obj);
        }
    }
}
