using GameFramework.Entity;
using Godot;
using GodotGameFramework;
using System;

public partial class GanTanEntity : AbstractRb2DEntity
{
    float m_LifeTime = 0f;

    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
    }
    public override void OnShow(object userData)
    {
        base.OnShow(userData);
    }
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        LinearVelocity = new Vector2(0, 1) * 200;
        m_LifeTime += elapseSeconds;
        if (m_LifeTime > 2f)
        {
            GF.Entity.HideEntity(this);
        }
    }


}
