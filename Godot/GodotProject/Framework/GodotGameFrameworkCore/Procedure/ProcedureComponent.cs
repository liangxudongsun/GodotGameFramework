//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Fsm;
using GameFramework.Procedure;
using Godot;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 流程组件。
    ///
    /// 基于有限状态机（FSM）的流程管理组件，直接透传核心层的 IProcedureManager。
    /// 流程（Procedure）是框架中管理游戏生命周期状态的核心机制。
    ///
    /// 工作原理：
    /// 1. 流程本质上是特殊的 FSM 状态，每个流程继承自 ProcedureBase
    /// 2. ProcedureComponent 在 _Ready 时获取核心层 IProcedureManager
    /// 3. 通过 CallDeferred 延迟到所有组件就绪后，自动通过反射创建流程实例
    /// 4. 将所有流程类注册到 FSM，然后启动入口流程
    /// 5. 流程之间通过 ChangeState 切换
    ///
    /// 配置方式（在 Godot Inspector 中设置）：
    /// - AvailableProcedureTypeNames: 所有可用流程的完整类型名（含命名空间）
    /// - EntranceProcedureTypeName: 入口流程的完整类型名
    ///
    /// </summary>
    public sealed partial class ProcedureComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的流程管理器实例。
        /// </summary>
        private IProcedureManager m_ProcedureManager = null;

        /// <summary>
        /// 入口流程实例引用。
        /// </summary>
        private ProcedureBase m_EntranceProcedure = null;

        /// <summary>
        /// 所有可用的流程类型名称（含命名空间的完整名称）。
        /// </summary>
        [Export]
        public string[] AvailableProcedureTypeNames = null;

        /// <summary>
        /// 入口流程的类型名称（含命名空间的完整名称）。
        /// 框架初始化完成后会自动启动此流程。
        /// 必须是 AvailableProcedureTypeNames 中的某一个。
        /// </summary>
        [Export]
        public string EntranceProcedureTypeName = null;

        /// <summary>
        /// 获取当前正在运行的流程。
        /// </summary>
        public ProcedureBase CurrentProcedure => m_ProcedureManager.CurrentProcedure;

        /// <summary>
        /// 获取当前流程的持续时间（秒）。
        /// 从进入当前流程开始计时。
        /// </summary>
        public float CurrentProcedureTime => m_ProcedureManager.CurrentProcedureTime;

        /// <summary>
        /// 节点初始化回调。
        /// 从核心框架获取 IProcedureManager 实例。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_ProcedureManager = GameFrameworkEntry.GetModule<IProcedureManager>();
            if (m_ProcedureManager == null)
            {
                Log.Fatal("Procedure manager is invalid.");
                return;
            }

            // 延迟到所有组件 _Ready 完成后再初始化流程
            // 确保所有核心模块（如 IFsmManager）都已就绪
            CallDeferred(MethodName.InitProcedures);
        }

        /// <summary>
        /// 初始化流程系统。
        ///
        /// 通过反射根据 Inspector 中配置的类型名称创建流程实例，
        /// 然后初始化流程管理器并启动入口流程。
        ///
        /// 对应 UGF 中 ProcedureComponent.Start() 协程的逻辑。
        /// </summary>
        private void InitProcedures()
        {
            if (AvailableProcedureTypeNames == null || AvailableProcedureTypeNames.Length == 0)
            {
                Log.Warning("AvailableProcedureTypeNames is empty, procedure system will not be initialized.");
                return;
            }

            if (string.IsNullOrEmpty(EntranceProcedureTypeName))
            {
                Log.Error("EntranceProcedureTypeName is not set.");
                return;
            }

            // 获取核心层 IFsmManager（Procedure 系统内部依赖 FSM）
            IFsmManager fsmManager = GameFrameworkEntry.GetModule<IFsmManager>();
            if (fsmManager == null)
            {
                Log.Fatal("FSM manager is invalid.");
                return;
            }

            // 通过反射创建所有流程实例
            ProcedureBase[] procedures = new ProcedureBase[AvailableProcedureTypeNames.Length];
            for (int i = 0; i < AvailableProcedureTypeNames.Length; i++)
            {
                Type procedureType = Utility.Assembly.GetType(AvailableProcedureTypeNames[i]);
                if (procedureType == null)
                {
                    Log.Error("Can not find procedure type '{0}'.", AvailableProcedureTypeNames[i]);
                    return;
                }

                procedures[i] = (ProcedureBase)Activator.CreateInstance(procedureType);
                if (procedures[i] == null)
                {
                    Log.Error("Can not create procedure instance '{0}'.", AvailableProcedureTypeNames[i]);
                    return;
                }

                // 记录入口流程
                if (EntranceProcedureTypeName == AvailableProcedureTypeNames[i])
                {
                    m_EntranceProcedure = procedures[i];
                }
            }

            if (m_EntranceProcedure == null)
            {
                Log.Error("Entrance procedure is invalid.");
                return;
            }

            // 初始化流程管理器
            m_ProcedureManager.Initialize(fsmManager, procedures);

            // 启动入口流程
            m_ProcedureManager.StartProcedure(m_EntranceProcedure.GetType());
        }

        /// <summary>
        /// 初始化流程管理器（手动模式）。
        ///
        /// 如果不使用 Inspector 配置，也可以通过代码手动初始化。
        /// 但注意：如果同时在 Inspector 中配置了 AvailableProcedureTypeNames，
        /// 会导致重复初始化。
        /// </summary>
        /// <param name="fsmManager">FSM 管理器</param>
        /// <param name="procedures">所有可用的流程实例</param>
        public void Initialize(IFsmManager fsmManager, params ProcedureBase[] procedures)
        {
            m_ProcedureManager.Initialize(fsmManager, procedures);
        }

        /// <summary>
        /// 是否存在指定类型的流程。
        /// </summary>
        /// <typeparam name="T">流程类型</typeparam>
        /// <returns>是否存在</returns>
        public bool HasProcedure<T>() where T : ProcedureBase
        {
            return m_ProcedureManager.HasProcedure<T>();
        }

        /// <summary>
        /// 是否存在指定类型的流程。
        /// </summary>
        /// <param name="procedureType">流程类型</param>
        /// <returns>是否存在</returns>
        public bool HasProcedure(Type procedureType)
        {
            return m_ProcedureManager.HasProcedure(procedureType);
        }

        /// <summary>
        /// 获取指定类型的流程。
        /// </summary>
        /// <typeparam name="T">流程类型</typeparam>
        /// <returns>流程实例</returns>
        public ProcedureBase GetProcedure<T>() where T : ProcedureBase
        {
            return m_ProcedureManager.GetProcedure<T>();
        }

        /// <summary>
        /// 获取指定类型的流程。
        /// </summary>
        /// <param name="procedureType">流程类型</param>
        /// <returns>流程实例</returns>
        public ProcedureBase GetProcedure(Type procedureType)
        {
            return m_ProcedureManager.GetProcedure(procedureType);
        }

        /// <summary>
        /// 启动指定类型的流程。
        ///
        /// 通常只在初始化时调用一次，后续流程切换通过
        /// ProcedureBase.ChangeState 在流程内部完成。
        /// </summary>
        /// <typeparam name="T">要启动的流程类型</typeparam>
        public void StartProcedure<T>() where T : ProcedureBase
        {
            m_ProcedureManager.StartProcedure<T>();
        }
    }
}
