using Godot;
using System;
using System.IO;
using System.Text.RegularExpressions;

[Tool]
public partial class GameFrameworkTopMenu : EditorPlugin
{
    public const string MenuName = "GameFramework";
    private const string DefineConstantsPattern = @"<DefineConstants>.*?</DefineConstants>";

    private PopupMenu m_Popup;

    private static readonly (string Label, string Define)[] LogLevels = new[]
    {
        ("Disable All Logs",              ""),
        ("Enable All Logs",               "ENABLE_LOG"),
        ("Enable Debug And Above Logs",   "ENABLE_LOG;ENABLE_DEBUG_AND_ABOVE_LOG"),
        ("Enable Info And Above Logs",    "ENABLE_LOG;ENABLE_INFO_AND_ABOVE_LOG"),
        ("Enable Warning And Above Logs", "ENABLE_LOG;ENABLE_WARNING_AND_ABOVE_LOG"),
        ("Enable Error And Above Logs",   "ENABLE_LOG;ENABLE_ERROR_AND_ABOVE_LOG"),
        ("Enable Fatal And Above Logs",   "ENABLE_LOG;ENABLE_FATAL_AND_ABOVE_LOG"),
    };

    public override void _EnterTree()
    {
        m_Popup = new PopupMenu();
        m_Popup.Name = "GameFrameworkPopup";

        for (int i = 0; i < LogLevels.Length; i++)
        {
            m_Popup.AddItem(LogLevels[i].Label, i);
        }
        m_Popup.AddSeparator();
        m_Popup.AddItem("About Game Framework...", LogLevels.Length);

        m_Popup.IdPressed += OnPopupIdPressed;

        AddToolSubmenuItem(MenuName, m_Popup);

        GD.Print("[GameFramework] Plugin loaded.");
    }

    public override void _ExitTree()
    {
        // 先释放 PopupMenu，再移除工具栏菜单项
        // 顺序很重要：避免在 RemoveToolMenuItem 后信号连接已被清理导致 disconnect 报错
        if (m_Popup != null)
        {
            m_Popup.IdPressed -= OnPopupIdPressed;
            if (m_Popup.GetParent() != null)
            {
                m_Popup.GetParent().RemoveChild(m_Popup);
            }
            m_Popup.QueueFree();
            m_Popup = null;
        }

        RemoveToolMenuItem(MenuName);

        GD.Print("[GameFramework] Plugin unloaded.");
    }

    private void OnMenuPressed()
    {
        if (m_Popup == null)
        {
            return;
        }

        // 如果 Popup 还没挂到场景树，挂到编辑器根控件下
        if (m_Popup.GetParent() == null)
        {
            EditorInterface.Singleton.GetBaseControl().AddChild(m_Popup);
        }

        Control baseControl = EditorInterface.Singleton.GetBaseControl();
        m_Popup.Position = (Vector2I)baseControl.GetLocalMousePosition();
        m_Popup.ResetSize();
        m_Popup.Popup();
    }

    private void OnPopupIdPressed(long id)
    {
        int index = (int)id;

        if (index == LogLevels.Length)
        {
            GD.Print("[GameFramework] Game Framework v2021.05.31 — Godot Edition");
            return;
        }

        if (index < 0 || index >= LogLevels.Length)
        {
            return;
        }

        try
        {
            ApplyDefineConstants(LogLevels[index].Define);
            GD.Print($"[GameFramework] Log level changed to: {LogLevels[index].Label}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameFramework] Failed to update .csproj: {ex.Message}");
        }
    }

    private static void ApplyDefineConstants(string define)
    {
        string csprojPath = Path.Combine(ProjectSettings.GlobalizePath("res://"), "GodotProject.csproj");

        if (!File.Exists(csprojPath))
        {
            throw new FileNotFoundException($"Project file not found: {csprojPath}");
        }

        string content = File.ReadAllText(csprojPath);

        if (Regex.IsMatch(content, DefineConstantsPattern))
        {
            if (string.IsNullOrEmpty(define))
            {
                content = Regex.Replace(content, @"[ \t]*<DefineConstants>.*?</DefineConstants>\r?\n?", "");
            }
            else
            {
                content = Regex.Replace(content, DefineConstantsPattern, $"<DefineConstants>{define}</DefineConstants>");
            }
        }
        else if (!string.IsNullOrEmpty(define))
        {
            content = content.Replace(
                "  </PropertyGroup>",
                $"    <DefineConstants>{define}</DefineConstants>\n  </PropertyGroup>");
        }

        content = Regex.Replace(content, @"\n{3,}", "\n\n");

        File.WriteAllText(csprojPath, content);
    }
}
