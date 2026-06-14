//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Resource;
using Godot;
using System;
using System.Diagnostics;

namespace GodotGameFramework
{
    /// <summary>
    /// 默认资源辅助器。
    ///
    /// 实现 IResourceHelper 接口，提供 Godot 引擎下的资源辅助操作。
    /// 该辅助器是核心框架与 Godot 引擎之间的桥梁，负责：
    /// 1. 从文件系统加载数据流（LoadBytes）
    /// 2. 卸载场景（UnloadScene，预留）
    /// 3. 释放资源（Release，Godot 引擎自动管理）
    ///
    /// 对应 Unity 版本中的 DefaultResourceHelper。
    /// </summary>
    public sealed class DefaultResourceHelper : IResourceHelper
    {
        /// <summary>
        /// 从指定文件路径加载数据流。
        ///
        /// 使用 Godot 的 FileAccess API 读取文件内容为字节数组，
        /// 然后通过回调函数返回加载结果。
        ///
        /// 此方法主要用于核心框架内部加载二进制数据（如配置文件、数据表等）。
        /// </summary>
        /// <param name="fileUri">要加载的文件路径（如 "res://Data/Config.dat"）。</param>
        /// <param name="loadBytesCallbacks">加载回调函数集，包含成功和失败回调。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void LoadBytes(string fileUri, LoadBytesCallbacks loadBytesCallbacks, object userData)
        {
            if (loadBytesCallbacks == null)
            {
                throw new GameFrameworkException("Load bytes callbacks is invalid.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                if (!FileAccess.FileExists(fileUri))
                {
                    if (loadBytesCallbacks.LoadBytesFailureCallback != null)
                    {
                        loadBytesCallbacks.LoadBytesFailureCallback(fileUri,
                            Utility.Text.Format("File '{0}' does not exist.", fileUri), userData);
                    }

                    return;
                }

                using var file = FileAccess.Open(fileUri, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    if (loadBytesCallbacks.LoadBytesFailureCallback != null)
                    {
                        loadBytesCallbacks.LoadBytesFailureCallback(fileUri,
                            Utility.Text.Format("Can not open file '{0}', error: {1}.",
                                fileUri, FileAccess.GetOpenError()), userData);
                    }

                    return;
                }

                long length = (long)file.GetLength();
                byte[] bytes = file.GetBuffer(length);
                stopwatch.Stop();

                loadBytesCallbacks.LoadBytesSuccessCallback(fileUri, bytes,
                    stopwatch.ElapsedMilliseconds / 1000f, userData);
            }
            catch (Exception e)
            {
                if (loadBytesCallbacks.LoadBytesFailureCallback != null)
                {
                    loadBytesCallbacks.LoadBytesFailureCallback(fileUri,
                        Utility.Text.Format("Load bytes exception: {0}", e.Message), userData);
                }
            }
        }

        /// <summary>
        /// 卸载场景。
        ///
        /// 预留实现。Scene 系统在后续 Phase 中实现。
        /// 当前直接调用失败回调。
        /// </summary>
        /// <param name="sceneAssetName">场景资源名称。</param>
        /// <param name="unloadSceneCallbacks">卸载场景回调函数集。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void UnloadScene(string sceneAssetName, UnloadSceneCallbacks unloadSceneCallbacks,
            object userData)
        {
            if (unloadSceneCallbacks != null && unloadSceneCallbacks.UnloadSceneFailureCallback != null)
            {
                unloadSceneCallbacks.UnloadSceneFailureCallback(sceneAssetName, userData);
            }
        }

        /// <summary>
        /// 释放资源。
        ///
        /// 在 Godot 中，资源由引擎的引用计数系统自动管理，
        /// 当没有引用指向资源时，引擎会自动释放。
        ///
        /// 注意：不应在此调用 Dispose()，因为 Godot 内部可能仍持有对资源的引用
        /// （如 ResourceLoader 缓存），强制 Dispose 会导致悬挂指针异常。
        /// 让 Godot 引擎通过引用计数自动回收是最安全的做法。
        /// </summary>
        /// <param name="objectToRelease">要释放的资源对象。</param>
        public void Release(object objectToRelease)
        {
            // Godot 引擎通过引用计数自动管理资源生命周期
            // 不调用 Dispose()，避免悬挂指针异常
        }
    }
}
