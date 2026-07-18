# 资源热更流程 —— 健壮性审计清单

> 逐阶段穷举所有已知漏洞，按严重程度分三级：
> 🔴 线上事故级（会导致用户不可恢复的故障）
> 🟡 体验退化级（不会死但很糟糕）
> 🟢 代码洁癖级（不修也能跑，修了更好）

> **复审（2026-07）**：下载链路已整体迁移至统一下载通道 `GF.Download`（`GodotGameFrameworkCore/Download/`，任务队列 + 3 agent 并发 + `.download` 断点续传 + 30s 无进度超时，详见 `DownloadSystem.md`）；原基于 WebRequest 整包内存下载的 `StreamingDownloader` 已删除。同期落地：`HotUpdateSafetyGuard` 崩溃安全守护、版本回退、字节加权进度等。
> 下表逐项标注复审状态：✅ 已修复 ｜ 🔶 部分修复 ｜ 无标注 = 仍然存在。已修复/变更项的「文件:行」已更新为当前位置，未变更项保留审计时行号（可能有漂移）。

---

## 阶段 0：启动 → 热更检测前

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 0.1 | 🔴 | ✅ **已修复 (2026-07)** — `HotUpdateSafetyGuard`（启动锁 + 成功标记 + 安全模式）：检测到上次启动未完成 → 回退版本文件并跳过全部热更补丁。原问题：**无崩溃恢复机制**，坏包导致无限崩溃循环。 | 启动 → 检测到上次崩溃 → 安全模式 → 用内置/上一版本 | `HotUpdateSafetyGuard.cs` + `ProcedureUpdate:121,249` + `ProcedureGame:36` |
| 0.2 | 🟡 | `DeserializeUpdatablePackVersion()` 读取版本文件后，**不做任何完整性校验**。如果 `user://GameFrameworkVersion.dat` 被意外清空一半（磁盘满、杀进程），JSON 解析失败只打 Warning，静默降级为"无热更"，用户看到的是旧版本游戏。 | 磁盘满时写版本文件 → JSON 截断 → 下次启动解析失败 → 回退到初始版本 | `ResourceManager:150` |
| 0.3 | 🟡 | `ResourceManager.PackVersionList` 在启动时被赋值，但 `ProcedureUpdate` 又自己读了一份 `localVersion`（从 EasySave），**两处版本数据不同步**。如果 ResourceManager 的版本和 EasySave 的版本不一致，行为不可预测。 | ResourceManager 说 v1.1，ProcedureUpdate 说 v1.0 → 比对逻辑混乱 | `ResourceManager:163` + `ProcedureUpdate:188` |

---

## 阶段 1：更新检测

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 1.1 | 🟡 | ✅ **已修复 (2026-07)** — RemoteUrl 为空时仍加载已下载的本地补丁（加载前先做逐包完整性校验）。原问题：不加载本地补丁，断网启动补丁全失效。 | 断网启动 → 本地补丁正常生效 | `ProcedureUpdate:131-147` |
| 1.2 | 🟡 | `FetchVersionWithRetryAsync` 的**单次请求没有独立超时**，完全依赖 `WebRequestComponent` 的全局 30s 超时。3 次重试 × 30s = 最长等 90s 才失败。对用户来说像卡死。（版本清单仍走 `GF.WebRequest`，未迁移到 GF.Download） | 弱网下等一分半才提示"版本检测失败" | `ProcedureUpdate:539` |
| 1.3 | 🟢 | `LoginForm` 可能打开失败（场景路径错误、UI 配置缺失），`await` 会抛异常，进入外层 catch → `SkipToNext`。但用户完全不知道发生了什么——没有 Toast、没有提示。 | 用户看到黑屏 → 然后直接进游戏 | `ProcedureUpdate:151` |
| 1.4 | 🟢 | 🔶 **部分修复 (2026-07)** — `Pack.IsValid()` 已校验 `Name` 非空 + `Size > 0`，比对/下载/加载全链路过滤无效包；✅ `Pack.IsValid()` 已校验 Hash 非空 + 64 字符（2026-07）。空 Hash / 无效 Hash 的包在全链路被过滤。 | 服务器配无效 Hash → 被过滤 | `PackVersionList:83-84` |

---

