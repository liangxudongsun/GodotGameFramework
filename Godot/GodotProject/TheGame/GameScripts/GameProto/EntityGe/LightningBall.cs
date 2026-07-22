using GameFramework.UI;
using Godot;
using GodotGameFramework.UI;
using GodotGameFramework;
using System;
using System.Collections.Generic;
using GameFramework.Entity;
namespace GameLogic
{
	/// <summary>
	/// 实体,生成时会被覆盖，请勿手动修改
	/// </summary>
	public partial class LightningBall : Area2D, IEntity
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



	}
}
