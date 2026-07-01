using GameFramework.Entity;
using Godot;
using System;
namespace GodotGameFramework
{
    [GlobalClass]
    public abstract partial class AbstractRb2DEntity : RigidBody2D, IEntity
    {
        #region Base
        /// <summary>
        /// 获取实体编号。
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// 获取实体资源名称（PackedScene 路径）。
        /// </summary>
        public string EntityAssetName { get; private set; }

        /// <summary>
        /// 获取实体实例。
        /// </summary>
        public object Handle => this;

        /// <summary>
        /// 获取实体所属的实体组。
        /// </summary>
        public IEntityGroup EntityGroup { get; private set; }
        #endregion

        /// <summary>
        /// 实体初始化。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="entityAssetName">实体资源名称。</param>
        /// <param name="entityGroup">实体所属的实体组。</param>
        /// <param name="isNewInstance">是否是新实例。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
        {
            Id = entityId;
            EntityAssetName = entityAssetName;
            Name = GameFramework.Utility.Text.Format("Entity_{0}_{1}", entityId, entityAssetName);
            EntityGroup = entityGroup;
        }

        /// <summary>
        /// 实体回收。
        /// Entity 节点不销毁，等待对象池复用或池释放。
        /// </summary>
        public virtual void OnRecycle()
        {
            Id = 0;
            EntityAssetName = null;
            Name = "Entity (Recycled)";
            Visible = false;
            Position = Vector2.Zero;
            Sleeping = true;
        }

        /// <summary>
        /// 实体显示。
        /// </summary>
        public virtual void OnShow(object userData)
        {
            Visible = true;
            Sleeping = false;
        }

        /// <summary>
        /// 实体隐藏。
        /// </summary>
        public virtual void OnHide(bool isShutdown, object userData)
        {
            Visible = false;

        }

        /// <summary>
        /// 实体附加子实体。
        /// </summary>
        public virtual void OnAttached(IEntity childEntity, object userData)
        {

        }

        /// <summary>
        /// 实体解除子实体。
        /// </summary>
        public virtual void OnDetached(IEntity childEntity, object userData)
        {

        }

        /// <summary>
        /// 实体被附加到父实体。
        /// </summary>
        public virtual void OnAttachTo(IEntity parentEntity, object userData)
        {

        }

        /// <summary>
        /// 实体从父实体解除。
        /// </summary>
        public virtual void OnDetachFrom(IEntity parentEntity, object userData)
        {

        }

        /// <summary>
        /// 实体轮询。
        /// 每帧调用
        /// </summary>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {

        }
    }
}

