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
		SceneComponentInspectorPlugin m_SceneComponent;
		SettingComponentInspectorPlugin m_SettingComponent;
		EntityComponentInspectorPlugin m_EntityComponent;
		UIComponentInspectorPlugin m_UIComponent;
		SoundComponentInspectorPlugin m_SoundComponent;
		LocalizationComponentInspectorPlugin m_LocalizationComponent;
		ScriptGenerateInspector m_ScriptGenerateInspector;
		public override void _EnterTree()
		{
			m_ProcedureComponent = new ProcedureComponentInspectorPlugin();
			m_BaseComponent = new BaseComponentInspectorPlugin();
			m_SceneComponent = new SceneComponentInspectorPlugin();
			m_SettingComponent = new SettingComponentInspectorPlugin();
			m_EntityComponent = new EntityComponentInspectorPlugin();
			m_UIComponent = new UIComponentInspectorPlugin();
			m_SoundComponent = new SoundComponentInspectorPlugin();
			m_LocalizationComponent = new LocalizationComponentInspectorPlugin();
			m_ScriptGenerateInspector = new ScriptGenerateInspector();
			AddInspectorPlugin(m_BaseComponent);
			AddInspectorPlugin(m_ProcedureComponent);
			AddInspectorPlugin(m_SceneComponent);
			AddInspectorPlugin(m_SettingComponent);
			AddInspectorPlugin(m_EntityComponent);
			AddInspectorPlugin(m_UIComponent);
			AddInspectorPlugin(m_SoundComponent);
			AddInspectorPlugin(m_LocalizationComponent);
			AddInspectorPlugin(m_ScriptGenerateInspector);
		}

		public override void _ExitTree()
		{
			RemoveInspectorPlugin(m_ProcedureComponent);
			RemoveInspectorPlugin(m_BaseComponent);
			RemoveInspectorPlugin(m_SceneComponent);
			RemoveInspectorPlugin(m_SettingComponent);
			RemoveInspectorPlugin(m_EntityComponent);
			RemoveInspectorPlugin(m_UIComponent);
			RemoveInspectorPlugin(m_SoundComponent);
			RemoveInspectorPlugin(m_LocalizationComponent);
			RemoveInspectorPlugin(m_ScriptGenerateInspector);
			m_ProcedureComponent.Free();
			m_BaseComponent.Free();
			m_SceneComponent.Free();
			m_SettingComponent.Free();
			m_EntityComponent.Free();
			m_UIComponent.Free();
			m_SoundComponent.Free();
			m_LocalizationComponent.Free();
			m_ScriptGenerateInspector.Free();
		}
	}
}
#endif
