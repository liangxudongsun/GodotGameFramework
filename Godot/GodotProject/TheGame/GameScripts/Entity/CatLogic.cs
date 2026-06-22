using Godot;
using GodotGameFramework.Entity;
using System;

public partial class CatLogic : EntityLogic
{
	protected internal override void OnShow(object userData)
	{
		base.OnShow(userData);
		Position2D = new Vector2(500, 500);
	}

}
