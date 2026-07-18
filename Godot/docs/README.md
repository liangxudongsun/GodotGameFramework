# GGF 文档索引

> GGF (Godot Game Framework) — Godot 4.6.2 + C# (.NET 8)，[Game Framework](https://gameframework.cn/) 的 Godot 移植。
> 本目录为各系统的深度介绍文档。项目总览与开发命令见仓库根 `CLAUDE.md`。

## 框架核心

| 文档 | 内容 |
|------|------|
| [FrameworkCore.md](FrameworkCore.md) | 双层架构、启动/关闭序列、GodotComponent 生命周期、GF 门面、ReferencePool、日志系统、SingletonNode、PhysicsCheck2D |
| [EventSystem.md](EventSystem.md) | EventPool 机制、EventId 约定、Fire vs FireNow、订阅退订、池化回收禁忌 |
| [FsmSystem.md](FsmSystem.md) | IFsm/FsmState 泛型设计、状态生命周期、SetData/GetData、销毁与池化 |
| [ProcedureSystem.md](ProcedureSystem.md) | 流程 = 顶层 FSM、启动链路、TheGame 流程链 Launch→Update→Prelode→Game、新增流程教程 |
| [DebuggerSystem.md](DebuggerSystem.md) | 运行时调试器：FPS 图标 + Console/Information/Profiler/Other 多级页签、BBCode-IMGUI 绘制模型、日志捕获、自定义调试窗口 |

## 资源与内容

| 文档 | 内容 |
|------|------|
| [ResourceSystem.md](ResourceSystem.md) | ResourceMode 现状、异步加载队列、子包加载、ExportInspector 导出工作流、ResourcesCollectionConstant |
| [DataTableSystem.md](DataTableSystem.md) | Luban 管线（Excel→C#+二进制）、运行时懒加载、新增表步骤、Config 子包热更时序 |
| [DataNodeSystem.md](DataNodeSystem.md) | 树形数据结构、路径访问语义、Variable 池化类型 |
| [SettingSystem.md](SettingSystem.md) | ConfigFile → user://settings.cfg、Save/Load 语义、与 EasySave 的区别 |
| [LocalizationSystem.md](LocalizationSystem.md) | TSV 字典格式、语言决定链、IStringKey 刷新机制、LocalizationEditor 翻译工作流 |

## 游戏对象

| 文档 | 内容 |
|------|------|
| [EntitySystem.md](EntitySystem.md) | 实体生命周期、实体组+实例池、EntityId 配置驱动、TheGame 继承树、新增实体步骤 |
| [UISystem.md](UISystem.md) | UIForm 生命周期、UI 组遮挡算法、OpenUIFormAsync、脚本生成器（Ge/Logic 双文件）工作流 |
| [SoundSystem.md](SoundSystem.md) | 声音组与 Audio Bus 映射、代理抢占算法、PlaySoundParams、AudioStreamPlayer 桥接 |
| [SceneSystem.md](SceneSystem.md) | 场景加载流程、实例挂载位置、LoadSceneAsync、与 ResourceComponent 的关系 |
| [ObjectPoolSystem.md](ObjectPoolSystem.md) | ObjectBase/IObjectPool 设计、四参数语义、与 ReferencePool 对照、框架内实际使用点 |

## 网络与热更

| 文档 | 内容 |
|------|------|
| [WebRequestSystem.md](WebRequestSystem.md) | SendRequestAsync、超时约定、与 Download 模块的分工（小文本 vs 大文件） |
| [DownloadSystem.md](DownloadSystem.md) | 下载模块全貌：任务队列/断点续传/校验/DownloadFileAsync/热更集成/错误语义表 |
| [ResourceHotUpdateAudit.md](ResourceHotUpdateAudit.md) | 资源热更审计：风险项清单与修复状态（2026-07 复审） |
| [CodeHotUpdateDesign.md](CodeHotUpdateDesign.md) | C# 程序集热更方案设计（ALC，未实施；下载/安全防护等前置能力已落地） |

## 其他

- `engine-reference/` — 引擎版本参考资料
- 文档风格约定：中文；开头引用块标注适用版本与代码路径；章节顺序为 概述 → 架构与数据流 → 文件清单 → 核心机制 → 组件与 API → FAQ → 已知边界；**所有断言以实际代码为准**，与 CLAUDE.md 冲突时以系统文档为准（CLAUDE.md 已于 2026-07 同步修订）
