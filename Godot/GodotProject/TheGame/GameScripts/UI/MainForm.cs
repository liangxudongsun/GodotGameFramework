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
        GF.Entity.ShowEntity(EntityId.Cat);
    }




}
