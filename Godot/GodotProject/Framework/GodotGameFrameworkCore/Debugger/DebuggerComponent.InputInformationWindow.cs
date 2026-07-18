using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 输入信息调试器窗口。
    /// </summary>
    private sealed class InputInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Input Information");
            Draw.BeginTable();
            Draw.DrawItem("Mouse Position (Viewport)", Component.GetViewport().GetMousePosition().ToString());
            Draw.DrawItem("Mouse Position (Screen)", DisplayServer.MouseGetPosition().ToString());
            Draw.DrawItem("Mouse Mode", Input.MouseMode.ToString());
            Draw.DrawItem("Mouse Button Mask", Input.GetMouseButtonMask().ToString());
            Draw.DrawItem("Use Accumulated Input", Input.UseAccumulatedInput.ToString());
            Draw.DrawItem("Accelerometer", Input.GetAccelerometer().ToString());
            Draw.DrawItem("Gravity", Input.GetGravity().ToString());
            Draw.DrawItem("Gyroscope", Input.GetGyroscope().ToString());
            Draw.DrawItem("Magnetometer", Input.GetMagnetometer().ToString());
            Draw.EndTable();

            Draw.Space();
            Draw.Title("Connected Joypads");
            var joypads = Input.GetConnectedJoypads();
            if (joypads.Count == 0)
            {
                Draw.Label("<None>");
            }
            else
            {
                Draw.BeginTable();
                foreach (int device in joypads)
                {
                    Draw.DrawItem($"Joypad {device}", $"{Input.GetJoyName(device)} ({Input.GetJoyGuid(device)})");
                }

                Draw.EndTable();
            }
        }
    }
}
