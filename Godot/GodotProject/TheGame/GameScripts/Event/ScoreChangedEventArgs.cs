using GameFramework;
using GameFramework.Event;

/// <summary>
/// 分数变化事件参数。
/// </summary>
public class ScoreChangedEventArgs : GameEventArgs
{
    public static int EventId => typeof(ScoreChangedEventArgs).GetHashCode();
    public override int Id => EventId;
    public int ScoreDelta { get; private set; }

    public override void Clear()
    {
        ScoreDelta = 0;
    }
    public static ScoreChangedEventArgs Create(int scoreDelta)
    {
        ScoreChangedEventArgs e = ReferencePool.Acquire<ScoreChangedEventArgs>();
        e.ScoreDelta = scoreDelta;
        return e;
    }
}
