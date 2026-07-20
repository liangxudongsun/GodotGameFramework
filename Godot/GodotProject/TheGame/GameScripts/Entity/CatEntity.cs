using GameConfig.Character;
using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework;
using GameFramework.Entity;
using GameLogic;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Sound;
using System.Linq;

public interface IActor
{
	void Heal(int heal);
	void Hurt(int entityId, int damage);
}

public struct ActorData
{
	public int Hp; //生命值
	public int MaxHp; //最大生命值
}

public partial class CatEntity : ActorEntity
{
	[Export]
	private Sprite2D m_CatSprite;
	private bool m_IsMoving;
	private Tween m_ScaleChange;
	float m_LastAtkTime;
	private CircleShape2D m_AimShape;


	public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
	{
		base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
		if (isNewInstance)
		{
			m_Config = GF.DataTable.GetTables().TbCharacterConfig.DataList.FirstOrDefault(x => x.EntityId == EntityId.Cat);
		}

		// 无论新实例还是池复用，都要重建 PhysicsCheck2D（旧的已在上次 _ExitTree 中释放）
		if (m_Check != null)
		{
			ReferencePool.Release(m_Check);
		}
		m_AimShape = new CircleShape2D();
		m_AimShape.Radius = m_Config.CheckRange;
		m_Check = PhysicsCheck2D.Create(
		this,
		m_AimShape,
		collisionMask: 1,
		maxResults: 16,
		collideWithBodies: true,
		collideWithAreas: false);
		Team = EntityTeam.Player;
	}

	public override void OnShow(object userData)
	{
		base.OnShow(userData);
		m_ScaleChange = CreateTween();
		m_ScaleChange.SetLoops();
		m_ScaleChange.TweenProperty(this, Node2D.PropertyName.Scale.ToString(), new Vector2(1.1f, 1.1f), 0.5f)
		.SetEase(Tween.EaseType.InOut);
		m_ScaleChange.TweenProperty(this, Node2D.PropertyName.Scale.ToString(), Vector2.One, 0.5f)
			.SetEase(Tween.EaseType.InOut);
	}

	public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
	{
		base.OnUpdate(elapseSeconds, realElapseSeconds);


		KeybordMove();

	}
	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		m_LastAtkTime += (float)delta;
		if (m_LastAtkTime >= m_Config.AtkSpeed)
		{
			m_LastAtkTime = 0;
			if (!m_Check.IsColliding())
				return;
			SpawnGanTan();
		}
	}



	/// <summary>
	/// 以玩家为中心做圆形区域检测，返回最近敌人的方向。
	/// 如果没有敌人，返回 <see cref="Vector2.Up"/>。
	/// </summary>
	private Vector2 GetAimDirection()
	{
		ActorEntity nearestEnemy = m_Check.GetCollidingNodesSorted().FirstOrDefault() as ActorEntity;
		if (nearestEnemy != null)
			return (nearestEnemy.GlobalPosition - GlobalPosition).Normalized();

		return Vector2.Up;
	}

	private async void SpawnGanTan()
	{
		Vector2 dir = GetAimDirection();

		var entity = await GF.Entity.ShowEntityAsync<GanTanEntity>(EntityId.GanTan,
			new BulletData
			{
				Direction = dir,
				IsPlayerBullet = true,
				Speed = 300f,
			});

		if (entity != null)
		{
			entity.Position = Position;
			GF.Sound.PlaySFX(ResourcesCollectionConstant.SFX_Shoot);
		}
	}

	private void KeybordMove()
	{
		float hor = Input.GetAxis("ui_left", "ui_right");
		float ver = Input.GetAxis("ui_up", "ui_down");
		Velocity = new Vector2(hor, ver) * m_Config.Speed;
		MoveAndSlide();
		m_IsMoving = hor != 0 || ver != 0;
		if (hor != 0) m_CatSprite.FlipH = hor < 0;
	}






}
