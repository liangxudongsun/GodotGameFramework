using Godot;
using System;
namespace GodotGameFramework
{
	public partial class UIStringKey : Label
	{
		[Export]
		public string Key { get; private set; }

		public void SetValue()
		{
			if (string.IsNullOrEmpty(Key))
				return;
			Text = GF.Localization.GetString(Key);
		}

	}
}

