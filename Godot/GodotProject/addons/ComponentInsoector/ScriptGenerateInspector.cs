using Godot;
namespace GodotGameFramework.Editor
{
    [Tool]
    public partial class ScriptGenerateInspector : EditorInspectorPlugin
    {
        const string SCRIPT_TEMPLATE = "res://Framework/GodotGameFrameworkCore/Templet/UIFormTemplet.txt";
        const string LOGIC_TEMPLATE = "res://Framework/GodotGameFrameworkCore/Templet/UIFormLogicTemplet.txt";
        const string Resc = "res://TheGame/Resources/ScriptGenerateRes.tres";
        const string NameSpaceReplace = "_NAMESPACE_";
        const string ParentClassReplace = "_PARENT_";
        const string ClassNameReplace = "_CLASSNAME_";
        const string DefaultNameSpace = "GameLogic";
        const string DefaultOutputPath = "res://TheGame/";
        private Button m_GenerateButton;
        public override bool _CanHandle(GodotObject @object)
        {
            // 模板生成的是 UIForm（IUIForm 需要 Control 基类），只对 Control 节点显示按钮
            return @object is Control;
        }

        public override void _ParseEnd(GodotObject @object)
        {
            base._ParseEnd(@object);

            // 只判断资源是否存在，不做强类型转换：即使 ScriptGenerateRes 尚未在编辑器注册，
            // 也能通过属性名读取配置，避免 InvalidCastException 和类型未注册导致按钮消失
            if (!ResourceLoader.Exists(Resc))
            {
                GD.PushWarning($"[ScriptGenerateInspector] 找不到配置资源: {Resc}");
                return;
            }

            m_GenerateButton = new Button();
            m_GenerateButton.Text = "Generate Script";
            m_GenerateButton.Pressed += () => OnGeneratePressed(@object);
            AddCustomControl(m_GenerateButton);
        }

        private void OnGeneratePressed(GodotObject @object)
        {
            if (@object is not Node node) return;

            string geTemplate = ReadText(SCRIPT_TEMPLATE);
            if (geTemplate == null) return;

            string parent = @object.GetType().Name;
            string className = Sanitize(node.Name);
            if (string.IsNullOrEmpty(className))
            {
                GD.PushError("[ScriptGenerateInspector] 节点名称无法转换为合法的类名。");
                return;
            }

            Godot.Resource config = ResourceLoader.Load(Resc);
            string namespaceStr = ReadProp(config, ScriptGenerateRes.Parameters.NameSpace, DefaultNameSpace);
            string outputDirGe = ReadProp(config, ScriptGenerateRes.Parameters.OutPutPathGe, DefaultOutputPath);
            string outputDirLogic = ReadProp(config, ScriptGenerateRes.Parameters.OutPutPathLogic, DefaultOutputPath);
            if (!outputDirGe.EndsWith("/")) outputDirGe += "/";
            if (!outputDirLogic.EndsWith("/")) outputDirLogic += "/";

            // 生成部分（Ge）：包含框架样板代码，每次都覆盖重写
            string geScript = geTemplate
                .Replace(NameSpaceReplace, namespaceStr)
                .Replace(ParentClassReplace, parent)
                .Replace(ClassNameReplace, className);
            string gePath = outputDirGe + className + ".Ge.cs";
            if (!WriteText(gePath, geScript)) return;

            // 逻辑部分（Logic）：用户业务代码，仅在首次生成时创建，避免覆盖已有逻辑
            string logicPath = outputDirLogic + className + ".Logic.cs";
            if (!FileAccess.FileExists(logicPath))
            {
                string logicTemplate = ReadText(LOGIC_TEMPLATE);
                if (logicTemplate != null)
                {
                    string logicScript = logicTemplate
                        .Replace(NameSpaceReplace, namespaceStr)
                        .Replace(ClassNameReplace, className);
                    WriteText(logicPath, logicScript);
                }

                // 刷新文件系统，让 Godot 立即识别新生成的脚本文件
                var fs = EditorInterface.Singleton.GetResourceFilesystem();
                fs.UpdateFile(gePath);
                if (FileAccess.FileExists(logicPath)) fs.UpdateFile(logicPath);

                // 加载 Ge 脚本并赋值给选中的节点
                var script = ResourceLoader.Load<Script>(gePath);
                if (script != null)
                {
                    node.SetScript(script);
                    EditorInterface.Singleton.GetResourceFilesystem().Scan();
                    GD.Print($"[ScriptGenerateInspector] 已生成并赋值脚本: {gePath}");
                }
                else
                {
                    GD.PushWarning($"[ScriptGenerateInspector] 脚本已生成，但尚未编译完成，无法立即赋值。请重新构建后手动附加: {gePath}");
                }
            }

        }

        private static string ReadProp(Godot.Resource res, string prop, string fallback)
        {
            if (res == null) return fallback;
            string value = res.Get(prop).AsString();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string ReadText(string path)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushError($"[ScriptGenerateInspector] 无法读取模板: {path} ({FileAccess.GetOpenError()})");
                return null;
            }
            return file.GetAsText();
        }

        private static bool WriteText(string path, string content)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"[ScriptGenerateInspector] 无法写入文件: {path} ({FileAccess.GetOpenError()})");
                return false;
            }
            file.StoreString(content);
            return true;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            }
            // C# 标识符不能以数字开头
            if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
            return sb.ToString();
        }
    }
}
