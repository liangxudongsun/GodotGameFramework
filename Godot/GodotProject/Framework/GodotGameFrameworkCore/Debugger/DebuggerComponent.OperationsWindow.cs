using GameFramework.ObjectPool;
using System;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 运行时操作调试器窗口。
    /// </summary>
    private sealed class OperationsWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Operations");
            Draw.Button("GC Collect", () => GC.Collect());
            Draw.Button("Release All Unused Object Pools", ReleaseAllUnusedObjectPools);
            Draw.NewLine();
            Draw.Space();

            Draw.Title("Shutdown");
            Draw.Label("None: 仅关闭游戏框架；Restart: 重启主场景；Quit: 退出游戏。");
            Draw.Button("Shutdown (None)", () => GameEntry.Shutdown(ShutdownType.None));
            Draw.Button("Shutdown (Restart)", () => GameEntry.Shutdown(ShutdownType.Restart));
            Draw.Button("Shutdown (Quit)", () => GameEntry.Shutdown(ShutdownType.Quit));
            Draw.NewLine();
        }

        private static void ReleaseAllUnusedObjectPools()
        {
            var objectPoolComponent = GF.ObjectPool;
            if (objectPoolComponent == null)
            {
                return;
            }

            foreach (ObjectPoolBase objectPool in objectPoolComponent.GetAllObjectPools())
            {
                objectPool.ReleaseAllUnused();
            }
        }
    }
}
