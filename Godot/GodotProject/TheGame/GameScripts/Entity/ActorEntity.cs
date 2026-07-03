using GameConfig.Character;
using GameFramework;
using GameFramework.Entity;
using Godot;
using GodotGameFramework;


/// <summary>
/// 实体阵营
/// </summary>
public enum EntityTeam
{
    Player,
    Enemy,
}

public partial class ActorEntity : AbstractCharacterBody2DEntity, IActor
{
    protected ActorData m_ActorData;
    public bool IsDead => m_ActorData.Hp <= 0;
    protected CharacterConfig m_Config;
    protected PhysicsCheck2D m_Check;

    /// <summary>
    /// 实体所属阵营
    /// </summary>
    public EntityTeam Team { get; set; } = EntityTeam.Player;

    public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        if (isNewInstance)
        {
            m_ActorData = new ActorData()
            {
                MaxHp = 100,
                Hp = 100
            };
        }

        m_ActorData.MaxHp = 100;
        m_ActorData.Hp = m_ActorData.MaxHp;
    }
    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
#if TOOLS
        QueueRedraw();
#endif
    }
    /// <summary>
    /// 受伤
    /// </summary>
    /// <param name="entityId">攻击者实体编号</param>
    /// <param name="damage">伤害值</param>
    public virtual void Hurt(int entityId, int damage)
    {
        m_ActorData.Hp -= damage;
        Log.Debug("{0} 受到 {1} 点伤害，剩余 HP: {2}", Name, damage, m_ActorData.Hp);

        if (m_ActorData.Hp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 治疗
    /// </summary>
    public void Heal(int heal)
    {
        m_ActorData.Hp += heal;
        if (m_ActorData.Hp > m_ActorData.MaxHp)
        {
            m_ActorData.Hp = m_ActorData.MaxHp;
        }
    }
    public override void _Draw()
    {
        base._Draw();
        if (m_Config != null)
        {
            DrawCircle(Vector2.Zero, m_Config.CheckRange, Colors.Red, false, 2f);
        }
        m_Check?.DrawDebugLines(Colors.Green);
    }
    /// <summary>
    /// 死亡
    /// </summary>
    protected virtual void Die()
    {
        GF.Entity.HideEntity(this);
    }
    public override void _ExitTree()
    {
        base._ExitTree();
        ReferencePool.Release(m_Check);
    }

}
