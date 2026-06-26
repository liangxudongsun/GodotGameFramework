using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;



public partial class MainForm : ControlUIForm
{
    public override void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        base.OnInit(serialId, uiFormAssetName, uiGroup, pauseCoveredUIForm, isNewInstance, userData);
        GF.Scene.LoadScene(ResourcesCollectionConstant.Scenes_Map);
        GF.Entity.ShowEntity(EntityId.Cat);
    }


    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        if (Input.IsActionJustPressed("ui_filedialog_show_hidden"))
        {
            GF.Entity.ShowEntity(EntityId.Cat);
        }
    }


}
