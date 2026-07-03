using GameFramework;
using GameFramework.Sound;
using Godot;
using GodotGameFramework.Sound;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class SoundComponentInspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith($"SoundComponent.cs");
            }
            return @object is SoundComponent;
        }
        public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usage, bool wide)
        {
            if (name == SoundComponent.Parameters.SoundHelper || name == SoundComponent.Parameters.SoundGroupHelper || name == SoundComponent.Parameters.SoundAgentHelper)
                return true;
            return false;
        }
        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            DrawSoundHelperDropDown(@object);
            DrawSoundGroupHelperDropDown(@object);
            DrawSoundAgentHelperDropDown(@object);
        }

        private void DrawSoundHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(ISoundHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "Sound Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(SoundComponent.Parameters.SoundHelper);
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
                @object.Set(SoundComponent.Parameters.SoundHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }

        private void DrawSoundGroupHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(ISoundGroupHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "Sound Group Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(SoundComponent.Parameters.SoundGroupHelper);
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
                @object.Set(SoundComponent.Parameters.SoundGroupHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }

        private void DrawSoundAgentHelperDropDown(GodotObject @object)
        {
            Type[] helperTypes = Utility.Assembly.GetAssignableFormTypes(typeof(ISoundAgentHelper));
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label label = new Label();
            label.Text = "Sound Agent Helper";
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(label);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string current = "";
            var curVar = @object.Get(SoundComponent.Parameters.SoundAgentHelper);
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
                @object.Set(SoundComponent.Parameters.SoundAgentHelper, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
        }
    }
}
