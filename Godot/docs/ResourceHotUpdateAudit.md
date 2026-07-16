# 资源热更流程 —— 健壮性审计清单

> 逐阶段穷举所有已知漏洞，按严重程度分三级：
> 🔴 线上事故级（会导致用户不可恢复的故障）
> 🟡 体验退化级（不会死但很糟糕）
> 🟢 代码洁癖级（不修也能跑，修了更好）

---

## 阶段 0：启动 → 热更检测前

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 0.1 | 🔴 | **无崩溃恢复机制**。上次热更如果导致 Godot 闪退，下次启动照样加载同一批坏包，形成死循环。用户只能清数据/重装。 | 启动 → 加载坏 .pck → 闪退 → 重启 → 又加载坏 .pck → 又闪退 → ∞ | `ProcedureUpdate:385` |
| 0.2 | 🟡 | `DeserializeUpdatablePackVersion()` 读取版本文件后，**不做任何完整性校验**。如果 `user://GameFrameworkVersion.dat` 被意外清空一半（磁盘满、杀进程），JSON 解析失败只打 Warning，静默降级为"无热更"，用户看到的是旧版本游戏。 | 磁盘满时写版本文件 → JSON 截断 → 下次启动解析失败 → 回退到初始版本 | `ResourceManager:150` |
| 0.3 | 🟡 | `ResourceManager.PackVersionList` 在启动时被赋值，但 `ProcedureUpdate` 又自己读了一份 `localVersion`（从 EasySave），**两处版本数据不同步**。如果 ResourceManager 的版本和 EasySave 的版本不一致，行为不可预测。 | ResourceManager 说 v1.1，ProcedureUpdate 说 v1.0 → 比对逻辑混乱 | `ResourceManager:19` + `ProcedureUpdate:111` |

---

## 阶段 1：更新检测

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 1.1 | 🟡 | `RemoteUrl` 为空时直接跳到 `ProcedurePrelode`，**不加载已下载的本地补丁**。如果用户之前下过热更包，只是这次没网/没配 URL，已下载的包也不会加载。 | 断网启动 → 之前下好的补丁全都不生效 | `ProcedureUpdate:79` |
| 1.2 | 🟡 | `FetchVersionWithRetryAsync` 的**单次请求没有独立超时**，完全依赖 `WebRequestComponent` 的全局 30s 超时。3 次重试 × 30s = 最长等 90s 才失败。对用户来说像卡死。 | 弱网下等一分半才提示"版本检测失败" | `ProcedureUpdate:343` |
| 1.3 | 🟢 | `LoginForm` 可能打开失败（场景路径错误、UI 配置缺失），`await` 会抛异常，进入外层 catch → `SkipToNext`。但用户完全不知道发生了什么——没有 Toast、没有提示。 | 用户看到黑屏 → 然后直接进游戏 | `ProcedureUpdate:88` |
| 1.4 | 🟢 | `PackVersionList.IsValid()` 只检查 `Version` 非空 + `Packs` 非空数组。**`Pack` 本身可能 `Size=0` 或 `Hash=""`**，这种明显无效的包数据会通过校验进入下载流程。 | 服务器配错 → 下载一个空包/无效包 | `PackVersionList:42` |

---

## 阶段 2：版本比对

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 2.1 | 🟡 | **服务器删掉了一个包**（新版本 Packs 里没有 old_pack），本地还留着 old_pack.pck。本地文件不会被清理，**磁盘泄漏**，永远占着空间。 | 运营调整子包结构 → 用户设备上残留废弃 .pck | `ProcedureUpdate:169` |
| 2.2 | 🟡 | Hash 比较是字符串 `OrdinalIgnoreCase`，但如果服务器返回的 Hash 有时大写有时小写，**本地计算的 SHA256 hex 是小写**，比较逻辑依赖服务器一致性。虽然用了 IgnoreCase，但如果服务器发的是带 "-" 的格式（如 `a1-b2-c3` vs `a1b2c3`），比对就失效。 | 运维换人/切换打包工具 → Hash 格式变了 → 每次都认为"有更新" → 重复下载 | `ProcedureUpdate:204` |
| 2.3 | 🟢 | `GetRemoteUrlBase()` 每次都从 `GF.Resource.UpdateSettingRes` 读，但如果 `UpdateSettingRes` 是 Resource 类型且在热更 .pck 中被覆盖了，**读到的 URL 可能是旧/新版本的**，行为不确定。 | URL 变更 → 下载请求打到错误的 CDN | `ProcedureUpdate:444` |

---

