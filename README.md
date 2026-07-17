# GGF

<div align="center">

**Godot 4.6.2 + C# (.NET 8) Game Framework**

[![Godot Version](https://img.shields.io/badge/Godot-4.6.2-blue?style=flat-square)](https://godotengine.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/NuoYan/GGF?style=flat-square)](GodotProject/LICENSE)
[![GameFramework](https://img.shields.io/badge/GameFramework-2025.07.10-green?style=flat-square)](https://gameframework.cn/)

</div>

---

## 📖 简介

**GGF** (Godot Game Framework) 是 [Game Framework](https://gameframework.cn/)（Jiang Yin）的 **Godot 4.6.2 + .NET 8 C# 移植版**。提供一套完整的模块化游戏开发框架，包含事件、FSM、流程、资源、实体、UI、音频、本地化、对象池、数据表、设置、Web 请求等子系统。

### ✨ 核心特性

- 🧩 **模块化架构** — 14 个独立子系统，高内聚低耦合，可按需替换
- 🔄 **双层架构** — 纯 C# 核心层（无 Godot 依赖）+ Godot 运行时组件层
- 🎯 **直接继承模式** — Entity/UI 脚本直接继承 Godot 原生类型 + 框架接口，无中间基类
- 📊 **数据管线** — 集成 Luban，Excel 配置 → C# 代码 + 二进制数据
- ♻️ **对象池** — 实体、UI、音频等资源自动池化管理复用
- 🔊 **音频系统** — 声音组 + 优先级抢占 + 扩展方法 `PlayBGM()`/`PlaySFX()`
- 📝 **条件日志** — `[Conditional("ENABLE_LOG")]` 编译时零开销移除，编辑器插件可切换
- 🔧 **编辑器插件** — 组件监视、UIForm/Entity 脚本生成器、C# Inspector 增强、AB 包可视化导出管理、AB 包标记与自动打包、日志切换、本地化导出、资源路径常量生成
- 🧬 **单例模式** — 泛型 `SingletonNode<T>` 提供类型安全的 Godot 节点单例
- 📦 **资源包系统** — 基于 AssetBundle 的 .pck 子包管理，支持可视化导出、标记资源目录、构建时自动打包、增量更新、版本清单生成

---

## 📚 目录

- [快速开始](#-快速开始)
- [架构概览](#-架构概览)
- [核心模块](#-核心模块)
- [项目结构](#-项目结构)
- [使用示例](#-使用示例)
- [数据管线](#-数据管线)
- [编辑器插件](#-编辑器插件)
- [系统要求](#-系统要求)
- [开源项目推荐](#-开源项目推荐)
- 📚 **[系统文档索引 (Godot/docs)](Godot/docs/README.md)** — 16 个子系统的深度文档 + 热更设计/审计

---

## 🚀 快速开始

### 环境要求

- **Godot**: 4.6.2+（.NET 版本，Godot .NET SDK 4.7.0）
- **.NET SDK**: 8.0+
- **渲染器**: D3D12（Forward Plus，默认）
- **物理引擎**: Jolt Physics（3D 默认）
- **NuGet**: Newtonsoft.Json 13.0.4

### 快速上手

1. **克隆项目**
   ```bash
   git clone <repo-url>
   cd Godot
   ```

2. **快速编译**
   ```bash
   cd GodotProject
   dotnet build
   ```

3. **打开编辑器**
   ```bash
   "<godot_exe>" --path GodotProject --editor
   ```

4. **添加新 .cs 文件后需执行完整构建**
   ```bash
   "<godot_exe>" --build-solutions --path GodotProject --no-window -q
   ```

> 💡 **Godot 编辑器路径示例**: `E:\Godot\Godot_v4.6.2-stable_mono_win64\...\Godot_v4.6.2-stable_mono_win64.exe`

---

## 🏗️ 架构概览

### 双层架构

```
┌─────────────────────────────────────────────────────┐
│              Godot Runtime Layer                     │
│  GodotGameFrameworkCore/                             │
│  ├── GodotComponent (Node)        生命周期虚方法      │
│  ├── GameFrameworkComponent       自动注册到 GameEntry │
│  ├── GameEntry                    根节点，驱动 Update  │
│  ├── GF                           静态门面            │
│  ├── EntityComponent / UIComponent / SoundComponent   │
│  ├── DefaultEntityHelper / DefaultUIFormHelper 实例化辅助 │
│  ├── SingletonNode<T>             泛型节点单例        │
│  └── PhysicsCheck2D               物理检测工具类      │
├─────────────────────────────────────────────────────┤
│               Pure C# Core Layer                     │
│  GameFramework/                                      │
│  ├── GameFrameworkEntry         模块入口              │
│  ├── GameFrameworkModule        模块基类              │
│  ├── ReferencePool/EventPool    引用池/事件调度        │
│  └── EntityManager/UIManager/... 核心 Manager         │
└─────────────────────────────────────────────────────┘
```

**核心规则：** `GameFramework/` 不引用任何 Godot 类型。`GodotGameFrameworkCore/` 依赖 `GameFramework/` 和 Godot。新增系统应保持此分层。

### 直接继承模式

脚本直接继承 Godot 原生类型 + 框架接口，无需中间基类。脚本生成器生成的 Ge partial 提供框架属性（`IEntity`/`IUIForm` 实现），Logic partial 由用户编写生命周期逻辑：

```
ActorEntity : CharacterBody2D, IEntity, IActor     ← 用户直接继承 Godot 类型 + 框架接口
MainForm (Ge) : Control, IUIForm                   ← 生成器产生的框架样板
MainForm (Logic) : partial                         ← 用户编写的生命周期逻辑
```

实体/UI 的实例化通过 `DefaultEntityHelper` / `DefaultUIFormHelper` 完成，从 `PackedScene` 实例化节点后直接挂载到实体组/UI 组容器。

---

## 🧩 核心模块

> 📚 每个模块都有对应的深度系统文档（架构 / 数据流 / 核心机制 / API / FAQ），完整索引见 **[Godot/docs/README.md](Godot/docs/README.md)**。下方仅为速览。

### 实体模块 (EntityComponent)

> 📖 详细文档：[EntitySystem.md](Godot/docs/EntitySystem.md)

- ✅ 基于 `IEntityManager` 的实体生命周期管理
- ✅ `ShowEntity(EntityId)` Luban 配置驱动，支持对象池复用
- ✅ `ShowEntityAsync<T>()` 异步加载，返回类型安全的 `T : Node, IEntity`
- ✅ 实体组管理（容量、过期时间、优先级可配）
- ✅ 父子实体挂载 + Godot 场景树 Node 关系同步
- ✅ 脚本直接继承 Godot 类型 + `IEntity`，Ge partial 提供框架属性，Logic partial 编写生命周期
- ✅ `DefaultEntityHelper` 从 PackedScene 实例化实体节点，挂载到实体组容器

**当前 TheGame 项目实体层级：**
```
CharacterBody2D + IEntity + IActor
  └── ActorEntity               ← 阵营 (EntityTeam)、血量、PhysicsCheck2D 检测、Die()
       ├── CatEntity            ← 玩家猫：键盘移动、自动瞄准、发射 GanTan
       ├── AngerEntity          ← 敌人
       └── GanTanEntity         ← 弹射物（BulletData：方向/速度/归属，Ge+Logic partial）
```

### UI 模块 (UIComponent)

> 📖 详细文档：[UISystem.md](Godot/docs/UISystem.md)

- ✅ 基于 `IUIManager` 的窗体管理
- ✅ 4 个默认 UI 层级：Background / Normal / Popup / Tips
- ✅ 界面组管理，支持深度排序
- ✅ `OpenUIForm(UIFormId)` Luban 配置驱动
- ✅ Ge partial 提供 `Control, IUIForm` 框架样板，Logic partial 编写生命周期逻辑
- ✅ 自动收集 `IStringKey` 子节点并刷新本地化文本
- ✅ 内置 `Close()` 方法

当前 TheGame UI：`MainForm`、`MenuForm`（Logic partial）、`GameOver`（Logic partial）、`PauseMenuForm`、`TestOverlayForm`、`ScorePopupItem`（UIItemBase 子类）。

### 音频模块 (SoundComponent)

> 📖 详细文档：[SoundSystem.md](Godot/docs/SoundSystem.md)

- ✅ 基于 `ISoundManager` 的音频管理
- ✅ 默认声音组：Music / SFX / UI（通过 LoadEntityGroup 阶段从 `TbSoundConfig` 配置）
- ✅ 优先级抢占算法
- ✅ 淡入/淡出控制
- ✅ 组级静音/音量级联
- ✅ 扩展方法：`GF.Sound.PlayBGM(assetName)` / `GF.Sound.PlaySFX(assetName)`

### 资源模块 (ResourceModule)

> 📖 详细文档：[ResourceSystem.md](Godot/docs/ResourceSystem.md) ｜ 热更审计：[ResourceHotUpdateAudit.md](Godot/docs/ResourceHotUpdateAudit.md)

- ✅ **精简 IResourceManager** — 从 Unity 版本 97 个成员精简为 6 个核心方法，移除所有 Unity 管线遗留代码
- ✅ **Godot 原生异步加载** — `ResourceLoader.LoadThreadedRequest` 后台线程加载，`Queue<LoadAssetTask>` 内部调度管理
- ✅ **同步读写** — 二进制文件通过 `FileAccess` 同步读写，`LoadBinary` 在调用时立即返回结果
- ✅ **子包加载系统** — `Updatable` 模式下由热更流程（`ProcedureUpdate`）下载并加载 `user://subpackages/` 的 .pck 更新包；`Package` 模式仅使用主包（不加载子包）
- ✅ **版本清单** — `GameFrameworkVersion.dat` 记录所有子包名称、大小、SHA256 哈希及 `MinAppVersion`/`ForceUpdate`，用于校验和热更新
- ✅ **多模式设计** — `ResourceMode` 枚举：`Package` / `Updatable` / `UpdatableWhilePlaying`（最后者未实现）
- ✅ `ResourceComponent` 便捷方法：`LoadBinary()`, `LoadText()`（同步）, `LoadAssetAsync<T>()`（异步；场景加载走 `SceneComponent`）
- ✅ `HasAsset()` — 检查资源/二进制文件是否存在
- ✅ **EasySave** — JSON 序列化存储工具（`SaveInUserAsync<T>()` / `LoadInUserAsync<T>()`），用于版本文件持久化

### Web 请求模块 (WebRequestComponent)

> 📖 详细文档：[WebRequestSystem.md](Godot/docs/WebRequestSystem.md)

- ✅ **异步 API** — `SendRequestAsync(url)` 返回 `Task<WebRequestCompleteEventArgs>`，支持 GET / POST
- ✅ **事件驱动** — `SendRequest(url)` 通过 `EventComponent` 推送结果，适合多请求集中处理
- ✅ **超时控制** — 默认 30s，可配置，超时自动取消底层请求
- ✅ **响应解析** — `WebRequestCompleteEventArgs` 提供 `Body`(byte[])、`ResponseCode`、`Headers`、`Url`
- ✅ **纯 C# 层** — `IWebRequestManager` + `WebRequestManager`（TaskPool 驱动）在 `GameFramework/WebRequest/` 中保留

### 下载模块 (DownloadComponent)

> 📖 详细文档：[DownloadSystem.md](Godot/docs/DownloadSystem.md)

- ✅ 任务队列 + 多代理并发（默认 3 agent），优先级 / 标签 / 暂停 / 速度统计
- ✅ 流式下载（64KB 缓冲）+ `.download` 断点续传（HTTP Range，失败自动续传）
- ✅ 无进度超时（默认 30s，非总时长限制）
- ✅ `DownloadFileAsync()` 可 await API：大小 + SHA256 校验，失败返回 false 不抛异常
- ✅ `user://`、`res://` 虚拟路径自动转换
- ✅ 热更流程 `ProcedureUpdate` 的多包并发下载即基于本模块

### 事件模块 (EventComponent)

> 📖 详细文档：[EventSystem.md](Godot/docs/EventSystem.md)

- ✅ 基于 `IEventManager` 的线程安全事件系统
- ✅ 延迟分发（Fire）和立即分发（FireNow）
- ✅ 自定义事件继承 `GameFrameworkEventArgs`

### 流程模块 (ProcedureComponent)

> 📖 详细文档：[ProcedureSystem.md](Godot/docs/ProcedureSystem.md) ｜ 状态机基础：[FsmSystem.md](Godot/docs/FsmSystem.md)

- ✅ 基于 `IFsmManager` 的流程状态机
- ✅ Inspector 配置可用的 Procedure 类型
- ✅ 启动流程链：`ProcedureLaunch`（组件验证）→ `ProcedureUpdate`（热更新检测与下载）→ `ProcedurePrelode`（子包加载、配置、实体组初始化）→ `ProcedureGame`（游戏主循环）
- ✅ 通过 `ChangeState<T>(procedureOwner)` 切换流程

### 数据表模块 (DataTableComponent)

> 📖 详细文档：[DataTableSystem.md](Godot/docs/DataTableSystem.md)

- ✅ Luban 生成的二进制数据反序列化
- ✅ `GF.DataTable` 返回类型安全的 `Tables` 实例
- ✅ 懒加载支持

### 场景模块 (SceneComponent)

> 📖 详细文档：[SceneSystem.md](Godot/docs/SceneSystem.md)

- ✅ 基于 `ISceneManager` 的场景加载/卸载管理
- ✅ `LoadScene(sceneAssetName, priority, userData)` / `UnloadScene(sceneAssetName)`
- ✅ 场景状态查询：`SceneIsLoaded()` / `SceneIsLoading()` / `SceneIsUnloading()`
- ✅ `DefaultSceneHelper` 通过 `ResourceLoader.LoadThreadedRequest` 异步加载场景
- ✅ 事件通知：`LoadSceneSuccess` / `LoadSceneFailure` / `UnloadSceneSuccess` / `UnloadSceneFailure`
- 🚧 场景切换过渡动画、异步加载进度回调待实现

### 其他模块

| 模块 | 速览 | 详细文档 |
|------|------|----------|
| 框架核心 (Base/GF/GameEntry) | 启动序列、组件生命周期、ReferencePool、条件日志 | [FrameworkCore.md](Godot/docs/FrameworkCore.md) |
| 状态机 (FsmComponent) | 泛型 IFsm/FsmState、SetData/GetData、池化销毁 | [FsmSystem.md](Godot/docs/FsmSystem.md) |
| 对象池 (ObjectPoolComponent) | Spawn/Unspawn、容量/过期/优先级、与 ReferencePool 对照 | [ObjectPoolSystem.md](Godot/docs/ObjectPoolSystem.md) |
| 数据结点 (DataNodeComponent) | 树形数据、路径访问、Variable 池化类型 | [DataNodeSystem.md](Godot/docs/DataNodeSystem.md) |
| 设置 (SettingComponent) | ConfigFile → `user://settings.cfg`、Save/Load | [SettingSystem.md](Godot/docs/SettingSystem.md) |
| 本地化 (LocalizationComponent) | TSV 字典、语言切换、IStringKey 自动刷新 | [LocalizationSystem.md](Godot/docs/LocalizationSystem.md) |

### NodeExtension 扩展

> 📖 详细文档：[FrameworkCore.md](Godot/docs/FrameworkCore.md)

`GodotGameFrameworkCore/Utility/NodeExtension.cs` 提供常用 Node 查询扩展方法：
- `FindChildOfType<T>()` — 递归查找子节点
- `FindChildrenOfType<T>()` — 递归查找所有匹配子孙节点
- `GetChild<T>()` / `GetChildren<T>()` / `GetParent<T>()`
- `GetOrAddChild<T>()` — 获取或创建指定类型子节点
- `RemoveAllChildren()` — 移除所有子节点

### SingletonNode&lt;T&gt;

`SingletonSystem/SingletonNode<T> : Node` — 泛型单例模式。在场景树中确保只有一个实例存活：
- `SingletonNode<T>.Instance` 静态属性首次访问时自动创建
- `_Ready()` 检测并销毁重复实例

### PhysicsCheck2D

`Utility/PhysicsCheck2D : IReference` — 封装 `PhysicsDirectSpaceState2D.IntersectShape`：
- 通过 `ReferencePool` 池化复用（`PhysicsCheck2D.Create()` / `ReferencePool.Release()`）
- 自动排除自身节点
- 支持按距离排序、Debug 绘制
- 在实体 `OnUpdate` 中每帧调用检测

---

## 📁 项目结构

```
Configs/                      ← Excel 配置源数据（Luban 管线输入）
  GameConfig/                 ← 表定义 + 业务 Excel
  Localization/               ← 多语言 Excel 源
GodotProject/                 ← Godot 项目根
├── Framework/
│   ├── GameFramework/        ← 纯 C# 框架（无 Godot 依赖）
│   │   ├── Base/             ← 入口、模块、引用池、事件池
│   │   ├── Entity/ UI/ Sound/ Resource/ Event/
│   │   ├── Fsm/ Procedure/ Config/ DataTable/
│   │   ├── DataNode/ ObjectPool/ Setting/
│   │   ├── Localization/ Scene/ Properties/
│   │   ├── Download/ Network/  ← 纯 C# 层已实现，Godot 桥接待实现
│   │   ├── Debugger/            ← 纯 C# 层已实现，Godot 窗口待实现
│   │   └── Utility/ WebRequest/ ← Utility（压缩、加密等）, WebRequest 纯 C# 层
│   └── GodotGameFrameworkCore/  ← Godot 运行时组件
│       ├── Base/             ← GameEntry, GF, GodotComponent, Log
│       ├── Entity/           ← EntityComponent, DefaultEntityHelper
│       ├── UI/               ← UIComponent, DefaultUIFormHelper, IStringKey
│       ├── Sound/            ← SoundComponent + PlayBGM/PlaySFX 扩展
│       ├── Resource/         ← ResourceManager + ResourceComponent + EasySave
│       ├── WebRequest/       ← WebRequestComponent（异步 API + 事件）+ WebRequestAgent
│       ├── Scene/ Localization/
│       ├── Event/ Fsm/ Procedure/ Config/
│       ├── DataTable/ DataNode/ ObjectPool/ Setting/
│       ├── SingletonSystem/  ← SingletonNode<T> 泛型单例
│       ├── Templet/          ← UIForm / Entity 脚本生成模板
│       ├── Variable/         ← VarInt32, VarString, VarBoolean 等
│       ├── Utility/          ← NodeExtension, PhysicsCheck2D
│       ├── Lib/              ← LubanLib (ByteBuf, BeanBase)
│       └── Json/             ← Newtonsoft.Json helper + EasySave
│   └── GameFramework.tscn    ← 主场景
├── TheGame/                  ← 当前活跃游戏项目
│   ├── Entitys/              ← 实体场景 (.tscn)
│   ├── GameScripts/
│   │   ├── Entity/           ← 实体脚本（ActorEntity, CatEntity, AngerEntity, GanTanEntity.Logic）
│   │   ├── UI/               ← UI 脚本（MainForm, MenuForm.Logic, GameOver.Logic, PauseMenuForm, TestOverlayForm, ScorePopupItem）
│   │   ├── Event/            ← 自定义事件参数（BlockClickedEventArgs 等）
│   │   ├── Procedure/        ← 流程（ProcedureLaunch, ProcedureUpdate, ProcedurePrelode, ProcedureGame）
│   │   ├── ObjectPool/       ← 自定义池对象
│   │   ├── Resources/        ← 资源组定义（EntityGroup, SoundGroup, UIGroup）+ 生成配置（ScriptGenerateRes）
│   │   └── GameProto/
│   │       ├── GameConfig/   ← Luban 生成的 C# 数据类
│   │       ├── EntityGe/     ← 实体脚本 Ge（GanTanEntity 等，自动覆盖）
│   │       └── UIGe/         ← UI 脚本 Ge（MenuForm, MainForm, GameOver 等，自动覆盖）
│   ├── DataTables/
│   │   ├── GameConfigs/      ← Luban 生成的二进制配置 (.bytes)
│   │   └── Localizations/    ← 本地化文本 (.txt)
│   ├── Resources/            ← Godot 资源配置（UpdateSettingRes, ScriptGenerateRes 等 .tres）
│   ├── UIs/                  ← UI 场景 (.tscn)
│   ├── Sprites/ Scenes/ Audios/ Fonts/ Themes/
└── addons/                   ← 编辑器插件
    ├── ComponentInsoector/   ← 框架组件监视 + UIForm/Entity 脚本生成器
    ├── ExportInspector/      ← AB 包可视化导出管理面板（C#）
    ├── asset_bundle/         ← AssetBundle 资源标记 + 构建时自动打包（GDScript）
    ├── ezpz_inspector/       ← C# Inspector 增强（Ezpz Inspector v1.2.1）
    ├── TopMenu/              ← 日志级别切换
    ├── LocalizationEditor/   ← Excel → TXT 转换
    └── Resources/            ← 资源路径常量生成
```

---

## 🎯 使用示例

### 实体系统

```csharp
// 1. 定义实体类（直接继承 Godot 类型 + 框架接口）
public partial class CatEntity : ActorEntity
{
    [Export] private Sprite2D m_CatSprite;

    public override void OnInit(int entityId, string entityAssetName,
        IEntityGroup entityGroup, bool isNewInstance, object userData)
    {
        base.OnInit(entityId, entityAssetName, entityGroup, isNewInstance, userData);
        // ActorEntity 直接继承 CharacterBody2D + IEntity + IActor
        m_Config = GF.DataTable.TbCharacterConfig.DataList.FirstOrDefault(x => x.EntityId == EntityId.Cat);
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        // 每帧移动、自动攻击逻辑
    }
}

// 2. 显示实体
int catId = GF.Entity.ShowEntity(EntityId.Cat);

// 3. 异步显示并获取实体引用
CatEntity cat = await GF.Entity.ShowEntityAsync<CatEntity>(EntityId.Cat);
cat.GlobalPosition = new Vector2(100, 200);

// 4. 隐藏实体
GF.Entity.HideEntity(catId);
GF.Entity.HideEntitySafe(catId);
```

### UI 系统

```csharp
// Ge partial（自动生成，覆盖）—— 提供 IUIForm 属性 + [Export] 子节点字段
public partial class MenuForm : Control, IUIForm
{
    [Export] public Button m_StartButton;
    [Export] public Label m_TitleLabel;
    // ... 框架属性（SerialId, UIFormAssetName, UIGroup 等）
}

// Logic partial（仅首次生成，不覆盖）—— 用户生命周期代码
public partial class MenuForm : IStringKey
{
    public void OnInit(int serialId, string uiFormAssetName,
        IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        if (isNewInstance) m_StartButton.Pressed += OnStartButtonPressed;
    }

    public void OnOpen(object userData)
    {
        GF.Sound.PlayBGM(ResourcesCollectionConstant.Music_Menu);
    }

    private void OnStartButtonPressed()
    {
        GF.UI.CloseUIForm(UIFormId.MenuForm);
        GF.UI.OpenUIForm(UIFormId.MainForm);
    }
}

// 打开界面
int menuId = GF.UI.OpenUIForm(UIFormId.MenuForm);
// 或异步
await GF.UI.OpenUIFormAsync<MenuForm>(UIFormId.MenuForm);
```

### 音频系统

```csharp
// 播放背景音乐（Music 组）
int bgmId = GF.Sound.PlayBGM("res://Audio/background.mp3");

// 播放音效（SFX 组）
int sfxId = GF.Sound.PlaySFX("res://Audio/Click.wav");

// 使用完整 PlaySound API
var sfxParams = PlaySoundParams.Create();
sfxParams.VolumeInSoundGroup = 0.8f;
GF.Sound.PlaySound("res://Audio/Shoot.wav", "SFX", sfxParams);

// 停止
GF.Sound.StopSound(bgmId, 1f);
```

### 资源加载

```csharp
// 同步加载文本/二进制文件
string text = GF.Resource.LoadText("res://Data/Config.txt");
byte[] data = GF.Resource.LoadBinary("res://Data/Config.dat");

// 异步加载资源
Godot.Resource res = await GF.Resource.LoadAssetAsync("res://Sprites/Player.png", 0);

// 检查资源是否存在
if (GF.Resource.Exists("res://Scenes/Enemy.tscn")) { }
```

### 事件系统

```csharp
// 定义自定义事件
public sealed class ScoreChangedEventArgs : GameFrameworkEventArgs
{
    public static readonly int EventId = typeof(ScoreChangedEventArgs).GetHashCode();
    public override int Id => EventId;
    public int Score { get; private set; }
}

// 订阅 / 触发
GF.Event.Subscribe(ScoreChangedEventArgs.EventId, OnScoreChanged);
GF.Event.Fire(this, new ScoreChangedEventArgs(100));
```

### PhysicsCheck2D 检测

```csharp
var shape = new CircleShape2D { Radius = 100f };
var check = PhysicsCheck2D.Create(
    this, shape,
    collisionMask: 1,
    maxResults: 16,
    collideWithBodies: true);

if (check.IsColliding())
{
    var sorted = check.GetCollidingNodesSorted();
    // sorted[0] 为最近节点
}

// 使用完毕后归还对象池
ReferencePool.Release(check);
```

### 本地化与日志

```csharp
string title = GF.Localization.GetString("GameTitle");

Log.Info("Player {0} scored {1} points", name, score);
Log.Warning("Health low: {0}", currentHp);
Log.Error("Failed to load: {0}", path);
```

> 💡 日志通过 `[Conditional("ENABLE_LOG")]` 实现编译时移除，在 Godot 编辑器 **Project > Tools > GameFramework** 菜单中可切换日志级别。

---

## 📊 数据管线

> 📖 详细文档：[DataTableSystem.md](Godot/docs/DataTableSystem.md)

集成 **Luban** 配置表解决方案：

```
Configs/GameConfig/Datas/*.xlsx
         │
         │ gen_code_bin_to_project.bat
         ▼
TheGame/GameScripts/GameProto/GameConfig/*.cs   ← C# 数据类（具类型访问）
TheGame/DataTables/GameConfigs/*.bytes             ← 二进制数据（运行时加载）
```

- **源文件**: `__tables__.xlsx`（表定义）、`__beans__.xlsx`（数据结构）、`__enums__.xlsx`（枚举）+ 业务 Excel（实体.xlsx、界面UI.xlsx、角色.xlsx 等）
- **运行时**: `LubanLib/ByteBuf` + `BeanBase` 反序列化
- **入口**: `GF.DataTable` 返回类型安全的 `Tables` 实例
- **完整管线**: `luban.conf` → 自定义模板 → C# 代码 + `.bytes` 数据

---

## 🔧 编辑器插件

| 插件 | 功能 |
|------|------|
| **ComponentInsoector** | 框架组件（Base / Procedure / Scene / Setting / Entity / UI / Sound / Localization）属性监视 + UIForm/Entity 双脚本生成器（详见下方）——自动收集子节点、自动赋值 `[Export]` 字段 |
| **ExportInspector** | AB 包可视化导出管理面板——扫描 AssetBundle 标记文件、展开查看包内资源及导入状态、一键导出 .pck 子包 + 版本清单，支持完整模式（含源文件）和仅产物模式（仅 .ctex/.fontdata/.sample，体积减少 80%+） |
| **asset_bundle** | AssetBundle 资源标记（GDScript）——在资源目录下创建 `AssetBundle.tres` 标记文件，配置是否启用/导出/打包外部依赖/仅导出导入产物；构建时自动通过 export_plugin 将标记目录打包为 .pck |
| **ezpz_inspector** | C# Inspector 增强（Ezpz Inspector v1.2.1 by Dilaura）——通过 C# Attribute 自定义 Inspector 显示，提供 `[ExportButton]`（方法按钮）、`[UpperDescription]`（字段说明）、`[ControlMargin]`、`[ControlSize]`、`[ControlModulateColor]` 等注解 |
| **TopMenu** | 切换日志级别（Debug / Info / Warning / Error / Fatal / 全部关闭） |
| **LocalizationEditor** | `Configs/Localization/*.xlsx` → `res://TheGame/DataTables/Localizations/*.txt` |
| **ResourcesCollection** | 扫描 `res://TheGame/` 非脚本资源，生成 `ResourcesCollectionConstant.cs`（文件路径常量） |

> 💡 TopMenu / LocalizationEditor / ResourcesCollection 通过 **Project > Tools** 菜单调用；ComponentInsoector 和 ezpz_inspector 直接作用于检视面板；ExportInspector 在编辑器底部面板显示；asset_bundle 在构建时自动生效。

### UIForm / Entity 脚本生成

> 📖 详细文档：[UISystem.md](Godot/docs/UISystem.md)（生成器工作流）

选中任意 **Control** 节点或 **实体节点** 后，检视面板会出现 **Generate Script** 按钮，一键脚手架拆分为**双 partial 文件**：

- `<类名>.cs`（`UIOutPutPathGe` / `EntityOutPutPathGe` 目录）— 框架样板（`[Export]` 子节点字段、`IUIForm`/`IEntity` 属性、本地化收集器），**每次生成都覆盖**
- `<类名>.Logic.cs`（`UIOutPutPathLogic` / `EntityOutPutPathLogic` 目录）— 用户生命周期代码（`OnInit` / `OnOpen` / `OnClose` / `OnShow` / `OnHide` …），**仅首次创建，不会覆盖已有逻辑**

**模板占位符:** `_NAMESPACE_` / `_PARENT_` / `_CLASSNAME_` / `_CHILDNODES_`

模板位于 `Framework/GodotGameFrameworkCore/Templet/`：
- UIForm：`UIFormTemplet.txt`（Ge）+ `UIFormLogicTemplet.txt`（Logic）
- Entity：`EntityTemplet.txt`（Ge）+ `EntityLogicTemplet.txt`（Logic）

**配置文件** `TheGame/Resources/ScriptGenerateRes.tres`（`ScriptGenerateRes : Resource`）:

| 字段 | 说明 | 默认值 |
|------|------|--------|
| `NameSpace` | 生成代码的命名空间 | `"GameLogic"` |
| `UIOutPutPathGe` / `EntityOutPutPathGe` | Ge 脚本输出目录 | `"res://TheGame/"` |
| `UIOutPutPathLogic` / `EntityOutPutPathLogic` | Logic 脚本输出目录 | `"res://TheGame/"` |
| `NodePrefix` | 子节点名称前缀（用于自动收集） | `"m_"` |

**子节点自动收集与赋值：** 递归遍历节点树，收集名称以 `NodePrefix`（默认 `m_`）开头的子节点，生成 `[Export] public Type NodeName;` 字段。`SetScript` 之后自动调用 `node.Set(child.Name, child)` 为每个 `[Export]` 字段赋值子节点引用，无需手动拖拽。

---

## 📋 场景树

主场景 `Framework/GameFramework.tscn` 注册为 `run/main_scene`：

```
GameFramework (GameEntry)
├── Base / Resource / Event / Fsm / Procedure
├── Setting / DataTable / DataNode
├── ObjectPool / Entity / UI / Sound / Localization
```

每种组件类型只允许注册一个实例，`GameEntry.RegisterComponent()` 会校验唯一性。

### 启动顺序

1. Godot 加载 `Framework/GameFramework.tscn`
2. `GameFrameworkComponent.OnInit()` → `GameEntry.RegisterComponent(this)`
3. `GameEntry._Process()` 驱动 `GameFrameworkEntry.Update()` 轮询所有模块
4. `GameEntry.CheckProcedure()` 在 `ProcedureComponent` 注册后自动调用 `StartProcedure()`
5. `ProcedureLaunch` 验证组件 → `ProcedureUpdate` 检测更新 → `ProcedurePrelode` 加载子包和配置 → `ProcedureGame`

---

## 🔧 系统要求

### Godot 版本

- **推荐版本**: Godot 4.6.2
- **渲染器**: D3D12（Windows 默认）
- **物理引擎**: Jolt Physics（3D 默认）

### 开发环境

- .NET SDK 8.0+
- Visual Studio 2022+ / Rider / VS Code
- Git

### 构建命令

| 命令 | 用途 |
|------|------|
| `dotnet build` | 日常开发快速编译 |
| `<godot> --build-solutions` | 添加 .cs 文件后必须执行 |
| `<godot> --editor` | 启动编辑器 |

---

## ⚠️ 开发注意事项

### 实体 ID 生成

实体 ID 使用 `Interlocked.Increment` 原子计数器生成，确保无碰撞。

### Async/Task 时序注意

`ShowEntityAsync` 和 `OpenUIFormAsync` 等异步方法依赖 `TaskCompletionSource` 监听底层事件。如果 Manager 在对象池中找到了缓存的实例，事件会**同步触发**。需在调用 Manager 方法**之前**注册 tcs，避免事件先于 tcs 注册导致异步操作永远挂起。

### 组件事件取消订阅

`EntityComponent` 在 `OnExitTree()` 中取消订阅 `IEntityManager` 事件，防止场景重载时内存泄漏和事件重复触发。

### 物理检测工具

`PhysicsCheck2D` 实现了 `IReference` 接口，使用完毕后必须调用 `ReferencePool.Release(check)` 归还对象池。检测时自动用当前帧的 `GlobalTransform` 更新查询位置。

---

## 🌟 开源项目推荐

| 项目 | 描述 | 链接 |
|------|------|------|
| **Game Framework** | 核心框架来源，Unity 游戏框架 by Jiang Yin | [GitHub](https://github.com/EllanJiang/GameFramework) |
| **Luban** | 游戏配置解决方案（Excel → C# + 二进制） | [GitHub](https://github.com/focus-creative-games/luban) |
| **Godot Engine** | 开源游戏引擎 | [GitHub](https://github.com/godotengine/godot) |
| **Jolt Physics** | Godot 使用的 3D 物理引擎 | [GitHub](https://github.com/jrouwe/JoltPhysics) |
| **Newtonsoft.Json** | 高性能 JSON 框架（NuGet 13.0.4） | [GitHub](https://github.com/JamesNK/Newtonsoft.Json) |
| **Ezpz Inspector** | Godot C# Inspector 增强插件 | [GitHub](https://github.com/Calcatz/ezpz-inspector) |
| **CodeGraph** | 代码知识图谱 MCP 工具 | [GitHub](https://github.com/colbymchenry/codegraph) |

---

## 🚧 待实现功能

### 资源系统

- [x] **Updatable 资源模式** — 热更管线已上线（2026-07）：版本比对 → `GF.Download` 并发下载 → SHA256 校验 → 子包加载，详见 [DownloadSystem.md](Godot/docs/DownloadSystem.md) 与 [ResourceHotUpdateAudit.md](Godot/docs/ResourceHotUpdateAudit.md)
- [ ] **UpdatableWhilePlaying 模式** — 边玩边下载，未实现
- [ ] **补丁包加载** — 运行时检测 `user://patch.pck`，通过 `ProjectSettings.LoadResourcePack()` 加载补丁包，优先级高于主包。同路径文件自动覆盖，未变动的从主包回退，无需重导整个游戏

### 网络系统

- [x] **纯 C# 层** — `NetworkManager`（含 TCP 通道、心跳、封包处理）、`DownloadManager`（含下载计数器）、`WebRequestManager` 已完整实现
- [x] **WebRequestComponent** — 基于 Godot `HttpRequest` 的异步 API + 事件驱动，支持 GET/POST、超时控制
- [x] **DownloadComponent** — Godot 桥接组件已实现（2026-07）：多代理并发、断点续传、`DownloadFileAsync` 校验下载，详见 [DownloadSystem.md](Godot/docs/DownloadSystem.md)
- [ ] **NetworkComponent** — Godot 桥接组件待实现

### 场景系统

- [x] **基础场景加载/卸载** — `ISceneManager` + `SceneComponent` + `DefaultSceneHelper` 已完成
- [ ] **场景切换过渡** — 过渡动画、异步加载进度回调
- [ ] **场景卸载完善** — 资源释放确保和卸载回调

### 调试与工具

- [x] **纯 C# 层** — `DebuggerManager` + `IDebuggerWindow` / `IDebuggerWindowGroup` 已完整实现
- [ ] **Godot 调试窗口** — 需在 Godot 层实现可视化调试窗口 UI
- [ ] **单元测试** — 建议引入测试框架覆盖核心模块

### 编辑器插件

- [ ] **Luban 一键生成菜单** — 将 `gen_code_bin_to_project.bat` 集成到 Godot 编辑器菜单中

---

## 📄 License

**MIT License**

本项目基于 MIT 协议开源。核心框架层 (`Framework/GameFramework/`) 版权归 © 2013-2021 Jiang Yin 所有，基于 [Game Framework](https://gameframework.cn/) 原项目移植。

查看 [LICENSE](GodotProject/LICENSE) 文件了解详情。

---

<div align="center">

**Made with ❤️ by GGF Contributors**

[⭐ Star](/) | [🐛 Issues](/) | [📖 Game Framework](https://gameframework.cn/)

</div>
