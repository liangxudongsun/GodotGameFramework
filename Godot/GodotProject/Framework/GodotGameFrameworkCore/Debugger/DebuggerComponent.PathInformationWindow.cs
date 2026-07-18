using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 路径信息调试器窗口。
    /// </summary>
    private sealed class PathInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Path Information");
            Draw.BeginTable();
            Draw.DrawItem("User Data Dir (user://)", OS.GetUserDataDir());
            Draw.DrawItem("Resource Path (res://)", ProjectSettings.GlobalizePath("res://"));
            Draw.DrawItem("Executable Path", OS.GetExecutablePath());
            Draw.DrawItem("Cache Dir", OS.GetCacheDir());
            Draw.DrawItem("Config Dir", OS.GetConfigDir());
            Draw.DrawItem("Data Dir", OS.GetDataDir());
            Draw.DrawItem("System Dir (Desktop)", OS.GetSystemDir(OS.SystemDir.Desktop));
            Draw.DrawItem("System Dir (Documents)", OS.GetSystemDir(OS.SystemDir.Documents));
            Draw.DrawItem("System Dir (Downloads)", OS.GetSystemDir(OS.SystemDir.Downloads));
            Draw.EndTable();

            Draw.Space();
            Draw.Button("Open User Data Dir", () => OS.ShellOpen(OS.GetUserDataDir()));
            Draw.NewLine();
            Draw.Button("Open Resource Path", () => OS.ShellOpen(ProjectSettings.GlobalizePath("res://")));
        }
    }
}
