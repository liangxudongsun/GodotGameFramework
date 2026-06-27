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
    public override async void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        base.OnInit(serialId, uiFormAssetName, uiGroup, pauseCoveredUIForm, isNewInstance, userData);
        Node2D scene = (Node2D)await GF.Scene.LoadSceneAsync(ResourcesCollectionConstant.Scenes_Map);
        Node2D spawnPoint = scene.GetNode<Node2D>("SpawnPoint");
        var cat = await GF.Entity.ShowEntityAsync<CatEntity>(EntityId.Cat);
        cat.Position = spawnPoint.Position;
        GF.Sound.PlayBGM(ResourcesCollectionConstant.Music_Fight);
    }


    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
    }


}
