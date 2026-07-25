using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;
using System;
namespace GodotGameFramework.Localization;

public partial class LabelTr : Label, IStringKey
{
	[Export]
	public string StringKey { get; private set; }
	public void SetLocalizationValue()
	{
		Text = GF.Localization.GetString(StringKey);
	}

}
