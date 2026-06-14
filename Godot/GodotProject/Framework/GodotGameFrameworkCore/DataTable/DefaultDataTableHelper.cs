//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.DataTable;
using System;
using System.IO;
using System.Text;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认数据表辅助器。
    ///
    /// 实现 IDataProviderHelper&lt;DataTableBase&gt; 和 IDataTableHelper 接口。
    /// 负责读取和解析 CSV 格式的数据表文件。
    ///
    /// 数据表文件格式说明：
    /// - 每行一条数据记录
    /// - 使用 Tab 分隔各字段
    /// - '#' 开头的行被忽略（注释行）
    /// - 第一行通常为字段名（注释行，以#开头）
    /// - 后续每行通过 DataTableBase.AddDataRow 解析为 IDataRow
    ///
    /// 示例 CSV 内容（以 Tab 分隔）：
    /// #Id	Name	Hp	Attack
    /// 1	Warrior	100	20
    /// 2	Mage	60	35
    /// 3	Archer	80	25
    ///
    /// 解析流程：
    /// 1. 按行读取文本内容
    /// 2. 跳过注释行（#开头）和空行
    /// 3. 将每行传递给 DataTableBase.AddDataRow
    /// 4. DataTableBase 内部调用 IDataRow.Parse 解析字段
    ///
    /// 对应 Unity 版本中的 DefaultDataTableHelper。
    /// </summary>
    public class DefaultDataTableHelper : IDataProviderHelper<DataTableBase>, IDataTableHelper
    {
        /// <summary>
        /// 读取数据表（从已加载的资源中）。
        ///
        /// 在 GGF 中，dataAsset 是通过 FileAccess 读取的文件内容。
        /// </summary>
        /// <param name="dataTableOwner">数据表实例。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataAsset">资源数据。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public bool ReadData(DataTableBase dataTableOwner, string dataAssetName, object dataAsset, object userData)
        {
            byte[] bytes = dataAsset as byte[];
            if (bytes != null)
            {
                return dataTableOwner.ParseData(bytes, userData);
            }

            string text = dataAsset as string;
            if (text != null)
            {
                return dataTableOwner.ParseData(text, userData);
            }

            Log.Warning("Data table asset '{0}' is invalid.", dataAssetName);
            return false;
        }

        /// <summary>
        /// 读取数据表（从二进制流中）。
        /// </summary>
        /// <param name="dataTableOwner">数据表实例。</param>
        /// <param name="dataAssetName">资源名称。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否读取成功。</returns>
        public bool ReadData(DataTableBase dataTableOwner, string dataAssetName, byte[] dataBytes, int startIndex, int length, object userData)
        {
            return dataTableOwner.ParseData(dataBytes, startIndex, length, userData);
        }

        /// <summary>
        /// 解析数据表（文本格式）。
        ///
        /// 逐行读取文本，跳过注释行和空行，
        /// 将每行传递给 DataTableBase.AddDataRow 进行解析。
        /// </summary>
        /// <param name="dataTableOwner">数据表实例。</param>
        /// <param name="dataString">要解析的文本内容。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(DataTableBase dataTableOwner, string dataString, object userData)
        {
            try
            {
                int position = 0;
                string dataRowString = null;
                while ((dataRowString = dataString.ReadLine(ref position)) != null)
                {
                    // 跳过空行
                    if (string.IsNullOrEmpty(dataRowString))
                    {
                        continue;
                    }

                    // 跳过注释行
                    if (dataRowString[0] == '#')
                    {
                        continue;
                    }

                    // 将每行数据添加到数据表
                    // DataTableBase 内部会调用 IDataRow.Parse 来解析
                    if (!dataTableOwner.AddDataRow(dataRowString, userData))
                    {
                        Log.Warning("Can not parse data row '{0}'.", dataRowString);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse data table string with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 解析数据表（二进制格式）。
        ///
        /// 二进制格式：连续的数据块，每个块前有 7bit 编码的长度。
        /// </summary>
        /// <param name="dataTableOwner">数据表实例。</param>
        /// <param name="dataBytes">二进制数据。</param>
        /// <param name="startIndex">起始位置。</param>
        /// <param name="length">数据长度。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>是否解析成功。</returns>
        public bool ParseData(DataTableBase dataTableOwner, byte[] dataBytes, int startIndex, int length, object userData)
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream(dataBytes, startIndex, length, false))
                {
                    using (BinaryReader binaryReader = new BinaryReader(memoryStream, Encoding.UTF8))
                    {
                        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                        {
                            // 读取 7bit 编码的行数据长度
                            int dataRowBytesLength = binaryReader.Read7BitEncodedInt();
                            if (!dataTableOwner.AddDataRow(dataBytes, (int)binaryReader.BaseStream.Position, dataRowBytesLength, userData))
                            {
                                Log.Warning("Can not parse data row bytes.");
                                return false;
                            }

                            binaryReader.BaseStream.Position += dataRowBytesLength;
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("Can not parse data table bytes with exception '{0}'.", exception);
                return false;
            }
        }

        /// <summary>
        /// 释放数据表资源。
        /// 在 GGF 单机模式下无需释放。
        /// </summary>
        /// <param name="dataTableOwner">数据表实例。</param>
        /// <param name="dataAsset">要释放的资源。</param>
        public void ReleaseDataAsset(DataTableBase dataTableOwner, object dataAsset)
        {
            // GGF 单机模式下不需要释放资源
        }
    }
}
