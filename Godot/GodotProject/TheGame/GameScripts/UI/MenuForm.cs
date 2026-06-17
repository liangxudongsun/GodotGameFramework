//------------------------------------------------------------
// 主菜单界面逻辑。
// 通过 UIComponent.OpenUIForm<MainMenuForm>() 打开。
// 演示 UIFormLogic 生命周期：OnInit → OnOpen → OnClose → OnRecycle
// Phase 7: 本地化文本 + 语言切换按钮
//------------------------------------------------------------

using System;
using GameConfig;
using GameFramework.Localization;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;

public partial class MenuForm : UIFormLogic
{
    [Export]
    private Label m_TitleLabel;

    [Export]
    private Label m_SubtitleLabel;

    [Export]
    private Button m_StartButton;

    [Export]
    private Button m_SettingButton;
    [Export]
    private Button m_CloseButton;
    [Export]
    private Control m_SettingPanel;
    protected internal override void OnInit(object userData)
    {
        base.OnInit(userData);
        m_SettingButton.Pressed += OnSettingButtonPressed;
        m_CloseButton.Pressed += OnCloseButtonPressed;
        m_StartButton.Pressed += OnStartButtonPressed;
    }

    private void OnStartButtonPressed()
    {
        Close();
        GF.UI.OpenUIForm(UIFormId.MainForm);
    }


    private void OnCloseButtonPressed()
    {
        m_SettingPanel.Visible = false;
    }


    private void OnSettingButtonPressed()
    {
        m_SettingPanel.Visible = true;
    }
}
