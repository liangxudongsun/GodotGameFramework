//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameConfig;
using GameFramework;
using GameFramework.DataTable;
using GameFramework.Resource;
using Godot;
using System;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 数据表组件。
    /// </summary>
    public sealed partial class DataTableComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的数据表管理器实例。
        /// </summary>
        private IDataTableManager m_DataTableManager = null;

        /// <summary>
        /// 节点初始化回调。
        /// 获取核心层 IDataTableManager，创建并设置 Helper。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_DataTableManager = GameFrameworkEntry.GetModule<IDataTableManager>();
            if (m_DataTableManager == null)
            {
                Log.Fatal("Data table manager is invalid.");
                return;
            }
            m_DataTableManager.SetResourcesComponent(GF.Resource);
        }

        /// <summary>
        /// 获取数据表数量。
        /// </summary>
        public Tables GetTables()
        {
            return m_DataTableManager.GetTables();
        }

    }
}
