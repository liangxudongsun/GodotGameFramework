//------------------------------------------------------------
// NodeExtension - Godot Node 便捷扩展方法
// 提供 Node 类型的常用便捷方法，简化场景树操作。
//
// 使用方式：
//   var label = node.GetOrAddChild<Label>("MyLabel");
//   var sprite = node.GetChild<Sprite2D>();
//   var children = node.GetChildren<Control>();
//   node.RemoveAllChildren();
//
// 对应 UGF 参考项目中的 TransformExtension + UnityExtension。
// Godot 用 Node 替代 Unity 的 Transform/GameObject，
// 提供 GetOrAddChild、GetChildren<T> 等便捷方法。
//------------------------------------------------------------

using Godot;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// Godot Node 扩展方法。
    ///
    /// 提供 Node 类型的常用便捷方法，
    /// 包括获取/添加子节点、按类型查找节点、批量操作等。
    ///
    /// 对应 Unity 版本中的 TransformExtension（DoBlinkScale、FindWithTag 等）
    /// 和 UnityExtension（GetOrAddComponent 等）。
    /// </summary>
    public static class NodeExtension
    {
        /// <summary>
        /// 获取或添加指定类型的子节点。
        ///
        /// 如果指定名称的子节点已存在且类型匹配，直接返回；
        /// 如果不存在，创建新的节点并添加为子节点。
        ///
        /// 适用场景：UI 组件中确保某个子节点一定存在。
        ///
        /// <code>
        /// // 确保 Label 子节点存在
        /// Label titleLabel = GetOrAddChild&lt;Label&gt;("Title");
        /// titleLabel.Text = "Hello";
        /// </code>
        /// </summary>
        /// <typeparam name="T">节点类型（必须有无参构造函数）。</typeparam>
        /// <param name="parent">父节点。</param>
        /// <param name="name">子节点名称。</param>
        /// <returns>找到或新创建的子节点。</returns>
        public static T GetOrAddChild<T>(this Node parent, string name) where T : Node, new()
        {
            T child = parent.GetNodeOrNull<T>(name);
            if (child == null)
            {
                child = new T { Name = name };
                parent.AddChild(child);
            }

            return child;
        }

        /// <summary>
        /// 获取第一个指定类型的子节点。
        ///
        /// 遍历直接子节点，返回第一个类型匹配的节点。
        /// </summary>
        /// <typeparam name="T">节点类型。</typeparam>
        /// <param name="parent">父节点。</param>
        /// <returns>第一个匹配的子节点，未找到返回 null。</returns>
        public static T GetChild<T>(this Node parent) where T : Node
        {
            int childCount = parent.GetChildCount();
            for (int i = 0; i < childCount; i++)
            {
                if (parent.GetChild(i) is T child)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取所有指定类型的子节点列表。
        ///
        /// 遍历直接子节点，返回所有类型匹配的节点。
        /// </summary>
        /// <typeparam name="T">节点类型。</typeparam>
        /// <param name="parent">父节点。</param>
        /// <returns>匹配的子节点列表。</returns>
        public static List<T> GetChildren<T>(this Node parent) where T : Node
        {
            List<T> result = new List<T>();
            int childCount = parent.GetChildCount();
            for (int i = 0; i < childCount; i++)
            {
                if (parent.GetChild(i) is T child)
                {
                    result.Add(child);
                }
            }

            return result;
        }

        /// <summary>
        /// 按名称获取指定类型的子节点。
        ///
        /// 组合了 GetNodeOrNull 和类型转换。
        /// </summary>
        /// <typeparam name="T">节点类型。</typeparam>
        /// <param name="parent">父节点。</param>
        /// <param name="name">子节点名称。</param>
        /// <returns>匹配的子节点，未找到返回 null。</returns>
        public static T GetChildByName<T>(this Node parent, string name) where T : Node
        {
            return parent.GetNodeOrNull<T>(name);
        }

        /// <summary>
        /// 在所有子孙节点中查找第一个指定类型的节点（深度优先）。
        ///
        /// 与 GetChildren&lt;T&gt; 不同，此方法递归搜索整个子树。
        /// </summary>
        /// <typeparam name="T">节点类型。</typeparam>
        /// <param name="parent">父节点。</param>
        /// <returns>第一个匹配的子孙节点，未找到返回 null。</returns>
        public static T FindChildOfType<T>(this Node parent) where T : Node
        {
            int childCount = parent.GetChildCount();
            for (int i = 0; i < childCount; i++)
            {
                Node child = parent.GetChild(i);
                if (child is T result)
                {
                    return result;
                }

                // 递归搜索子节点的子树
                T found = child.FindChildOfType<T>();
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 在所有子孙节点中查找所有指定类型的节点（深度优先）。
        ///
        /// 与 GetChildren&lt;T&gt; 不同，此方法递归搜索整个子树。
        /// </summary>
        /// <typeparam name="T">节点类型。</typeparam>
        /// <param name="parent">父节点。</param>
        /// <returns>匹配的子孙节点列表。</returns>
        public static List<T> FindChildrenOfType<T>(this Node parent) where T : Node
        {
            List<T> result = new List<T>();
            FindChildrenOfType(parent, result);
            return result;
        }

        /// <summary>
        /// 移除所有子节点。
        ///
        /// 从后往前遍历并释放（QueueFree）所有子节点。
        /// </summary>
        /// <param name="parent">父节点。</param>
        public static void RemoveAllChildren(this Node parent)
        {
            int childCount = parent.GetChildCount();
            for (int i = childCount - 1; i >= 0; i--)
            {
                parent.GetChild(i).QueueFree();
            }
        }

        /// <summary>
        /// 获取父节点并转换为指定类型。
        ///
        /// 适用于已知父节点类型的场景（如 UIItem 获取所属 UIForm）。
        /// </summary>
        /// <typeparam name="T">父节点类型。</typeparam>
        /// <param name="node">当前节点。</param>
        /// <returns>父节点（已转换类型），无父节点或类型不匹配返回 null。</returns>
        public static T GetParent<T>(this Node node) where T : Node
        {
            return node.GetParent() as T;
        }

        /// <summary>
        /// 递归搜索所有子孙节点中指定类型的节点。
        /// </summary>
        private static void FindChildrenOfType<T>(Node parent, List<T> result) where T : Node
        {
            int childCount = parent.GetChildCount();
            for (int i = 0; i < childCount; i++)
            {
                Node child = parent.GetChild(i);
                if (child is T typedChild)
                {
                    result.Add(typedChild);
                }

                // 递归搜索
                FindChildrenOfType(child, result);
            }
        }
    }
}
