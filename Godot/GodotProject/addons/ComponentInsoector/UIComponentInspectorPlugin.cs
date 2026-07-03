using GameFramework;
using GameFramework.UI;
using Godot;
using GodotGameFramework.UI;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class UIComponentInspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith($"UIComponent.cs");
            }
            return @object is UIComponent;
        }
        public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usage, bool wide)
        {
            if (name == UIComponent.Parameters.UIFormHelper || name == UIComponent.Parameters.UIGroupHelper)
                return true;
            return false;
        }
        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            DrawUIFormHelperDropDown(@object);
            DrawUIGroupHelperDropDown(@object);
        }

        private void DrawUIFormHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(IUIFormHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "UI Form Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(UIComponent.Parameters.UIFormHelper);
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
                @object.Set(UIComponent.Parameters.UIFormHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }

        private void DrawUIGroupHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(IUIGroupHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "UI Group Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(UIComponent.Parameters.UIGroupHelper);
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
                @object.Set(UIComponent.Parameters.UIGroupHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }
    }
}
