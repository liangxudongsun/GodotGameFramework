using GameConfig.Entity;
using GameFramework.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using System;
using System.Threading.Tasks;


public partial class AngerEntity : ActorEntity
{

    [Export]
    private HSlider m_HSlider;

    // 攻击参数
    [Export]
    private float m_AttackRange = 350f;
    [Export]
    private float m_AttackInterval = 1.5f;
    [Export]
    private float m_MoveSpeed = 80f;
    [Export]
    private int m_AttackDamage = 15;

    private float m_AttackTimer = 0f;
    private CatEntity m_TargetPlayer = null;

    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);

        Team = EntityTeam.Enemy;
        m_HSlider.MaxValue = m_ActorData.MaxHp;
        m_HSlider.Value = m_ActorData.Hp;
    }

    public override void OnShow(object userData)
    {
        base.OnShow(userData);

        m_AttackTimer = 0f;
        m_HSlider.Value = m_ActorData.Hp;
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        // 查找玩家
        if (m_TargetPlayer == null || !IsInstanceValid(m_TargetPlayer))
        {
            m_TargetPlayer = FindPlayer();
            if (m_TargetPlayer == null) return;
        }

        float distance = GlobalPosition.DistanceTo(m_TargetPlayer.GlobalPosition);

        if (distance <= m_AttackRange)
        {
            // 在攻击范围内 — 面向玩家并攻击
            FaceDirection(m_TargetPlayer.GlobalPosition - GlobalPosition);

            m_AttackTimer += elapseSeconds;
            if (m_AttackTimer >= m_AttackInterval)
            {
                m_AttackTimer = 0f;
                _ = ShootAtPlayer();
            }
        }
        else
        {
            // 在攻击范围外 — 向玩家靠近
            Vector2 dir = (m_TargetPlayer.GlobalPosition - GlobalPosition).Normalized();
            Velocity = dir * m_MoveSpeed;
            FaceDirection(dir);
            MoveAndSlide();
        }
    }

    private void FaceDirection(Vector2 dir)
    {
        if (Mathf.Abs(dir.X) > 0.01f)
        {
            var sprite = GetNode<Sprite2D>("Sprite2D");
            if (sprite != null)
            {
                sprite.FlipH = dir.X < 0;
            }
        }
    }

    /// <summary>
    /// 查找场景中的 CatEntity（玩家）
    /// </summary>
    private CatEntity FindPlayer()
    {
        // 方法1：通过 Godot 场景树查找
        var cat = GetTree().GetFirstNodeInGroup("Player");
        if (cat is CatEntity catEntity)
            return catEntity;

        // 方法2：通过实体管理器查找
        // 遍历所有已加载实体，找到 CatEntity
        var allEntities = GF.Entity.GetAllLoadedEntities();
        foreach (var entity in allEntities)
        {
            if (entity is CatEntity ce && IsInstanceValid(ce))
                return ce;
        }

        return null;
    }

    /// <summary>
    /// 朝玩家方向发射子弹
    /// </summary>
    private async Task ShootAtPlayer()
    {
        if (m_TargetPlayer == null || !IsInstanceValid(m_TargetPlayer))
            return;

        Vector2 dir = (m_TargetPlayer.GlobalPosition - GlobalPosition).Normalized();

        BulletData bulletData = new BulletData
        {
            Direction = dir,
            IsPlayerBullet = false,
            Speed = 250f,
        };

        var bullet = await GF.Entity.ShowEntityAsync<GanTanEntity>(EntityId.GanTan, bulletData);
        if (bullet != null)
        {
            bullet.Position = GlobalPosition;
        }
    }

    /// <summary>
    /// 受伤时更新血条
    /// </summary>
    public override void Hurt(int entityId, int damage)
    {
        base.Hurt(entityId, damage);
        m_HSlider.Value = m_ActorData.Hp;
    }


}
