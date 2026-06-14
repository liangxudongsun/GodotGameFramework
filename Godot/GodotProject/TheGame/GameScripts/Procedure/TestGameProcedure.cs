//------------------------------------------------------------
// 游戏流程（GameProcedure）
// 点击方块小游戏的主要游戏逻辑，包含 FSM 状态管理
// Phase 5: 集成 UI 系统（GameHUD / PauseMenu / GameOver）
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using GameFramework;
using GameFramework.DataNode;
using GameFramework.DataTable;
using GameFramework.Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Localization;
using GameFramework.Procedure;
using GameFramework.Sound;
using Godot;
using GodotGameFramework;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
///
/// 这是点击方块小游戏的核心流程，使用嵌套 FSM 管理游戏状态：
/// - ReadyState：3 秒倒计时，打开 GameHUD
/// - PlayingState：方块生成、游戏计时，更新 HUD
/// - PauseState：暂停游戏，显示 PauseMenu
/// - GameOverState：显示 GameOver UI，等待重新开始
///
/// 框架特性展示：
/// - Phase 1: Log, Event, FSM, Procedure
/// - Phase 2: Config, DataTable, Setting, DataNode, ObjectPool
/// - Phase 3: Resource（通过 EntityComponent 间接加载）
/// - Phase 4: Entity（ShowEntity/HideEntity + 对象池复用）
/// - Phase 5: UI（OpenUIForm/CloseUIForm + UIFormLogic + UI 分组）
/// - Phase 5+: UIItem 对象池, userData 传参, OnPause/OnResume, Depth 排序
/// - Phase 6: Sound（BGM 循环播放/暂停/恢复/淡出 + SFX 音效）
/// - Phase 7: Localization（GetString + Tr() TranslationServer 桥接）
/// </summary>
public class TestGameProcedure : ProcedureBase
{
    // ================================================================
    //  组件引用
    // ================================================================

    private EntityComponent m_EntityComponent;
    private EventComponent m_EventComponent;
    private ConfigComponent m_ConfigComponent;
    private DataTableComponent m_DataTableComponent;
    private DataNodeComponent m_DataNodeComponent;
    private SettingComponent m_SettingComponent;
    private FsmComponent m_FsmComponent;
    private UIComponent m_UIComponent;
    private SoundComponent m_SoundComponent;
    private LocalizationComponent m_LocalizationComponent;

    // ================================================================
    //  游戏状态
    // ================================================================

    /// <summary>当前分数。</summary>
    private int m_Score;

    /// <summary>游戏计时器（秒）。</summary>
    private float m_GameTimer;

    /// <summary>方块生成计时器（秒）。</summary>
    private float m_SpawnTimer;

    /// <summary>下一个实体 ID。</summary>
    private int m_NextEntityId;

    /// <summary>游戏状态 FSM。</summary>
    private IFsm<TestGameProcedure> m_GameStateFsm;

    /// <summary>当前活跃的方块实体 ID 集合。</summary>
    private HashSet<int> m_ActiveEntityIds = new HashSet<int>();

    // ================================================================
    //  UI 序列号
    // ================================================================

    /// <summary>GameHUD 界面序列号。</summary>
    private int m_HUDSerialId;

    /// <summary>PauseMenu 界面序列号。</summary>
    private int m_PauseSerialId;

    /// <summary>GameOver 界面序列号。</summary>
    private int m_GameOverSerialId;

    /// <summary>TestOverlay 界面序列号列表（Feature 3+4: 暂停/恢复 + 深度排序）。</summary>
    private readonly List<int> m_OverlaySerialIds = new();

    /// <summary>背景音乐序列号（Phase 6: 音频系统测试）。</summary>
    private int m_BGMSerialId;

    // ================================================================
    //  配置参数（从 ConfigComponent 加载）
    // ================================================================

    private int m_MaxBlocks = 8;
    private float m_SpawnInterval = 1.5f;
    private int m_GameDuration = 30;
    private float m_PlayAreaWidth = 800f;
    private float m_PlayAreaHeight = 600f;

    // ================================================================
    //  公开属性（供 FSM 状态访问）
    // ================================================================

    /// <summary>当前分数。</summary>
    public int Score => m_Score;

    /// <summary>游戏计时器。</summary>
    public float GameTimer => m_GameTimer;

    /// <summary>方块生成计时器。</summary>
    public float SpawnTimer => m_SpawnTimer;

