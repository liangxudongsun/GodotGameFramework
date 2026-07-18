# 数据表系统 (DataTable Module + Luban 管线)

> 适用版本：Godot 4.6.2 + .NET 8 ｜ 对应代码：`Framework/GameFramework/DataTable/`、`Framework/GodotGameFrameworkCore/DataTable/`、`Framework/GodotGameFrameworkCore/Lib/LubanLib/`、`TheGame/GameScripts/GameProto/GameConfig/`（生成代码）｜ 配置源：仓库根 `Configs/GameConfig/`
> 本文档描述 GGF 的数据表系统：Excel → Luban → C# + 二进制的完整管线、运行时加载机制、Tb 表访问 API 与新增表的完整步骤。

---

## 1. 概述

数据表系统是 [Game Framework](https://gameframework.cn/) DataTable 模块的 Godot 移植，但**核心已重写为 [Luban](https://github.com/focus-creative-games/luban) 驱动**：原版"CSV 文本 + IDataRow 逐行解析"的机制被替换为"Luban 生成的强类型 `Tables` 类 + 二进制反序列化"。

| 层 | 位置 | 职责 | Godot 依赖 |
|----|------|------|:--:|
| 纯 C# 层 | `GameFramework/DataTable/` | `DataTableManager`：持有 Luban `Tables` 实例、懒加载、二进制读取回调 | ⚠️ 见下 |
| Godot 桥接层 | `GodotGameFrameworkCore/DataTable/` | `DataTableComponent`：组件封装、注入 `ResourceComponent` | ✅ |
| Luban 运行时 | `GodotGameFrameworkCore/Lib/LubanLib/` | `ByteBuf`（二进制读写）、`BeanBase`（Bean 基类）、`StringUtil` | ❌ |
| 生成代码 | `TheGame/GameScripts/GameProto/GameConfig/` | `Tables` / `TbXxx` / `XxxConfig` / 枚举，由 Luban 生成，**勿手改** | ❌ |

> ⚠️ **分层现状说明**：`DataTableManager.cs` 虽位于纯 C# 层，但直接 `using GodotGameFramework.Resource`（引用桥接层的 `ResourceComponent`）并依赖生成命名空间 `GameConfig`，**并未遵守"GameFramework 层零 Godot 依赖"的架构规则**。这是 Luban 化改造后的已知妥协，见 §6。

### 能力清单

- ✅ Excel 配置 → Luban 一键生成强类型 C# 代码 + 二进制数据
- ✅ 强类型访问：`GF.DataTable.GetTables().TbEntityConfig.Get(id)`，字段全部 `readonly`
- ✅ 懒加载：首次访问 `Tables` 时才读取所有 `.bytes` 文件
- ✅ 表间引用解析（Luban `ResolveRef`）
- ✅ 枚举生成（如 `EntityId.Cat`），配置驱动实体/UI 创建
- ✅ 二进制数据可打入 Config 类型子包热更（`ProcedureUpdate` 优先加载 Config 包）

---

## 2. 架构与数据流

### 2.1 编辑期（Luban 管线）

```
Configs/GameConfig/Datas/*.xlsx（策划编辑）
    │  __tables__.xlsx / __beans__.xlsx / __enums__.xlsx（结构定义）
    │  实体.xlsx / 界面UI.xlsx / 角色.xlsx（数据）
    ▼  gen_code_bin_to_project.bat（dotnet Tools/Luban/Luban.dll -t client -c cs-bin -d bin）
    ├──► C# 代码 → Godot/GodotProject/TheGame/GameScripts/GameProto/GameConfig/
    │        Tables.cs / TbEntityConfig.cs / EntityConfig.cs / EntityId.cs ...
    └──► 二进制数据 → Godot/GodotProject/TheGame/DataTables/GameConfigs/
             entity_tbentityconfig.bytes / ui_tbuiformconfig.bytes / character_tbcharacterconfig.bytes
```

### 2.2 运行期

```
调用方（EntityExtension / UIExtension / 业务代码）
    │  GF.DataTable.GetTables().TbXxx...
    ▼
DataTableComponent (Godot 桥接层，场景节点 "DataTable")
    │  OnInit: GetModule<IDataTableManager>() + SetResourcesComponent(GF.Resource)
    ▼
DataTableManager : GameFrameworkModule (核心层)
    │  Tables 属性首次访问 → Load() → new Tables(LoadByteBuf)
    │      LoadByteBuf(file):
    │        path = "res://TheGame/DataTables/GameConfigs/{file}.bytes"   ← GameFolderConstant.GameConfigs
    │        bytes = ResourceComponent.LoadBinary(path)                   ← FileAccess 读取
    │        return new ByteBuf(bytes)
    ▼
Tables 构造函数（Luban 生成）
    ├── TbUIFormConfig    = new(loader("ui_tbuiformconfig"))
    ├── TbCharacterConfig = new(loader("character_tbcharacterconfig"))
    ├── TbEntityConfig    = new(loader("entity_tbentityconfig"))
    └── ResolveRef()      ← 解析表间引用
```

### 文件清单

| 文件 | 职责 |
|------|------|
| `GameFramework/DataTable/IDataTableManager.cs` | 管理器接口（仅 2 个成员：`SetResourcesComponent` / `GetTables`） |
| `GameFramework/DataTable/DataTableManager.cs` | Luban `Tables` 持有者，懒加载 + `LoadByteBuf` |
| `GameFramework/DataTable/DataTableBase.cs` 等 | **原版 GF 数据表机制遗留代码，当前未接线**（见 §6） |
| `GodotGameFrameworkCore/DataTable/DataTableComponent.cs` | 组件封装，注入 ResourceComponent |
| `GodotGameFrameworkCore/DataTable/DefaultDataTableHelper.cs` | 原版 CSV/二进制解析辅助器，**当前无调用方** |
| `GodotGameFrameworkCore/Lib/LubanLib/ByteBuf.cs` | Luban 二进制缓冲（ReadInt/ReadString/ReadSize…） |
| `GodotGameFrameworkCore/Lib/LubanLib/BeanBase.cs` | 生成 Bean 的基类（`ITypeId`） |
| `GodotGameFrameworkCore/Config/GameFolderConstant.cs` | `GameConfigs = "res://TheGame/DataTables/GameConfigs/{0}.bytes"` |
| `TheGame/GameScripts/GameProto/GameConfig/Tables.cs` | 总入口（生成），聚合所有 TbXxx |
| `TheGame/GameScripts/GameProto/ExternalTypeUtil.cs` | 外部类型转换（生成时从 `CustomTemplate/` 拷贝） |
| `TheGame/GameScripts/GameProto/GameConfig/vector2.cs` 等 | Luban 内建数学类型 |

---

## 3. Luban 配置管线

### 3.1 目录结构（仓库根 `Configs/GameConfig/`）

```
Configs/GameConfig/
  Datas/
    __tables__.xlsx        ← 表定义（表名、模式、索引、数据源文件）
    __beans__.xlsx         ← Bean（结构体）字段定义
    __enums__.xlsx         ← 枚举定义（如 EntityId）
    实体.xlsx / 界面UI.xlsx / 角色.xlsx   ← 实际数据
  Defines/builtin.xml      ← Luban 内建类型定义
  CustomTemplate/          ← ExternalTypeUtil.cs 模板（生成时拷入项目）
  luban.conf               ← Luban 主配置
  gen_code_bin_to_project.bat/.sh          ← 客户端生成（常用）
  gen_code_bin_to_project_lazyload.bat/.sh ← 懒加载模式变体
  gen_code_bin_to_server.bat/.sh           ← 服务端生成
```

### 3.2 luban.conf 关键内容

```json
{
  "groups": [ {"names":["c"]}, {"names":["s"]}, {"names":["e"]} ],   // 客户端/服务端/编辑器分组
  "schemaFiles": [
    {"fileName":"Defines", "type":""},
    {"fileName":"Datas/__tables__.xlsx", "type":"table"},
    {"fileName":"Datas/__beans__.xlsx",  "type":"bean"},
    {"fileName":"Datas/__enums__.xlsx",  "type":"enum"}
  ],
  "dataDir": "Datas",
  "targets": [
    {"name":"client", "manager":"Tables", "groups":["c"], "topModule":"GameConfig"},
    ...
  ]
}
```

### 3.3 生成命令（gen_code_bin_to_project.bat 实际内容）

```bat
set WORKSPACE=../..
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set DATA_OUTPATH=%WORKSPACE%/Godot/GodotProject/TheGame/DataTables/GameConfigs
set CODE_OUTPATH=%WORKSPACE%/Godot/GodotProject/TheGame/GameScripts/GameProto/GameConfig/

copy /y "CustomTemplate\ExternalTypeUtil.cs" "...\GameScripts\GameProto\ExternalTypeUtil.cs"

dotnet %LUBAN_DLL% ^
    -t client ^          ← 目标：客户端（组 c）
    -c cs-bin ^          ← 代码：C# 二进制反序列化
    -d bin ^             ← 数据：二进制
    --conf luban.conf ^
    -x code.lineEnding=crlf ^
    -x outputCodeDir=%CODE_OUTPATH% ^
    -x outputDataDir=%DATA_OUTPATH%
if not defined AI_MODE pause      ← CI/脚本环境设置 AI_MODE 可跳过暂停
```

生成产物文件名规则：`<模块小写>_<表名小写>.bytes`（如 `entity_tbentityconfig.bytes`），与 `Tables.cs` 构造函数中 `loader("entity_tbentityconfig")` 的参数一一对应。

---

## 4. 运行时机制

### 4.1 初始化与懒加载

- `DataTableComponent.OnInit()`（组件注册时）：获取 `IDataTableManager` 模块，调用 `SetResourcesComponent(GF.Resource)` 注入资源组件。**此时不读任何文件。**
- 首次调用 `GetTables()`（即访问 `DataTableManager.Tables` 属性）→ `Load()` → `new Tables(LoadByteBuf)` → 一次性同步读入所有表的 `.bytes` 并反序列化。
- **没有任何流程（Procedure）显式预加载数据表**：`ProcedureLaunch` 只校验 `GF.DataTable != null`；真正触发加载的是首个业务访问（如 `ProcedurePrelode` 之后 `EntityExtension.ShowEntity` 内部查 `TbEntityConfig`）。
- `LoadByteBuf` 读不到文件会 `throw Exception`（快速失败），`Shutdown()` 只重置 `_init` 标记。

### 4.2 生成代码结构（以 EntityConfig 为例）

```csharp
// Tables.cs — 总入口
public partial class Tables {
    public UI.TbUIFormConfig TbUIFormConfig { get; }
    public Character.TbCharacterConfig TbCharacterConfig { get; }
    public Entity.TbEntityConfig TbEntityConfig { get; }
    public Tables(System.Func<string, ByteBuf> loader) { ...; ResolveRef(); }
}

// TbEntityConfig.cs — 表：Dictionary + List 双容器
public partial class TbEntityConfig {
    public Dictionary<int, EntityConfig> DataMap { get; }
    public List<EntityConfig> DataList { get; }
    public EntityConfig Get(int key);              // 不存在抛异常
    public EntityConfig GetOrDefault(int key);     // 不存在返回 null
    public EntityConfig this[int key] { get; }
}

// EntityConfig.cs — 行：全 readonly 字段 + XML 注释（来自 Excel 表头）
public sealed partial class EntityConfig : Luban.BeanBase {
    public readonly int Id;
    public readonly Entity.EntityId EntityId;   // 枚举，来自 __enums__.xlsx
    public readonly string AssetPath;           // 场景路径
    public readonly string EntityGroupName;
    public readonly int Priority;
}

// EntityId.cs — 枚举
public enum EntityId { Cat = 0, GanTan = 1, Anger = 2 }
```

### 4.3 访问方式

```csharp
// 主键访问
EntityConfig cfg = GF.DataTable.GetTables().TbEntityConfig.Get(1);
EntityConfig cfgOrNull = GF.DataTable.GetTables().TbEntityConfig.GetOrDefault(999);

// 条件查找（框架内 EntityExtension 的实际写法）
EntityConfig cfg = GF.DataTable.GetTables().TbEntityConfig.DataList
    .FirstOrDefault(x => x.EntityId == entityId);

// 游戏侧（CatEntity 的实际写法）
m_Config = GF.DataTable.GetTables().TbCharacterConfig.DataList
    .FirstOrDefault(x => x.EntityId == EntityId.Cat);
```

配置驱动链路示例：`GF.Entity.ShowEntity<CatEntity>(EntityId.Cat)` → `EntityExtension` 查 `TbEntityConfig` 取 `AssetPath` / `EntityGroupName` / `Priority` → 加载场景并显示实体。

---

## 5. 新增一张表的完整步骤

1. **定义结构**：在 `Configs/GameConfig/Datas/__beans__.xlsx` 中定义 Bean 字段；如需枚举，在 `__enums__.xlsx` 中定义。
2. **注册表**：在 `__tables__.xlsx` 中登记表名（如 `TbItemConfig`）、模块、索引字段、数据源文件名。
3. **填数据**：新建/编辑数据 Excel（如 `道具.xlsx`），表头注释即生成代码的 XML 注释。
4. **生成**：双击 `Configs/GameConfig/gen_code_bin_to_project.bat`（命令行/CI 先 `set AI_MODE=1` 免暂停）。
   - C# 代码落至 `TheGame/GameScripts/GameProto/GameConfig/`
   - `.bytes` 数据落至 `TheGame/DataTables/GameConfigs/`
5. **编译**：`cd GodotProject && dotnet build`（新文件首次生成后建议再执行 `--build-solutions` 刷新解决方案）。
6. **使用**：`GF.DataTable.GetTables().TbItemConfig.Get(id)`。`Tables.cs` 中的新表属性和加载调用由 Luban 自动补齐，无需手写注册代码。

热更说明：`.bytes` 属于 `PackType.Config` 类型资源，可打入 Config 子包；`ProcedureUpdate.LoadDownloadedPacks` 会**先加载 Config 包再加载 Resource 包**，保证场景实例化时新配置已生效。注意：由于 `Tables` 是懒加载 + 一次性加载，子包必须在**首次访问 `GetTables()` 之前**完成 `LoadResourcePack`（当前流程顺序 ProcedureUpdate → ProcedurePrelode → 业务访问，天然满足）。

---

## 6. 注意事项 / FAQ

**Q: `DataTableBase` / `IDataTable<T>` / `IDataRow` / `DefaultDataTableHelper` 还能用吗？**
✅（2026-07 已清理）这些原版 Game Framework"CSV 逐行解析"的遗留代码已删除。当前仅保留 Luban 驱动路径（`IDataTableManager` → `DataTableManager` → `Tables`）。新表一律走 Luban。

**Q: 修改 Excel 后运行时数据没变？**
必须重新执行生成脚本——运行时读的是 `.bytes` 二进制，不是 Excel。生成后无需重启 Godot 编辑器，但需要 `dotnet build`（若结构变化产生了新代码）。

**Q: 表加载是什么时机？会卡顿吗？**
首次 `GetTables()` 时同步加载**全部**表。当前表量小无感知；表规模变大后可考虑使用 `gen_code_bin_to_project_lazyload.bat` 生成懒加载版本代码（按表首次访问再读文件）。

**Q: `GameFramework/DataTable` 为什么依赖了 Godot 桥接层？**
✅（2026-07 已修复）`DataTableManager` 已改为接收 `Func<string, byte[]>` 加载器，不再直接引用 `ResourceComponent`。路径格式化（`GameFolderConstant.GameConfigs`）移至 `DataTableComponent`，纯 C# 层零 Godot 依赖。

**Q: 能在运行时增删行吗？**
不能。生成代码所有字段 `readonly`，容器只读暴露。运行时可变数据请使用 DataNode（见 `DataNodeSystem.md`）或 Setting。

**Q: `ExternalTypeUtil.cs` 是干什么的？**
Luban 内建 `vector2/vector3` 等数学类型与 Godot `Vector2/Vector3` 之间的转换工具，每次生成时从 `Configs/GameConfig/CustomTemplate/` 覆盖拷贝到 `GameScripts/GameProto/`，勿手改。

---

## 7. 已知边界与后续计划

- [x] `DataTableManager` 解耦 `ResourceComponent` 具体类型（恢复纯 C# 层洁净）✅ 2026-07
- [x] 清理原版 CSV 数据表遗留代码（6 个文件：DataTableBase、DataTableTable、IDataRow、IDataTable、IDataTableHelper、DefaultDataTableHelper）✅ 2026-07
- [ ] 表规模增长后切换 lazyload 生成模式
