using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 时间信息调试器窗口。
    /// </summary>
    private sealed class TimeInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Time Information");
            Draw.BeginTable();
            Draw.DrawItem("Time Scale", Engine.TimeScale.ToString("F2"));
            Draw.DrawItem("Process Frames", Engine.GetProcessFrames().ToString());
            Draw.DrawItem("Physics Frames", Engine.GetPhysicsFrames().ToString());
            Draw.DrawItem("Max FPS", Engine.MaxFps == 0 ? "Unlimited" : Engine.MaxFps.ToString());
            Draw.DrawItem("Physics Ticks Per Second", Engine.PhysicsTicksPerSecond.ToString());
            Draw.DrawItem("Ticks", $"{Time.GetTicksMsec()} ms / {Time.GetTicksUsec()} us");
            Draw.DrawItem("Date Time (Local)", Time.GetDatetimeStringFromSystem(false, true));
            Draw.DrawItem("Date Time (UTC)", Time.GetDatetimeStringFromSystem(true, true));

            var timeZone = Time.GetTimeZoneFromSystem();
            if (timeZone.TryGetValue("name", out var zoneName) && timeZone.TryGetValue("bias", out var zoneBias))
            {
                Draw.DrawItem("Time Zone", $"{zoneName.AsString()} (bias {zoneBias.AsInt32()} min)");
            }

            Draw.DrawItem("Unix Time", Time.GetUnixTimeFromSystem().ToString("F0"));
            Draw.EndTable();
        }
    }
}
