using GameConfig.Constant;
using GameConfig.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.UI;



public partial class MainForm : UIFormLogic
{

    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GF.Scene.LoadScene(ResourcesCollectionConstant.Map);
        Log.Info(GF.Scene.HasScene(ResourcesCollectionConstant.Map) + GF.Scene.CurrentActiveScene.Name);
        GF.Entity.ShowEntity(EntityId.Cat);
    }




}
