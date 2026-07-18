using Godot;
using System.Runtime.InteropServices;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 环境信息调试器窗口。
    /// </summary>
    private sealed class EnvironmentInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Environment Information");
            Draw.BeginTable();
            Draw.DrawItem("Godot Version", Engine.GetVersionInfo()["string"].AsString());
            Draw.DrawItem(".NET Runtime", RuntimeInformation.FrameworkDescription);
            Draw.DrawItem("CLR Version", System.Environment.Version.ToString());
            Draw.DrawItem("OS Architecture", RuntimeInformation.OSArchitecture.ToString());
            Draw.DrawItem("Process Architecture", RuntimeInformation.ProcessArchitecture.ToString());
            Draw.DrawItem("Debug Build", OS.IsDebugBuild().ToString());
            Draw.DrawItem("Editor Hint", Engine.IsEditorHint().ToString());
            Draw.DrawItem("Project Name", ProjectSettings.GetSetting("application/config/name", string.Empty).AsString());
            Draw.DrawItem("Project Version", ProjectSettings.GetSetting("application/config/version", string.Empty).AsString());
            Draw.DrawItem("Main Scene", ProjectSettings.GetSetting("application/run/main_scene", string.Empty).AsString());
            Draw.DrawItem("Executable Path", OS.GetExecutablePath());
            Draw.DrawItem("Command Line Args", string.Join(" ", OS.GetCmdlineArgs()));
            Draw.DrawItem("Feature: Editor", OS.HasFeature("editor").ToString());
            Draw.DrawItem("Feature: Template", OS.HasFeature("template").ToString());
            Draw.EndTable();
        }
    }
}
