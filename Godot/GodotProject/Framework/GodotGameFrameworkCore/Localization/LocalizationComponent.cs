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

namespace GodotGameFramework
{
    /// <summary>
    /// 本地化组件。
    ///
    /// 这是本地化系统的封装组件，提供多语言字典加载、翻译文本查询、语言切换等功能。
    /// 支持两种翻译方式：
    /// 1. UGF API：通过 GetString(key) 查询翻译文本（支持参数化格式化）
    /// 2. Godot 原生：通过 Tr(key) 自动翻译（TranslationServer 桥接）
    ///
    /// 架构说明：
    /// GGF 的 LocalizationComponent 采用与 ConfigComponent、DataTableComponent 相同的策略 —
    /// 绕过核心 DataProvider 的 ReadData 管道（因为核心 DataProvider.ReadData 内部调用
    /// ResourceManager.LoadAsset，需要版本列表），直接使用 ResourceComponent.LoadText()
    /// 加载字典文件后调 ParseData() 解析。
    ///
    /// TranslationServer 桥接（方案 B 独有）：
    /// 在 ParseData 成功后，LocalizationComponent 会重新解析文本内容，将键值对填充到
    /// Godot 的 OptimizedTranslation 资源中，并注册到 TranslationServer。
    /// 这样 Godot 的 Tr("key") 函数也能正常工作。
    ///
    /// 使用方式：
    /// <code>
    /// LocalizationComponent loc = GF.Localization;
    ///
    /// // 加载字典文件
    /// loc.ReadData("res://Data/Localization/ChineseSimplified.txt");
    ///
    /// // 设置语言（同时同步到 TranslationServer）
    /// loc.Language = Language.ChineseSimplified;
    ///
    /// // 方式1：UGF API 查询翻译
    /// string title = loc.GetString("GameTitle");              // "点击方块"
    /// string score = loc.GetString("ScoreFormat", 100);       // "得分：100"
    ///
    /// // 方式2：Godot 原生 Tr() 自动翻译
    /// string title2 = Tr("GameTitle");                       // "点击方块"
    /// </code>
    ///
    /// 对应 Unity 版本中的 LocalizationComponent。
    /// </summary>
    public sealed partial class LocalizationComponent : GameFrameworkComponent
    {
        // ================================================================
        //  私有字段
        // ================================================================

        /// <summary>
        /// 核心层的本地化管理器实例。
        /// 提供字典存储、GetString 查询、ParseData 解析等功能。
        /// </summary>
        private ILocalizationManager m_LocalizationManager = null;

        /// <summary>
        /// Godot 优化翻译资源。
        ///
        /// 用于桥接到 TranslationServer，使 Tr("key") 也能工作。
        /// 每次调用 ParseData(string) 时创建新的实例（替换旧的）。
        /// 当 RemoveAllRawStrings() 时从 TranslationServer 移除并置空。
        /// </summary>
        private OptimizedTranslation m_OptimizedTranslation = null;

        // ================================================================
        //  公共属性
        // ================================================================

        /// <summary>
        /// 获取或设置当前本地化语言。
        ///
        /// 设置语言时会同步更新 Godot TranslationServer 的 locale，
        /// 使 Tr("key") 自动翻译切换到对应语言。
        ///
        /// 注意：设置语言不会自动重新加载字典。切换语言的典型流程：
        /// 1. RemoveAllRawStrings() — 清空旧字典
        /// 2. ReadData(newLanguageFile) — 加载新语言的字典
        /// 3. Language = newLanguage — 设置新语言（如果 ReadData 中未设置）
        ///
        /// 从 UGF LocalizationComponent.Language 属性移植，增加了 TranslationServer 同步。
        /// </summary>
        public Language Language
        {
            get { return m_LocalizationManager.Language; }
            set
            {
                m_LocalizationManager.Language = value;
                // 同步到 Godot TranslationServer
                string locale = DefaultLocalizationHelper.GetLocaleByLanguage(value);
                TranslationServer.SetLocale(locale);
            }
        }

