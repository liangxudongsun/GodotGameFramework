using GameFramework.ObjectPool;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 对象池信息调试器窗口。
    /// </summary>
    private sealed class ObjectPoolInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Object Pool Information");

            var objectPoolComponent = GF.ObjectPool;
            if (objectPoolComponent == null)
            {
                Draw.Label("Object pool component is invalid.");
                return;
            }

            ObjectPoolBase[] objectPools = objectPoolComponent.GetAllObjectPools(true);
            Draw.BeginTable();
            Draw.DrawItem("Object Pool Count", objectPools.Length.ToString());
            Draw.EndTable();

            foreach (ObjectPoolBase objectPool in objectPools)
            {
                Draw.Space();
                Draw.Title(string.IsNullOrEmpty(objectPool.FullName) ? "<Unnamed>" : objectPool.FullName);
                Draw.BeginTable();
                Draw.DrawItem("Name", string.IsNullOrEmpty(objectPool.Name) ? "<Unnamed>" : objectPool.Name);
                Draw.DrawItem("Type", objectPool.ObjectType.FullName);
                Draw.DrawItem("Count / Capacity", $"{objectPool.Count} / {(objectPool.Capacity == int.MaxValue ? "∞" : objectPool.Capacity.ToString())}");
                Draw.DrawItem("Can Release Count", objectPool.CanReleaseCount.ToString());
                Draw.DrawItem("Allow Multi Spawn", objectPool.AllowMultiSpawn.ToString());
                Draw.DrawItem("Auto Release Interval", $"{objectPool.AutoReleaseInterval:F2} s");
                Draw.DrawItem("Expire Time", objectPool.ExpireTime >= float.MaxValue ? "Never" : $"{objectPool.ExpireTime:F2} s");
                Draw.DrawItem("Priority", objectPool.Priority.ToString());
                Draw.EndTable();

                ObjectPoolBase capturedPool = objectPool;
                Draw.Button("Release", () => capturedPool.Release());
                Draw.Button("Release All Unused", () => capturedPool.ReleaseAllUnused());
                Draw.NewLine();
            }
        }
    }
}
