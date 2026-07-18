using GameFramework.Debugger;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 可滚动调试器窗口基类（对齐 UGF ScrollableDebuggerWindowBase）。
    /// 内容写入 DebuggerDraw 上下文，由 DebuggerComponent 渲染到滚动内容区。
    /// </summary>
    public abstract class ScrollableDebuggerWindowBase : IDebuggerWindow
    {
        private const int MbSize = 1024 * 1024;
        private const long GbSize = 1024L * 1024 * 1024;

        /// <summary>
        /// 获取调试器组件（经 RegisterDebuggerWindow 注入的首个初始化参数）。
        /// </summary>
        protected DebuggerComponent Component { get; private set; }

        /// <summary>
        /// 获取绘制上下文。
        /// </summary>
        protected DebuggerDraw Draw => Component?.Draw;

        public virtual void Initialize(params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                Component = args[0] as DebuggerComponent;
            }

            Component ??= GameEntry.GetComponent<DebuggerComponent>();
        }

        public virtual void Shutdown()
        {
        }

        public virtual void OnEnter()
        {
        }

        public virtual void OnLeave()
        {
        }

        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        public void OnDraw()
        {
            if (Draw == null)
            {
                return;
            }

            OnDrawScrollableWindow();
        }

        /// <summary>
        /// 绘制窗口内容（写入 Draw 上下文）。
        /// </summary>
        protected abstract void OnDrawScrollableWindow();

        /// <summary>
        /// 字节数格式化为可读字符串。
        /// </summary>
        protected static string GetByteLengthString(long byteLength)
        {
            if (byteLength < 1024)
            {
                return $"{byteLength} B";
            }

            if (byteLength < MbSize)
            {
                return $"{byteLength / 1024f:F2} KB";
            }

            if (byteLength < GbSize)
            {
                return $"{byteLength / (float)MbSize:F2} MB";
            }

            return $"{byteLength / (float)GbSize:F2} GB";
        }
    }
}
