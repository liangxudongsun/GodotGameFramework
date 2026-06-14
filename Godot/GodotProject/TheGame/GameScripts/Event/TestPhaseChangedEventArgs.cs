//------------------------------------------------------------
// 测试用事件：流程切换事件
// 当流程发生切换时触发此事件，用于验证事件系统是否正常工作
//------------------------------------------------------------

using GameFramework;
using GameFramework.Event;

/// <summary>
/// 流程切换事件参数。
///
/// 当一个流程切换到另一个流程时，会触发此事件。
/// 这个事件通过 ReferencePool 进行对象复用，避免频繁的 GC。
///
/// 使用方式：
/// <code>
/// // 触发事件
/// TestPhaseChangedEventArgs e = TestPhaseChangedEventArgs.Create("Menu", "Game");
/// eventComponent.Fire(this, e);
///
/// // 订阅事件
/// eventComponent.Subscribe(TestPhaseChangedEventArgs.EventId, OnPhaseChanged);
/// </code>
/// </summary>
public class TestPhaseChangedEventArgs : GameEventArgs
{
    /// <summary>
    /// 事件编号。
    /// 每种事件需要一个唯一 ID，这里用简单的数字即可。
    /// </summary>
    public const int EventId = 10001;

    /// <summary>
    /// 获取类型编号（实现 BaseEventArgs 的抽象属性）。
    /// </summary>
    public override int Id => EventId;

    /// <summary>
    /// 从哪个流程切换出来（上一个流程名称）。
    /// </summary>
    public string FromProcedure { get; private set; }

    /// <summary>
    /// 切换到哪个流程（新流程名称）。
    /// </summary>
    public string ToProcedure { get; private set; }

    /// <summary>
    /// 清理引用（IReference 接口实现）。
    /// 事件处理完成后，框架会自动调用此方法回收对象。
    /// </summary>
    public override void Clear()
    {
        FromProcedure = null;
        ToProcedure = null;
    }

    /// <summary>
    /// 创建事件参数实例。
    ///
    /// 使用静态工厂方法 + ReferencePool 来复用对象，
    /// 这是 UGF/GGF 框架中事件系统的标准模式。
    /// </summary>
    /// <param name="fromProcedure">来源流程名</param>
    /// <param name="toProcedure">目标流程名</param>
    /// <returns>事件参数实例（可能从对象池复用）</returns>
    public static TestPhaseChangedEventArgs Create(string fromProcedure, string toProcedure)
    {
        // 从引用池获取实例（如果有），否则新建一个
        TestPhaseChangedEventArgs e = ReferencePool.Acquire<TestPhaseChangedEventArgs>();
        e.FromProcedure = fromProcedure;
        e.ToProcedure = toProcedure;
        return e;
    }
}