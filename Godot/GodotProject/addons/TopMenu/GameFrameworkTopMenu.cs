#if TOOLS
using Godot;
using System;
using System.IO;
using System.Text.RegularExpressions;

[Tool]
public partial class GameFrameworkTopMenu : EditorPlugin
{
    public const string MenuName = "GameFramework";
    public const string OpenFolderName = "OpenFolder";
    private const string DefineConstantsPattern = @"<DefineConstants>.*?</DefineConstants>";

    private PopupMenu m_LogPopup;
    private PopupMenu m_OpenFolderPopup;

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
    private static readonly (string Label, string Define)[] Folder = new[]
    {
        ("Data Path", ProjectSettings.GlobalizePath("res://")),
        ("User Data Path", ProjectSettings.GlobalizePath("user://"))
    };

    public override void _EnterTree()
    {
        m_LogPopup = new PopupMenu();
        m_LogPopup.Name = "GameFrameworkLogPopup";

        m_OpenFolderPopup = new PopupMenu();
        m_OpenFolderPopup.Name = "GameFrameworkOpenFolderPopup";


        for (int i = 0; i < LogLevels.Length; i++)
        {
            m_LogPopup.AddItem(LogLevels[i].Label, i);
        }

        for (int i = 0; i < Folder.Length; i++)
        {
            m_OpenFolderPopup.AddItem(Folder[i].Label, i);
        }

        m_LogPopup.IdPressed += OnLogPopupIdPressed;
        m_OpenFolderPopup.IdPressed += OnOpenFolderPopupIdPressed;
        AddToolSubmenuItem(MenuName, m_LogPopup);
        AddToolSubmenuItem(OpenFolderName, m_OpenFolderPopup);
        GD.Print("[GameFramework] Plugin loaded.");
    }




    public override void _ExitTree()
    {
        if (m_LogPopup != null)
        {
            m_LogPopup.IdPressed -= OnLogPopupIdPressed;
            if (m_LogPopup.GetParent() != null)
            {
                m_LogPopup.GetParent().RemoveChild(m_LogPopup);
            }
            m_LogPopup.QueueFree();
            m_LogPopup = null;
        }
        if (m_OpenFolderPopup != null)
        {
            m_OpenFolderPopup.IdPressed -= OnOpenFolderPopupIdPressed;
            if (m_OpenFolderPopup.GetParent() != null)
            {
                m_OpenFolderPopup.GetParent().RemoveChild(m_OpenFolderPopup);
            }
            m_OpenFolderPopup.QueueFree();
            m_OpenFolderPopup = null;
        }

        RemoveToolMenuItem(MenuName);
        RemoveToolMenuItem(OpenFolderName);
        GD.Print("[GameFramework] Plugin unloaded.");
    }

    private void OnMenuPressed()
    {
        if (m_LogPopup == null)
        {
            return;
        }

        // 如果 Popup 还没挂到场景树，挂到编辑器根控件下
        if (m_LogPopup.GetParent() == null)
        {
            EditorInterface.Singleton.GetBaseControl().AddChild(m_LogPopup);
        }

        Control baseControl = EditorInterface.Singleton.GetBaseControl();
        m_LogPopup.Position = (Vector2I)baseControl.GetLocalMousePosition();
        m_LogPopup.ResetSize();
        m_LogPopup.Popup();
    }

    private void OnLogPopupIdPressed(long id)
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
    private void OnOpenFolderPopupIdPressed(long id)
    {
        int index = (int)id;
        if (index < 0 || index >= Folder.Length)
        {
            return;
        }
        try
        {
            string path = Folder[index].Define.Replace("/", "\\");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[GameFramework] Failed to open folder: {ex.Message}");
        }
        GD.Print($"[GameFramework] Open folder: {Folder[index].Label}");
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
#endif