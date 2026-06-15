//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Fsm;
using System;
using System.Collections.Generic;

namespace GodotGameFramework
{
    /// <summary>
    /// 有限状态机组件。
    ///
    /// 直接透传核心层的 IFsmManager，提供有限状态机的创建、销毁和查询功能。
    /// FSM（Finite State Machine）是框架中流程管理（Procedure）的基础。
    ///
    /// 使用方式：
    /// <code>
    /// // 获取组件
    /// FsmComponent fsmComp = GF.Fsm;
    ///
    /// // 创建状态机
    /// IFsm&lt;MyOwner&gt; fsm = fsmComp.CreateFsm(owner,
    ///     new StateIdle(),
    ///     new StateRunning()
    /// );
    ///
    /// // 检查状态机是否存在
    /// bool exists = fsmComp.HasFsm&lt;MyOwner&gt;();
    ///
    /// // 销毁状态机
    /// fsmComp.DestroyFsm&lt;MyOwner&gt;();
    /// </code>
    ///
    /// </summary>
    public sealed partial class FsmComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的状态机管理器实例。
        /// </summary>
        private IFsmManager m_FsmManager = null;

        /// <summary>
        /// 获取当前有限状态机的数量。
        /// </summary>
        public int Count => m_FsmManager.Count;

        /// <summary>
        /// 节点初始化回调。
        /// 从核心框架获取 IFsmManager 实例。
        /// </summary>
        public override void _Ready()
        {
            base._Ready();

            m_FsmManager = GameFrameworkEntry.GetModule<IFsmManager>();
            if (m_FsmManager == null)
            {
                Log.Fatal("FSM manager is invalid.");
                return;
            }
        }

        /// <summary>
        /// 检查是否存在有限状态机（按持有者类型）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <returns>是否存在</returns>
        public bool HasFsm<T>() where T : class
        {
            return m_FsmManager.HasFsm<T>();
        }

        /// <summary>
        /// 检查是否存在有限状态机（按持有者类型）。
        /// </summary>
        /// <param name="ownerType">有限状态机持有者类型</param>
        /// <returns>是否存在</returns>
        public bool HasFsm(Type ownerType)
        {
            return m_FsmManager.HasFsm(ownerType);
        }

        /// <summary>
        /// 检查是否存在有限状态机（按持有者类型和名称）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <param name="name">有限状态机名称</param>
        /// <returns>是否存在</returns>
        public bool HasFsm<T>(string name) where T : class
        {
            return m_FsmManager.HasFsm<T>(name);
        }

        /// <summary>
        /// 检查是否存在有限状态机（按持有者类型和名称）。
        /// </summary>
        /// <param name="ownerType">有限状态机持有者类型</param>
        /// <param name="name">有限状态机名称</param>
        /// <returns>是否存在</returns>
        public bool HasFsm(Type ownerType, string name)
        {
            return m_FsmManager.HasFsm(ownerType, name);
        }

        /// <summary>
        /// 获取有限状态机（按持有者类型）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <returns>状态机实例</returns>
        public IFsm<T> GetFsm<T>() where T : class
        {
            return m_FsmManager.GetFsm<T>();
        }

        /// <summary>
        /// 获取有限状态机（按持有者类型）。
        /// </summary>
        /// <param name="ownerType">有限状态机持有者类型</param>
        /// <returns>状态机基类实例</returns>
        public FsmBase GetFsm(Type ownerType)
        {
            return m_FsmManager.GetFsm(ownerType);
        }

        /// <summary>
        /// 获取有限状态机（按持有者类型和名称）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <param name="name">有限状态机名称</param>
        /// <returns>状态机实例</returns>
        public IFsm<T> GetFsm<T>(string name) where T : class
        {
            return m_FsmManager.GetFsm<T>(name);
        }

        /// <summary>
        /// 获取有限状态机（按持有者类型和名称）。
        /// </summary>
        /// <param name="ownerType">有限状态机持有者类型</param>
        /// <param name="name">有限状态机名称</param>
        /// <returns>状态机基类实例</returns>
        public FsmBase GetFsm(Type ownerType, string name)
        {
            return m_FsmManager.GetFsm(ownerType, name);
        }

