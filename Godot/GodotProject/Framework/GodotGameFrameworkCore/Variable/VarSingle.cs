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
    /// float 变量类。
    ///
    /// 继承自 Variable&lt;float&gt;，用于 DataNode 等需要存储浮点数值的系统。
    /// 支持与 float 类型的隐式转换，使用引用池优化内存分配。
    ///
    /// 使用方式：
    /// <code>
    /// VarSingle varFloat = 3.14f;
    /// float value = varFloat.Value;
    /// float value2 = varFloat; // 隐式转换
    /// </code>
    /// </summary>
    public sealed class VarSingle : Variable<float>
    {
        /// <summary>
        /// 初始化 float 变量类的新实例。
        /// </summary>
        public VarSingle()
        {
        }

        /// <summary>
        /// 从 float 到 VarSingle 的隐式转换。
        /// </summary>
        /// <param name="value">浮点数值。</param>
        public static implicit operator VarSingle(float value)
        {
            VarSingle varValue = ReferencePool.Acquire<VarSingle>();
            varValue.Value = value;
            return varValue;
        }

        /// <summary>
        /// 从 VarSingle 到 float 的隐式转换。
        /// </summary>
        /// <param name="value">变量实例。</param>
        public static implicit operator float(VarSingle value)
        {
            return value.Value;
        }
    }
}
