//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameConfig;
using Luban;
using System;

namespace GameFramework.DataTable
{
    /// <summary>
    /// 数据表管理器。
    /// 通过注入的 Func&lt;string, byte[]&gt; 加载器读取 Luban 二进制配置，
    /// 不再依赖 Godot 桥接层的 ResourceComponent。
    /// </summary>
    internal sealed partial class DataTableManager : GameFrameworkModule, IDataTableManager
    {
        private bool _init = false;
        private Tables _tables;
        private Func<string, byte[]> _dataLoader;

        public Tables Tables
        {
            get
            {
                if (!_init)
                {
                    Load();
                }
                return _tables;
            }
        }

        public void Load()
        {
            _tables = new Tables(LoadByteBuf);
            _init = true;
        }

        internal override void Shutdown()
        {
            _init = false;
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// Luban Tables 构造函数的加载委托。
        /// 由桥接层注入的 _dataLoader 提供实际文件读取能力。
        /// </summary>
        private ByteBuf LoadByteBuf(string file)
        {
            byte[] bytes = _dataLoader(file);
            if (bytes == null || bytes.Length == 0)
            {
                throw new Exception($"Failed to load config file: {file}");
            }
            return new ByteBuf(bytes);
        }

        public void SetDataLoader(Func<string, byte[]> loader)
        {
            _dataLoader = loader ?? throw new GameFrameworkException("Data loader is invalid.");
        }

        public Tables GetTables()
        {
            return Tables;
        }
    }
}
