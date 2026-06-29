using GameConfig.Character;
using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Sound;
using System;

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
	private CharacterConfig m_CatConfig;
	private bool m_IsMoving;
	private Tween m_ScaleChange;
	float m_LastMoveTime;

	// 自动瞄准参数
	[Export]
	private float m_AimRange = 350f;

	private CircleShape2D m_AimShape;

	public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
	{
		base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
		if (isNewInstance)
		{
			m_CatConfig = GF.DataTable.TbCharacterConfig.Get(1);

			m_AimShape = new CircleShape2D();
			m_AimShape.Radius = m_AimRange;
		}
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
		if (Input.IsActionJustPressed("ui_accept"))
		{
			SpawnGanTan();
		}
	}

	/// <summary>
	/// 以玩家为中心做射线（圆形区域）检测，返回最近敌人的方向。
	/// 如果没有敌人，返回 Vector2.Up（默认向上）。
	/// </summary>
	private Vector2 GetAimDirection()
	{
		var spaceState = GetWorld2D().DirectSpaceState;

		var query = new PhysicsShapeQueryParameters2D();
		query.Shape = m_AimShape;
		query.Transform = new Transform2D(0, GlobalPosition);
		query.CollisionMask = 1; // 检测默认碰撞层上的物体

		var results = spaceState.IntersectShape(query, 16);

		ActorEntity nearestEnemy = null;
		float nearestDistSq = float.MaxValue;

		foreach (var result in results)
		{
			if (result["collider"].Obj is ActorEntity actor
				&& actor.Team == EntityTeam.Enemy
				&& !actor.IsDead
				&& IsInstanceValid(actor))
			{
				float distSq = GlobalPosition.DistanceSquaredTo(actor.GlobalPosition);
				if (distSq < nearestDistSq)
				{
					nearestDistSq = distSq;
					nearestEnemy = actor;
				}
			}
		}

		if (nearestEnemy != null)
		{
			return (nearestEnemy.GlobalPosition - GlobalPosition).Normalized();
		}

		return Vector2.Up; // 没有敌人时默认向上
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
		Velocity = new Vector2(hor, ver) * m_CatConfig.Speed;
		MoveAndSlide();
		m_IsMoving = hor != 0 || ver != 0;
		if (hor != 0) m_CatSprite.FlipH = hor < 0;
	}

	public override void _Draw()
	{
		base._Draw();
		DrawCircle(Vector2.Zero, m_AimRange, Colors.Orange, false, 2f);
	}

}
