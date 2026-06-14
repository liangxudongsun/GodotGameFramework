//------------------------------------------------------------
// ResourceExtension - 资源组件便捷扩展方法
// 提供 ResourceComponent 的异步加载便捷方法。
//
// 使用方式：
//   var tex = await GF.Resource.LoadAssetAsync<Texture2D>("res://Textures/icon.png");
//   var scene = await GF.Resource.LoadAssetAsync<PackedScene>("res://Scenes/Enemy.tscn");
//
// 对应 UGF 参考项目中的 ResourceExtension + AwaitExtension 中的 LoadAssetAwait。
// GGF 版本使用标准 C# Task，不依赖 UniTask 等第三方库。
//------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace GodotGameFramework
{
    /// <summary>
    /// 资源组件扩展方法。
    ///
    /// 提供 ResourceComponent 的异步加载便捷方法，
    /// 将回调式的 LoadAssetAsync 封装为 Task&lt;T&gt;，支持 async/await。
    ///
    /// 实现原理：
    /// 使用 TaskCompletionSource&lt;T&gt; 包装回调式的 LoadAssetAsync，
    /// 加载成功时调用 TrySetResult，失败时调用 TrySetException。
    ///
    /// 对应 Unity 版本中游戏项目的 ResourceExtension + AwaitExtension。
    /// </summary>
    public static class ResourceExtension
    {
        /// <summary>
        /// 异步加载资源，返回 Task&lt;T&gt;。
        ///
        /// 将 ResourceComponent.LoadAssetAsync 的回调式 API 封装为
        /// 标准 C# Task，支持 async/await 语法。
        ///
        /// 使用方式：
        /// <code>
        /// try
        /// {
        ///     Texture2D tex = await GF.Resource.LoadAssetAsync&lt;Texture2D&gt;("res://Textures/icon.png");
        ///     GD.Print("Loaded: " + tex.ResourcePath);
        /// }
        /// catch (Exception ex)
        /// {
        ///     GD.PrintErr("Load failed: " + ex.Message);
        /// }
        /// </code>
        ///
        /// 注意事项：
        /// - 此方法返回的 Task 在 Godot 主线程完成（因为 ResourceComponent
        ///   的 LoadThreadedRequest 轮询在 _Process 中进行）
        /// - 加载失败时 Task 会以异常形式抛出（InvalidOperationException）
        /// - 调用方可使用 try/catch 捕获加载失败
        /// </summary>
        /// <typeparam name="T">资源类型（如 PackedScene、Texture2D、AudioStream 等）。</typeparam>
        /// <param name="resourceComponent">资源组件。</param>
        /// <param name="assetPath">资源路径（res:// 协议）。</param>
        /// <returns>加载完成的资源实例。</returns>
        public static Task<T> LoadAssetAsync<T>(this ResourceComponent resourceComponent, string assetPath)
            where T : class
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

            resourceComponent.LoadAssetAsync(
                assetPath,
                typeof(T),
                asset =>
                {
                    // 加载成功：将结果设置到 TCS
                    if (asset is T result)
                    {
                        tcs.TrySetResult(result);
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException(
                            string.Format("Loaded asset '{0}' is not of type '{1}'.", assetPath, typeof(T).Name)));
                    }
                },
                errorMsg =>
                {
                    // 加载失败：将错误设置到 TCS
                    tcs.TrySetException(new InvalidOperationException(
                        string.Format("Failed to load asset '{0}': {1}", assetPath, errorMsg ?? "Unknown error")));
                });

            return tcs.Task;
        }
    }
}
