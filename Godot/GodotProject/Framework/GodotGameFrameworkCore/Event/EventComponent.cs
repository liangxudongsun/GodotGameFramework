//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Event;
using System;

namespace GodotGameFramework
{
    /// <summary>
    /// 事件组件。
    /// </summary>
    public sealed partial class EventComponent : GameFrameworkComponent
    {
        /// <summary>
        /// 核心层的事件管理器实例。
        /// 通过 GameFrameworkEntry.GetModule 获取。
        /// </summary>
        private IEventManager m_EventManager = null;

        /// <summary>
        /// 获取当前已注册的事件处理函数总数。
        /// </summary>
        public int EventHandlerCount => m_EventManager.EventHandlerCount;

        /// <summary>
        /// 获取当前队列中的事件数量。
        /// </summary>
        public int EventCount => m_EventManager.EventCount;

        public override void OnInit()
        {
            base.OnInit();
            m_EventManager = GameFrameworkEntry.GetModule<IEventManager>();
            if (m_EventManager == null)
            {
                Log.Fatal("Event manager is invalid.");
                return;
            }
        }


        /// <summary>
        /// 获取指定事件类型的处理函数数量。
        /// </summary>
        /// <param name="id">事件类型编号</param>
        /// <returns>处理函数数量</returns>
        public int Count(int id)
        {
            return m_EventManager.Count(id);
        }

        /// <summary>
        /// 检查是否存在指定的事件处理函数。
        /// </summary>
        /// <param name="id">事件类型编号</param>
        /// <param name="handler">要检查的事件处理函数</param>
        /// <returns>是否存在</returns>
        public bool Check(int id, EventHandler<GameEventArgs> handler)
        {
            return m_EventManager.Check(id, handler);
        }

        /// <summary>
        /// 订阅事件处理回调函数。
        ///
        /// 当指定 ID 的事件被触发时，handler 会被调用。
        /// </summary>
        /// <param name="id">事件类型编号</param>
        /// <param name="handler">要订阅的事件处理回调函数</param>
        public void Subscribe(int id, EventHandler<GameEventArgs> handler)
        {
            m_EventManager.Subscribe(id, handler);
        }

        /// <summary>
        /// 取消订阅事件处理回调函数。
        /// </summary>
        /// <param name="id">事件类型编号</param>
        /// <param name="handler">要取消订阅的事件处理回调函数</param>
        public void Unsubscribe(int id, EventHandler<GameEventArgs> handler)
        {
            m_EventManager.Unsubscribe(id, handler);
        }

        /// <summary>
        /// 设置默认事件处理函数。
        /// 当某个事件没有任何处理器时，会调用默认处理器。
        /// </summary>
        /// <param name="handler">默认事件处理函数</param>
        public void SetDefaultHandler(EventHandler<GameEventArgs> handler)
        {
            m_EventManager.SetDefaultHandler(handler);
        }

        /// <summary>
        /// 触发事件（线程安全模式）。
        ///
        /// 这个操作是线程安全的，即使不在主线程中触发，
        /// 也能保证在主线程中回调事件处理函数。
        /// 事件会在触发后的下一帧分发。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public void Fire(object sender, GameEventArgs e)
        {
            m_EventManager.Fire(sender, e);
        }

        /// <summary>
        /// 触发事件（立即模式）。
        ///
        /// 这个操作不是线程安全的，事件会立刻分发。
        /// 只在确定处于主线程时使用。
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        public void FireNow(object sender, GameEventArgs e)
        {
            m_EventManager.FireNow(sender, e);
        }
    }
}