## 阶段 3：下载

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 3.1 | 🔴 | **整个文件内容先加载到内存**（`result.Body` 是 `byte[]`），然后 `File.WriteAllBytes` 写入。如果单个 .pck 有 2GB，直接 OOM 崩溃。 | 大包下载 → 内存爆 → Godot 闪退 | `ProcedureUpdate:297` |
| 3.2 | 🔴 | `SubpackDir` 指向 `ExeDir/subpackages/`。**Android 上 `OS.GetExecutablePath()` 可能返回 APK 内部路径（只读）**；Windows 上如果用户把游戏装到 `C:\Program Files\`，普通权限也写不进去。 | Android 下载失败（Permission denied） | `ProcedureUpdate:41` |
| 3.3 | 🟡 | `File.Move(tmp, final)` **不是跨卷原子操作**。某些 Android 设备上 `/data` 和用户存储在不同分区，`File.Move` 会失败。 | 下载完成 → rename 失败 → .tmp 残留 → 下次启动检测不到 .pck | `ProcedureUpdate:323` |
| 3.4 | 🟡 | 下载进度计算只按**包数量均分**，不按文件大小加权。一个 500MB 的包和一个 1KB 的包各占 50% 进度条。500MB 下载过程中进度条一动不动。 | 用户看到 "10%" → 卡了 5 分钟 → 突然跳到 "60%" | `ProcedureUpdate:241` |
| 3.5 | 🟡 | **没有磁盘空间预检**。开始下载前不检查剩余空间是否足够。如果空间不足，下载到一半失败，.tmp 残留。 | 用户空间不足 → 下载失败 → 不清楚原因 | 整个 `DownloadPacksWithProgressAsync` |
| 3.6 | 🟡 | **没有断点续传**。如果下载 80% 时 App 被杀，下次启动 .tmp 被 `TryDeleteFile` 删掉，从头开始。 | 大包下载到一半 → 用户切出去回消息 → App 被杀 → 重来 | `ProcedureUpdate:239` |
| 3.7 | 🟡 | `totalBytes` 用 `long` 累加，但如果服务器配置错误（Pack.Size 是随机大数），累加可能溢出变成负数。 | 服务器配错 → 进度条异常 | `ProcedureUpdate:228` |
| 3.8 | 🟢 | 下载失败后 `await Task.Delay` 是**阻塞 ProcedureUpdate 状态机的**。这期间用户不能取消、不能退出、不能做任何操作。LoginForm 上没有取消按钮。 | 用户不想等了 → 只能杀 App | `ProcedureUpdate:283` |

---

## 阶段 4：校验

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 4.1 | 🟡 | `ComputeSHA256` 对**大文件是同步的**（在 `Task.Run` 里，但 `SHA256.ComputeHash(stream)` 本身会读完整个流）。两个并发的 SHA256 计算各占一个线程池线程，各占一份完整文件内存。 | 两个大包同时校验 → 内存峰值 2× 文件大小 | `ProcedureUpdate:436` |
| 4.2 | 🟢 | Hash 为空时**跳过 SHA256 校验**（`if (!string.IsNullOrEmpty(pack.Hash))`）。这意味着如果服务器忘记配 Hash，任何下载内容都被接受。应该是 Hash 为空 = 拒绝。 | 运维忘记配 Hash → 下载到的垃圾内容通过校验 | `ProcedureUpdate:310` |

---

## 阶段 5：应用（保存版本 + 加载子包）

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 5.1 | 🔴 | **先保存版本文件，后加载子包**。如果 `LoadResourcePack` 过程中 Godot 崩溃（.pck 里的资源损坏），版本文件已经是新版了。下次启动读新版版本文件 → 又加载坏包 → 又崩溃 → 死循环。 | 加载坏包 → 崩溃 → 重启 → 版本说"已更新" → 又加载坏包 → 又崩溃 | `ProcedureUpdate:134-151` |
| 5.2 | 🟡 | `LoadDownloadedPacks` 加载前只检查**文件大小**，不重算 SHA256。磁盘静默损坏（bit rot）检测不到。 | 文件损坏 → 大小没变 → 加载成功 → 运行时随机崩溃 | `ProcedureUpdate:403` |
| 5.3 | 🟡 | 部分包加载失败时**没有任何回退**。比如 5 个包里第 3 个加载失败，前 2 个已加载的资源还在 Godot 资源系统里，**无法卸载**（`LoadResourcePack` 没有对应的 Unload API）。 | 半成功状态 → 部分新资源 + 部分旧资源 → 版本不匹配 | `ProcedureUpdate:390-419` |
| 5.4 | 🟡 | **没有 "MinAppVersion" 强制检查**。`PackVersionList.MinAppVersion` 字段定义了但没被使用。服务器要求 App 2.0，但 App 1.0 也能下载热更包——然后因为 C# 代码不兼容而崩溃。 | 旧版本 App 下载了为新版本 App 准备的热更包 → 崩溃 | `PackVersionList:24` |
| 5.5 | 🟡 | **没有 "ForceUpdate" 强制更新逻辑**。`PackVersionList.ForceUpdate` 字段定义了但没被使用。如果运营需要强制所有人更新，当前代码无法阻止用户跳过。 | 有严重 Bug 需要强制热更 → 用户可以关掉网络跳过 | `PackVersionList:27` |

---

## 阶段 6：运行时

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 6.1 | 🔴 | **无 "启动成功" 标记**。没有地方在游戏成功进入可玩状态后写标记。这是崩溃恢复机制的前置依赖——没有成功标记，就检测不到"上次启动失败了"。 | 无法区分"正常启动"和"启动后崩溃" | 整个流程 |
| 6.2 | 🟡 | `ProcedurePrelode` 加载 EntityGroup/UIGroup 时使用了热更 .pck 里的资源（.tscn 场景）。如果 .pck 里的场景引用了 .pck 里不存在的依赖资源，**Godot 在 Load 阶段就会报错**，但错误不被捕获，可能直接崩溃。 | .pck 打包遗漏依赖 → 加载场景 → Godot 内部报错 → 崩溃 | `ProcedurePrelode:66-78` |
| 6.3 | 🟢 | `ResourceManager.Update()` 每帧轮询加载队列，但只在有任务时干活。开销可忽略，但**没有最大并发限制**——如果有人同时发起 100 个 LoadAsset，全部进队列，全部同时走 `LoadThreadedRequest`，Godot 内部可能过载。 | 极端情况：100 并发加载 → Godot 卡死 | `ResourceManager:106` |

---

## 阶段 7：异常恢复

| # | 严重度 | 问题 | 现象 | 文件:行 |
|---|:---:|------|------|---------|
| 7.1 | 🟡 | `.bak` 备份文件**永远不会被自动使用**。备份逻辑写了，但没有任何代码在检测到问题时自动回退到 `.bak`。备份只是占着磁盘。 | 新版本坏了 → 用户不知道可以手动恢复 → 备份文件没用 | `ProcedureUpdate:138-143` |
| 7.2 | 🟡 | `TryDeleteFile` 吞掉所有异常。如果文件被其他进程锁定删不掉，静默失败。残留的 `.tmp` 或旧 `.pck` 可能干扰后续流程。 | Windows 上杀进程 → .tmp 被锁定 → 删不掉 → 下次下载写不进去 | `ProcedureUpdate:456` |
| 7.3 | 🟢 | **没有日志持久化**。所有错误打 `Log.Error`，但崩溃后日志丢失。无法排查"用户崩溃前发生了什么"。 | 用户报告"更新后闪退" → 没有任何现场数据 | 全局 |

---

## 汇总

```
严重度分布:
  🔴 致命  5 个  (0.1, 3.1, 3.2, 5.1, 6.1)
  🟡 危险 15 个
  🟢 轻度  7 个
  ─────────────
  总计    27 个
