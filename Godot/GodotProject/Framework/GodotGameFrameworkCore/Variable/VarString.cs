//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;

namespace GodotGameFramework
{
    /// <summary>
    /// string 变量类。
    ///
    /// 继承自 Variable&lt;string&gt;，用于 DataNode 等需要存储字符串值的系统。
    /// 支持与 string 类型的隐式转换，使用引用池优化内存分配。
    ///
    /// 使用方式：
    /// <code>
    /// VarString varStr = "Hello";
    /// string value = varStr.Value;
    /// string value2 = varStr; // 隐式转换
    /// </code>
    /// </summary>
    public sealed class VarString : Variable<string>
    {
        /// <summary>
        /// 初始化 string 变量类的新实例。
        /// </summary>
        public VarString()
        {
        }

        /// <summary>
        /// 从 string 到 VarString 的隐式转换。
        /// </summary>
        /// <param name="value">字符串值。</param>
        public static implicit operator VarString(string value)
        {
            VarString varValue = ReferencePool.Acquire<VarString>();
            varValue.Value = value;
            return varValue;
        }

        /// <summary>
        /// 从 VarString 到 string 的隐式转换。
        /// </summary>
        /// <param name="value">变量实例。</param>
        public static implicit operator string(VarString value)
        {
            return value.Value;
        }
    }
}
