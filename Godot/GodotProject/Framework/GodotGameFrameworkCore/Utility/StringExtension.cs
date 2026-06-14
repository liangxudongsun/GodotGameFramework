//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

/// <summary>
/// 对 string 的扩展方法。
///
/// 提供从字符串中逐行读取的工具方法，
/// 用于解析配置文件和数据表等文本内容。
///
/// 这是 UGF 原始代码中的 StringExtension，
/// 在核心框架之外定义，因此需要在此处提供。
/// </summary>
public static class StringExtension
{
    /// <summary>
    /// 从指定字符串中的指定位置处开始读取一行。
    ///
    /// 此方法不会创建字符串的子串拷贝（除非找到一行），
    /// 通过 ref position 参数追踪当前位置，实现高效的逐行扫描。
    ///
    /// 自动处理 \r、\n、\r\n 三种换行格式。
    /// </summary>
    /// <param name="rawString">要读取的字符串。</param>
    /// <param name="position">起始位置，读取后更新为下一行的起始位置。</param>
    /// <returns>读取的一行字符串，如果已到达末尾则返回 null。</returns>
    public static string ReadLine(this string rawString, ref int position)
    {
        if (position < 0)
        {
            return null;
        }

        int length = rawString.Length;
        int offset = position;
        while (offset < length)
        {
            char ch = rawString[offset];
            switch (ch)
            {
                case '\r':
                case '\n':
                    if (offset > position)
                    {
                        string line = rawString.Substring(position, offset - position);
                        position = offset + 1;
                        if ((ch == '\r') && (position < length) && (rawString[position] == '\n'))
                        {
                            position++;
                        }

                        return line;
                    }

                    offset++;
                    position++;
                    break;

                default:
                    offset++;
                    break;
            }
        }

        if (offset > position)
        {
            string line = rawString.Substring(position, offset - position);
            position = offset;
            return line;
        }

        return null;
    }
}
