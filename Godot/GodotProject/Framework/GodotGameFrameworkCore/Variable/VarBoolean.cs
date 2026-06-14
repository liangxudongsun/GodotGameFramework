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
    /// bool 变量类。
    ///
    /// 继承自 Variable&lt;bool&gt;，用于 DataNode 等需要存储 bool 值的系统。
    /// 支持与 bool 类型的隐式转换，使用引用池优化内存分配。
    ///
    /// 使用方式：
    /// <code>
    /// // 通过隐式转换创建（自动从引用池获取）
    /// VarBoolean varBool = true;
    ///
    /// // 读取值
    /// bool value = varBool.Value;
    /// bool value2 = varBool; // 隐式转换
    /// </code>
    /// </summary>
    public sealed class VarBoolean : Variable<bool>
    {
        /// <summary>
        /// 初始化 bool 变量类的新实例。
        /// </summary>
        public VarBoolean()
        {
        }

        /// <summary>
        /// 从 bool 到 VarBoolean 的隐式转换。
        /// 自动从引用池获取实例并设置值。
        /// </summary>
        /// <param name="value">布尔值。</param>
        public static implicit operator VarBoolean(bool value)
        {
            VarBoolean varValue = ReferencePool.Acquire<VarBoolean>();
            varValue.Value = value;
            return varValue;
        }

        /// <summary>
        /// 从 VarBoolean 到 bool 的隐式转换。
        /// </summary>
        /// <param name="value">变量实例。</param>
        public static implicit operator bool(VarBoolean value)
        {
            return value.Value;
        }
    }
}
