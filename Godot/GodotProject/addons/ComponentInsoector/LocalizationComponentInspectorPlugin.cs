#if TOOLS
using GameFramework;
using GameFramework.Localization;
using Godot;
using GodotGameFramework.Localization;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class LocalizationComponentInspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith($"LocalizationComponent.cs");
            }
            return @object is LocalizationComponent;
        }
        public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usage, bool wide)
        {
            if (name == LocalizationComponent.Parameters.LocalizationHelper)
                return true;
            return false;
        }
        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            DrawLocalizationHelperDropDown(@object);
        }

        private void DrawLocalizationHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(ILocalizationHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "Localization Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(LocalizationComponent.Parameters.LocalizationHelper);
            if (curVar.VariantType == Variant.Type.String)
                current = curVar.AsString();

            int selectedIdx = -1;
            foreach (var type in helperTypes)
            {
                if (type.IsAbstract) continue;
                int idx = dropdown.ItemCount;
                dropdown.AddItem(type.FullName);
                if (type.FullName == current) selectedIdx = idx;
            }
            if (selectedIdx >= 0) dropdown.Select(selectedIdx);

            dropdown.ItemSelected += (long index) =>
            {
                string selected = dropdown.GetItemText((int)index);
                @object.Set(LocalizationComponent.Parameters.LocalizationHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }
    }
}
#endif