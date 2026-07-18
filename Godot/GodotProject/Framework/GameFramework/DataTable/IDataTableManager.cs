//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;

namespace GameFramework.DataTable
{
    /// <summary>
    /// 数据表管理器接口。
    /// </summary>
    public interface IDataTableManager
    {
        /// <summary>
        /// 设置数据加载器。加载器接收文件名，返回二进制数据。
        /// </summary>
        void SetDataLoader(Func<string, byte[]> loader);

        /// <summary>
        /// 获取数据表集合（懒加载，首次访问时触发所有表的一次性加载）。
        /// </summary>
        GameConfig.Tables GetTables();
    }
}
