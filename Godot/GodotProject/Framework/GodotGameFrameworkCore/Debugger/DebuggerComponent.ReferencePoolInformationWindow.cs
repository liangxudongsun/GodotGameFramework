using GameFramework;
using System;
using System.Collections.Generic;

namespace GodotGameFramework.Debugger;

public sealed partial class DebuggerComponent
{
    /// <summary>
    /// 引用池信息调试器窗口。
    /// </summary>
    private sealed class ReferencePoolInformationWindow : ScrollableDebuggerWindowBase
    {
        private bool m_ShowFullClassName;

        protected override void OnDrawScrollableWindow()
        {
            Draw.Title("Reference Pool Information");
            Draw.Toggle(ReferencePool.EnableStrictCheck, "Enable Strict Check", value => ReferencePool.EnableStrictCheck = value);
            Draw.Toggle(m_ShowFullClassName, "Show Full Class Name", value => m_ShowFullClassName = value);
            Draw.NewLine();

            ReferencePoolInfo[] referencePoolInfos = ReferencePool.GetAllReferencePoolInfos();
            Draw.BeginTable();
            Draw.DrawItem("Reference Pool Count", referencePoolInfos.Length.ToString());
            Draw.EndTable();
            Draw.Space();

            if (referencePoolInfos.Length <= 0)
            {
                return;
            }

            Array.Sort(referencePoolInfos, Comparison);

            // 7 列表格：类名 | 未使用 | 使用中 | 获取 | 归还 | 增加 | 移除
            Draw.AppendRaw("[table=7]");
            AppendHeaderCell("Class Name");
            AppendHeaderCell("Unused");
            AppendHeaderCell("Using");
            AppendHeaderCell("Acquire");
            AppendHeaderCell("Release");
            AppendHeaderCell("Add");
            AppendHeaderCell("Remove");
            foreach (ReferencePoolInfo info in referencePoolInfos)
            {
                AppendCell(m_ShowFullClassName ? info.Type.FullName : info.Type.Name);
                AppendCell(info.UnusedReferenceCount.ToString());
                AppendCell(info.UsingReferenceCount.ToString());
                AppendCell(info.AcquireReferenceCount.ToString());
                AppendCell(info.ReleaseReferenceCount.ToString());
                AppendCell(info.AddReferenceCount.ToString());
                AppendCell(info.RemoveReferenceCount.ToString());
            }

            Draw.AppendRaw("[/table]\n");
        }

        private void AppendHeaderCell(string text)
        {
            Draw.AppendRaw($"[cell][b][color=#8FBCE6]{DebuggerDraw.Esc(text)}[/color][/b]  [/cell]");
        }

        private void AppendCell(string text)
        {
            Draw.AppendRaw($"[cell]{DebuggerDraw.Esc(text)}  [/cell]");
        }

        private int Comparison(ReferencePoolInfo a, ReferencePoolInfo b)
        {
            return m_ShowFullClassName
                ? string.Compare(a.Type.FullName, b.Type.FullName, StringComparison.Ordinal)
                : string.Compare(a.Type.Name, b.Type.Name, StringComparison.Ordinal);
        }
    }
}