    /// <summary>游戏时长（秒）。</summary>
    public int GameDuration => m_GameDuration;

    /// <summary>方块生成间隔（秒）。</summary>
    public float SpawnInterval => m_SpawnInterval;

    /// <summary>活跃方块 ID 集合。</summary>
    public HashSet<int> ActiveEntityIds => m_ActiveEntityIds;

    // ================================================================
    //  Procedure 生命周期
    // ================================================================

    /// <summary>
    /// 状态初始化（只调用一次）。
    /// 获取组件引用，订阅事件。
    /// </summary>
    protected internal override void OnInit(ProcedureOwner procedureOwner)
    {
        base.OnInit(procedureOwner);

        m_EntityComponent = GF.Entity;
        m_EventComponent = GF.Event;
        m_ConfigComponent = GF.Config;
        // m_DataTableComponent = GF.DataTable;
        m_DataNodeComponent = GF.DataNode;
        m_SettingComponent = GF.Setting;
        m_FsmComponent = GF.Fsm;
        m_UIComponent = GF.UI;
        m_SoundComponent = GF.Sound;
        m_LocalizationComponent = GF.Localization;
    }

    /// <summary>
    /// 进入流程。
    /// 加载配置、重置游戏状态、创建并启动游戏状态 FSM。
    /// </summary>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);

        GD.Print("\n[GameProcedure] OnEnter - 进入游戏流程");

        // 加载配置参数
        if (m_ConfigComponent != null)
        {
            m_MaxBlocks = m_ConfigComponent.GetInt("MaxBlocks", 8);
            m_SpawnInterval = m_ConfigComponent.GetFloat("SpawnInterval", 1.5f);
            m_GameDuration = m_ConfigComponent.GetInt("GameDuration", 30);
            m_PlayAreaWidth = m_ConfigComponent.GetFloat("PlayAreaWidth", 800f);
            m_PlayAreaHeight = m_ConfigComponent.GetFloat("PlayAreaHeight", 600f);
        }

        // 重置游戏状态
        m_Score = 0;
        m_GameTimer = 0f;
        m_SpawnTimer = 0f;
        m_NextEntityId = 1;
        m_ActiveEntityIds.Clear();
        m_HUDSerialId = 0;
        m_PauseSerialId = 0;
        m_GameOverSerialId = 0;
        m_OverlaySerialIds.Clear();

        // 订阅事件
        if (m_EventComponent != null)
        {
            m_EventComponent.Subscribe(ScoreChangedEventArgs.EventId, OnScoreChangedEvent);
            m_EventComponent.Subscribe(BlockClickedEventArgs.EventId, OnBlockClickedEvent);
            m_EventComponent.Subscribe(ShowEntitySuccessEventArgs.EventId, OnShowEntitySuccessEvent);
            m_EventComponent.Subscribe(ShowEntityFailureEventArgs.EventId, OnShowEntityFailureEvent);
            m_EventComponent.Subscribe(HideEntityCompleteEventArgs.EventId, OnHideEntityCompleteEvent);
        }

        // 通过 DataNode 存储运行时游戏状态
        if (m_DataNodeComponent != null)
        {
            m_DataNodeComponent.SetData("Game.Score", (VarInt32)0);
            m_DataNodeComponent.SetData("Game.ActiveBlocks", (VarInt32)0);
            m_DataNodeComponent.SetData("Game.State", (VarString)"Ready");
            m_DataNodeComponent.SetData("Game.TimeRemaining", (VarInt32)m_GameDuration);

            int highScore = m_SettingComponent?.GetInt("HighScore", 0) ?? 0;
            m_DataNodeComponent.SetData("Game.HighScore", (VarInt32)highScore);
        }

