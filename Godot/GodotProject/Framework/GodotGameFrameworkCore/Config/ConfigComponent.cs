//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Config;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 全局配置组件。
    ///
    /// 这是全局配置系统的封装组件，透传核心层的 IConfigManager。
    /// 用于管理游戏运行时需要读取的键值对配置数据。
    ///
    /// 配置与设置的区别：
    /// - Config（配置）：从文件加载的只读键值对数据，通常是策划配置的游戏参数
    ///   例如：角色属性表、关卡配置、道具参数等
    /// - Setting（设置）：可读写的用户偏好数据，通过 SettingComponent 管理
    ///   例如：音量大小、画质设置、语言选择等
    ///
    /// 使用方式（通过 ResourceComponent 加载配置文件）：
    /// <code>
    /// ConfigComponent config = GF.Config;
    ///
    /// // 从文件加载配置（通过 ResourceComponent 读取）
    /// config.ReadData("res://Data/Config/defaultconfig.txt");
    ///
    /// // 读取配置值
    /// string gameName = config.GetString("GameName");
    /// int maxLevel = config.GetInt("MaxLevel", 1);
    /// float dropRate = config.GetFloat("DropRate", 0.5f);
    /// </code>
    ///
    /// 初始化流程：
    /// 1. _Ready() 中获取核心层 IConfigManager
    /// 2. 创建 DefaultConfigHelper 并设置到 Manager
    /// 3. 用户在合适的时机（如 Procedure 中）调用 ReadData 加载配置
    ///
    /// 对应 Unity 版本中的 ConfigComponent。
    /// </summary>
    public sealed partial class ConfigComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的配置管理器实例。
        /// </summary>
        private IConfigManager m_ConfigManager = null;

        /// <summary>
        /// 获取全局配置项数量。
        /// </summary>
        public int Count => m_ConfigManager.Count;

        /// <summary>
        /// 获取缓冲二进制流的大小。
        /// </summary>
        public int CachedBytesSize => m_ConfigManager.CachedBytesSize;

        /// <summary>
        /// 节点初始化回调。
        /// 获取核心层 IConfigManager，创建并设置 Helper。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_ConfigManager = GameFrameworkEntry.GetModule<IConfigManager>();
            if (m_ConfigManager == null)
            {
                Log.Fatal("Config manager is invalid.");
                return;
            }

            // 创建默认配置辅助器
            // DefaultConfigHelper 负责解析 Tab 分隔的配置文件
            DefaultConfigHelper configHelper = new DefaultConfigHelper();
            m_ConfigManager.SetDataProviderHelper(configHelper);
            m_ConfigManager.SetConfigHelper(configHelper);
        }

        /// <summary>
        /// 从文件读取配置数据并解析。
        ///
        /// 通过 ResourceComponent 加载文件内容，然后调用 ParseData 解析。
        /// 文件格式：每行一条配置，Tab 分隔 4 列（类型、名称、未使用、值）。
        /// '#' 开头的行被忽略。
        /// </summary>
        /// <param name="dataAssetName">
        /// 文件路径，使用 Godot 路径格式：
        /// - "res://Data/Config/defaultconfig.txt" — 项目资源目录
        /// </param>
        /// <returns>是否加载并解析成功。</returns>
        public bool ReadData(string dataAssetName)
        {
            ResourceComponent resourceComponent = GF.Resource;
            if (resourceComponent == null)
            {
                Log.Fatal("Resource component is invalid.");
                return false;
            }

            string content = resourceComponent.LoadText(dataAssetName);
            if (content == null)
            {
                Log.Warning("Can not load config data from '{0}'.", dataAssetName);
                return false;
            }

            return m_ConfigManager.ParseData(content);
        }

        /// <summary>
        /// 从文件读取二进制配置数据并解析。
        ///
        /// 通过 ResourceComponent 以二进制方式加载文件内容，然后调用 ParseData 解析。
        /// </summary>
        /// <param name="dataAssetName">文件路径。</param>
        /// <returns>是否加载并解析成功。</returns>
        public bool ReadDataBinary(string dataAssetName)
        {
            ResourceComponent resourceComponent = GF.Resource;
            if (resourceComponent == null)
            {
                Log.Fatal("Resource component is invalid.");
                return false;
            }

            byte[] bytes = resourceComponent.LoadBinary(dataAssetName);
            if (bytes == null)
            {
                Log.Warning("Can not load config binary data from '{0}'.", dataAssetName);
                return false;
            }

            return m_ConfigManager.ParseData(bytes);
        }

        /// <summary>
        /// 确保二进制流缓存分配足够大小的内存并缓存。
        /// </summary>
        /// <param name="ensureSize">要确保的大小。</param>
        public void EnsureCachedBytesSize(int ensureSize)
        {
            m_ConfigManager.EnsureCachedBytesSize(ensureSize);
        }

        /// <summary>
        /// 释放缓存的二进制流。
        /// </summary>
        public void FreeCachedBytes()
        {
            m_ConfigManager.FreeCachedBytes();
        }

        /// <summary>
        /// 解析全局配置（文本格式）。
        /// </summary>
        /// <param name="configString">配置文本内容。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(string configString)
        {
            return m_ConfigManager.ParseData(configString);
        }

        /// <summary>
        /// 解析全局配置（文本格式，带用户数据）。
        /// </summary>
        /// <param name="configString">配置文本内容。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(string configString, object userData)
        {
            return m_ConfigManager.ParseData(configString, userData);
        }

        /// <summary>
        /// 解析全局配置（二进制格式）。
        /// </summary>
        /// <param name="configBytes">配置二进制数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] configBytes)
        {
            return m_ConfigManager.ParseData(configBytes);
        }

        /// <summary>
        /// 解析全局配置（二进制格式，带用户数据）。
        /// </summary>
        /// <param name="configBytes">配置二进制数据。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] configBytes, object userData)
        {
            return m_ConfigManager.ParseData(configBytes, userData);
        }

        /// <summary>
        /// 解析全局配置（二进制格式，指定范围）。
        /// </summary>
        /// <param name="configBytes">配置二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] configBytes, int startIndex, int length)
        {
            return m_ConfigManager.ParseData(configBytes, startIndex, length);
        }

        /// <summary>
        /// 解析全局配置（二进制格式，指定范围，带用户数据）。
        /// </summary>
        /// <param name="configBytes">配置二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] configBytes, int startIndex, int length, object userData)
        {
            return m_ConfigManager.ParseData(configBytes, startIndex, length, userData);
        }

        /// <summary>
        /// 检查是否存在指定配置项。
        /// </summary>
        /// <param name="configName">配置项名称。</param>
        /// <returns>是否存在。</returns>
        public bool HasConfig(string configName)
        {
            return m_ConfigManager.HasConfig(configName);
        }

        /// <summary>
        /// 读取布尔值配置。
        /// </summary>
        public bool GetBool(string configName)
        {
            return m_ConfigManager.GetBool(configName);
        }

        /// <summary>
        /// 读取布尔值配置（带默认值）。
        /// </summary>
        public bool GetBool(string configName, bool defaultValue)
        {
            return m_ConfigManager.GetBool(configName, defaultValue);
        }

        /// <summary>
        /// 读取整数值配置。
        /// </summary>
        public int GetInt(string configName)
        {
            return m_ConfigManager.GetInt(configName);
        }

        /// <summary>
        /// 读取整数值配置（带默认值）。
        /// </summary>
        public int GetInt(string configName, int defaultValue)
        {
            return m_ConfigManager.GetInt(configName, defaultValue);
        }

        /// <summary>
        /// 读取浮点数值配置。
        /// </summary>
        public float GetFloat(string configName)
        {
            return m_ConfigManager.GetFloat(configName);
        }

        /// <summary>
        /// 读取浮点数值配置（带默认值）。
        /// </summary>
        public float GetFloat(string configName, float defaultValue)
        {
            return m_ConfigManager.GetFloat(configName, defaultValue);
        }

        /// <summary>
        /// 读取字符串值配置。
        /// </summary>
        public string GetString(string configName)
        {
            return m_ConfigManager.GetString(configName);
        }

        /// <summary>
        /// 读取字符串值配置（带默认值）。
        /// </summary>
        public string GetString(string configName, string defaultValue)
        {
            return m_ConfigManager.GetString(configName, defaultValue);
        }

        /// <summary>
        /// 增加配置项。
        /// </summary>
        /// <param name="configName">配置项名称。</param>
        /// <param name="configValue">配置项值。</param>
        /// <returns>是否添加成功。</returns>
        public bool AddConfig(string configName, string configValue)
        {
            return m_ConfigManager.AddConfig(configName, configValue);
        }

        /// <summary>
        /// 增加配置项（多类型值）。
        /// </summary>
        public bool AddConfig(string configName, bool boolValue, int intValue, float floatValue, string stringValue)
        {
            return m_ConfigManager.AddConfig(configName, boolValue, intValue, floatValue, stringValue);
        }

        /// <summary>
        /// 移除指定配置项。
        /// </summary>
        /// <param name="configName">配置项名称。</param>
        /// <returns>是否移除成功。</returns>
        public bool RemoveConfig(string configName)
        {
            return m_ConfigManager.RemoveConfig(configName);
        }

        /// <summary>
        /// 清空所有配置项。
        /// </summary>
        public void RemoveAllConfigs()
        {
            m_ConfigManager.RemoveAllConfigs();
        }
    }
}
