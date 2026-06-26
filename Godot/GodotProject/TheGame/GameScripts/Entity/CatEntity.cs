using GameConfig.Character;
using GameConfig.Entity;
using GameFramework.Entity;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using System;

public partial class CatEntity : AbstractCharacterBody2DEntity
{
	[Export]
	private Sprite2D m_CatSprite;
	private CharacterConfig m_CatConfig;
	private bool m_IsMoving;
	private Tween m_ScaleChange;
	float m_LastMoveTime;

	public override void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
	{
		base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
		if (isNewInstance)
		{
			m_CatConfig = GF.DataTable.TbCharacterConfig.Get(1);
		}
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
	public override async void OnUpdate(float elapseSeconds, float realElapseSeconds)
	{
		base.OnUpdate(elapseSeconds, realElapseSeconds);
		KeybordMove();
		m_LastMoveTime += elapseSeconds;
		if (m_LastMoveTime > 0.5f)
		{
			var entity = await GF.Entity.ShowEntityAsync<GanTanEntity>(EntityId.GanTan);
			entity.Position = Position + new Vector2(0, 10);
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
}