        // 创建游戏状态 FSM（新增 PauseState）
        if (m_FsmComponent != null)
        {
            m_GameStateFsm = m_FsmComponent.CreateFsm("GameState", this,
                new GameReadyState(),
                new GamePlayingState(),
                new GamePauseState(),
                new GameOverState());

            m_GameStateFsm.Start<GameReadyState>();
            GD.Print("[GameProcedure] 游戏状态 FSM 已创建并启动（含 PauseState）");
        }
    }

    /// <summary>
    /// 每帧更新。
    /// </summary>
    protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds,
        float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

        // 检查重新开始标志
        if (m_GameStateFsm != null && m_GameStateFsm.HasData("Restart"))
        {
            VarBoolean restartFlag = m_GameStateFsm.GetData<VarBoolean>("Restart");
            if (restartFlag != null && restartFlag.Value)
            {
                GD.Print("[GameProcedure] 重新开始游戏...");

                if (m_FsmComponent != null)
                {
                    m_FsmComponent.DestroyFsm(m_GameStateFsm);
                    m_GameStateFsm = null;
                }

                // 清理 UI
                if (m_UIComponent != null)
                {
                    m_UIComponent.CloseAllLoadedUIForms();
                }

                if (m_DataNodeComponent != null)
                {
                    m_DataNodeComponent.RemoveNode("Game.Score");
                    m_DataNodeComponent.RemoveNode("Game.ActiveBlocks");
                    m_DataNodeComponent.RemoveNode("Game.State");
                    m_DataNodeComponent.RemoveNode("Game.TimeRemaining");
                }

                ChangeState<TestMenuProcedure>(procedureOwner);
                return;
            }
        }
    }

    /// <summary>
    /// 离开流程。
    /// </summary>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);

        // 取消订阅事件
        if (m_EventComponent != null)
        {
            m_EventComponent.Unsubscribe(ScoreChangedEventArgs.EventId, OnScoreChangedEvent);
            m_EventComponent.Unsubscribe(BlockClickedEventArgs.EventId, OnBlockClickedEvent);
            m_EventComponent.Unsubscribe(ShowEntitySuccessEventArgs.EventId, OnShowEntitySuccessEvent);
            m_EventComponent.Unsubscribe(ShowEntityFailureEventArgs.EventId, OnShowEntityFailureEvent);
            m_EventComponent.Unsubscribe(HideEntityCompleteEventArgs.EventId, OnHideEntityCompleteEvent);
        }

        // 隐藏所有实体
        if (m_EntityComponent != null)
        {
            m_EntityComponent.HideAllLoadedEntities();
        }

        // 销毁游戏 FSM
        if (m_FsmComponent != null && m_GameStateFsm != null)
        {
            m_FsmComponent.DestroyFsm(m_GameStateFsm);
            m_GameStateFsm = null;
        }

        m_ActiveEntityIds.Clear();
        m_OverlaySerialIds.Clear();

        GD.Print("[GameProcedure] OnLeave - 离开游戏流程");
    }

    // ================================================================
    //  事件处理
    // ================================================================

    private void OnScoreChangedEvent(object sender, GameEventArgs e)
    {
        ScoreChangedEventArgs args = (ScoreChangedEventArgs)e;
        m_Score += args.ScoreDelta;

        if (m_DataNodeComponent != null)
        {
            m_DataNodeComponent.SetData("Game.Score", (VarInt32)m_Score);
        }

        string sign = args.ScoreDelta > 0 ? "+" : "";
        GD.Print($"  [Game] 分数变化: {sign}{args.ScoreDelta}, 总分: {m_Score}");
    }

    private void OnBlockClickedEvent(object sender, GameEventArgs e)
    {
        // Feature 1: UIItem 对象池测试 — 获取 GameHUDForm 并显示分数弹出
        // 注意：ScoreChangedEventArgs 先于 BlockClickedEventArgs 触发，所以 m_Score 已是更新后的值
        BlockClickedEventArgs args = (BlockClickedEventArgs)e;

        // Phase 6: 播放点击方块音效
        if (m_SoundComponent != null)
        {
            m_SoundComponent.PlaySFX("res://AAAGame/Audio/点击方块音效.mp3");
        }

        if (m_UIComponent != null)
        {
            GameHUDForm hud = m_UIComponent.GetUIForm<GameHUDForm>(m_HUDSerialId);
            if (hud != null)
            {
                hud.ShowScorePopup(args.ScoreDelta, m_Score);
            }
        }
    }

    private void OnShowEntitySuccessEvent(object sender, GameEventArgs e)
    {
        ShowEntitySuccessEventArgs args = (ShowEntitySuccessEventArgs)e;
        if (args.Entity != null)
        {
            m_ActiveEntityIds.Add(args.Entity.Id);

            if (m_DataNodeComponent != null)
            {
                m_DataNodeComponent.SetData("Game.ActiveBlocks", (VarInt32)m_ActiveEntityIds.Count);
            }
        }
    }

    private void OnShowEntityFailureEvent(object sender, GameEventArgs e)
    {
        ShowEntityFailureEventArgs args = (ShowEntityFailureEventArgs)e;
        m_ActiveEntityIds.Remove(args.EntityId);
        Log.Warning("Show entity failed, id '{0}', asset '{1}', error: {2}",
            args.EntityId, args.EntityAssetName, args.ErrorMessage);
    }

    private void OnHideEntityCompleteEvent(object sender, GameEventArgs e)
    {
        HideEntityCompleteEventArgs args = (HideEntityCompleteEventArgs)e;
        m_ActiveEntityIds.Remove(args.EntityId);

        if (m_DataNodeComponent != null)
        {
            m_DataNodeComponent.SetData("Game.ActiveBlocks", (VarInt32)m_ActiveEntityIds.Count);
        }
    }

    // ================================================================
    //  方块生成
    // ================================================================

    public void TrySpawnBlock()
    {
        if (m_EntityComponent == null) return;
        if (m_ActiveEntityIds.Count >= m_MaxBlocks) return;

        int entityId = m_NextEntityId++;

        float x = (float)GD.RandRange(50, (double)(m_PlayAreaWidth - 50));
        float y = (float)GD.RandRange(50, (double)(m_PlayAreaHeight - 50));
        Vector2 pos = new Vector2(x, y);

        bool isScoreBlock = GD.Randf() < 0.6f;

        // ob blockData = null;
        // IDataTable<BlockTypeData> blockTable = null;// m_DataTableComponent?.GetDataTable<BlockTypeData>();
        // if (blockTable != null)
        // {
        //     blockData = blockTable.GetDataRow(isScoreBlock ? 1 : 2);
        // }

        // if (isScoreBlock)
        // {
        //     // 从 BlockTypeData 读取分值和颜色（数据驱动）
        //     int score = blockData?.Score ?? 10;
        //     Color color = blockData != null
        //         ? new Color(blockData.ColorR, blockData.ColorG, blockData.ColorB)
        //         : new Color(0.2f, 0.8f, 0.2f);
        //     var spawnData = new BlockSpawnData(pos, blockData?.Id ?? 1, score, color, blockData?.Lifetime ?? 0f);
        //     m_EntityComponent.ShowEntity<ScoreBlockLogic>(entityId,
        //         "res://AAAGame/Entity/ScoreBlock.tscn", "BlockGroup", spawnData);
        // }
        // else
        // {
        //     // 从 BlockTypeData 读取分值和颜色（数据驱动）
        //     int score = blockData?.Score ?? -5;
        //     Color color = blockData != null
        //         ? new Color(blockData.ColorR, blockData.ColorG, blockData.ColorB)
        //         : new Color(0.9f, 0.2f, 0.2f);
        //     var spawnData = new BlockSpawnData(pos, blockData?.Id ?? 2, score, color, blockData?.Lifetime ?? 3.0f);
        //     m_EntityComponent.ShowEntity<RedBlockLogic>(entityId,
        //         "res://AAAGame/Entity/RedBlock.tscn", "BlockGroup", spawnData);
        // }
    }

    // ================================================================
    //  游戏计时（供 FSM 状态调用）
    // ================================================================

    public void AddGameTime(float delta) { m_GameTimer += delta; }
    public void AddSpawnTime(float delta) { m_SpawnTimer += delta; }
    public void ResetSpawnTime() { m_SpawnTimer = 0f; }

    // ================================================================
    //  游戏状态 FSM 状态类
    // ================================================================

    /// <summary>
    /// 准备状态。
    /// 3 秒倒计时后切换到 Playing 状态。
    /// Phase 5: 打开 GameHUD 界面。
    /// </summary>
    private class GameReadyState : FsmState<TestGameProcedure>
    {
        private float m_ReadyTimer;
        private int m_LastCountdown;

        protected internal override void OnEnter(IFsm<TestGameProcedure> fsm)
        {
            base.OnEnter(fsm);
            m_ReadyTimer = 0f;
            m_LastCountdown = 3;

            var proc = fsm.Owner;

            if (proc.m_DataNodeComponent != null)
            {
                proc.m_DataNodeComponent.SetData("Game.State", (VarString)"Ready");
            }

            // Phase 5: 打开 GameHUD 界面（Normal 组）
            if (proc.m_UIComponent != null)
            {
                proc.m_HUDSerialId = proc.m_UIComponent.OpenUIForm(
                    "res://AAAGame/UI/GameHUD.tscn", "Normal");
                GD.Print($"  [GameState] GameHUD 已打开 (SerialId={proc.m_HUDSerialId})");
            }

            // Phase 7: 验证本地化字典（已在 LaunchProcedure 中加载）
            if (proc.m_LocalizationComponent != null)
            {
                GD.Print($"  [Phase 7] Localization: {proc.m_LocalizationComponent.Language}, {proc.m_LocalizationComponent.DictionaryCount} entries");

                // 测试 GetString API（UGF 风格）
                string title = proc.m_LocalizationComponent.GetString("GameTitle");
                string scoreFmt = proc.m_LocalizationComponent.GetString("ScoreFormat", 100);
                string blocksFmt = proc.m_LocalizationComponent.GetString("BlockCountFormat", 3, 8);
                GD.Print($"  [Phase 7] GetString('GameTitle') = {title}");
                GD.Print($"  [Phase 7] GetString('ScoreFormat', 100) = {scoreFmt}");
                GD.Print($"  [Phase 7] GetString('BlockCountFormat', 3, 8) = {blocksFmt}");

                // 测试 Tr() 桥接（Godot TranslationServer）
                string trTitle = TranslationServer.Translate("GameTitle");
                GD.Print($"  [Phase 7] TranslationServer.Translate('GameTitle') = {trTitle}");

                // 测试 HasRawString
                GD.Print($"  [Phase 7] HasRawString('GameTitle') = {proc.m_LocalizationComponent.HasRawString("GameTitle")}");
            }

            GD.Print("\n[GameState] Ready — 准备开始！");
            GD.Print("  3...");
        }

        protected internal override void OnUpdate(IFsm<TestGameProcedure> fsm, float elapseSeconds,
            float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

            m_ReadyTimer += elapseSeconds;
            int countdown = 3 - (int)m_ReadyTimer;

            if (countdown != m_LastCountdown && countdown > 0)
            {
                m_LastCountdown = countdown;
                GD.Print($"  {countdown}...");

                // 更新 DataNode 中的倒计时显示（使用 CountdownFormat 本地化）
                var proc = fsm.Owner;
                if (proc.m_DataNodeComponent != null)
                {
                    string countdownText = proc.m_LocalizationComponent != null
                        ? proc.m_LocalizationComponent.GetString("CountdownFormat", countdown)
                        : $"{countdown}...";
                    proc.m_DataNodeComponent.SetData("Game.State", (VarString)countdownText);
                }
            }

            if (m_ReadyTimer >= 3f)
            {
                ChangeState<GamePlayingState>(fsm);
            }
        }
    }

    /// <summary>
    /// 游戏进行状态。
    /// 按间隔生成方块，倒计时游戏时长。
    /// Phase 5: 更新 DataNode 供 HUD 读取，支持 Esc 暂停。
    /// </summary>
    private class GamePlayingState : FsmState<TestGameProcedure>
    {
        private int m_LastLoggedTime;

        /// <summary>上一帧的帧号（用于 T/R 键防抖）。</summary>
        private ulong m_LastTFrame;
        private ulong m_LastRFrame;

        protected internal override void OnEnter(IFsm<TestGameProcedure> fsm)
        {
            base.OnEnter(fsm);
            m_LastLoggedTime = 0;
            m_LastTFrame = 0;
            m_LastRFrame = 0;

            var proc = fsm.Owner;
            if (proc.m_DataNodeComponent != null)
            {
                proc.m_DataNodeComponent.SetData("Game.State", (VarString)"Playing");
            }

            GD.Print("\n[GameState] Playing — GO! 点击方块得分！");
            GD.Print("  提示: T=测试覆盖层(暂停/深度)  Esc=暂停菜单  R=Refocus第一个覆盖层");

            // Phase 6: 播放背景音乐（循环）
            if (proc.m_SoundComponent != null)
            {
                var bgmParams = PlaySoundParams.Create();
                bgmParams.Loop = true;
                bgmParams.VolumeInSoundGroup = 0.5f;  // BGM 音量较低
                proc.m_BGMSerialId = proc.m_SoundComponent.PlaySound(
                    "res://AAAGame/Audio/背景音乐.mp3", "Music", bgmParams);
                GD.Print($"  [Phase 6] BGM 已开始播放 (SerialId={proc.m_BGMSerialId})");
            }
        }

        protected internal override void OnUpdate(IFsm<TestGameProcedure> fsm, float elapseSeconds,
            float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

            var proc = fsm.Owner;
            ulong frame = (ulong)Engine.GetProcessFrames();

            // Phase 5: 检测 Escape 键 → 暂停
            if (Input.IsActionJustPressed("ui_cancel"))
            {
                GD.Print("  [GameState] Esc 按下 → 暂停游戏");
                ChangeState<GamePauseState>(fsm);
                return;
            }

            // Feature 3+4: 检测 T 键 → 切换 TestOverlay（帧防抖）
            if (Input.IsPhysicalKeyPressed(Key.T) && frame != m_LastTFrame)
            {
                m_LastTFrame = frame;

                if (proc.m_UIComponent != null)
                {
                    var overlays = proc.m_UIComponent.GetUIForms("res://AAAGame/UI/TestOverlay.tscn");
                    if (overlays != null && overlays.Length > 0)
                    {
                        // 关闭最后一个（最顶层的）
                        int lastSerialId = overlays[overlays.Length - 1].SerialId;
                        proc.m_UIComponent.CloseUIForm(lastSerialId);
                        proc.m_OverlaySerialIds.Remove(lastSerialId);
                        GD.Print($"  [GameState] TestOverlay 已关闭 (SerialId={lastSerialId}), 剩余: {proc.m_OverlaySerialIds.Count}");
                    }
                    else
                    {
                        // 打开新的 TestOverlay（Normal 组, PauseCoveredUIForm=true）
                        // 这会触发同组 GameHUD 的 OnPause（框架自动调用）
                        int serialId = proc.m_UIComponent.OpenUIForm(
                            "res://AAAGame/UI/TestOverlay.tscn", "Normal",
                            true);  // pauseCoveredUIForm = true
                        proc.m_OverlaySerialIds.Add(serialId);
                        GD.Print($"  [GameState] TestOverlay 已打开 (SerialId={serialId}), 总计: {proc.m_OverlaySerialIds.Count}");
                        GD.Print("  [GameState] → GameHUD 应收到 OnPause（被同组覆盖）");
                    }
                }
            }

            // Feature 4: 检测 R 键 → Refocus 第一个 TestOverlay（帧防抖）
            if (Input.IsPhysicalKeyPressed(Key.R) && frame != m_LastRFrame && proc.m_OverlaySerialIds.Count > 0)
            {
                m_LastRFrame = frame;

                int firstSerialId = proc.m_OverlaySerialIds[0];
                proc.m_UIComponent?.RefocusUIForm(firstSerialId);
                GD.Print($"  [GameState] Refocus TestOverlay (SerialId={firstSerialId}) → 移至顶部");
            }

            proc.AddGameTime(elapseSeconds);
            proc.AddSpawnTime(elapseSeconds);

            // 检查游戏是否结束
            if (proc.GameTimer >= proc.GameDuration)
            {
                ChangeState<GameOverState>(fsm);
                return;
            }

            // 按间隔生成方块
            if (proc.SpawnTimer >= proc.SpawnInterval)
            {
                proc.ResetSpawnTime();
                proc.TrySpawnBlock();
            }

            // Phase 5: 更新 DataNode 中的剩余时间（供 HUD 读取）
            if (proc.m_DataNodeComponent != null)
            {
                int remaining = proc.GameDuration - (int)proc.GameTimer;
                proc.m_DataNodeComponent.SetData("Game.TimeRemaining", (VarInt32)Math.Max(0, remaining));
            }

            // 每 5 秒输出一次状态
            int elapsed = (int)proc.GameTimer;
            if (elapsed > 0 && elapsed / 5 > m_LastLoggedTime / 5)
            {
                int remaining = proc.GameDuration - elapsed;
                GD.Print($"  [Game] 剩余 {remaining}s | 分数: {proc.Score} | 活跃方块: {proc.ActiveEntityIds.Count}");
            }
            m_LastLoggedTime = elapsed;
        }
    }

    /// <summary>
    /// 暂停状态。
    /// Phase 5: 打开 PauseMenu 界面（Popup 组），游戏速度由 PauseMenuForm 控制。
    /// </summary>
    private class GamePauseState : FsmState<TestGameProcedure>
    {
        protected internal override void OnEnter(IFsm<TestGameProcedure> fsm)
        {
            base.OnEnter(fsm);

            var proc = fsm.Owner;

            if (proc.m_DataNodeComponent != null)
            {
                proc.m_DataNodeComponent.SetData("Game.State", (VarString)"Paused");
            }

            // Phase 5: 打开暂停菜单（Popup 组）
            // PauseMenuForm.OnOpen 会调用 PauseGame() 设置 TimeScale=0
            // Phase 6: 暂停背景音乐
            if (proc.m_SoundComponent != null && proc.m_BGMSerialId > 0)
            {
                proc.m_SoundComponent.PauseSound(proc.m_BGMSerialId, 0.5f);
                GD.Print("  [Phase 6] BGM 已暂停（带 0.5s 淡出）");
            }

            if (proc.m_UIComponent != null)
            {
                proc.m_PauseSerialId = proc.m_UIComponent.OpenUIForm(
                    "res://AAAGame/UI/PauseMenu.tscn", "Popup");
                GD.Print($"  [GameState] PauseMenu 已打开 (SerialId={proc.m_PauseSerialId})");
            }

            GD.Print("  [GameState] 游戏已暂停");
        }

        protected internal override void OnUpdate(IFsm<TestGameProcedure> fsm, float elapseSeconds,
            float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

            var proc = fsm.Owner;

            // 检测 Escape 键 → 恢复游戏
            // 注意：TimeScale=0 时 Input 仍可检测（Godot Input 不受 TimeScale 影响）
            if (Input.IsActionJustPressed("ui_cancel"))
            {
                GD.Print("  [GameState] Esc 按下 → 恢复游戏");

                // Phase 6: 恢复背景音乐（带 0.5s 淡入）
                if (proc.m_SoundComponent != null && proc.m_BGMSerialId > 0)
                {
                    proc.m_SoundComponent.ResumeSound(proc.m_BGMSerialId, 0.5f);
                    GD.Print("  [Phase 6] BGM 已恢复（带 0.5s 淡入）");
                }

                // 关闭暂停菜单
                // PauseMenuForm.OnClose 会调用 ResumeGame() 恢复 TimeScale
                if (proc.m_UIComponent != null && proc.m_PauseSerialId > 0)
                {
                    proc.m_UIComponent.CloseUIForm(proc.m_PauseSerialId);
                    proc.m_PauseSerialId = 0;
                }

                // 更新 DataNode 状态
                if (proc.m_DataNodeComponent != null)
                {
                    proc.m_DataNodeComponent.SetData("Game.State", (VarString)"Playing");
                }

                ChangeState<GamePlayingState>(fsm);
            }
        }
    }

    /// <summary>
    /// 游戏结束状态。
    /// Phase 5: 关闭 GameHUD，打开 GameOver 界面（Popup 组）。
    /// </summary>
    private class GameOverState : FsmState<TestGameProcedure>
    {
        private float m_EndTimer;

        protected internal override void OnEnter(IFsm<TestGameProcedure> fsm)
        {
            base.OnEnter(fsm);
            m_EndTimer = 0f;

            var proc = fsm.Owner;

            if (proc.m_DataNodeComponent != null)
            {
                proc.m_DataNodeComponent.SetData("Game.State", (VarString)"GameOver");
            }

            // 隐藏所有剩余实体
            proc.m_EntityComponent?.HideAllLoadedEntities();
            proc.ActiveEntityIds.Clear();

            // Phase 6: 停止背景音乐（带 1s 淡出）
            if (proc.m_SoundComponent != null && proc.m_BGMSerialId > 0)
            {
                proc.m_SoundComponent.StopSound(proc.m_BGMSerialId, 1f);
                proc.m_BGMSerialId = 0;
                GD.Print("  [Phase 6] BGM 已停止（带 1s 淡出）");
            }

            // Feature 3+4: 关闭所有 TestOverlay
            foreach (int overlayId in proc.m_OverlaySerialIds)
            {
                proc.m_UIComponent?.CloseUIForm(overlayId);
            }
            proc.m_OverlaySerialIds.Clear();

            // Phase 5: 关闭 GameHUD
            if (proc.m_UIComponent != null && proc.m_HUDSerialId > 0)
            {
                proc.m_UIComponent.CloseUIForm(proc.m_HUDSerialId);
                proc.m_HUDSerialId = 0;
                GD.Print("  [GameState] GameHUD 已关闭");
            }

            // 读取历史高分
            int highScore = 0;
            if (proc.m_SettingComponent != null)
            {
                highScore = proc.m_SettingComponent.GetInt("HighScore", 0);
            }
            if (proc.m_DataNodeComponent != null)
            {
                VarInt32 highScoreVar = proc.m_DataNodeComponent.GetData<VarInt32>("Game.HighScore");
                if (highScoreVar != null && highScoreVar.Value > highScore)
                {
                    highScore = highScoreVar.Value;
                }
            }

            // 检查是否打破记录
            bool newHighScore = proc.Score > highScore;
            if (newHighScore)
            {
                highScore = proc.Score;

                if (proc.m_SettingComponent != null)
                {
                    proc.m_SettingComponent.SetInt("HighScore", highScore);
                    proc.m_SettingComponent.Save();
                }

                if (proc.m_DataNodeComponent != null)
                {
                    proc.m_DataNodeComponent.SetData("Game.HighScore", (VarInt32)highScore);
                }
            }

            // 输出结果
            GD.Print("\n========================================");
            GD.Print("  GAME OVER!");
            GD.Print($"  最终分数: {proc.Score}");
            GD.Print($"  最高记录: {highScore}");
            if (newHighScore && proc.Score > 0)
            {
                GD.Print("  *** 新纪录！***");
            }
            GD.Print("========================================");
            GD.Print("\n  按 Enter 重新开始，或关闭窗口退出。\n");

            // Phase 5+: 打开 GameOver 界面（Popup 组），通过 userData 传递游戏结果
            if (proc.m_UIComponent != null)
            {
                var gameOverData = new GameOverUserData
                {
                    Score = proc.Score,
                    HighScore = highScore,
                    NewRecord = newHighScore
                };
                proc.m_GameOverSerialId = proc.m_UIComponent.OpenUIForm(
                    "res://AAAGame/UI/GameOver.tscn", "Popup",
                    false, gameOverData);
                GD.Print($"  [GameState] GameOver 已打开 (SerialId={proc.m_GameOverSerialId}) [userData 传参]");
            }

            // 打印特性使用总结
            GD.Print("--- 本游戏使用的框架特性 ---");
            GD.Print("  [Phase 1] Log → 游戏日志输出");
            GD.Print("  [Phase 1] Event → ScoreChanged / BlockClicked 事件");
            GD.Print("  [Phase 1] FSM → GameState (Ready→Playing→Pause→GameOver)");
            GD.Print("  [Phase 1] Procedure → Launch→Menu→Game 流程");
            GD.Print("  [Phase 2] Config → GameConfig.txt 游戏参数");
            GD.Print("  [Phase 2] DataTable → BlockTypeData 方块类型");
            GD.Print("  [Phase 2] Setting → HighScore 高分持久化");
            GD.Print("  [Phase 2] DataNode → Game.Score/State 运行时状态");
            GD.Print("  [Phase 2] ObjectPool → 实体/UI 实例池复用");
            GD.Print("  [Phase 3] Resource → 加载方块/UI PackedScene");
            GD.Print("  [Phase 4] Entity → ShowEntity/HideEntity + EntityLogic");
            GD.Print("  [Phase 5] UI → OpenUIForm/CloseUIForm + UIFormLogic");
            GD.Print("  [Phase 5] UI → 分组管理 (Normal/Popup) + 深度排序");
            GD.Print("  [Phase 5] UI → 对象池复用 + 生命周期管理");
            GD.Print("  [Phase 5+] UIItem → SpawnItem/UnspawnItem 对象池复用");
            GD.Print("  [Phase 5+] userData → OpenUIForm 泛型 userData 传参");
            GD.Print("  [Phase 5+] OnPause/OnResume → 同组暂停/恢复机制");
            GD.Print("  [Phase 5+] OnDepthChanged → 深度排序 + RefocusUIForm");
            GD.Print("  [Phase 6] Sound → BGM循环播放/暂停/恢复/淡出 + SFX音效");
            GD.Print("  [Phase 7] Localization → GetString + Tr() TranslationServer 桥接");
            GD.Print("-------------------------------\n");
        }

        protected internal override void OnUpdate(IFsm<TestGameProcedure> fsm, float elapseSeconds,
            float realElapseSeconds)
        {
            base.OnUpdate(fsm, elapseSeconds, realElapseSeconds);

            m_EndTimer += elapseSeconds;

            // 等待 1 秒后才能重新开始（防止误触）
            if (m_EndTimer > 1f && Input.IsActionJustPressed("ui_accept"))
            {
                // 设置重新开始标志
                fsm.SetData("Restart", (VarBoolean)true);
            }
        }
    }
}
