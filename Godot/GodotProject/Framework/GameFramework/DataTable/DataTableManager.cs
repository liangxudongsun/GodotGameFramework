//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameConfig;
using GameFramework.Resource;
using GodotGameFramework;
using GodotGameFramework.Resource;
using Luban;
using System;
using System.Collections.Generic;

namespace GameFramework.DataTable
{
    /// <summary>
    /// 数据表管理器。
    /// </summary>
    internal sealed partial class DataTableManager : GameFrameworkModule, IDataTableManager
    {
        private bool _init = false;

        private Tables _tables;

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
        private ResourceComponent m_ResourceCmp;

        /// <summary>
        /// 加载配置。
        /// </summary>
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
        /// 加载二进制配置。
        /// </summary>
        /// <param name="file">FileName</param>
        /// <returns>ByteBuf</returns>
        private ByteBuf LoadByteBuf(string file)
        {
            string path = Utility.Text.Format(GameFolderConstant.GameConfigs, file);
            byte[] bytes = m_ResourceCmp.LoadBinary(path);
            if (bytes == null || bytes.Length == 0)
            {
                throw new Exception($"Failed to load config file: {path}");
            }
            return new ByteBuf(bytes);
        }

        public void SetResourcesComponent(object dataTableComponent)
        {
            m_ResourceCmp = dataTableComponent as ResourceComponent;
        }

        public Tables GetTables()
        {
            return Tables;
        }

    }
}
