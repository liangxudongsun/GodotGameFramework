using GameFramework;
using Godot;
using System;
namespace GodotGameFramework
{
	/// <summary>
	/// Godot组件基类 生命周期回调
	/// </summary>
	public partial class GodotComponent : Node
	{
		#region 原生生命周期回调
		public override void _EnterTree()
		{
			ChildEnteredTree += OnChildEnteredTree;
			ChildExitingTree += OnChildExitingTree;
			OnInit();
		}

		public override void _Ready()
		{
			OnEnter();
		}

		public override void _Process(double delta)
		{
			OnUpdate(delta);
		}

		public override void _PhysicsProcess(double delta)
		{
			OnPhysicsUpdate(delta);
		}

		public override void _ExitTree()
		{
			ChildEnteredTree -= OnChildEnteredTree;
			ChildExitingTree -= OnChildExitingTree;
			OnExitTree();
		}
		#endregion

		#region 输入系统回调
		public override void _Input(InputEvent @event)
		{
			OnInput(@event);
		}

		public override void _UnhandledInput(InputEvent @event)
		{
			OnUnhandledInput(@event);
		}

		// ✅ 修复：Godot 4.6 签名为 InputEvent，不是 InputEventKey
		public override void _UnhandledKeyInput(InputEvent @event)
		{
			if (@event is InputEventKey keyEvent)
				OnUnhandledKeyInput(keyEvent);
		}

		// ✅ 修复：Godot 4.6 签名为 InputEvent，不是 InputEventShortcut
		public override void _ShortcutInput(InputEvent @event)
		{
			if (@event is InputEventShortcut shortcutEvent)
				OnShortcutInput(shortcutEvent);
		}
		#endregion

		#region 通知系统分发
		public override void _Notification(int what)
		{
			base._Notification(what);
			switch (what)
			{
				case (int)NotificationPostEnterTree:
					OnPostEnterTree();
					break;
				case (int)NotificationParented:
					OnParented();
					break;
				case (int)NotificationUnparented:
					OnUnparented();
					break;
				// ✅ 修复：Godot 4.6 C# 未暴露 NotificationRenamed 常量，使用数值 21
				case 21:
					OnRenamed();
					break;
				case (int)NotificationChildOrderChanged:
					OnChildOrderChanged();
					break;
				case (int)NotificationPaused:
					OnPaused();
					break;
				case (int)NotificationUnpaused:
					OnUnpaused();
					break;
				case (int)NotificationPredelete:
					OnPreDestroy();
					break;
			}
		}
		#endregion

