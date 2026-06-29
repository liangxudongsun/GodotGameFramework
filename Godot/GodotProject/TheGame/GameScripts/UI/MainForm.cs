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
        Line2D line2D = scene.GetNode<Line2D>("Line2D");
        CatEntity cat = await GF.Entity.ShowEntityAsync<CatEntity>(EntityId.Cat);
        cat.Position = spawnPoint.Position;
        GF.Sound.PlayBGM(ResourcesCollectionConstant.Music_Fight);

        for (int i = 0; i < line2D.Points.Length; i++)
        {
            var point = line2D.Points[i];
            var enemy = await GF.Entity.ShowEntityAsync<AngerEntity>(EntityId.Anger);
            enemy.Position = point;
        }
    }




    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
    }


}
