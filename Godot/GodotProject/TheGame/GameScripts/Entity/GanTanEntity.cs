using GameConfig.Constant;
using GameFramework.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Sound;
using System;

/// <summary>
/// 子弹初始化数据
/// </summary>
public struct BulletData
{
    /// <summary>飞行方向（单位向量）</summary>
    public Vector2 Direction;
    /// <summary>是否是玩家发射的子弹</summary>
    public bool IsPlayerBullet;
    /// <summary>速度（像素/秒）</summary>
    public float Speed;
}

public partial class GanTanEntity : AbstractArea2DEntity
{
    private float m_LifeTime = 0f;
    private Vector2 m_Direction = Vector2.Up;
    private bool m_IsPlayerBullet = true;
    private float m_Speed = 300f;
    private bool m_HasHit = false;

    /// <summary>
    /// 防止 HideEntity 后 Id=0 又被 OnUpdate/BodyEntered 重复调用
    /// </summary>
    private bool m_IsDead = false;

    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

        // 只对新实例连接信号
        if (isNewInstance)
        {
            BodyEntered += OnBodyEntered;
        }
    }

    public override void OnShow(object userData)
    {
        base.OnShow(userData);

        // 重置状态
        m_LifeTime = 0f;
        m_HasHit = false;
        m_IsDead = false;
        m_Direction = Vector2.Up;
        m_IsPlayerBullet = true;
        m_Speed = 300f;

        // 从 userData 读取子弹参数
        if (userData is BulletData data)
        {
            m_Direction = data.Direction.Normalized();
            m_IsPlayerBullet = data.IsPlayerBullet;
            m_Speed = data.Speed > 0 ? data.Speed : m_Speed;
            Rotation = Mathf.RadToDeg(Mathf.Atan2(m_Direction.Y, m_Direction.X));
        }
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        if (m_IsDead) return;

        m_LifeTime += elapseSeconds;
        Position += m_Direction * m_Speed * elapseSeconds;

        // 超时自动销毁
        if (m_LifeTime > 3f)
        {
            m_IsDead = true;
            GF.Entity.HideEntity(this);
        }
    }

    /// <summary>
    /// 碰撞检测：当子弹碰触到 PhysicsBody2D 时触发
    /// </summary>
    private void OnBodyEntered(Node body)
    {
        if (m_HasHit || m_IsDead) return;
        if (body == null) return;

        // 只对 ActorEntity 造成伤害
        if (body is ActorEntity actor && !actor.IsDead)
        {
            // 玩家子弹 → 敌人；敌人子弹 → 玩家
            if (m_IsPlayerBullet && actor.Team == EntityTeam.Enemy)
            {
                m_HasHit = true;
                m_IsDead = true;
                actor.Hurt(Id, 20);
                GF.Entity.HideEntity(this);
                GF.Sound.PlaySFX(ResourcesCollectionConstant.SFX_Dead);
            }
            else if (!m_IsPlayerBullet && actor.Team == EntityTeam.Player)
            {
                m_HasHit = true;
                m_IsDead = true;
                actor.Hurt(Id, 15);
                GF.Entity.HideEntity(this);
                GF.Sound.PlaySFX(ResourcesCollectionConstant.SFX_Dead);
            }
        }
    }
}
