//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Localization;
using Godot;
using GodotGameFramework.Extensions;
using System;
using System.IO;
using System.Text;

namespace GodotGameFramework.Localization
{
    /// <summary>
    /// 默认本地化辅助器。
    /// </summary>
    public partial class DefaultLocalizationHelper : LocalizationHelperBase
    {
        private static readonly string[] ColumnSplitSeparator = new string[] { "\t" };

        /// <summary>
        /// 每行翻译的最小列数（4列：A-空、B-索引、C-键名、D-翻译值）。
        /// </summary>
        private const int MinColumnCount = 4;

        /// <summary>
        /// 读取本地化字典数据（从已加载的资源中）。
        /// <returns>是否读取成功。</returns>
        public override bool ReadData(ILocalizationManager localizationManager, string dataAssetName, object dataAsset,
            object userData)
        {
            byte[] bytes = dataAsset as byte[];
            if (bytes != null)
            {
                return localizationManager.ParseData(bytes, userData);
            }

            string text = dataAsset as string;
            if (text != null)
            {
                return localizationManager.ParseData(text, userData);
            }

            Log.Warning("Localization asset '{0}' is invalid.", dataAssetName);
            return false;
        }

        /// <summary>
        /// 读取本地化字典数据（从二进制流中）。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public override bool ReadData(ILocalizationManager localizationManager, string dataAssetName, byte[] dataBytes,
            int startIndex, int length, object userData)
        {
            return localizationManager.ParseData(dataBytes, startIndex, length, userData);
        }

        /// <summary>
        /// 解析本地化字典（文本格式）。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataString">要解析的文本内容。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public override bool ParseData(ILocalizationManager localizationManager, string dataString, object userData)
        {
            try
            {
                int position = 0;
                string dictionaryLineString = null;
                while ((dictionaryLineString = dataString.ReadLine(ref position)) != null)
                {
                    // 跳过空行
                    if (string.IsNullOrEmpty(dictionaryLineString))
                    {
                        continue;
                    }

                    // 跳过注释行（以 '#' 开头）
                    if (dictionaryLineString[0] == '#')
                    {
                        continue;
                    }

                    string[] splitedLine = dictionaryLineString.Split(ColumnSplitSeparator, StringSplitOptions.None);
                    if (splitedLine.Length < MinColumnCount)
                    {
                        continue;
                    }

                    // 列[2] = 字典键（C列），列[3] = 翻译值（D列）
                    string key = splitedLine[2];
                    string value = splitedLine[3];
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    if (!localizationManager.AddRawString(key, value))
                    {
                        Log.Warning("Can not add dictionary '{0}', may be invalid or duplicate.", key);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse dictionary string with exception '{0}'.", exception);
                return false;
            }
        }


        /// <summary>
        /// 解析本地化字典（二进制格式）。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public override bool ParseData(ILocalizationManager localizationManager, byte[] dataBytes, int startIndex,
            int length, object userData)
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream(dataBytes, startIndex, length, false))
                {
                    using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))
                    {
                        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                        {
                            string key = binaryReader.ReadString();
                            string value = binaryReader.ReadString();
                            if (!localizationManager.AddRawString(key, value))
                            {
                                Log.Warning("Can not add dictionary '{0}', may be invalid or duplicate.", key);
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse dictionary bytes with exception '{0}'.", exception);
                return false;
            }
        }



        public override Language SystemLanguage
        {
            get
            {
                string locale = OS.GetLocale();
                return GetLanguageByLocale(locale);
            }
        }
    }
}
