#if TOOLS
using GameFramework;
using GameFramework.Entity;
using Godot;
using GodotGameFramework.Entity;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class EntityComponentInspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith($"EntityComponent.cs");
            }
            return @object is EntityComponent;
        }
        public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usage, bool wide)
        {
            if (name == EntityComponent.Parameters.EntityHelper || name == EntityComponent.Parameters.EntityGroupHelper)
                return true;
            return false;
        }
        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            DrawEntityHelperDropDown(@object);
            DrawEntityGroupHelperDropDown(@object);
        }

        private void DrawEntityHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(IEntityHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "Entity Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(EntityComponent.Parameters.EntityHelper);
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
                @object.Set(EntityComponent.Parameters.EntityHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }

        private void DrawEntityGroupHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(IEntityGroupHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "Entity Group Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(EntityComponent.Parameters.EntityGroupHelper);
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
                @object.Set(EntityComponent.Parameters.EntityGroupHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }
    }
}
#endif