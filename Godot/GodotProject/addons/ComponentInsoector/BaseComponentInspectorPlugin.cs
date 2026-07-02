using GameFramework;
using Godot;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class BaseComponentInspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith($"BaseComponent.cs");
            }
            return @object is BaseComponent;
        }
        public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usage, bool wide)
        {
            if (name == BaseComponent.Parameters.JsonHelper)
                return true;
            return false;
        }
        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            DrawDropDown(@object);
        }
        private GodotObject DrawDropDown(GodotObject @object)
        {
            Type[] procedureTypes = Utility.Assembly.GetAssignableFormTypes(typeof(Utility.Json.IJsonHelper));
            // 左右排版：Label | OptionButton 放在同一行
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label enterLabel = new Label();
            enterLabel.Text = "Json Helper";
            enterLabel.VerticalAlignment = VerticalAlignment.Center;
            enterLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(enterLabel);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string currentEnter = "";
            var enterVar = @object.Get(BaseComponent.Parameters.JsonHelper);
            if (enterVar.VariantType == Variant.Type.String)
                currentEnter = enterVar.AsString();

            int selectedIdx = -1;
            dropdown.AddItem("None");
            foreach (var procType in procedureTypes)
            {
                if (procType.IsAbstract)
                    continue;
                int idx = dropdown.ItemCount;
                dropdown.AddItem(procType.FullName);
                if (procType.FullName == currentEnter)
                    selectedIdx = idx;
            }
            if (selectedIdx >= 0)
                dropdown.Select(selectedIdx);

            var target = @object;
            dropdown.ItemSelected += (long index) =>
            {
                string selected = dropdown.GetItemText((int)index);
                target.Set(BaseComponent.Parameters.JsonHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
            return target;
        }
    }

}
