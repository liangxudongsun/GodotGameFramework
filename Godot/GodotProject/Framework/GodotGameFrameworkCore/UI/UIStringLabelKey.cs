using Godot;
using System;
namespace GodotGameFramework.UI
{
	public partial class UIStringLabelKey : Label
	{
		[Export]
		public string Key { get; set; }
		public void SetValue()
		{
			if (string.IsNullOrEmpty(Key))
				return;
			Text = GF.Localization.GetString(Key);
		}

	}
}
