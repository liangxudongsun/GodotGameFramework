using Godot;

namespace GodotGameFramework.UI
{
	public partial class UIStringLabelKey : Label, IStringKey
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