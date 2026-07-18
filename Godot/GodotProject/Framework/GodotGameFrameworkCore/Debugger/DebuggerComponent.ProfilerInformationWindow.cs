using GameFramework;
using Godot;
using System;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 性能概况调试器窗口。
    /// </summary>
    private sealed class ProfilerInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Memory");
            Draw.BeginTable();
            Draw.DrawItem("Static Memory", GetByteLengthString((long)OS.GetStaticMemoryUsage()));
            Draw.DrawItem("Static Memory Peak", GetByteLengthString((long)OS.GetStaticMemoryPeakUsage()));
            Draw.DrawItem("Video Memory", GetByteLengthString((long)Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)));
            Draw.DrawItem("Texture Memory", GetByteLengthString((long)Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed)));
            Draw.DrawItem("Buffer Memory", GetByteLengthString((long)Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed)));
            Draw.EndTable();

            Draw.Space();
            Draw.Title(".NET GC");
            Draw.BeginTable();
            Draw.DrawItem("GC Total Memory", GetByteLengthString(GC.GetTotalMemory(false)));
            Draw.DrawItem("GC Total Allocated", GetByteLengthString(GC.GetTotalAllocatedBytes()));
            Draw.DrawItem("GC Collection Count", $"Gen0: {GC.CollectionCount(0)}  Gen1: {GC.CollectionCount(1)}  Gen2: {GC.CollectionCount(2)}");
            Draw.EndTable();

            Draw.Space();
            Draw.Title("Objects");
            Draw.BeginTable();
            Draw.DrawItem("Object Count", Performance.GetMonitor(Performance.Monitor.ObjectCount).ToString("F0"));
            Draw.DrawItem("Node Count", Performance.GetMonitor(Performance.Monitor.ObjectNodeCount).ToString("F0"));
            Draw.DrawItem("Orphan Node Count", Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount).ToString("F0"));
            Draw.DrawItem("Resource Count", Performance.GetMonitor(Performance.Monitor.ObjectResourceCount).ToString("F0"));
            Draw.EndTable();

            Draw.Space();
            Draw.Title("Game Framework");
            Draw.BeginTable();
            Draw.DrawItem("Reference Pool Count", ReferencePool.Count.ToString());
            try
            {
                var eventComponent = GF.Event;
                if (eventComponent != null)
                {
                    Draw.DrawItem("Event Handler Count", eventComponent.EventHandlerCount.ToString());
                    Draw.DrawItem("Event Pending Count", eventComponent.EventCount.ToString());
                }

                var objectPoolComponent = GF.ObjectPool;
                if (objectPoolComponent != null)
                {
                    Draw.DrawItem("Object Pool Count", objectPoolComponent.Count.ToString());
                }
            }
            catch (Exception)
            {
                // 组件不可用时忽略
            }

            Draw.EndTable();
        }
    }
}
