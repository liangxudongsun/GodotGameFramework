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
using System.IO;
using System.Text;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认全局配置辅助器。
    ///
    /// 实现 IDataProviderHelper&lt;IConfigManager&gt; 和 IConfigHelper 接口。
    /// 负责读取和解析配置文件，将配置键值对添加到 ConfigManager 中。
    ///
    /// 配置文件格式（与 UGF 一致的 Tab 分隔格式）：
    /// 每行一条配置，使用 Tab 分隔 4 列：
    /// #Type	[配置名]	[未使用]	[配置值]
    /// 例如：
    /// #string	GameName	-	My Game
    /// #int	MaxLevel	-	100
    ///
    /// '#' 开头的行被忽略（注释行），但格式行（含#Type）中的第一列也会以#开头。
    /// 实际解析时跳过以 '#' 开头的行。
    ///
    /// 对应 Unity 版本中的 DefaultConfigHelper（使用 Unity TextAsset 加载）。
    /// </summary>
    public class DefaultConfigHelper : IDataProviderHelper<IConfigManager>, IConfigHelper
    {
        /// <summary>
        /// 列分隔符：Tab 字符。
        /// </summary>
        private static readonly string[] ColumnSplitSeparator = new string[] { "\t" };

        /// <summary>
        /// 每行配置的列数（4列：类型、名称、未使用、值）。
        /// </summary>
        private const int ColumnCount = 4;

        /// <summary>
        /// 读取全局配置数据（从已加载的资源中）。
        ///
        /// 在 GGF 中，dataAsset 是通过 FileAccess 读取的文件内容的字节数组。
        /// </summary>
        /// <param name="configManager">配置管理器。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataAsset">资源数据（字节数组）。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public bool ReadData(IConfigManager configManager, string dataAssetName, object dataAsset, object userData)
        {
            byte[] bytes = dataAsset as byte[];
            if (bytes != null)
            {
                return configManager.ParseData(bytes, userData);
            }

            string text = dataAsset as string;
            if (text != null)
            {
                return configManager.ParseData(text, userData);
            }

            Log.Warning("Config asset '{0}' is invalid.", dataAssetName);
            return false;
        }

        /// <summary>
        /// 读取全局配置数据（从二进制流中）。
        /// </summary>
        /// <param name="configManager">配置管理器。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public bool ReadData(IConfigManager configManager, string dataAssetName, byte[] dataBytes, int startIndex, int length, object userData)
        {
            return configManager.ParseData(dataBytes, startIndex, length, userData);
        }

        /// <summary>
        /// 解析全局配置（文本格式）。
        ///
        /// 格式说明：
        /// - 每行一条配置，使用 Tab 分隔
        /// - 4列：类型、配置名、未使用、配置值
        /// - '#' 开头的行被忽略
        ///
        /// 例如：
        /// GameName	x	My Game
        /// MaxLevel	x	100
        /// </summary>
        /// <param name="configManager">配置管理器。</param>
        /// <param name="dataString">要解析的文本内容。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(IConfigManager configManager, string dataString, object userData)
        {
            try
            {
                int position = 0;
                string configLineString = null;
                while ((configLineString = dataString.ReadLine(ref position)) != null)
                {
                    // 跳过空行
                    if (string.IsNullOrEmpty(configLineString))
                    {
                        continue;
                    }

                    // 跳过注释行（以 '#' 开头）
                    if (configLineString[0] == '#')
                    {
                        continue;
                    }

                    string[] splitedLine = configLineString.Split(ColumnSplitSeparator, StringSplitOptions.None);
                    if (splitedLine.Length != ColumnCount)
                    {
                        Log.Warning(string.Format(
                            "Can not parse config line '{0}', column count is {1}, expected {2}.",
                            configLineString, splitedLine.Length, ColumnCount));
                        return false;
                    }

                    string configName = splitedLine[1];
                    string configValue = splitedLine[3];
                    if (!configManager.AddConfig(configName, configValue))
                    {
                        Log.Warning("Can not add config '{0}', may be invalid or duplicate.", configName);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse config string with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 解析全局配置（二进制格式）。
        ///
        /// 二进制格式：连续的 string pair（配置名 + 配置值）。
        /// 使用 BinaryReader.ReadString() 读取。
        /// </summary>
        /// <param name="configManager">配置管理器。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(IConfigManager configManager, byte[] dataBytes, int startIndex, int length, object userData)
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream(dataBytes, startIndex, length, false))
                {
                    using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))
                    {
                        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                        {
                            string configName = binaryReader.ReadString();
                            string configValue = binaryReader.ReadString();
                            if (!configManager.AddConfig(configName, configValue))
                            {
                                Log.Warning("Can not add config '{0}', may be invalid or duplicate.", configName);
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse config bytes with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 释放配置资源。
        /// 在 GGF 单机模式下无需释放（文件内容已在内存中）。
        /// </summary>
        /// <param name="configManager">配置管理器。</param>
        /// <param name="dataAsset">要释放的资源。</param>
        public void ReleaseDataAsset(IConfigManager configManager, object dataAsset)
        {
            // GGF 单机模式下不需要释放资源
            // 资源是通过 FileAccess 直接读取的，不需要通过 ResourceComponent 管理
        }
    }
}
