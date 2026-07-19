using GameConfig.Constant;
using GameFramework.Entity;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.NodePool;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;
using System;
namespace GameLogic
{
	/// <summary>
	/// 子弹初始化数据
	/// </summary>
	public struct BulletData
	{
		/// <summary>飞行方向（单位向量）</summary>
		public Vector2 Direction;
		/// <summary>是否是玩家发射的子弹</summary>
		public bool IsPlayerBullet;
		/// <summary>速度（像素/秒）</summary>
		public float Speed;
	}
	/// <summary>
	/// 界面逻辑（此文件仅在首次生成时创建，之后不会被覆盖）。
	/// </summary>
	public partial class GanTanEntity
	{
		private float m_LifeTime = 0f;
		private Vector2 m_Direction = Vector2.Up;
		private bool m_IsPlayerBullet = true;
		private float m_Speed = 300f;
		private bool m_HasHit = false;

		/// <summary>
		/// 防止 HideEntity 后 Id=0 又被 OnUpdate/BodyEntered 重复调用
		/// </summary>
		private bool m_IsDead = false;
		/// <summary>
		/// 实体初始化。
		/// </summary>
		/// <param name="entityId">实体编号。</param>
		/// <param name="entityAssetName">实体资源名称。</param>
		/// <param name="entityGroup">实体所属的实体组。</param>
		/// <param name="isNewInstance">是否是新实例。</param>
		/// <param name="userData">用户自定义数据。</param>
		public void OnInit(int entityId, string entityAssetName, IEntityGroup entityGroup, bool isNewInstance, object userData)
		{
			#region 框架逻辑
			Id = entityId;
			EntityAssetName = entityAssetName;
			Name = GameFramework.Utility.Text.Format("Entity_{0}_{1}", entityId, entityAssetName);
			EntityGroup = entityGroup;
			#endregion

			if (isNewInstance)
			{
				BodyEntered += OnBodyEntered;
			}
		}

		/// <summary>
		/// 实体回收。
		/// Entity 节点不销毁，等待对象池复用或池释放。
		/// </summary>
		public void OnRecycle()
		{
			Id = 0;
			EntityAssetName = null;
			Name = "Entity (Recycled)";
			Visible = false;
		}

		/// <summary>
		/// 实体显示。
		/// </summary>
		public void OnShow(object userData)
		{
			Visible = true;
			// 重置状态
			m_LifeTime = 0f;
			m_HasHit = false;
			m_IsDead = false;
			m_Direction = Vector2.Up;
			m_IsPlayerBullet = true;
			m_Speed = 300f;

			// 从 userData 读取子弹参数
			if (userData is BulletData data)
			{
				m_Direction = data.Direction.Normalized();
				m_IsPlayerBullet = data.IsPlayerBullet;
				m_Speed = data.Speed > 0 ? data.Speed : m_Speed;
				// Node2D.Rotation 单位是弧度，Vector2.Angle() 即 Atan2(Y, X)
				Rotation = m_Direction.Angle();
			}
		}

		/// <summary>
		/// 实体隐藏。
		/// </summary>
		public void OnHide(bool isShutdown, object userData)
		{
			Visible = false;
		}

		/// <summary>
		/// 实体附加子实体。
		/// </summary>
		public void OnAttached(IEntity childEntity, object userData)
		{

		}

		/// <summary>
		/// 实体解除子实体。
		/// </summary>
		public void OnDetached(IEntity childEntity, object userData)
		{

		}

		/// <summary>
		/// 实体被附加到父实体。
		/// </summary>
		public void OnAttachTo(IEntity parentEntity, object userData)
		{

		}

		/// <summary>
		/// 实体从父实体解除。
		/// </summary>
		public void OnDetachFrom(IEntity parentEntity, object userData)
		{

		}

		/// <summary>
		/// 实体轮询。
		/// 每帧调用，用于处理实体逻辑。
		/// </summary>
		public void OnUpdate(float elapseSeconds, float realElapseSeconds)
		{
			if (m_IsDead) return;

			m_LifeTime += elapseSeconds;
			Position += m_Direction * m_Speed * elapseSeconds;

			// 超时自动销毁
			if (m_LifeTime > 8f)
			{
				m_IsDead = true;
				GF.Entity.HideEntity(this);
			}
		}

		/// <summary>
		/// 碰撞检测：当子弹碰触到 PhysicsBody2D 时触发
		/// </summary>
		private void OnBodyEntered(Node body)
		{
			if (m_HasHit || m_IsDead) return;
			if (body == null) return;

			// 只对 ActorEntity 造成伤害
			if (body is ActorEntity actor && !actor.IsDead)
			{
				// 玩家子弹 → 敌人；敌人子弹 → 玩家
				if (m_IsPlayerBullet && actor.Team == EntityTeam.Enemy)
				{
					m_HasHit = true;
					m_IsDead = true;
					actor.Hurt(Id, 20);
					GF.Entity.HideEntity(this);
					GF.Sound.PlaySFX(ResourcesCollectionConstant.SFX_Dead);
					var d = NodePool.Get<DamagePop>(ResourcesCollectionConstant.UIs_DamagePop, actor);
					d?.SetText(actor.GlobalPosition, 20);
				}
				else if (!m_IsPlayerBullet && actor.Team == EntityTeam.Player)
				{
					m_HasHit = true;
					m_IsDead = true;
					actor.Hurt(Id, 15);
					GF.Entity.HideEntity(this);
					GF.Sound.PlaySFX(ResourcesCollectionConstant.SFX_Dead);
				}
			}
		}
	}
}
