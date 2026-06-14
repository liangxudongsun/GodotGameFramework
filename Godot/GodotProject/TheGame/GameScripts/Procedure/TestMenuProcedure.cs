//------------------------------------------------------------
// 菜单流程（MenuProcedure）
// 显示游戏说明，等待玩家点击开始按钮开始游戏
// Phase 5: 通过 UIComponent 打开主菜单界面
//------------------------------------------------------------

using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using GameFramework.Localization;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 菜单流程。
///
/// 通过 UIComponent 打开主菜单界面（MainMenuForm），
/// 等待玩家点击开始按钮开始游戏。
///
/// 框架特性展示：
/// - UIComponent.OpenUIForm：打开主菜单 UI
/// - UIComponent.CloseUIForm：关闭主菜单 UI
/// - UIFormLogic 生命周期：OnInit → OnOpen → OnClose
/// - UI 对象池：关闭后 UI 归还池，下次打开复用
/// </summary>
public class TestMenuProcedure : ProcedureBase
{
    /// <summary>主菜单界面的序列号。</summary>
    private int m_MainMenuSerialId;

    /// <summary>UI 组件引用。</summary>
    private UIComponent m_UIComponent;

    /// <summary>本地化组件引用（Phase 7）。</summary>
    private LocalizationComponent m_LocalizationComponent;

    /// <summary>
    /// 状态初始化。
    /// </summary>
    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);
        m_UIComponent = GF.UI;
        m_LocalizationComponent = GF.Localization;
    }

    /// <summary>
    /// 进入流程。
    /// 打开主菜单界面。
    /// </summary>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        GD.Print("\n[MenuProcedure] OnEnter - 进入菜单流程");

        // Phase 7: 使用本地化系统输出菜单文本
        if (m_LocalizationComponent != null)
        {
            string welcomeMsg = m_LocalizationComponent.GetString("WelcomeMessage");
            string instructions = m_LocalizationComponent.GetString("Instructions");
            GD.Print($"  [Phase 7] {welcomeMsg}");
            GD.Print($"  [Phase 7] {instructions}");
        }

        // 打开主菜单界面（Normal 组）
        if (m_UIComponent != null)
        {
            m_MainMenuSerialId = m_UIComponent.OpenUIForm<MainMenuForm>(
                "res://AAAGame/UI/MainMenu.tscn", "Normal");
            GD.Print($"  [MenuProcedure] 主菜单已打开 (SerialId={m_MainMenuSerialId})");
        }
    }

    /// <summary>
    /// 每帧更新。
    /// 检查主菜单的开始按钮是否被点击。
    /// </summary>
    protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds,
        float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

        // 检查主菜单的开始按钮是否被点击
        if (m_UIComponent != null && m_MainMenuSerialId > 0)
        {
            var uiForm = m_UIComponent.GetUIForm(m_MainMenuSerialId);
            if (uiForm?.Logic is MainMenuForm mainMenu && mainMenu.StartRequested)
            {
                GD.Print("[MenuProcedure] 开始游戏！");

                // 关闭主菜单界面
                m_UIComponent.CloseUIForm(m_MainMenuSerialId);
                GD.Print("  [MenuProcedure] 主菜单已关闭");

                // 触发流程切换事件
                if (GF.Event != null)
                {
                    GF.Event.Fire(this, TestPhaseChangedEventArgs.Create(
                        "TestMenuProcedure", "TestGameProcedure"));
                }

                ChangeState<TestGameProcedure>(procedureOwner);
            }
        }
    }

    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        GD.Print("[MenuProcedure] OnLeave - 离开菜单流程");
    }
}
