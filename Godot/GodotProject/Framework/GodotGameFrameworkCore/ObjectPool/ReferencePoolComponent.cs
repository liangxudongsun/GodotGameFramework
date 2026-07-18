using GameFramework;
using Godot;
using System;
namespace GodotGameFramework.Reference;
/// <summary>
/// 引用强制检查类型。
/// </summary>
public enum ReferenceStrictCheckType : byte
{
    /// <summary>
    /// 总是启用。
    /// </summary>
    AlwaysEnable = 0,
    /// <summary>
    /// 仅在编辑器中启用。
    /// </summary>
    OnlyEnableInEditor,
    /// <summary>
    /// 仅在开发模式时打开。
    /// </summary>
    OnlyOpenWhenDevelopment,

    /// <summary>
    /// 总是禁用。
    /// </summary>
    AlwaysDisable,
}
public partial class ReferencePoolComponent : GameFrameworkComponent
{
    [Export]
    private ReferenceStrictCheckType m_EnableStrictCheck = ReferenceStrictCheckType.AlwaysEnable;
    /// <summary>
    /// 获取或设置是否开启强制检查。
    /// </summary>
    public bool EnableStrictCheck
    {
        get
        {
            return ReferencePool.EnableStrictCheck;
        }
        set
        {
            ReferencePool.EnableStrictCheck = value;
            if (value)
            {
                Log.Info("Strict checking is enabled for the Reference Pool. It will drastically affect the performance.");
            }
        }
    }
    public override void OnEnter()
    {
        base.OnEnter();
        switch (m_EnableStrictCheck)
        {
            case ReferenceStrictCheckType.AlwaysEnable:
                EnableStrictCheck = true;
                break;
            case ReferenceStrictCheckType.OnlyEnableInEditor:
                EnableStrictCheck = OS.HasFeature("editor");
                break;
            case ReferenceStrictCheckType.OnlyOpenWhenDevelopment:
                EnableStrictCheck = OS.IsDebugBuild();
                break;
            case ReferenceStrictCheckType.AlwaysDisable:
                EnableStrictCheck = false;
                break;
        }
    }

}
