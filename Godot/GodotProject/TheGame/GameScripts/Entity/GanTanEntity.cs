using GameFramework.Entity;
using Godot;
using GodotGameFramework;
using System;

public partial class GanTanEntity : AbstractArea2DEntity
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
        m_LifeTime += elapseSeconds;
        Position += Vector2.Up * 10f;
        if (m_LifeTime > 2f)
        {
            GF.Entity.HideEntity(this);
            m_LifeTime = 0f;
        }
    }


}