        /// <summary>
        /// 获取所有有限状态机。
        /// </summary>
        /// <returns>所有状态机数组</returns>
        public FsmBase[] GetAllFsms()
        {
            return m_FsmManager.GetAllFsms();
        }

        /// <summary>
        /// 获取所有有限状态机。
        /// </summary>
        /// <param name="results">存储结果的列表</param>
        public void GetAllFsms(List<FsmBase> results)
        {
            m_FsmManager.GetAllFsms(results);
        }

        /// <summary>
        /// 创建有限状态机。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <param name="owner">有限状态机持有者实例</param>
        /// <param name="states">状态集合</param>
        /// <returns>创建的状态机</returns>
        public IFsm<T> CreateFsm<T>(T owner, params FsmState<T>[] states) where T : class
        {
            return m_FsmManager.CreateFsm(owner, states);
        }

        /// <summary>
        /// 创建有限状态机（带名称）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <param name="name">状态机名称</param>
        /// <param name="owner">持有者实例</param>
        /// <param name="states">状态集合</param>
        /// <returns>创建的状态机</returns>
        public IFsm<T> CreateFsm<T>(string name, T owner, params FsmState<T>[] states) where T : class
        {
            return m_FsmManager.CreateFsm(name, owner, states);
        }

        /// <summary>
        /// 创建有限状态机（使用 List）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <param name="owner">持有者实例</param>
        /// <param name="states">状态列表</param>
        /// <returns>创建的状态机</returns>
        public IFsm<T> CreateFsm<T>(T owner, List<FsmState<T>> states) where T : class
        {
            return m_FsmManager.CreateFsm(owner, states);
        }

        /// <summary>
        /// 创建有限状态机（带名称，使用 List）。
        /// </summary>
        /// <typeparam name="T">有限状态机持有者类型</typeparam>
        /// <param name="name">状态机名称</param>
        /// <param name="owner">持有者实例</param>
        /// <param name="states">状态列表</param>
        /// <returns>创建的状态机</returns>
        public IFsm<T> CreateFsm<T>(string name, T owner, List<FsmState<T>> states) where T : class
        {
            return m_FsmManager.CreateFsm(name, owner, states);
        }

        /// <summary>
        /// 销毁有限状态机（按持有者类型）。
        /// </summary>
        /// <typeparam name="T">持有者类型</typeparam>
        /// <returns>是否销毁成功</returns>
        public bool DestroyFsm<T>() where T : class
        {
            return m_FsmManager.DestroyFsm<T>();
        }

        /// <summary>
        /// 销毁有限状态机（按持有者类型）。
        /// </summary>
        /// <param name="ownerType">持有者类型</param>
        /// <returns>是否销毁成功</returns>
        public bool DestroyFsm(Type ownerType)
        {
            return m_FsmManager.DestroyFsm(ownerType);
        }

        /// <summary>
        /// 销毁有限状态机（按持有者类型和名称）。
        /// </summary>
        /// <typeparam name="T">持有者类型</typeparam>
        /// <param name="name">状态机名称</param>
        /// <returns>是否销毁成功</returns>
        public bool DestroyFsm<T>(string name) where T : class
        {
            return m_FsmManager.DestroyFsm<T>(name);
        }

        /// <summary>
        /// 销毁有限状态机（按持有者类型和名称）。
        /// </summary>
        /// <param name="ownerType">持有者类型</param>
        /// <param name="name">状态机名称</param>
        /// <returns>是否销毁成功</returns>
        public bool DestroyFsm(Type ownerType, string name)
        {
            return m_FsmManager.DestroyFsm(ownerType, name);
        }

        /// <summary>
        /// 销毁有限状态机（按实例）。
        /// </summary>
        /// <typeparam name="T">持有者类型</typeparam>
        /// <param name="fsm">要销毁的状态机</param>
        /// <returns>是否销毁成功</returns>
        public bool DestroyFsm<T>(IFsm<T> fsm) where T : class
        {
            return m_FsmManager.DestroyFsm(fsm);
        }

        /// <summary>
        /// 销毁有限状态机（按基类实例）。
        /// </summary>
        /// <param name="fsm">要销毁的状态机</param>
        /// <returns>是否销毁成功</returns>
        public bool DestroyFsm(FsmBase fsm)
        {
            return m_FsmManager.DestroyFsm(fsm);
        }
    }
}
