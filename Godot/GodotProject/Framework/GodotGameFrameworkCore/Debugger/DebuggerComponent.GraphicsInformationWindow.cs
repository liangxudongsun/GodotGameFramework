using Godot;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 图形信息调试器窗口。
    /// </summary>
    private sealed class GraphicsInformationWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            Viewport viewport = Component.GetViewport();

            Draw.Title("Graphics Information");
            Draw.BeginTable();
            Draw.DrawItem("Rendering Method", RenderingServer.GetCurrentRenderingMethod());
            Draw.DrawItem("Rendering Driver", RenderingServer.GetCurrentRenderingDriverName());
            Draw.DrawItem("Adapter Name", RenderingServer.GetVideoAdapterName());
            Draw.DrawItem("Adapter Vendor", RenderingServer.GetVideoAdapterVendor());
            Draw.DrawItem("Adapter Type", RenderingServer.GetVideoAdapterType().ToString());
            Draw.DrawItem("Adapter API Version", RenderingServer.GetVideoAdapterApiVersion());

            if (viewport != null)
            {
                Draw.DrawItem("MSAA 2D", viewport.Msaa2D.ToString());
                Draw.DrawItem("MSAA 3D", viewport.Msaa3D.ToString());
                Draw.DrawItem("Screen Space AA", viewport.ScreenSpaceAA.ToString());
                Draw.DrawItem("TAA", viewport.UseTaa.ToString());
                Draw.DrawItem("Scaling 3D Mode", viewport.Scaling3DMode.ToString());
                Draw.DrawItem("Scaling 3D Scale", viewport.Scaling3DScale.ToString("F2"));
            }

            Draw.DrawItem("Objects In Frame", Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame).ToString("F0"));
            Draw.DrawItem("Primitives In Frame", Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame).ToString("F0"));
            Draw.DrawItem("Draw Calls In Frame", Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame).ToString("F0"));
            Draw.DrawItem("Video Memory", GetByteLengthString((long)Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)));
            Draw.DrawItem("Texture Memory", GetByteLengthString((long)Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed)));
            Draw.DrawItem("Buffer Memory", GetByteLengthString((long)Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed)));
            Draw.EndTable();
        }
    }
}
