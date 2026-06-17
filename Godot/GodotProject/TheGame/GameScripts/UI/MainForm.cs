using System.Collections.Generic;
using GameConfig.Entity;
using GameFramework;
using GameFramework.DataNode;
using GameFramework.Localization;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Localization;
using GodotGameFramework.UI;


public partial class MainForm : UIFormLogic
{
    protected internal override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        GF.Entity.ShowEntity(EntityId.Cat);
    }

}
