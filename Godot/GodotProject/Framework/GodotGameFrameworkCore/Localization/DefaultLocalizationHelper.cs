//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Localization;
using Godot;
using System;
using System.IO;
using System.Text;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认本地化辅助器。
    ///
    /// 实现 IDataProviderHelper&lt;ILocalizationManager&gt; 和 ILocalizationHelper 接口。
    /// 同一个实例承担两个角色（与 UGF 的 LocalizationHelperBase 模式一致）：
    /// 1. IDataProviderHelper — 负责解析本地化字典文件（文本/二进制格式）
    /// 2. ILocalizationHelper — 负责查询操作系统的当前语言
    ///
    /// 字典文件格式（与 UGF 一致的 Tab 分隔格式）：
    /// 每行一条翻译，使用 Tab 分隔 4 列：
    /// #注释	[键名]	[未使用]	[翻译值]
    /// 例如：
    /// x	GameTitle	x	点击方块
    /// x	StartGame	x	开始游戏
    /// x	ScoreFormat	x	得分：{0}
    ///
    /// '#' 开头的行被忽略（注释行）。
    /// 列[1] = 字典键（如 "GameTitle"）
    /// 列[3] = 翻译值（如 "点击方块"）
    /// 列[0] 和 列[2] 未使用
    ///
    /// 二进制格式：连续的 string pair（键 + 值），使用 BinaryReader.ReadString() 读取。
    ///
    /// 对应 Unity 版本中的 DefaultLocalizationHelper（LocalizationHelperBase 子类）。
    /// GGF 版本直接实现两个接口，无需 MonoBehaviour 基类。
    /// </summary>
    public class DefaultLocalizationHelper : IDataProviderHelper<ILocalizationManager>, ILocalizationHelper
    {
        // ================================================================
        //  常量
        // ================================================================

        /// <summary>
        /// 列分隔符：Tab 字符。
        /// </summary>
        private static readonly string[] ColumnSplitSeparator = new string[] { "\t" };

        /// <summary>
        /// 每行翻译的列数（4列：未使用、键名、未使用、翻译值）。
        /// </summary>
        private const int ColumnCount = 4;

        // ================================================================
        //  IDataProviderHelper<ILocalizationManager> 实现
        // ================================================================

        /// <summary>
        /// 读取本地化字典数据（从已加载的资源中）。
        ///
        /// 在 GGF 中，dataAsset 可以是 byte[]（二进制）或 string（文本）。
        /// 根据类型分派到对应的 ParseData 方法。
        ///
        /// 注意：此方法在 GGF 的绕过模式下不会被调用（LocalizationComponent
        /// 直接调 ParseData），保留仅为满足接口契约。
        ///
        /// 从 UGF DefaultLocalizationHelper.ReadData() 方法体级别移植。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataAsset">资源数据（byte[] 或 string）。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public bool ReadData(ILocalizationManager localizationManager, string dataAssetName, object dataAsset,
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
        ///
        /// 注意：此方法在 GGF 的绕过模式下不会被调用，保留仅为满足接口契约。
        /// 从 UGF DefaultLocalizationHelper.ReadData() 方法体级别移植。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public bool ReadData(ILocalizationManager localizationManager, string dataAssetName, byte[] dataBytes,
            int startIndex, int length, object userData)
        {
            return localizationManager.ParseData(dataBytes, startIndex, length, userData);
        }

        /// <summary>
        /// 解析本地化字典（文本格式）。
        ///
        /// 格式说明（与 UGF 完全一致）：
        /// - 每行一条翻译，使用 Tab 分隔
        /// - 4列：未使用、键名、未使用、翻译值
        /// - '#' 开头的行被忽略（注释行）
        /// - 空行被忽略
        ///
        /// 例如：
        /// x	GameTitle	x	点击方块
        /// x	ScoreFormat	x	得分：{0}
        ///
        /// 使用 ReadLine(ref position) 扩展方法逐行读取。
        /// 从 UGF DefaultLocalizationHelper.ParseData() 方法体级别移植。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataString">要解析的文本内容。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(ILocalizationManager localizationManager, string dataString, object userData)
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
                    if (splitedLine.Length != ColumnCount)
                    {
                        Log.Warning(string.Format(
                            "Can not parse dictionary line '{0}', column count is {1}, expected {2}.",
                            dictionaryLineString, splitedLine.Length, ColumnCount));
                        return false;
                    }

                    // 列[1] = 字典键，列[3] = 翻译值
                    string key = splitedLine[1];
                    string value = splitedLine[3];
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
        ///
        /// 二进制格式：连续的 string pair（键 + 值）。
        /// 使用 BinaryReader.ReadString() 读取（.NET 长度前缀 + UTF-8 编码）。
        /// 从 UGF DefaultLocalizationHelper.ParseData() 方法体级别移植。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(ILocalizationManager localizationManager, byte[] dataBytes, int startIndex,
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

        /// <summary>
        /// 释放本地化字典资源。
        /// 在 GGF 单机模式下无需释放（文件内容已在内存中）。
        /// </summary>
        /// <param name="localizationManager">本地化管理器。</param>
        /// <param name="dataAsset">要释放的资源。</param>
        public void ReleaseDataAsset(ILocalizationManager localizationManager, object dataAsset)
        {
            // GGF 单机模式下不需要释放资源
        }

        public Language SystemLanguage
        {
            get
            {
                string locale = OS.GetLocale();
                return GetLanguageByLocale(locale);
            }
        }

        internal static string GetLocaleByLanguage(Language language)
        {
            return language switch
            {
                Language.ChineseSimplified => "zh_CN",
                Language.ChineseTraditional => "zh_TW",
                Language.English => "en",
                Language.Japanese => "ja",
                Language.Korean => "ko",
                Language.French => "fr",
                Language.German => "de",
                Language.Spanish => "es",
                Language.Italian => "it",
                Language.PortugueseBrazil => "pt_BR",
                Language.PortuguesePortugal => "pt_PT",
                Language.Russian => "ru",
                Language.Arabic => "ar",
                Language.Thai => "th",
                Language.Vietnamese => "vi",
                Language.Polish => "pl",
                Language.Dutch => "nl",
                Language.Turkish => "tr",
                Language.Ukrainian => "uk",
                Language.Romanian => "ro",
                Language.Hungarian => "hu",
                Language.Czech => "cs",
                Language.Swedish => "sv",
                Language.Danish => "da",
                Language.Finnish => "fi",
                Language.Norwegian => "no",
                Language.Greek => "el",
                Language.Hebrew => "he",
                Language.Indonesian => "id",
                Language.Bulgarian => "bg",
                Language.Croatian => "hr",
                Language.Slovak => "sk",
                Language.Slovenian => "sl",
                Language.Estonian => "et",
                Language.Lithuanian => "lt",
                Language.Latvian => "lv",
                Language.Persian => "fa",
                Language.Macedonian => "mk",
                Language.SerboCroatian => "sr",
                Language.SerbianCyrillic => "sr",
                Language.SerbianLatin => "sr",
                Language.Afrikaans => "af",
                Language.Basque => "eu",
                Language.Belarusian => "be",
                Language.Catalan => "ca",
                Language.Faroese => "fo",
                Language.Georgian => "ka",
                Language.Icelandic => "is",
                Language.Malayalam => "ml",
                Language.Albanian => "sq",
                _ => "en" // Unspecified 和未映射的语言回退到英语
            };
        }

        private static Language GetLanguageByLocale(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return Language.English;
            }

            // 提取语言代码（'_' 或 '-' 之前的部分）
            int separatorIndex = locale.IndexOf('_');
            if (separatorIndex < 0)
            {
                separatorIndex = locale.IndexOf('-');
            }

            string languageCode = separatorIndex > 0
                ? locale.Substring(0, separatorIndex)
                : locale;

            // 提取地区代码（用于区分中文简繁体、葡萄牙语巴西/葡萄牙等）
            string regionCode = separatorIndex >= 0 && locale.Length > separatorIndex + 1
                ? locale.Substring(separatorIndex + 1)
                : string.Empty;

            string langLower = languageCode.ToLowerInvariant();
            string regionLower = regionCode.ToLowerInvariant();

            return langLower switch
            {
                // 中文：需要区分简体和繁体
                "zh" => regionLower switch
                {
                    "cn" or "hans" or "sg" => Language.ChineseSimplified,
                    "tw" or "hant" or "hk" or "mo" => Language.ChineseTraditional,
                    _ => Language.ChineseSimplified // 默认简体
                },
                // 葡萄牙语：需要区分巴西和葡萄牙
                "pt" => regionLower switch
                {
                    "br" => Language.PortugueseBrazil,
                    _ => Language.PortuguesePortugal
                },
                "af" => Language.Afrikaans,
                "sq" => Language.Albanian,
                "ar" => Language.Arabic,
                "eu" => Language.Basque,
                "be" => Language.Belarusian,
                "bg" => Language.Bulgarian,
                "ca" => Language.Catalan,
                "hr" or "sh" => Language.Croatian,
                "cs" => Language.Czech,
                "da" => Language.Danish,
                "nl" => Language.Dutch,
                "en" => Language.English,
                "et" => Language.Estonian,
                "fo" => Language.Faroese,
                "fi" => Language.Finnish,
                "fr" => Language.French,
                "ka" => Language.Georgian,
                "de" => Language.German,
                "el" => Language.Greek,
                "he" => Language.Hebrew,
                "hu" => Language.Hungarian,
                "is" => Language.Icelandic,
                "id" => Language.Indonesian,
                "it" => Language.Italian,
                "ja" => Language.Japanese,
                "ko" => Language.Korean,
                "lv" => Language.Latvian,
                "lt" => Language.Lithuanian,
                "mk" => Language.Macedonian,
                "ml" => Language.Malayalam,
                "no" => Language.Norwegian,
                "fa" => Language.Persian,
                "pl" => Language.Polish,
                "ro" => Language.Romanian,
                "ru" => Language.Russian,
                "sr" => Language.SerboCroatian,
                "sk" => Language.Slovak,
                "sl" => Language.Slovenian,
                "es" => Language.Spanish,
                "sv" => Language.Swedish,
                "th" => Language.Thai,
                "tr" => Language.Turkish,
                "uk" => Language.Ukrainian,
                "vi" => Language.Vietnamese,
                _ => Language.English // 未映射的语言回退到英语
            };
        }
    }
}
