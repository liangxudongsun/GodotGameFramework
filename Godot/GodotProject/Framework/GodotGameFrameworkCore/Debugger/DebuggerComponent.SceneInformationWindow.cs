using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 场景信息调试器窗口。
    /// </summary>
    private sealed class SceneInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            SceneTree sceneTree = Component.GetTree();

            Draw.Title("Scene Information");
            Draw.BeginTable();
            Draw.DrawItem("Current Scene", sceneTree.CurrentScene != null ? sceneTree.CurrentScene.Name.ToString() : "<None>");
            Draw.DrawItem("Scene File", sceneTree.CurrentScene != null ? sceneTree.CurrentScene.SceneFilePath : "<None>");
            Draw.DrawItem("Tree Paused", sceneTree.Paused.ToString());
            Draw.DrawItem("FPS", $"{Component.CurrentFps:F2} ({Engine.GetFramesPerSecond():F0})");
            Draw.DrawItem("Process Time", $"{Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:F2} ms");
            Draw.DrawItem("Physics Process Time", $"{Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0:F2} ms");
            Draw.DrawItem("Object Count", Performance.GetMonitor(Performance.Monitor.ObjectCount).ToString("F0"));
            Draw.DrawItem("Node Count", Performance.GetMonitor(Performance.Monitor.ObjectNodeCount).ToString("F0"));
            Draw.DrawItem("Orphan Node Count", Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount).ToString("F0"));
            Draw.DrawItem("Resource Count", Performance.GetMonitor(Performance.Monitor.ObjectResourceCount).ToString("F0"));
            Draw.EndTable();
        }
    }
}
