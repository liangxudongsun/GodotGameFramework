//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System;
using System.Text;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认文本格式化辅助器。
    ///
    /// 实现了核心框架的 Utility.Text.ITextHelper 接口，
    /// 使用 StringBuilder 缓存来优化字符串格式化的性能。
    ///
    /// 工作原理：
    /// - 每个线程维护一个独立的 StringBuilder 缓存（[ThreadStatic]）
    /// - 每次格式化时复用同一个 StringBuilder，减少 GC 压力
    /// - 初始容量为 1024 字符，按需自动扩容
    ///
    /// 对应 Unity 版本中的 DefaultTextHelper。
    /// </summary>
    public class DefaultTextHelper : Utility.Text.ITextHelper
    {
        /// <summary>
        /// StringBuilder 的初始容量。
        /// </summary>
        private const int StringBuilderCapacity = 1024;

        /// <summary>
        /// 线程静态变量，每个线程有独立的 StringBuilder 缓存。
        /// 这样在多线程环境下也不需要加锁。
        /// </summary>
        [ThreadStatic]
        private static StringBuilder s_CachedStringBuilder = null;

        /// <summary>
        /// 获取格式化字符串（1个参数）。
        /// </summary>
        public string Format<T>(string format, T arg)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（2个参数）。
        /// </summary>
        public string Format<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（3个参数）。
        /// </summary>
        public string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（4个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（5个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（6个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（7个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（8个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（9个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（10个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（11个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（12个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（13个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（14个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（15个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串（16个参数）。
        /// </summary>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
        {
            if (format == null)
            {
                throw new GameFrameworkException("Format is invalid.");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 检查并初始化当前线程的 StringBuilder 缓存。
        /// 如果当前线程还没有创建 StringBuilder，则创建一个。
        /// </summary>
        private static void CheckCachedStringBuilder()
        {
            if (s_CachedStringBuilder == null)
            {
                s_CachedStringBuilder = new StringBuilder(StringBuilderCapacity);
            }
        }
    }
}
