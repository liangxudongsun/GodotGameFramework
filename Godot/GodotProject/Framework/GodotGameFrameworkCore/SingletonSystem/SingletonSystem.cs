using GameFramework;
using Godot;
using System;
using System.Collections.Generic;
namespace GodotGameFrameworkCore.SingletonSystem
{
    public interface ISingleton
    {
        /// <summary>
        /// 激活接口，通常用于在某个时机手动实例化
        /// </summary>
        void Active();

        /// <summary>
        /// 释放接口
        /// </summary>
        void Release();
    }

    public interface IUpdate
    {
        /// <summary>
        /// 游戏框架模块轮询。
        /// </summary>
        void OnUpdate(double delta);
    }

    public interface IFixedUpdate
    {
        /// <summary>
        /// 游戏框架模块轮询。
        /// </summary>
        void OnFixedUpdate(double delta);
    }

    public static partial class SingletonSystem
    {
        private static IUpdateDriver _updateDriver;

        /// <summary>
        /// 设置 Update 驱动器，由 Godot 侧的 Node 在 _Ready 时调用以注入驱动。
        /// </summary>
        public static void SetUpdateDriver(IUpdateDriver driver)
        {
            _updateDriver = driver;
        }

        private static readonly List<ISingleton> _singletons = new List<ISingleton>();
        private static readonly List<IUpdate> _updates = new List<IUpdate>();
        private static readonly List<IFixedUpdate> _fixedUpdates = new List<IFixedUpdate>();

        private static readonly Dictionary<string, Node> _nodes = new Dictionary<string, Node>();

        public static void Retain(ISingleton singleton)
        {
            CheckInit();

            _singletons.Add(singleton);

            BuildLifeCycle(singleton);
        }

        public static void Retain(Node node, object singleton)
        {
            CheckInit();

            if (_nodes.TryAdd(node.Name, node))
            {
                BuildLifeCycle(singleton);
            }
        }

        private static void BuildLifeCycle(object singleton)
        {
            Type iUpdate = typeof(IUpdate);
            bool needUpdate = iUpdate.IsInstanceOfType(singleton);
            if (needUpdate && singleton is IUpdate update)
            {
                _updates.Add(update);
            }

            Type iFixedUpdate = typeof(IFixedUpdate);
            bool needFixedUpdate = iFixedUpdate.IsInstanceOfType(singleton);
            if (needFixedUpdate && singleton is IFixedUpdate fixedUpdate)
            {
                _fixedUpdates.Add(fixedUpdate);
            }
        }

        public static void Release(Node go, object singleton)
        {
            if (_nodes != null && _nodes.ContainsKey(go.Name))
            {
                _nodes.Remove(go.Name);
                go.QueueFree();
                ReleaseLifeCycle(singleton);
            }
        }

        public static void Release(ISingleton singleton)
        {
            if (_singletons != null && _singletons.Contains(singleton))
            {
                _singletons.Remove(singleton);
                ReleaseLifeCycle(singleton);
            }
        }

        private static void ReleaseLifeCycle(object singleton)
        {
            Type iUpdate = typeof(IUpdate);
            bool needUpdate = iUpdate.IsInstanceOfType(singleton);
            if (needUpdate && singleton is IUpdate update)
            {
                if (_updates.Contains(update))
                {
                    _updates.Remove(update);
                }
            }

            Type iFixedUpdate = typeof(IFixedUpdate);
            bool needFixedUpdate = iFixedUpdate.IsInstanceOfType(singleton);
            if (needFixedUpdate && singleton is IFixedUpdate fixedUpdate)
            {
                if (_fixedUpdates.Contains(fixedUpdate))
                {
                    _fixedUpdates.Remove(fixedUpdate);
                }
            }
        }

        public static void Release()
        {
            if (_nodes != null)
            {
                foreach (var item in _nodes)
                {
                    item.Value.QueueFree();
                }

                _nodes.Clear();
            }

            if (_singletons != null)
            {
                for (int i = _singletons.Count - 1; i >= 0; i--)
                {
                    _singletons[i].Release();
                }

                _singletons.Clear();
            }

        }

        public static Node GetNode(string name)
        {
            Node go = null;
            if (_nodes != null)
            {
                _nodes.TryGetValue(name, out go);
            }

            return go;
        }

        internal static bool ContainsKey(string name)
        {
            if (_nodes != null)
            {
                return _nodes.ContainsKey(name);
            }

            return false;
        }


        internal static ISingleton GetSingleton(string name)
        {
            for (int i = 0; i < _singletons.Count; ++i)
            {
                if (_singletons[i].ToString() == name)
                {
                    return _singletons[i];
                }
            }

            return null;
        }

        #region 生命周期

        private static bool _isInit = false;

        private static void CheckInit()
        {
            if (_isInit == true)
            {
                return;
            }

            _isInit = true;

            if (_updateDriver == null)
            {
                _updateDriver = GameFrameworkEntry.GetModule<IUpdateDriver>();
            }

            _updateDriver.AddUpdateListener(OnUpdate);
            _updateDriver.AddFixedUpdateListener(OnFixedUpdate);
        }

        private static void OnUpdate(double delta)
        {
            foreach (var update in _updates)
            {
                update.OnUpdate(delta);
            }
        }

        private static void OnFixedUpdate(double delta)
        {
            foreach (var fixedUpdate in _fixedUpdates)
            {
                fixedUpdate.OnFixedUpdate(delta);
            }
        }
        #endregion
    }
}
