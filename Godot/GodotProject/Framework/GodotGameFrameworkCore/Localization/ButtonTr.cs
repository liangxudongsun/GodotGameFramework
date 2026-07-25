using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;
using System;
namespace GodotGameFramework.Localization;

public partial class ButtonTr : Button, IStringKey
{
    [Export]
    public string StringKey { get; private set; }
    public void SetLocalizationValue()
    {
        Text = GF.Localization.GetString(StringKey);
    }
}
