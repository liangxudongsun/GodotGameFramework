//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameConfig;
using GameConfig.Constant;
using GameFramework;
using GameFramework.DataTable;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 数据表组件。
    /// 将 Godot 侧的 ResourceComponent.LoadBinary 封装为 Func&lt;string, byte[]&gt;，
    /// 注入纯 C# 层 DataTableManager，保持双层架构洁净。
    /// </summary>
    public sealed partial class DataTableComponent : GameFrameworkComponent
    {
        private IDataTableManager m_DataTableManager = null;

        public override void OnInit()
        {
            base.OnInit();
            m_DataTableManager = GameFrameworkEntry.GetModule<IDataTableManager>();
            if (m_DataTableManager == null)
            {
                Log.Fatal("Data table manager is invalid.");
                return;
            }

            // 将 Godot 资源加载桥接为纯 Func，路径格式化在桥接层完成
            m_DataTableManager.SetDataLoader(file =>
            {
                string path = Utility.Text.Format(GameFolderConstant.GameConfigs, file);
                return GF.Resource.LoadBinary(path);
            });
        }

        /// <summary>
        /// 获取 Luban 数据表集合（懒加载）。
        /// </summary>
        public Tables GetTables()
        {
            return m_DataTableManager?.GetTables();
        }
    }
}