		#region 编辑器与属性系统
		public override Variant _Get(StringName property)
		{
			return OnGetProperty(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			return OnSetProperty(property, value);
		}

		public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
		{
			return OnGetPropertyList();
		}

		public override void _ValidateProperty(Godot.Collections.Dictionary property)
		{
			OnValidateProperty(property);
		}

		// ✅ 修复：返回类型必须是 string[]
		public override string[] _GetConfigurationWarnings()
		{
			var warning = OnGetConfigurationWarning();
			if (!string.IsNullOrEmpty(warning))
				return [warning];
			return [];
		}

		public override bool _PropertyCanRevert(StringName property)
		{
			return OnPropertyCanRevert(property);
		}

		public override Variant _PropertyGetRevert(StringName property)
		{
			return OnPropertyGetRevert(property);
		}
		#endregion

		#region 生命周期虚方法
		/// <summary>
		/// 节点就绪时触发（仅一次，所有子节点已进入树）。
		/// 对应 Godot _Ready 阶段。
		/// </summary>
		public virtual void OnEnter()
		{
		}

		/// <summary>
		/// 节点进入场景树时触发（可多次触发）。
		/// 对应 Godot _EnterTree 阶段。
		/// </summary>
		public virtual void OnInit()
		{
		}

		/// <summary>
		/// 每帧更新
		/// </summary>
		public virtual void OnUpdate(double delta)
		{
		}

		/// <summary>
		/// 物理帧更新
		/// </summary>
		public virtual void OnPhysicsUpdate(double delta)
		{
		}

		/// <summary>
		/// 节点退出场景树时触发
		/// </summary>
		public virtual void OnExitTree()
		{
		}
		#endregion

		#region 输入系统虚方法
		/// <summary>
		/// 输入事件回调，所有输入事件最先到达这里
		/// </summary>
		public virtual void OnInput(InputEvent @event)
		{
		}

		/// <summary>
		/// 未被GUI/其他节点消耗的输入事件
		/// </summary>
		public virtual void OnUnhandledInput(InputEvent @event)
		{
		}

		/// <summary>
		/// 未被消耗的键盘按键事件
		/// </summary>
		public virtual void OnUnhandledKeyInput(InputEventKey @event)
		{
		}

		/// <summary>
		/// 快捷键输入事件
		/// </summary>
		public virtual void OnShortcutInput(InputEventShortcut @event)
		{
		}
		#endregion

		#region 场景树事件虚方法
		/// <summary>
		/// 进入场景树后、Ready前触发
		/// </summary>
		public virtual void OnPostEnterTree()
		{
		}

		/// <summary>
		/// 被设置父节点时触发
		/// </summary>
		public virtual void OnParented()
		{
		}

		/// <summary>
		/// 被移除父节点时触发
		/// </summary>
		public virtual void OnUnparented()
		{
		}

		/// <summary>
		/// 子节点进入场景树时触发（通过信号连接）
		/// </summary>
		public virtual void OnChildEnteredTree(Node node)
		{
		}

		/// <summary>
		/// 子节点退出场景树时触发（通过信号连接）
		/// </summary>
		public virtual void OnChildExitingTree(Node node)
		{
		}

		/// <summary>
		/// 节点被重命名时触发
		/// </summary>
		public virtual void OnRenamed()
		{
		}

		/// <summary>
		/// 子节点顺序发生变化时触发
		/// </summary>
		public virtual void OnChildOrderChanged()
		{
		}

		/// <summary>
		/// 场景树暂停时触发
		/// </summary>
		public virtual void OnPaused()
		{
		}

		/// <summary>
		/// 场景树恢复时触发
		/// </summary>
		public virtual void OnUnpaused()
		{
		}

		/// <summary>
		/// 节点被销毁前触发
		/// </summary>
		public virtual void OnPreDestroy()
		{
		}
		#endregion

		#region 属性与编辑器虚方法
		/// <summary>
		/// 编辑器中返回节点配置警告信息
		/// </summary>
		public virtual string OnGetConfigurationWarning()
		{
			return string.Empty;
		}

		/// <summary>
		/// 自定义导出属性列表，返回属性字典数组
		/// </summary>
		public virtual Godot.Collections.Array<Godot.Collections.Dictionary> OnGetPropertyList()
		{
			return new Godot.Collections.Array<Godot.Collections.Dictionary>();
		}

		/// <summary>
		/// 获取自定义属性值
		/// </summary>
		public virtual Variant OnGetProperty(StringName property)
		{
			return default;
		}

		/// <summary>
		/// 设置自定义属性值
		/// </summary>
		public virtual bool OnSetProperty(StringName property, Variant value)
		{
			return false;
		}

		/// <summary>
		/// 验证属性元数据，property 字典包含 name、type 等字段
		/// </summary>
		public virtual void OnValidateProperty(Godot.Collections.Dictionary property)
		{
		}

		/// <summary>
		/// 属性是否支持还原默认值
		/// </summary>
		public virtual bool OnPropertyCanRevert(StringName property)
		{
			return false;
		}

		/// <summary>
		/// 获取属性还原的默认值
		/// </summary>
		public virtual Variant OnPropertyGetRevert(StringName property)
		{
			return default;
		}
		#endregion

		public static Node Create<T>(Node parent = null) where T : Node
		{
			var node = Activator.CreateInstance(typeof(T)) as Node;
			if (parent != null)
				parent.AddChild(node);
			return node;
		}
		public static Node Create(Type type, Node parent = null)
		{
			var node = Activator.CreateInstance(type) as Node;
			if (parent != null)
				parent.AddChild(node);
			return node;
		}
		public static Node Create(string type, Node parent = null)
		{
			var resolvedType = Utility.Assembly.GetType(type);
			if (resolvedType == null)
			{
				Log.Error("Can not find type '{0}'.", type);
				return null;
			}
			var node = Activator.CreateInstance(resolvedType) as Node;
			if (parent != null)
				parent.AddChild(node);
			return node;
		}
		public static void Destroy(Node node)
		{
			if (node == null) return;
			node.QueueFree();
		}
	}
}
