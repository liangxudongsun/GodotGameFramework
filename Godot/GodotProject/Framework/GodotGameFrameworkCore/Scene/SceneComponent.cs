using GameFramework;
using GameFramework.Resource;
using GameFramework.Scene;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GodotGameFramework.Scene
{
    /// <summary>
    /// 场景组件。管理场景（PackedScene）的加载、实例化、卸载。
    /// </summary>
    public partial class SceneComponent : GameFrameworkComponent
    {
        private const int DefaultPriority = 0;
        private ISceneManager m_SceneManager;
        private EventComponent m_EventComponent;
        private IResourceManager m_ResourceManager;
        private SceneHelperBase m_SceneHelper;
        private readonly Dictionary<string, TaskCompletionSource<Node>> m_LoadingTasks = new();

        [Export] private bool m_EnableLoadSceneSuccessEvent = true;
        [Export] private bool m_EnableLoadSceneFailureEvent = true;

        [Export]
        private string m_SceneHelperTypeName = "GodotGameFramework.Scene.DefaultSceneHelper";

        public override void OnInit()
        {
            base.OnInit();

            m_SceneManager = GameFrameworkEntry.GetModule<ISceneManager>();
            if (m_SceneManager == null)
            {
                Log.Fatal("Scene manager is invalid.");
                return;
            }

            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            if (m_EventComponent == null)
            {
                Log.Fatal("Event component is invalid.");
                return;
            }
            m_SceneManager.LoadSceneSuccess += OnLoadSceneSuccess;
            m_SceneManager.LoadSceneFailure += OnLoadSceneFailure;
        }

        public override void OnEnter()
        {
            base.OnEnter();

            m_ResourceManager = GameFrameworkEntry.GetModule<IResourceManager>();
            m_SceneManager.SetResourceManager(m_ResourceManager);

            m_SceneHelper = Helper.CreateHelper(m_SceneHelperTypeName, m_SceneHelper);
            if (m_SceneHelper == null)
            {
                Log.Fatal("Can not create scene helper.");
                return;
            }
            m_SceneHelper.Name = m_SceneHelperTypeName;
            m_SceneManager.SetSceneHelper(m_SceneHelper);
            AddChild(m_SceneHelper);
        }

        public override void OnExitTree()
        {
            if (m_SceneManager != null)
            {
                m_SceneManager.LoadSceneSuccess -= OnLoadSceneSuccess;
                m_SceneManager.LoadSceneFailure -= OnLoadSceneFailure;
            }
            base.OnExitTree();
        }



        /// <summary>
        /// 场景是否已加载。
        /// </summary>
        public bool IsSceneLoaded(string assetPath) => m_SceneManager.SceneIsLoaded(assetPath);

        /// <summary>
        /// 场景是否正在加载中。
        /// </summary>
        public bool IsSceneLoading(string assetPath) => m_SceneManager.SceneIsLoading(assetPath);

        /// <summary>
        /// 获取已加载的场景实例。
        /// </summary>
        public T GetLoadedScene<T>(string assetPath) where T : Node
        {
            object instance = m_SceneManager.GetSceneInstance(assetPath);
            return instance as T;
        }


        public void LoadScene(string sceneAssetName, int priority, object userData) => m_SceneManager.LoadScene(sceneAssetName, priority, userData);

        public void LoadScene(string sceneAssetName, int priority) => m_SceneManager.LoadScene(sceneAssetName, priority, null);
        public void LoadScene(string sceneAssetName) => m_SceneManager.LoadScene(sceneAssetName, DefaultPriority, null);
        /// <summary>
        /// 异步加载场景
        /// </summary>
        private Task<Node> LoadSceneAsyncInternal(string sceneAssetName, int priority, object userData)
        {
            if (string.IsNullOrEmpty(sceneAssetName))
            {
                Log.Error("Scene asset path is invalid.");
                return Task.FromResult<Node>(null);
            }

            var tcs = new TaskCompletionSource<Node>();
            m_SceneManager.LoadScene(sceneAssetName, priority, userData);
            m_LoadingTasks.Add(sceneAssetName, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        public Task<Node> LoadSceneAsync(string sceneAssetName, int priority, object userData)
        {
            return LoadSceneAsyncInternal(sceneAssetName, priority, userData);
        }

        public Task<Node> LoadSceneAsync(string sceneAssetName, int priority)
        {
            return LoadSceneAsyncInternal(sceneAssetName, priority, null);
        }

        public Task<Node> LoadSceneAsync(string sceneAssetName)
        {
            return LoadSceneAsyncInternal(sceneAssetName, DefaultPriority, null);
        }

        /// <summary>
        /// 卸载场景。SceneManager 卸载时会通过 Helper 释放节点。
        /// </summary>
        public void UnloadScene(string assetPath)
        {
            m_SceneManager.UnloadScene(assetPath);
        }

        /// <summary>
        /// 卸载所有已加载场景。
        /// </summary>
        public void UnloadAllScenes()
        {
            string[] names = m_SceneManager.GetLoadedSceneAssetNames();
            foreach (string assetPath in names)
            {
                UnloadScene(assetPath);
            }
            m_LoadingTasks.Clear();
        }

        private void OnLoadSceneSuccess(object sender, GameFramework.Scene.LoadSceneSuccessEventArgs e)
        {
            string assetPath = e.SceneAssetName;
            Node instance = e.SceneInstance as Node;

            // 将实例添加到场景树
            if (instance != null)
            {
                instance.Name = GetSceneName(assetPath);
                AddChild(instance);
            }

            // 完成异步等待任务
            if (m_LoadingTasks.TryGetValue(assetPath, out TaskCompletionSource<Node> tcs))
            {
                if (instance != null)
                    tcs.TrySetResult(instance);
                else
                    tcs.TrySetException(new Exception($"Scene instance for '{assetPath}' is null."));
                m_LoadingTasks.Remove(assetPath);
            }

            // 触发 Godot 层事件
            if (m_EnableLoadSceneSuccessEvent && instance != null)
                m_EventComponent.Fire(this, Scene.LoadSceneSuccessEventArgs.Create(assetPath, instance));
        }

        private void OnLoadSceneFailure(object sender, GameFramework.Scene.LoadSceneFailureEventArgs e)
        {
            Log.Warning("Load scene failure, asset '{0}', msg '{1}'.", e.SceneAssetName, e.ErrorMessage);

            // 完成异步等待任务
            if (m_LoadingTasks.TryGetValue(e.SceneAssetName, out TaskCompletionSource<Node> tcs))
            {
                tcs.TrySetException(new Exception(e.ErrorMessage));
                m_LoadingTasks.Remove(e.SceneAssetName);
            }

            // 触发 Godot 层事件
            if (m_EnableLoadSceneFailureEvent)
                m_EventComponent.Fire(this, Scene.LoadSceneFailureEventArgs.Create(e.SceneAssetName, e.ErrorMessage));
        }

        /// <summary>
        /// 从资源路径提取场景名称（如 "res://TheGame/Scenes/Map.tscn" → "Map"）。
        /// </summary>
        public static string GetSceneName(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "Scene";

            int nameStart = assetPath.LastIndexOf('/') + 1;
            int nameEnd = assetPath.LastIndexOf('.');
            if (nameEnd < 0 || nameEnd <= nameStart)
                nameEnd = assetPath.Length;

            return assetPath.Substring(nameStart, nameEnd - nameStart);
        }
    }
}
