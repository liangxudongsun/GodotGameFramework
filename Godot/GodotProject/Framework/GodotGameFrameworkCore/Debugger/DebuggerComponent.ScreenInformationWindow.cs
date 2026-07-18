using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 屏幕信息调试器窗口。
    /// </summary>
    private sealed class ScreenInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Window window = Component.GetWindow();

            Draw.Title("Screen Information");
            Draw.BeginTable();
            Draw.DrawItem("Screen Count", DisplayServer.GetScreenCount().ToString());
            Draw.DrawItem("Current Screen", DisplayServer.WindowGetCurrentScreen().ToString());
            Draw.DrawItem("Screen Size", DisplayServer.ScreenGetSize().ToString());
            Draw.DrawItem("Screen DPI", DisplayServer.ScreenGetDpi().ToString());
            Draw.DrawItem("Screen Refresh Rate", $"{DisplayServer.ScreenGetRefreshRate():F2} Hz");
            Draw.DrawItem("Screen Scale", DisplayServer.ScreenGetScale().ToString("F2"));
            Draw.DrawItem("Screen Orientation", DisplayServer.ScreenGetOrientation().ToString());
            Draw.DrawItem("Window Size", DisplayServer.WindowGetSize().ToString());
            Draw.DrawItem("Window Position", DisplayServer.WindowGetPosition().ToString());
            Draw.DrawItem("Window Mode", DisplayServer.WindowGetMode().ToString());
            Draw.DrawItem("VSync Mode", DisplayServer.WindowGetVsyncMode().ToString());
            Draw.DrawItem("Touchscreen Available", DisplayServer.IsTouchscreenAvailable().ToString());

            if (window != null)
            {
                Draw.DrawItem("Viewport Size", window.GetVisibleRect().Size.ToString());
                Draw.DrawItem("Content Scale Mode", window.ContentScaleMode.ToString());
                Draw.DrawItem("Content Scale Aspect", window.ContentScaleAspect.ToString());
                Draw.DrawItem("Content Scale Size", window.ContentScaleSize.ToString());
                Draw.DrawItem("Content Scale Factor", window.ContentScaleFactor.ToString("F2"));
            }

            Draw.EndTable();
        }
    }
}
