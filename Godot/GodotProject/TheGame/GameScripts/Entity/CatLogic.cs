using GameConfig.Character;
using GameConfig.Constant;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Sound;


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
		float hor = Input.GetAxis("ui_left", "ui_right");
		float ver = Input.GetAxis("ui_up", "ui_down");
		Position2D += new Vector2(hor, ver) * m_CatConfig.Speed * elapseSeconds;
		m_IsMoving = hor != 0 || ver != 0;
		FlipX = hor < 0;
	}

}
