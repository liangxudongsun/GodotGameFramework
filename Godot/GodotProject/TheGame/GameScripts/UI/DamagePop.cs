using Godot;
using GodotGameFramework.NodePool;
using System;
using System.Threading.Tasks;

public partial class DamagePop : Label, IPoolable
{
	public void OnGet()
	{

	}

	public void OnRelease()
	{

	}

	public async void SetText(Vector2 pos, int damage)
	{
		GlobalPosition = pos;
		Text = damage.ToString();
		AddToGroup("DamagePop");
		await Task.Delay(500);
		NodePool.Release(this);
	}

}
