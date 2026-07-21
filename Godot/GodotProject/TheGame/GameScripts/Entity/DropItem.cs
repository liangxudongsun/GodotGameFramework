using Godot;
using GodotGameFramework.DoTween;
using GodotGameFramework.NodePool;
using System;

public partial class DropItem : Node2D, IPoolable
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

	public void MoveTo(Vector2 position, Action finish)
	{
		if (m_Tween == null)
		{
			m_Tween = this.DOMove(position, 0.5f);
			m_Tween.Finished += () =>
			{
				finish?.Invoke();
				NodePool.Release(this);
			};
		}
	}
}
