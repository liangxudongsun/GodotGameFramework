using GameFramework;
using GameFramework.Procedure;
using Godot;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class ProcedureComponentInspectorPlugin : EditorInspectorPlugin
    {
        public override bool _CanHandle(GodotObject @target)
        {
            // ProcedureComponent 在 GameFramework.tscn 中存储为 type="Node" + 脚本附加，
            // C# 运行时类型是 Node 而非 ProcedureComponent，所以不能直接用 is 检查。
            var scriptVar = @target.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript.ResourcePath.EndsWith("ProcedureComponent.cs");
            }
            return @target is ProcedureComponent;
        }
        public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usage, bool wide)
        {
            if (name == ProcedureComponent.Parameters.Procedures || name == ProcedureComponent.Parameters.EnterProcedure)
                return true;
            return false;
        }
        public override void _ParseBegin(GodotObject @object)
        {
            base._ParseBegin(@object);
            Type[] procedureTypes = Utility.Assembly.GetChildTypes(typeof(ProcedureBase));
            if (procedureTypes.Length == 0)
                return;

            GodotObject target = DrawDropDown(@object, procedureTypes);

            DrawProceduresList(@object, procedureTypes, target);

            HSeparator separator = new HSeparator();
            AddCustomControl(separator);
        }

        private void DrawProceduresList(GodotObject @object, Type[] procedureTypes, GodotObject target)
        {
            var currentProcs = new System.Collections.Generic.HashSet<string>();
            var procVar = @object.Get(ProcedureComponent.Parameters.Procedures);
            if (procVar.VariantType != Variant.Type.Nil)
            {
                try
                {
                    var arr = procVar.AsStringArray();
                    if (arr != null)
                        foreach (var p in arr)
                            currentProcs.Add(p);
                }
                catch
                {
                    var godotArr = procVar.AsGodotArray();
                    foreach (var v in godotArr)
                        currentProcs.Add(v.AsString());
                }
            }

            Label title = new Label();
            title.Text = "Procedures";
            AddCustomControl(title);

            ScrollContainer scroll = new ScrollContainer();
            scroll.CustomMinimumSize = new Vector2(0, 240);
            var scrollBg = new StyleBoxFlat();
            scrollBg.BgColor = new Color(0.12f, 0.12f, 0.12f, 1.0f);
            scroll.AddThemeStyleboxOverride("panel", scrollBg);

            VBoxContainer vbox = new VBoxContainer();
            vbox.SizeFlagsVertical = Control.SizeFlags.Expand;

            foreach (var procType in procedureTypes)
            {
                if (procType.IsAbstract)
                    continue;

                CheckButton check = new CheckButton();
                check.Text = procType.Name;
                check.ButtonPressed = currentProcs.Contains(procType.Name);

                var capturedName = procType.Name;

                check.Toggled += (bool toggledOn) =>
                {
                    var curVar = target.Get(ProcedureComponent.Parameters.Procedures);
                    var list = new System.Collections.Generic.List<string>();
                    if (curVar.VariantType != Variant.Type.Nil)
                    {
                        try
                        {
                            list.AddRange(curVar.AsStringArray());
                        }
                        catch
                        {
                            var godotArr = curVar.AsGodotArray();
                            foreach (var v in godotArr)
                                list.Add(v.AsString());
                        }
                    }

                    if (toggledOn)
                    {
                        if (!list.Contains(capturedName))
                            list.Add(capturedName);
                    }
                    else
                    {
                        list.Remove(capturedName);
                    }

                    target.Set(ProcedureComponent.Parameters.Procedures, list.ToArray());
                };

                vbox.AddChild(check);
            }

            scroll.AddChild(vbox);
            AddCustomControl(scroll);
        }

        private GodotObject DrawDropDown(GodotObject @object, Type[] procedureTypes)
        {
            // 左右排版：Label | OptionButton 放在同一行
            HBoxContainer hbox = new HBoxContainer();
            hbox.CustomMinimumSize = new Vector2(0, 28);
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            Label enterLabel = new Label();
            enterLabel.Text = "Enter Procedure";
            enterLabel.VerticalAlignment = VerticalAlignment.Center;
            enterLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddChild(enterLabel);

            OptionButton dropdown = new OptionButton();
            dropdown.CustomMinimumSize = new Vector2(0, 0);
            dropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string currentEnter = "";
            var enterVar = @object.Get(ProcedureComponent.Parameters.EnterProcedure);
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
                target.Set(ProcedureComponent.Parameters.EnterProcedure, selected);
            };

            hbox.AddChild(dropdown);
            AddCustomControl(hbox);
            return target;
        }
    }
}