        /// <summary>
        /// 获取操作系统语言。
        /// 通过 DefaultLocalizationHelper 从 OS.GetLocale() 映射而来。
        /// </summary>
        public Language SystemLanguage
        {
            get { return m_LocalizationManager.SystemLanguage; }
        }

        /// <summary>
        /// 获取字典中翻译条目的数量。
        /// </summary>
        public int DictionaryCount
        {
            get { return m_LocalizationManager.DictionaryCount; }
        }

        /// <summary>
        /// 获取缓冲二进制流的大小。
        /// </summary>
        public int CachedBytesSize
        {
            get { return m_LocalizationManager.CachedBytesSize; }
        }

        // ================================================================
        //  生命周期
        // ================================================================

        /// <summary>
        /// 节点初始化回调。
        /// 获取核心层 ILocalizationManager，创建并设置 DefaultLocalizationHelper。
        ///
        /// 与 ConfigComponent._Ready() 模式一致：
        /// 1. 从模块系统获取核心管理器
        /// 2. 创建 Helper 实例
        /// 3. 将同一个 Helper 注册为 DataProviderHelper 和 LocalizationHelper（双角色）
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_LocalizationManager = GameFrameworkEntry.GetModule<ILocalizationManager>();
            if (m_LocalizationManager == null)
            {
                Log.Fatal("Localization manager is invalid.");
                return;
            }

            // 创建默认本地化辅助器
            // 同一个实例承担两个角色（与 UGF LocalizationHelperBase 模式一致）：
            // 1. IDataProviderHelper — 解析字典文件
            // 2. ILocalizationHelper — 查询系统语言
            DefaultLocalizationHelper localizationHelper = new DefaultLocalizationHelper();
            m_LocalizationManager.SetDataProviderHelper(localizationHelper);
            m_LocalizationManager.SetLocalizationHelper(localizationHelper);
        }

        // ================================================================
        //  读取字典（绕过模式：ResourceComponent → ParseData）
        // ================================================================

