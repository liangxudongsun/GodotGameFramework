using GameFramework;
using GameFramework.DataNode;

namespace GodotGameFramework
{
    /// <summary>
    /// 数据节点组件。
    /// </summary>
    public sealed partial class DataNodeComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的数据节点管理器实例。
        /// </summary>
        private IDataNodeManager m_DataNodeManager = null;

        /// <summary>
        /// 获取根数据节点。
        /// 所有数据节点都是根节点的子节点。
        /// </summary>
        public IDataNode Root => m_DataNodeManager.Root;

        public override void OnInit()
        {
            base.OnInit();
            m_DataNodeManager = GameFrameworkEntry.GetModule<IDataNodeManager>();
            if (m_DataNodeManager == null)
            {
                Log.Fatal("Data node manager is invalid.");
                return;
            }
        }


        /// <summary>
        /// 根据类型获取数据节点的数据。
        /// </summary>
        /// <typeparam name="T">要获取的数据类型（必须是 Variable 的子类）。</typeparam>
        /// <param name="path">相对于根节点的查找路径，使用点号分隔（如 "Player.Name"）。</param>
        /// <returns>指定类型的数据。</returns>
        public T GetData<T>(string path) where T : Variable
        {
            return m_DataNodeManager.GetData<T>(path);
        }

        /// <summary>
        /// 获取数据节点的数据。
        /// </summary>
        /// <param name="path">相对于根节点的查找路径。</param>
        /// <returns>数据节点的数据。</returns>
        public Variable GetData(string path)
        {
            return m_DataNodeManager.GetData(path);
        }

        /// <summary>
        /// 根据类型获取数据节点的数据。
        /// </summary>
        /// <typeparam name="T">要获取的数据类型。</typeparam>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="node">查找起始节点。</param>
        /// <returns>指定类型的数据。</returns>
        public T GetData<T>(string path, IDataNode node) where T : Variable
        {
            return m_DataNodeManager.GetData<T>(path, node);
        }

        /// <summary>
        /// 获取数据节点的数据。
        /// </summary>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="node">查找起始节点。</param>
        /// <returns>数据节点的数据。</returns>
        public Variable GetData(string path, IDataNode node)
        {
            return m_DataNodeManager.GetData(path, node);
        }

        /// <summary>
        /// 设置数据节点的数据。
        /// </summary>
        /// <typeparam name="T">要设置的数据类型。</typeparam>
        /// <param name="path">相对于根节点的查找路径。</param>
        /// <param name="data">要设置的数据。</param>
        public void SetData<T>(string path, T data) where T : Variable
        {
            m_DataNodeManager.SetData(path, data);
        }

        /// <summary>
        /// 设置数据节点的数据。
        /// </summary>
        /// <param name="path">相对于根节点的查找路径。</param>
        /// <param name="data">要设置的数据。</param>
        public void SetData(string path, Variable data)
        {
            m_DataNodeManager.SetData(path, data);
        }

        /// <summary>
        /// 设置数据节点的数据。
        /// </summary>
        /// <typeparam name="T">要设置的数据类型。</typeparam>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="data">要设置的数据。</param>
        /// <param name="node">查找起始节点。</param>
        public void SetData<T>(string path, T data, IDataNode node) where T : Variable
        {
            m_DataNodeManager.SetData(path, data, node);
        }

        /// <summary>
        /// 设置数据节点的数据。
        /// </summary>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="data">要设置的数据。</param>
        /// <param name="node">查找起始节点。</param>
        public void SetData(string path, Variable data, IDataNode node)
        {
            m_DataNodeManager.SetData(path, data, node);
        }

        /// <summary>
        /// 获取数据节点。
        /// </summary>
        /// <param name="path">相对于根节点的查找路径。</param>
        /// <returns>指定位置的数据节点，如果没有找到则返回空。</returns>
        public IDataNode GetNode(string path)
        {
            return m_DataNodeManager.GetNode(path);
        }

        /// <summary>
        /// 获取数据节点。
        /// </summary>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="node">查找起始节点。</param>
        /// <returns>指定位置的数据节点，如果没有找到则返回空。</returns>
        public IDataNode GetNode(string path, IDataNode node)
        {
            return m_DataNodeManager.GetNode(path, node);
        }

        /// <summary>
        /// 获取或增加数据节点。
        /// 如果指定路径的节点不存在，会自动创建。
        /// </summary>
        /// <param name="path">相对于根节点的查找路径。</param>
        /// <returns>指定位置的数据节点。</returns>
        public IDataNode GetOrAddNode(string path)
        {
            return m_DataNodeManager.GetOrAddNode(path);
        }

        /// <summary>
        /// 获取或增加数据节点。
        /// </summary>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="node">查找起始节点。</param>
        /// <returns>指定位置的数据节点。</returns>
        public IDataNode GetOrAddNode(string path, IDataNode node)
        {
            return m_DataNodeManager.GetOrAddNode(path, node);
        }

        /// <summary>
        /// 移除数据节点。
        /// </summary>
        /// <param name="path">相对于根节点的查找路径。</param>
        public void RemoveNode(string path)
        {
            m_DataNodeManager.RemoveNode(path);
        }

        /// <summary>
        /// 移除数据节点。
        /// </summary>
        /// <param name="path">相对于 node 的查找路径。</param>
        /// <param name="node">查找起始节点。</param>
        public void RemoveNode(string path, IDataNode node)
        {
            m_DataNodeManager.RemoveNode(path, node);
        }

        /// <summary>
        /// 移除所有数据节点。
        /// </summary>
        public void Clear()
        {
            m_DataNodeManager.Clear();
        }
    }
}
