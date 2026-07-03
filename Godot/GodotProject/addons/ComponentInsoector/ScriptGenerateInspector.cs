using GameFramework.UI;
using Godot;
using System;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class ScriptGenerateInspector : EditorInspectorPlugin
    {
        private Button m_GenerateButton;
        public override bool _CanHandle(GodotObject @object)
        {
            var scriptVar = @object.Get("script");
            if (scriptVar.VariantType == Variant.Type.Object &&
                scriptVar.AsGodotObject() is CSharpScript csScript)
            {
                return csScript is IUIForm;
            }
            return @object is BaseComponent;
        }

        public override void _ParseEnd(GodotObject @object)
        {
            base._ParseEnd(@object);
        }

    }
}
