using Godot;
using GodotGameFramework.DoTween;
using GodotGameFramework.NodePool;
using System;
using System.Threading.Tasks;


public partial class DamagePop : Label, IPoolable
{
	private Tween m_Tween;
	public void OnGet()
	{

	}

	public void OnRelease()
	{
		if (m_Tween != null)
		{
			m_Tween.Kill();
			m_Tween = null;
		}
	}

	public void SetText(Vector2 pos, int damage, Color color = default)
	{
		GlobalPosition = pos;
		Text = damage.ToString();
		if (color != default)
		{
			Modulate = color;
		}
		if (m_Tween == null)
		{
			m_Tween = this.DoScale(0.5f, 0.5f);
			m_Tween.Finished += () =>
			{
				Scale = new Vector2(1, 1);
				Modulate = Colors.White;
				NodePool.Release(this);
			};
		}
	}

}