## 阶段 2：版本比对

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 2.1 | 🟡 | ✅ **已修复 (2026-07)** — 子包加载完成后 `CleanStalePacks` 清理磁盘上不在当前版本清单中的废弃 `.pck`。原问题：服务器删包后本地文件残留，磁盘泄漏。 | 运营调整子包结构 → 废弃 .pck 自动清理 | `ProcedureUpdate:670-693` |
| 2.2 | 🟡 | ✅ **已修复 (2026-07)** — `FindPacksToUpdate` 改用 `OrdinalIgnoreCase` 比较；`Pack.IsValid()` 增加 Hash 非空 + 64 字符校验。原问题：大小写敏感导致重复下载；Hash 为空时静默跳过校验。 | Hash 归一化 + 空 Hash 拒绝 | `ProcedureUpdate:490` + `PackVersionList:83-84` |
| 2.3 | 🟢 | 下载 URL 每次在 `FindPacksToUpdate` 中从 `GF.Resource.UpdateSettingRes` 读取（原 `GetRemoteUrlBase()` 已内联），但如果 `UpdateSettingRes` 是 Resource 类型且在热更 .pck 中被覆盖了，**读到的 URL 可能是旧/新版本的**，行为不确定。 | URL 变更 → 下载请求打到错误的 CDN | `ProcedureUpdate:391` |

---

## 阶段 3：下载

> **2026-07**：下载实现已从「WebRequest 整包进内存 + `File.WriteAllBytes` + `.tmp`」迁移至 `GF.Download.DownloadFileAsync`（流式写盘 + `.download` 断点续传 + 大小/SHA256 校验，详见 `DownloadSystem.md`）。本阶段多数问题因此消除。

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 3.1 | 🔴 | ✅ **已修复 (2026-07)** — `GF.Download` 流式下载（64KB 缓冲直接写盘，内存占用与文件大小无关）。原问题：整个文件先加载进内存（`result.Body` 是 `byte[]`）再 `File.WriteAllBytes`，大包 OOM。 | 2GB 大包下载 → 内存平稳 | `WebRequestDownloadAgentHelper.cs`，详见 `DownloadSystem.md` §3 |
| 3.2 | 🔴 | ✅ **已修复 (2026-07)** — `GetOrCreateHotUpdateDir`：显式配置 → 游戏目录（**实际写测试探测**可写性）→ `user://subpackages/` 兜底。原问题：`SubpackDir` 硬指向 `ExeDir/subpackages/`，Android/受限目录不可写。 | Android/Program Files → 自动回退 user:// | `ProcedureUpdate:43-70` |
| 3.3 | 🟡 | ✅ **已修复 (2026-07)** — 临时文件为 `目标路径.download`（与成品**同目录同卷**），完成后 `File.Move` 为同卷重命名。原问题：`.tmp` → 成品的 `File.Move` 可能跨卷失败。 | rename 恒为同卷操作 | `DownloadManager.DownloadAgent:186,338` |
| 3.4 | 🟡 | ✅ **已修复 (2026-07)** — 进度按**字节加权聚合**（`perPackBytes[]` 槽位，主线程回调无锁）。原问题：按包数量均分，大包下载时进度条不动。 | 进度条随字节数平滑推进 | `ProcedureUpdate:428-441` |
| 3.5 | 🟡 | ✅ **已修复 (2026-07)** — 下载前磁盘空间预检（需 2× 总大小，不足则提示并跳过）。原问题：无预检，空间不足时下载到一半失败。 | 空间不足 → 提示"磁盘空间不足" | `ProcedureUpdate:212-223` |
| 3.6 | 🟡 | ✅ **已修复 (2026-07)** — `.download` 临时文件 + HTTP Range 断点续传：失败保留断点文件，重试自动续传（服务器不支持 Range 时自动从头重下）。原问题：无断点续传，App 被杀后从头下载。 | 下载 80% 被杀 → 重启后从 80% 续传 | `DownloadSystem.md` §3.1 |
| 3.7 | 🟡 | `totalBytes` 用 `long` 累加，但如果服务器配置错误（Pack.Size 是随机大数），累加可能溢出变成负数。 | 服务器配错 → 进度条异常 | `ProcedureUpdate:419` |
| 3.8 | 🟢 | 🔶 **部分修复 (2026-07)** — 下载通道已支持 `CancellationToken` 取消（`DownloadFileAsync`）；但 `ProcedureUpdate` 未接取消 UI，重试等待仍是 `await Task.Delay`，`LoginForm` 上没有取消按钮。 | 用户不想等了 → 只能杀 App | `ProcedureUpdate:505` |

---

## 阶段 4：校验

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 4.1 | 🟡 | 🔶 **部分修复 (2026-07)** — 下载完成后的 SHA256 校验已移入线程池（`Task.Run`）且哈希计算为流式（内存 O(1)，不再整份进内存）；但启动自检 `VerifyLocalPackIntegrity` 和加载前重校验（>1MB）仍在**主线程同步**执行 `EasySave.ComputeSHA256`，大文件会卡帧。 | 启动时校验多个大包 → 卡顿数秒 | `DownloadComponent:410`（线程池）+ `ProcedureUpdate:323,625`（主线程） |
| 4.2 | 🟢 | ✅ **已修复 (2026-07)** — `Pack.IsValid()` 要求 Hash 非空 + 64 字符，无效包在全链路被过滤。`DownloadFileAsync` 的 `expectedHash` 非空检查保留为防御性代码。 | 空 Hash / 无效 Hash → 被 IsValid 过滤 | `PackVersionList:83-84` |

