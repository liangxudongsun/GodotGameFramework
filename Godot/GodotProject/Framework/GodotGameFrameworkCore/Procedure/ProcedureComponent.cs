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
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 流程组件。
    /// </summary>
    public sealed partial class ProcedureComponent : GameFrameworkComponent
    {
        public static class Parameters
        {
            public static readonly string Procedures = "Procedures";
            public static readonly string EnterProcedure = "EnterProcedure";
            public static readonly string CurrentProcedure = "CurrentProcedure";
        }
        /// <summary>
        /// 核心层的流程管理器实例。
        /// </summary>
        private IProcedureManager m_ProcedureManager = null;

        /// <summary>
        /// 入口流程实例引用。
        /// </summary>
        private ProcedureBase m_EntranceProcedure = null;

        /// <summary>
        /// 入口流程的类型名称。
        /// </summary>
        [Export]
        public string EnterProcedure = null;

        /// <summary>
        /// 获取当前正在运行的流程。
        /// </summary>
        public ProcedureBase CurrentProcedure => m_ProcedureManager.CurrentProcedure;

        /// <summary>
        /// 获取当前流程的持续时间（秒）。
        /// 从进入当前流程开始计时。
        /// </summary>
        public float CurrentProcedureTime => m_ProcedureManager.CurrentProcedureTime;
        [Export]
        public string[] Procedures;
        public override void OnInit()
        {
            base.OnInit();
            m_ProcedureManager = GameFrameworkEntry.GetModule<IProcedureManager>();
            if (m_ProcedureManager == null)
            {
                Log.Fatal("Procedure manager is invalid.");
                return;
            }
            LoadProcedures();
        }

        /// <summary>
        /// 初始化流程系统。
        /// 通过反射获得所有继承自 ProcedureBase 的类，
        /// 然后初始化流程管理器并启动入口流程。
        /// </summary>
        public void LoadProcedures()
        {
            if (Procedures == null || Procedures.Length <= 0)
            {
                Log.Fatal("Available procedure type names is invalid.");
                return;
            }

            IFsmManager fsmManager = GameFrameworkEntry.GetModule<IFsmManager>();
            if (fsmManager == null)
            {
                Log.Fatal("FSM manager is invalid.");
                return;
            }

            ProcedureBase[] procedureList = new ProcedureBase[Procedures.Length];
            for (int i = 0; i < Procedures.Length; i++)
            {
                Type procedureType = Type.GetType(Procedures[i]);
                if (procedureType == null)
                {
                    Log.Error("Procedure type name '{0}' is invalid.", Procedures[i]);
                    return;
                }

                if (!typeof(ProcedureBase).IsAssignableFrom(procedureType))
                {
                    Log.Error("{0} is not ProcedureBase type.", procedureType);
                    return;
                }
                procedureList[i] = (ProcedureBase)Activator.CreateInstance(procedureType);
                if (procedureList[i] == null)
                {
                    Log.Error("Can not create procedure instance.");
                    return;
                }

                if (procedureType.FullName == EnterProcedure)
                {
                    m_EntranceProcedure = procedureList[i];
                }
            }

            if (m_EntranceProcedure == null)
            {
                Log.Error("Entrance procedure is invalid.");
                return;
            }

            // 初始化流程管理器
            m_ProcedureManager.Initialize(fsmManager, procedureList);
        }
        /// <summary>
        /// 手动开始流程
        /// </summary>
        public void StartProcedure()
        {
            if (m_EntranceProcedure == null)
            {
                Log.Error("Entrance procedure is invalid.");
                return;
            }
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
