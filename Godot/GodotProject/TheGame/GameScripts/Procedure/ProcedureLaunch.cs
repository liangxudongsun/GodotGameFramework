//------------------------------------------------------------
// 启动流程（LaunchProcedure）
// 游戏的入口流程，完成框架初始化、加载配置和数据表、创建实体组
//------------------------------------------------------------

using System.Collections.Concurrent;
using System.Threading.Tasks;
using GameConfig;
using GameFramework;
using GameFramework.DataNode;
using GameFramework.DataTable;
using GameFramework.Fsm;
using GameFramework.Localization;
using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 启动流程。
/// </summary>
public class ProcedureLaunch : ProcedureBase
{
    /// <summary>
    /// 状态初始化。
    /// </summary>
    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);
    }

    /// <summary>
    /// 进入流程。
    /// 执行所有初始化工作后立即切换到菜单流程。
    /// </summary>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        Log.Info($"Log 系统正常");
        Log.Info($"[LaunchProcedure] 验证框架组件...");
        Log.Info($"[EventComponent]: {(GF.Event != null ? "OK" : "缺失")}");
        Log.Info($"[FsmComponent]: {(GF.Fsm != null ? "OK" : "缺失")}");
        Log.Info($"[ConfigComponent]: {(GF.Config != null ? "OK" : "缺失")}");
        Log.Info($"[DataTableComponent]: {(GF.DataTable != null ? "OK" : "缺失")}");
        Log.Info($"[SettingComponent]: {(GF.Setting != null ? "OK" : "缺失")}");
        Log.Info($"[DataNodeComponent]: {(GF.DataNode != null ? "OK" : "缺失")}");
        Log.Info($"[ResourceComponent]: {(GF.Resource != null ? "OK" : "缺失")}");
        Log.Info($"[EntityComponent]: {(GF.Entity != null ? "OK" : "缺失")}");
        Log.Info($"[UIComponent]: {(GF.UI != null ? "OK" : "缺失")}");
        Log.Info($"[SoundComponent]: {(GF.Sound != null ? "OK" : "缺失")}");
        Log.Info($"[LocalizationComponent]: {(GF.Localization != null ? "OK" : "缺失")}");

        Task.Yield();
        LoadLocalization();
        LoadUIGroup();
        LoadEntityGroup();

        ChangeState<ProcedureGame>(procedureOwner);

    }
    private void LoadLocalization()
    {
        GF.Localization.ReadData(Utility.Text.Format(GameFolderConstant.LOCALIZATION, GF.Localization.Language.ToString()));
    }
    private void LoadUIGroup()
    {
        var groups = GF.DataTable.TbUIGroupConfig.DataList;
        for (int i = 0; i < groups.Count; i++)
        {
            if (!GF.UI.AddUIGroup(groups[i].Name, groups[i].Depth))
            {
                Log.Warning("Add UI group '{0}' failure.", groups[i].Name);
                continue;
            }
        }
    }
    private void LoadEntityGroup()
    {
        var groups = GF.DataTable.TbEntityGroupConfig.DataList;
        for (int i = 0; i < groups.Count; i++)
        {
            if (!GF.Entity.AddEntityGroup(groups[i].Name, groups[i].ReleaseInterval, groups[i].Capacity, groups[i].ExpireTime, groups[i].Priority))
            {
                Log.Warning("Add UI group '{0}' failure.", groups[i].Name);
                continue;
            }
        }
    }

    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }
}
