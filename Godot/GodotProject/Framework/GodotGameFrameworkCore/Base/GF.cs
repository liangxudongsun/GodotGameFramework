//------------------------------------------------------------
// GF - 全局静态门面
// 提供所有框架组件的便捷静态属性访问
//
// 使用方式：
//   GF.Entity.ShowEntity<EnemyLogic>(1, "res://...", "Enemy");
//   GF.UI.OpenUIForm<MainMenuForm>("res://...", "Normal");
//   GF.Sound.PlayBGM("res://Audio/bgm.mp3");
//   GF.Event.Fire(this, args);
//   GF.Localization.GetString("GameTitle");
//
// 对应 UGF 的 GFBuiltin（提供 Entity/UI/Sound 等静态属性访问）。
// GGF 版本为纯静态类（非 Node），更轻量，无场景树依赖。
//------------------------------------------------------------

using GameConfig;
using Godot;

namespace GodotGameFramework
{
    /// <summary>
    /// 全局静态门面。
    ///
    /// 提供所有 GGF 框架组件的便捷静态属性访问，
    /// 使用户代码无需每次调用 GGFEntry.GetComponent&lt;T&gt;()。
    ///
    /// 设计说明：
    /// - 纯静态类（非 Node），无场景树依赖
    /// - 每次访问属性时调用 GGFEntry.GetComponent&lt;T&gt;()
    /// - 命名空间为 GodotGameFramework，与所有组件一致
    /// - 对应 UGF 的 GFBuiltin（MonoBehaviour 子类）
    ///
    /// 注意事项：
    /// - 在 GGFEntry._Ready() 之前访问会返回 null（组件尚未注册）
    /// - 在 GGFEntry._ExitTree() 之后访问同样返回 null（组件已注销）
    /// - 建议仅在 Procedure/EntityLogic/UIFormLogic 等生命周期内使用
    ///
    /// 对应 Unity 版本中的 GFBuiltin。
    /// </summary>
    public static class GF
    {
        /// <summary>
        /// 获取事件组件。
        /// 用于订阅/取消订阅/触发全局事件。
        /// <code>GF.Event.Fire(this, args);</code>
        /// </summary>
        public static EventComponent Event => GameEntry.GetComponent<EventComponent>();

        /// <summary>
        /// 获取有限状态机组件。
        /// 用于创建/销毁/查询 FSM 实例。
        /// <code>GF.Fsm.CreateFsm&lt;MyOwner&gt;(owner, states);</code>
        /// </summary>
        public static FsmComponent Fsm => GameEntry.GetComponent<FsmComponent>();

        /// <summary>
        /// 获取流程组件。
        /// 用于管理游戏流程（Procedure）的启动和切换。
        /// <code>GF.Procedure.StartProcedure&lt;MenuProcedure&gt;();</code>
        /// </summary>
        public static ProcedureComponent Procedure => GameEntry.GetComponent<ProcedureComponent>();

        /// <summary>
        /// 获取对象池组件。
        /// 用于创建/销毁/查询对象池，管理可复用对象。
        /// <code>GF.ObjectPool.CreateSingleSpawnObjectPool&lt;MyObject&gt;();</code>
        /// </summary>
        public static ObjectPoolComponent ObjectPool => GameEntry.GetComponent<ObjectPoolComponent>();

        /// <summary>
        /// 获取数据节点组件。
        /// 用于管理树形结构数据，支持路径式层级访问。
        /// <code>GF.DataNode.SetData("Game/Score", new VarInt32(100));</code>
        /// </summary>
        public static DataNodeComponent DataNode => GameEntry.GetComponent<DataNodeComponent>();

        /// <summary>
        /// 获取资源组件。
        /// 用于加载 Godot 资源（PackedScene、Texture2D、AudioStream 等）。
        /// <code>GF.Resource.LoadAsset&lt;PackedScene&gt;("res://Scenes/Enemy.tscn");</code>
        /// </summary>
        public static ResourceComponent Resource => GameEntry.GetComponent<ResourceComponent>();

        /// <summary>
        /// 获取实体组件。
        /// 用于动态创建/销毁实体，管理实体组和父子关系。
        /// <code>GF.Entity.ShowEntity&lt;EnemyLogic&gt;(1, "res://...", "Enemy");</code>
        /// </summary>
        public static EntityComponent Entity => GameEntry.GetComponent<EntityComponent>();

        /// <summary>
        /// 获取界面组件。
        /// 用于打开/关闭 UI 窗体，管理界面组和深度排序。
        /// <code>GF.UI.OpenUIForm&lt;MainMenuForm&gt;("res://...", "Normal");</code>
        /// </summary>
        public static UIComponent UI => GameEntry.GetComponent<UIComponent>();

        /// <summary>
        /// 获取音频组件。
        /// 用于播放 BGM 和音效，管理声音组。
        /// <code>GF.Sound.PlaySound("res://Audio/click.wav", "SFX");</code>
        /// </summary>
        public static SoundComponent Sound => GameEntry.GetComponent<SoundComponent>();

        /// <summary>
        /// 获取配置组件。
        /// 用于加载和管理配置键值对。
        /// <code>GF.Config.GetBool("IsDebug");</code>
        /// </summary>
        public static ConfigComponent Config => GameEntry.GetComponent<ConfigComponent>();

        /// <summary>
        /// 获取数据表组件。
        /// 用于加载和管理 CSV 数据表。
        /// <code>GF.DataTable.GetDataTable&lt;TestItemData&gt;();</code>
        /// </summary>
        public static Tables DataTable => GameEntry.GetComponent<DataTableComponent>().GetTables();

        /// <summary>
        /// 获取本地化组件。
        /// 用于加载多语言字典，获取翻译文本。
        /// <code>GF.Localization.GetString("GameTitle");</code>
        /// </summary>
        public static LocalizationComponent Localization => GameEntry.GetComponent<LocalizationComponent>();

        /// <summary>
        /// 获取设置组件。
        /// 用于持久化游戏设置（音量、语言等）。
        /// <code>GF.Setting.SetBool("MusicMuted", true);</code>
        /// </summary>
        public static SettingComponent Setting => GameEntry.GetComponent<SettingComponent>();

        /// <summary>
        /// 获取基础组件。
        /// 用于控制帧率、游戏速度、暂停/恢复等基础功能。
        /// <code>GF.Base.PauseGame();</code>
        /// </summary>
        public static BaseComponent Base => GameEntry.GetComponent<BaseComponent>();
    }
}
