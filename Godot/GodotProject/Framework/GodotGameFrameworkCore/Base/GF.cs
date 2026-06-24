// GF - 全局静态门面
// 使用方式：GF.Entity.ShowEntity<T>() / GF.UI.OpenUIForm() / GF.Sound.PlayMusic() 等

using GameConfig;
using Godot;
using GodotGameFramework.Entity;
using GodotGameFramework.Localization;
using GodotGameFramework.Resource;
using GodotGameFramework.Scene;
using GodotGameFramework.Setting;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GodotGameFramework
{
    public static class GF
    {
        public static EventComponent Event => GameEntry.GetComponent<EventComponent>();
        public static FsmComponent Fsm => GameEntry.GetComponent<FsmComponent>();
        public static ProcedureComponent Procedure => GameEntry.GetComponent<ProcedureComponent>();
        public static ObjectPoolComponent ObjectPool => GameEntry.GetComponent<ObjectPoolComponent>();
        public static DataNodeComponent DataNode => GameEntry.GetComponent<DataNodeComponent>();
        public static ResourceComponent Resource => GameEntry.GetComponent<ResourceComponent>();
        public static EntityComponent Entity => GameEntry.GetComponent<EntityComponent>();
        public static UIComponent UI => GameEntry.GetComponent<UIComponent>();
        public static SoundComponent Sound => GameEntry.GetComponent<SoundComponent>();
        public static Tables DataTable => GameEntry.GetComponent<DataTableComponent>()?.GetTables();
        public static LocalizationComponent Localization => GameEntry.GetComponent<LocalizationComponent>();
        public static SettingComponent Setting => GameEntry.GetComponent<SettingComponent>();
        public static BaseComponent Base => GameEntry.GetComponent<BaseComponent>();
        public static SceneComponent Scene => GameEntry.GetComponent<SceneComponent>();
    }
}
