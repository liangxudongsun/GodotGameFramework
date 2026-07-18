using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 系统信息调试器窗口。
    /// </summary>
    private sealed class SystemInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("System Information");
            Draw.BeginTable();
            Draw.DrawItem("Operating System", $"{OS.GetName()} {OS.GetVersion()}");
            Draw.DrawItem("Distribution", OS.GetDistributionName());
            Draw.DrawItem("Model Name", OS.GetModelName());
            Draw.DrawItem("Processor", OS.GetProcessorName());
            Draw.DrawItem("Processor Count", OS.GetProcessorCount().ToString());

            var memoryInfo = OS.GetMemoryInfo();
            if (memoryInfo.TryGetValue("physical", out var physical))
            {
                Draw.DrawItem("Physical Memory", GetByteLengthString(physical.AsInt64()));
            }

            if (memoryInfo.TryGetValue("free", out var free))
            {
                Draw.DrawItem("Free Memory", GetByteLengthString(free.AsInt64()));
            }

            if (memoryInfo.TryGetValue("available", out var available))
            {
                Draw.DrawItem("Available Memory", GetByteLengthString(available.AsInt64()));
            }

            Draw.DrawItem("Static Memory", GetByteLengthString((long)OS.GetStaticMemoryUsage()));
            Draw.DrawItem("Static Memory Peak", GetByteLengthString((long)OS.GetStaticMemoryPeakUsage()));
            Draw.DrawItem("Locale", OS.GetLocale());
            Draw.DrawItem("Locale Language", OS.GetLocaleLanguage());
            Draw.DrawItem("Unique ID", OS.GetUniqueId());
            Draw.DrawItem("Process ID", OS.GetProcessId().ToString());
            Draw.DrawItem("Sandboxed", OS.IsSandboxed().ToString());
            Draw.DrawItem("Low Processor Usage Mode", OS.LowProcessorUsageMode.ToString());
            Draw.EndTable();
        }
    }
}