        /// <summary>
        /// 从文件读取本地化字典并解析（文本格式）。
        ///
        /// 通过 ResourceComponent.LoadText() 加载文件内容，
        /// 然后调用 ParseData() 解析到核心字典并桥接到 TranslationServer。
        ///
        /// 文件格式：Tab 分隔 4 列，'#' 开头为注释，列[1]=key，列[3]=value。
        ///
        /// 与 ConfigComponent.ReadData() 相同的绕过模式：
        /// 核心框架的 DataProvider.ReadData() 需要 ResourceManager 版本列表，
        /// GGF 直接通过 ResourceComponent 加载后调 ParseData。
        /// </summary>
        /// <param name="dataAssetName">字典文件路径（如 "res://Data/Localization/ChineseSimplified.txt"）。</param>
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
                Log.Warning("Can not load localization data from '{0}'.", dataAssetName);
                return false;
            }

            return ParseData(content);
        }

        /// <summary>
        /// 从文件读取本地化字典并解析（二进制格式）。
        ///
        /// 通过 ResourceComponent.LoadBinary() 加载文件内容，
        /// 然后调用核心的 ParseData(byte[]) 解析。
        ///
        /// 注意：二进制格式的 ParseData 不会桥接到 TranslationServer。
        /// 如需 TranslationServer 桥接，请使用文本格式的 ReadData。
        /// </summary>
        /// <param name="dataAssetName">字典文件路径（如 "res://Data/Localization/ChineseSimplified.bytes"）。</param>
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
                Log.Warning("Can not load localization binary data from '{0}'.", dataAssetName);
                return false;
            }

            return m_LocalizationManager.ParseData(bytes);
        }

        // ================================================================
        //  解析字典（ParseData + TranslationServer 桥接）
        // ================================================================

        /// <summary>
        /// 解析本地化字典（文本格式）。
        ///
        /// 这是 TranslationServer 桥接的核心方法。执行流程：
        /// 1. 清除旧的 OptimizedTranslation（从 TranslationServer 移除）
        /// 2. 调用核心 ParseData 填充内部 Dictionary
        /// 3. 重新解析文本填充新的 OptimizedTranslation，注册到 TranslationServer
        /// 4. 设置 TranslationServer 的 locale
        ///
        /// 双解析说明：
        /// 核心的 ParseData 通过 DefaultLocalizationHelper 填充 m_Dictionary。
        /// 然后我们再次解析同一文本填充 OptimizedTranslation（Godot 资源）。
        /// 本地化文件通常很小（&lt;100KB），性能影响可忽略。
        /// 这种设计保持桥接逻辑与核心框架隔离，不侵入核心代码。
        /// </summary>
        /// <param name="dictionaryString">字典文本内容。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(string dictionaryString)
        {
            // 步骤1：清除旧的 TranslationServer 状态
            UnregisterTranslation();

            // 步骤2：调用核心 ParseData（通过 DefaultLocalizationHelper 填充 m_Dictionary）
            bool result = m_LocalizationManager.ParseData(dictionaryString);
            if (!result)
            {
                return false;
            }

            // 步骤3：桥接到 TranslationServer（重新解析文本填充 OptimizedTranslation）
            BridgeToTranslationServer(dictionaryString);
            return true;
        }

        /// <summary>
        /// 解析本地化字典（文本格式，带用户数据）。
        /// 直接委托给核心管理器，不桥接 TranslationServer。
        /// </summary>
        /// <param name="dictionaryString">字典文本内容。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(string dictionaryString, object userData)
        {
            return m_LocalizationManager.ParseData(dictionaryString, userData);
        }

        /// <summary>
        /// 解析本地化字典（二进制格式）。
        /// 直接委托给核心管理器，不桥接 TranslationServer。
        /// </summary>
        /// <param name="dictionaryBytes">字典二进制数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] dictionaryBytes)
        {
            return m_LocalizationManager.ParseData(dictionaryBytes);
        }

        /// <summary>
        /// 解析本地化字典（二进制格式，带用户数据）。
        /// </summary>
        /// <param name="dictionaryBytes">字典二进制数据。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] dictionaryBytes, object userData)
        {
            return m_LocalizationManager.ParseData(dictionaryBytes, userData);
        }

        /// <summary>
        /// 解析本地化字典（二进制格式，指定范围）。
        /// </summary>
        /// <param name="dictionaryBytes">字典二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] dictionaryBytes, int startIndex, int length)
        {
            return m_LocalizationManager.ParseData(dictionaryBytes, startIndex, length);
        }

        /// <summary>
        /// 解析本地化字典（二进制格式，指定范围，带用户数据）。
        /// </summary>
        /// <param name="dictionaryBytes">字典二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(byte[] dictionaryBytes, int startIndex, int length, object userData)
        {
            return m_LocalizationManager.ParseData(dictionaryBytes, startIndex, length, userData);
        }

        // ================================================================
        //  翻译查询 - GetString（17 个重载，全部委托给核心管理器）
        // ================================================================

        /// <summary>
        /// 获取翻译后的字符串。
        /// 如果键不存在，返回 "&lt;NoKey&gt;{key}"。
        /// </summary>
        /// <param name="key">字典键。</param>
        /// <returns>翻译后的字符串。</returns>
        public string GetString(string key)
        {
            return m_LocalizationManager.GetString(key);
        }

        /// <summary>
        /// 获取翻译后的字符串（1 个参数）。
        /// 使用 Utility.Text.Format 进行参数化格式化。
        /// </summary>
        public string GetString<T>(string key, T arg)
        {
            return m_LocalizationManager.GetString(key, arg);
        }

        /// <summary>
        /// 获取翻译后的字符串（2 个参数）。
        /// </summary>
        public string GetString<T1, T2>(string key, T1 arg1, T2 arg2)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2);
        }

        /// <summary>
        /// 获取翻译后的字符串（3 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3>(string key, T1 arg1, T2 arg2, T3 arg3)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3);
        }

        /// <summary>
        /// 获取翻译后的字符串（4 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4);
        }

        /// <summary>
        /// 获取翻译后的字符串（5 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5);
        }

        /// <summary>
        /// 获取翻译后的字符串（6 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5,
            T6 arg6)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6);
        }

        /// <summary>
        /// 获取翻译后的字符串（7 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            T5 arg5, T6 arg6, T7 arg7)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        /// <summary>
        /// 获取翻译后的字符串（8 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8>(string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4,
            T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        /// <summary>
        /// 获取翻译后的字符串（9 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string key, T1 arg1, T2 arg2, T3 arg3,
            T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        /// <summary>
        /// 获取翻译后的字符串（10 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string key, T1 arg1, T2 arg2,
            T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10);
        }

        /// <summary>
        /// 获取翻译后的字符串（11 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string key, T1 arg1, T2 arg2,
            T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10, arg11);
        }

        /// <summary>
        /// 获取翻译后的字符串（12 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string key, T1 arg1,
            T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11,
            T12 arg12)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10, arg11, arg12);
        }

        /// <summary>
        /// 获取翻译后的字符串（13 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string key, T1 arg1,
            T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11,
            T12 arg12, T13 arg13)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10, arg11, arg12, arg13);
        }

        /// <summary>
        /// 获取翻译后的字符串（14 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string key,
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10,
            T11 arg11, T12 arg12, T13 arg13, T14 arg14)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10, arg11, arg12, arg13, arg14);
        }

        /// <summary>
        /// 获取翻译后的字符串（15 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string key,
            T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10,
            T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10, arg11, arg12, arg13, arg14, arg15);
        }

        /// <summary>
        /// 获取翻译后的字符串（16 个参数）。
        /// </summary>
        public string GetString<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(
            string key, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9,
            T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
        {
            return m_LocalizationManager.GetString(key, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9,
                arg10, arg11, arg12, arg13, arg14, arg15, arg16);
        }

        // ================================================================
        //  原始字典操作（委托给核心管理器）
        // ================================================================

        /// <summary>
        /// 检查字典中是否存在指定键。
        /// </summary>
        /// <param name="key">字典键。</param>
        /// <returns>是否存在。</returns>
        public bool HasRawString(string key)
        {
            return m_LocalizationManager.HasRawString(key);
        }

        /// <summary>
        /// 获取原始翻译值。
        /// 如果键不存在，返回 null。
        /// </summary>
        /// <param name="key">字典键。</param>
        /// <returns>原始翻译值，不存在返回 null。</returns>
        public string GetRawString(string key)
        {
            return m_LocalizationManager.GetRawString(key);
        }

        /// <summary>
        /// 添加翻译条目到字典。
        /// 如果键已存在或无效，返回 false。
        /// </summary>
        /// <param name="key">字典键。</param>
        /// <param name="value">翻译值。</param>
        /// <returns>是否添加成功。</returns>
        public bool AddRawString(string key, string value)
        {
            return m_LocalizationManager.AddRawString(key, value);
        }

        /// <summary>
        /// 从字典中移除指定键。
        /// </summary>
        /// <param name="key">字典键。</param>
        /// <returns>是否移除成功。</returns>
        public bool RemoveRawString(string key)
        {
            return m_LocalizationManager.RemoveRawString(key);
        }

        /// <summary>
        /// 清空所有翻译条目。
        ///
        /// 同时清除 TranslationServer 中的 OptimizedTranslation。
        /// 从 UGF LocalizationComponent.RemoveAllRawStrings() 移植，
        /// 增加了 TranslationServer 清理。
        /// </summary>
        public void RemoveAllRawStrings()
        {
            m_LocalizationManager.RemoveAllRawStrings();
            UnregisterTranslation();
        }

        // ================================================================
        //  缓冲管理（委托给核心管理器）
        // ================================================================

        /// <summary>
        /// 确保二进制流缓存分配足够大小的内存并缓存。
        /// </summary>
        /// <param name="ensureSize">要确保的大小。</param>
        public void EnsureCachedBytesSize(int ensureSize)
        {
            m_LocalizationManager.EnsureCachedBytesSize(ensureSize);
        }

        /// <summary>
        /// 释放缓存的二进制流。
        /// </summary>
        public void FreeCachedBytes()
        {
            m_LocalizationManager.FreeCachedBytes();
        }

        // ================================================================
        //  私有方法 - TranslationServer 桥接
        // ================================================================

        /// <summary>
        /// 桥接字典数据到 Godot TranslationServer。
        ///
        /// 执行流程：
        /// 1. 获取当前语言对应的 locale 字符串
        /// 2. 创建新的 OptimizedTranslation 资源
        /// 3. 重新解析文本，将每个键值对添加到 OptimizedTranslation
        /// 4. 注册到 TranslationServer 并激活
        ///
        /// 为什么需要重新解析：
        /// 核心的 ParseData 通过 DataProvider → DefaultLocalizationHelper 填充 m_Dictionary，
        /// 我们无法拦截单个 AddRawString 调用来同步 OptimizedTranslation。
        /// 重新解析保持桥接逻辑与核心框架完全隔离。
        /// 本地化文件通常很小（&lt;100KB），双解析的性能影响可忽略。
        /// </summary>
        /// <param name="dictionaryString">字典文本内容（与传给核心 ParseData 的相同）。</param>
        private void BridgeToTranslationServer(string dictionaryString)
        {
            // 如果语言未设置（Unspecified），不创建 Translation
            // （核心管理器会抛异常，但这里我们用 Language 枚举的默认值判断）
            try
            {
                Language currentLanguage = m_LocalizationManager.Language;
            }
            catch
            {
                // Language 为 Unspecified 时会抛异常，此时跳过桥接
                return;
            }

            Language language = m_LocalizationManager.Language;
            string locale = DefaultLocalizationHelper.GetLocaleByLanguage(language);

            // 创建 OptimizedTranslation 并设置 locale
            m_OptimizedTranslation = new OptimizedTranslation();
            m_OptimizedTranslation.Locale = locale;

            // 重新解析文本，填充 OptimizedTranslation
            // 使用与 DefaultLocalizationHelper.ParseData 相同的解析逻辑
            int position = 0;
            string line;
            while ((line = dictionaryString.ReadLine(ref position)) != null)
            {
                // 跳过空行和注释行
                if (string.IsNullOrEmpty(line) || line[0] == '#')
                {
                    continue;
                }

                string[] columns = line.Split(new string[] { "\t" }, StringSplitOptions.None);
                if (columns.Length != 4)
                {
                    continue; // 跳过格式错误的行（核心已验证过，这里跳过即可）
                }

                string key = columns[1];
                string value = columns[3];
                m_OptimizedTranslation.AddMessage(key, value);
            }

            // 先移除可能存在的同名 Translation（防止重复注册）
            TranslationServer.RemoveTranslation(m_OptimizedTranslation);
            // 注册到 TranslationServer
            TranslationServer.AddTranslation(m_OptimizedTranslation);
            // 激活该 locale
            TranslationServer.SetLocale(locale);
        }

        /// <summary>
        /// 从 TranslationServer 注销当前的 OptimizedTranslation。
        ///
        /// 在以下场景调用：
        /// - ParseData 开始时（清除旧的 Translation）
        /// - RemoveAllRawStrings 时（切换语言前的清理）
        /// </summary>
        private void UnregisterTranslation()
        {
            if (m_OptimizedTranslation != null)
            {
                TranslationServer.RemoveTranslation(m_OptimizedTranslation);
                m_OptimizedTranslation = null;
            }
        }
    }
}
