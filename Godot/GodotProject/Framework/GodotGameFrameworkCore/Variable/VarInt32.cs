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
    /// int 变量类。
    ///
    /// 继承自 Variable&lt;int&gt;，用于 DataNode 等需要存储整数值的系统。
    /// 支持与 int 类型的隐式转换，使用引用池优化内存分配。
    ///
    /// 使用方式：
    /// <code>
    /// VarInt32 varInt = 100;
    /// int value = varInt.Value;
    /// int value2 = varInt; // 隐式转换
    /// </code>
    /// </summary>
    public sealed class VarInt32 : Variable<int>
    {
        /// <summary>
        /// 初始化 int 变量类的新实例。
        /// </summary>
        public VarInt32()
        {
        }

        /// <summary>
        /// 从 int 到 VarInt32 的隐式转换。
        /// </summary>
        /// <param name="value">整数值。</param>
        public static implicit operator VarInt32(int value)
        {
            VarInt32 varValue = ReferencePool.Acquire<VarInt32>();
            varValue.Value = value;
            return varValue;
        }

        /// <summary>
        /// 从 VarInt32 到 int 的隐式转换。
        /// </summary>
        /// <param name="value">变量实例。</param>
        public static implicit operator int(VarInt32 value)
        {
            return value.Value;
        }
    }
}