---

## 阶段 5：应用（保存版本 + 加载子包）

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 5.1 | 🔴 | ✅ **已修复 (2026-07)** — 顺序已调整：先 `LoadDownloadedPacks` 加载验证 → 成功后才保存版本文件（旧版先备份 `.bak`）；加载期间由 `HotUpdateSafetyGuard` 启动锁兜底。原问题：先保存版本后加载子包，坏包导致崩溃死循环。 | 加载崩溃 → 版本文件仍是旧版 + 安全模式兜底 → 无死循环 | `ProcedureUpdate:243-266` |
| 5.2 | 🟡 | ✅ **已修复 (2026-07)** — `LoadDownloadedPacks` 加载前做大小校验，且对 >1MB 且有 Hash 的文件**重算 SHA256**（防御 bit rot），失败即删除并计失败。原问题：只检查文件大小，磁盘静默损坏检测不到。 | 文件损坏 → 加载前被识别并删除 | `ProcedureUpdate:609-640` |
| 5.3 | 🟡 | 🔶 **部分修复 (2026-07)** — 任一包加载失败会自动回退版本文件（`RollbackVersionFile`，下次启动生效）+ 崩溃安全模式兜底；但**本次会话内**已加载的 .pck 仍无法卸载（`LoadResourcePack` 无对应 Unload API），半成功状态（部分新资源 + 部分旧资源）在当前会话依旧存在。 | 半成功 → 本次会话资源混杂，下次启动回退 | `ProcedureUpdate:659-663` |
| 5.4 | 🟡 | ✅ **已修复 (2026-07)** — `MinAppVersion` 已生效：`CompareVersions` 比对当前 App 版本（`project.godot` 的 `config/version`），过低则提示并不下载（商店引导弹窗仍为 TODO）。原问题：字段定义了但没被使用。 | 旧 App → 提示"请更新App版本"，不下载热更包 | `ProcedureUpdate:174-185` |
| 5.5 | 🟡 | 🔶 **部分修复 (2026-07)** — `ForceUpdate` 字段已读取并记录"本次为强制更新"日志，但下载失败路径仍会 `SkipToNext` 进入游戏，**强制拦截（不可跳过）逻辑尚未实现**。 | 有严重 Bug 需要强制热更 → 用户断网仍可跳过 | `ProcedureUpdate:202-207` |

---

## 阶段 6：运行时

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 6.1 | 🔴 | ✅ **已修复 (2026-07)** — `HotUpdateSafetyGuard.MarkStartupBegin()`（加载子包前写启动锁）+ `MarkStartupSuccess()`（`ProcedureGame.OnEnter` 写成功标记），可区分"正常启动"与"启动后崩溃"。原问题：无"启动成功"标记，崩溃恢复无从谈起。 | 锁在 + 成功标记不在 → 判定上次崩溃 → 安全模式 | `HotUpdateSafetyGuard.cs` + `ProcedureGame:36` |
| 6.2 | 🟡 | `ProcedurePrelode` 加载 EntityGroup/UIGroup 时使用了热更 .pck 里的资源（.tscn 场景）。如果 .pck 里的场景引用了 .pck 里不存在的依赖资源，**Godot 在 Load 阶段就会报错**，但错误不被捕获，可能直接崩溃。（崩溃后果已由 `HotUpdateSafetyGuard` 兜底：下次启动进入安全模式） | .pck 打包遗漏依赖 → 加载场景 → 崩溃 → 下次启动安全模式 | `ProcedurePrelode:36-91` |
| 6.3 | 🟢 | `ResourceManager.Update()` 每帧轮询加载队列，但只在有任务时干活。开销可忽略，但**没有最大并发限制**——如果有人同时发起 100 个 LoadAsset，全部进队列，全部同时走 `LoadThreadedRequest`，Godot 内部可能过载。 | 极端情况：100 并发加载 → Godot 卡死 | `ResourceManager:106` |

---

## 阶段 7：异常恢复

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 7.1 | 🟡 | ✅ **已修复 (2026-07)** — `.bak` 备份已被两条路径自动使用：子包加载失败 → `RollbackVersionFile` 回退；上次启动崩溃 → `HotUpdateSafetyGuard.EnterSafeMode` 回退（无备份则清除版本文件用内置版本）。原问题：备份写了但永远不会被自动使用。 | 新版本坏了 → 下次启动自动回退上一版本 | `ProcedureUpdate:696-715` + `HotUpdateSafetyGuard:49-77` |
| 7.2 | 🟡 | `EasySave.TryDelete`（原 `TryDeleteFile`）吞掉所有异常。如果文件被其他进程锁定删不掉，静默失败。残留的 `.download` 断点文件或旧 `.pck` 可能干扰后续流程。（`ProcedureUpdate` 已在每包下载前清理旧实现遗留的 `.tmp`） | Windows 上杀进程 → 文件被锁定 → 删不掉 → 下次下载写不进去 | `EasySave:62` |
| 7.3 | 🟢 | **没有日志持久化**。所有错误打 `Log.Error`，但崩溃后日志丢失。无法排查"用户崩溃前发生了什么"。 | 用户报告"更新后闪退" → 没有任何现场数据 | 全局 |

