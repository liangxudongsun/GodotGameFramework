//------------------------------------------------------------
// 测试用对象池对象。
// 继承 ObjectBase，用于验证 ObjectPoolComponent 的功能。
//
// 【独立示例】本文件未在测试游戏流程中实际加载和使用。
// 作为 ObjectBase 实现的参考模板，开发者可参照此模式创建自定义对象池。
// 如需使用，参考模式：
//   var pool = GF.ObjectPool.CreateSingleSpawnObjectPool<TestPoolObject>("TestPool");
//   var obj = TestPoolObject.Create("item1", new TestPoolItem(1, "Test"));
//   pool.Register(obj, true);
//   var spawned = pool.Spawn("item1");
//------------------------------------------------------------

using GameFramework;
using GameFramework.ObjectPool;
using Godot;
using GodotGameFramework;

/// <summary>
/// 测试对象池包装项。
/// 作为 ObjectPool 中包装的实际数据对象。
/// </summary>
public class TestPoolItem
{
    /// <summary>
    /// 道具编号。
    /// </summary>
    public int Id;

    /// <summary>
    /// 道具名称。
    /// </summary>
    public string Name;

    public TestPoolItem(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>
/// 测试用对象池对象。
///
/// 继承 ObjectBase，包装 TestPoolItem 用于对象池管理。
/// 演示了如何实现 ObjectBase：
/// 1. 静态 Create 工厂方法（方便创建实例）
/// 2. Initialize 初始化（设置名称和被包装对象）
/// 3. Release 释放（清理被包装对象的状态）
/// 4. OnSpawn/OnUnspawn 生命周期回调
///
/// 对象池的工作流程：
/// Register → Spawn（获取）→ 使用 → Unspawn（归还）→ 可再次 Spawn
/// </summary>
public class TestPoolObject : ObjectBase
{
    /// <summary>
    /// 无参构造函数（必须的，用于引用池 ReferencePool）。
    /// </summary>
    public TestPoolObject()
    {
    }

    /// <summary>
    /// 静态工厂方法。
    /// 创建一个新的 TestPoolObject 实例并初始化。
    /// </summary>
    /// <param name="name">对象名称（用于在池中查找）。</param>
    /// <param name="item">被包装的道具数据。</param>
    /// <returns>初始化后的 TestPoolObject 实例。</returns>
    public static TestPoolObject Create(string name, TestPoolItem item)
    {
        TestPoolObject obj = ReferencePool.Acquire<TestPoolObject>();
        obj.Initialize(name, item);
        return obj;
    }

    /// <summary>
    /// 获取被包装的道具数据。
    /// </summary>
    public TestPoolItem Item => (TestPoolItem)Target;

    /// <summary>
    /// 对象被获取（Spawn）时调用。
    /// </summary>
    protected internal override void OnSpawn()
    {
        base.OnSpawn();
        GD.Print($"    → OnSpawn: {Name} (Item: {Item?.Name})");
    }

    /// <summary>
    /// 对象被归还（Unspawn）时调用。
    /// </summary>
    protected internal override void OnUnspawn()
    {
        base.OnUnspawn();
        GD.Print($"    → OnUnspawn: {Name} (Item: {Item?.Name})");
    }

    /// <summary>
    /// 释放对象。
    /// 当对象被从池中永久移除时调用。
    /// </summary>
    /// <param name="isShutdown">是否是关闭对象池时触发。</param>
    protected internal override void Release(bool isShutdown)
    {
        GD.Print($"    → Release: {Name}, isShutdown={isShutdown}");
    }
}
