//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Localization;

namespace GodotGameFramework.Localization
{
    public abstract partial class LocalizationHelperBase : GameFrameworkComponent, IDataProviderHelper<ILocalizationManager>, ILocalizationHelper
    {
        public abstract Language SystemLanguage { get; }

        public abstract bool ParseData(ILocalizationManager dataProviderOwner, string dataString, object userData);

        public abstract bool ParseData(ILocalizationManager dataProviderOwner, byte[] dataBytes, int startIndex, int length, object userData);

        public abstract bool ReadData(ILocalizationManager dataProviderOwner, string dataAssetName, object dataAsset, object userData);

        public abstract bool ReadData(ILocalizationManager dataProviderOwner, string dataAssetName, byte[] dataBytes, int startIndex, int length, object userData);

        public abstract void ReleaseDataAsset(ILocalizationManager dataProviderOwner, object dataAsset);
        public static string GetLocaleByLanguage(Language language)
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
        public static Language GetLanguageByLocale(string locale)
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
