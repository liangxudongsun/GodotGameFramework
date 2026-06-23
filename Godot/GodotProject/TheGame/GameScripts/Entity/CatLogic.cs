using GameConfig.Character;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using System;

public partial class CatLogic : EntityLogic
{
	private CharacterConfig m_CatConfig;
	private bool m_IsMoving;
	private Tween m_ScaleChange;
	public override void OnInit()
	{
		base.OnInit();
		m_CatConfig = GF.DataTable.TbCharacterConfig.Get(1);
	}
	protected internal override void OnShow(object userData)
	{
		base.OnShow(userData);
		m_ScaleChange = CreateTween();
		m_ScaleChange.SetLoops(10);
		m_ScaleChange.TweenProperty(this, Node2D.PropertyName.Scale.ToString(), new Vector2(1.1f, 1.1f), 0.5f)
		.SetEase(Tween.EaseType.InOut);
		m_ScaleChange.TweenProperty(this, Node2D.PropertyName.Scale.ToString(), Vector2.One, 0.5f)
			.SetEase(Tween.EaseType.InOut);
	}
	protected internal override void OnUpdate(float elapseSeconds, float realElapseSeconds)
	{
		base.OnUpdate(elapseSeconds, realElapseSeconds);
		KeybordMove(elapseSeconds);
	}

	private void KeybordMove(float elapseSeconds)
	{
		//WASD控制猫移动
		if (Input.IsActionPressed("ui_right"))
		{
			Position2D = new Vector2(Position2D.X + m_CatConfig.Speed * elapseSeconds, Position2D.Y);
			FlipX = false;
			m_IsMoving = true;
		}
		else
		{
			m_IsMoving = false;
		}
		if (Input.IsActionPressed("ui_left"))
		{
			Position2D = new Vector2(Position2D.X - m_CatConfig.Speed * elapseSeconds, Position2D.Y);
			FlipX = true;
			m_IsMoving = true;
		}
		else
		{
			m_IsMoving = false;
		}
		if (Input.IsActionPressed("ui_down"))
		{
			Position2D = new Vector2(Position2D.X, Position2D.Y + m_CatConfig.Speed * elapseSeconds);
			m_IsMoving = true;
		}
		else
		{
			m_IsMoving = false;
		}
		if (Input.IsActionPressed("ui_up"))
		{
			Position2D = new Vector2(Position2D.X, Position2D.Y - m_CatConfig.Speed * elapseSeconds);
			m_IsMoving = true;
		}
		else
		{
			m_IsMoving = false;
		}
	}

}