```

### 修复优先级

| 优先级 | 编号 | 修复项 | 工作量 | 影响 |
|:--:|------|------|:--:|------|
| **P0** | 0.1 + 6.1 | 崩溃恢复：启动锁 + 成功标记，下次启动检测到上次崩溃 → 禁用热更 | 0.5d | 消除死循环 |
| **P0** | 5.1 | 调整顺序：先加载子包（验证通过），**后**保存版本文件 | 5min | 消除死循环根因 |
| **P0** | 3.2 | `SubpackDir` 改用 `user://subpackages/` | 0.5d | Android 可写 |
| **P1** | 3.1 | 下载改为流式写入（分块读 → 分块写），不把整个文件加载到内存 | 1d | 大包不 OOM |
| **P1** | 5.4 + 5.5 | 实现 `MinAppVersion` 和 `ForceUpdate` | 0.5d | 运维必备 |
| **P1** | 3.6 | 断点续传：检测已有 .tmp 的大小，HTTP Range 请求续传 | 1d | 弱网体验 |
| **P1** | 1.1 | RemoteUrl 为空时也加载本地已有补丁 | 5min | 离线可用 |
| **P2** | 3.4 | 进度按字节数加权 | 10min | 进度条不骗人 |
| **P2** | 5.2 | 加载前 SHA256 重校验（可选，对大文件异步） | 0.5d | 防御 bit rot |
| **P2** | 7.1 | 加载失败自动回退 .bak | 0.5d | 备份真正有用 |