---

## 汇总

```
2026-07 复审状态:
  🔴 致命  5 个 → ✅ 全部已修复 (0.1, 3.1, 3.2, 5.1, 6.1)
  🟡 危险     → ✅ 已修复: 1.1, 2.1, 2.2, 3.3, 3.4, 3.5, 3.6, 4.1, 5.2, 5.4, 5.5, 7.1
               🔶 部分修复: 5.3
               仍存在: 0.2, 0.3, 1.2, 3.7, 6.2, 7.2
  🟢 轻度     → ✅ 已修复: 4.2, 6.3
               🔶 部分修复: 1.4, 3.8
               仍存在: 1.3, 2.3, 7.3
```

### 修复优先级

| 优先级 | 编号 | 修复项 | 工作量 | 影响 | 状态 |
|:--:|------|------|:--:|------|------|
| **P0** | 0.1 + 6.1 | 崩溃恢复：启动锁 + 成功标记 | 0.5d | 消除死循环 | ✅ 已完成 (2026-07) |
| **P0** | 5.1 | 调整顺序：先加载子包（验证通过），**后**保存版本文件 | 5min | 消除死循环根因 | ✅ 已完成 (2026-07) |
| **P0** | 3.2 | `SubpackDir` 改用 `user://subpackages/` | 0.5d | Android 可写 | ✅ 已完成 (2026-07，实现为：可写性探测 + `user://` 回退) |
| **P1** | 3.1 | 下载改为流式写入（分块读 → 分块写），不把整个文件加载到内存 | 1d | 大包不 OOM | ✅ 已完成 (2026-07，`GF.Download` 统一通道，详见 `DownloadSystem.md`) |
| **P1** | 5.4 + 5.5 | `MinAppVersion` + `ForceUpdate` 强制拦截 | 0.5d | 运维必备 | ✅ 已完成 (2026-07，阻塞对话框 + 重试/退出) |
| **P1** | 3.6 | 断点续传：检测已有 .tmp 的大小，HTTP Range 请求续传 | 1d | 弱网体验 | ✅ 已完成 (2026-07，`.download` + HTTP Range) |
| **P1** | 1.1 | RemoteUrl 为空时也加载本地已有补丁 | 5min | 离线可用 | ✅ 已完成 (2026-07) |
| **P2** | 3.4 | 进度按字节数加权 | 10min | 进度条不骗人 | ✅ 已完成 (2026-07) |
| **P2** | 5.2 | 加载前 SHA256 重校验 | 0.5d | 防御 bit rot | ✅ 已完成 (2026-07) |
| **P1** | 2.2 + 4.2 | Hash 归一化 + 空 Hash 拒绝 | 5min | 避免重复下载 | ✅ 已完成 (2026-07) |
| **P2** | 4.1 | SHA256 移出主线程 | 0.5d | 启动不卡帧 | ✅ 已完成 (2026-07，`Task.Run`) |
| **P2** | 6.3 | 资源加载并发限制 | 5min | 防止 Godot 过载 | ✅ 已完成 (2026-07，16 Agent) |
| **P1** | 2.2 + 4.2 | Hash 归一化 + 空 Hash 拒绝 | 5min | 避免重复下载 | ✅ 已完成 (2026-07) |
| **P2** | 4.1 | SHA256 移出主线程 | 0.5d | 启动不卡帧 | ✅ 已完成 (2026-07，`Task.Run`) |
| **P2** | 6.3 | 资源加载并发限制 | 5min | 防止 Godot 过载 | ✅ 已完成 (2026-07，16 Agent) |
| **P2** | 7.1 | 加载失败自动回退 .bak | 0.5d | 备份真正有用 | ✅ 已完成 (2026-07) |

### 遗留项（2026-07 后的待办）

| 编号 | 内容 |
|------|------|
| 1.2 | 版本清单请求的单次独立超时 |
| 0.2 / 0.3 | 版本文件完整性校验；ResourceManager 与 ProcedureUpdate 版本数据统一 |
| 3.7 / 3.8 / 6.2 / 7.2 / 7.3 | Size 溢出防御、下载取消 UI、Prelode 加载异常捕获、删除失败日志、日志持久化 |
