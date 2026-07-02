#if TOOLS
using Godot;
using System;
namespace GodotGameFramework.Editor
{
	[Tool]
	public partial class ComponentInsoector : EditorPlugin
	{
		ProcedureComponentInspectorPlugin m_ProcedureComponent;
		BaseComponentInspectorPlugin m_BaseComponent;
		public override void _EnterTree()
		{
			m_ProcedureComponent = new ProcedureComponentInspectorPlugin();
			m_BaseComponent = new BaseComponentInspectorPlugin();
			AddInspectorPlugin(m_BaseComponent);
			AddInspectorPlugin(m_ProcedureComponent);
		}

		public override void _ExitTree()
		{
			RemoveInspectorPlugin(m_ProcedureComponent);
			RemoveInspectorPlugin(m_BaseComponent);
			m_ProcedureComponent.Free();
			m_BaseComponent.Free();
		}
	}
}
#endif
