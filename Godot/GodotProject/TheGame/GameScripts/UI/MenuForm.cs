using System;
using GameConfig;
using GameConfig.Constant;
using GameFramework.Localization;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

public partial class MenuForm : ControlUIForm
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
    public override void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        base.OnInit(serialId, uiFormAssetName, uiGroup, pauseCoveredUIForm, isNewInstance, userData);
        if (isNewInstance)
        {
            m_SettingButton.Pressed += OnSettingButtonPressed;
            m_CloseButton.Pressed += OnCloseButtonPressed;
            m_StartButton.Pressed += OnStartButtonPressed;
        }
    }

    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GF.Sound.PlayBGM(ResourcesCollectionConstant.Music_Menu);
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
