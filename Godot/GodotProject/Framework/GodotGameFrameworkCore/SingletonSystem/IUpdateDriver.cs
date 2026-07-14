using Godot;
using System;
namespace GodotGameFrameworkCore.SingletonSystem
{
    public interface IUpdateDriver
    {
        #region 注入Godot 的Update驱动

        /// <summary>
        /// 为给外部提供的 添加帧更新事件。
        /// </summary>
        /// <param name="action"></param>
        void AddUpdateListener(Action<double> action);

        /// <summary>
        /// 为给外部提供的 添加物理帧更新事件。
        /// </summary>
        /// <param name="action"></param>
        void AddFixedUpdateListener(Action<double> action);

        /// <summary>
        /// 移除帧更新事件。
        /// </summary>
        /// <param name="action"></param>
        void RemoveUpdateListener(Action<double> action);

        /// <summary>
        /// 移除物理帧更新事件。
        /// </summary>
        /// <param name="action"></param>
        void RemoveFixedUpdateListener(Action<double> action);

        #endregion
    }
}

