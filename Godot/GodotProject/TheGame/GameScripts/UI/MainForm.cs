using GameConfig.Constant;
using GameConfig.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;



public partial class MainForm : UIFormLogic
{

    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GF.Scene.LoadScene(ResourcesCollectionConstant.Scenes_Map);
        GF.Entity.ShowEntity(EntityId.Cat);
    }




}
