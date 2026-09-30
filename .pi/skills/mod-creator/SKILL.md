---
name: mod-creator
description: Escape From Duckov 的 C# mod 制作与修改：目录结构、info.ini 字段、csproj 要点、ModBehaviour 入口类、内容数据查询（`docs/guides/`）、配置文件写法、以及用 validate_mod 编译校验。当玩家要新建/修改/解释一个 mod 怎么写、要查游戏内数据、或排查编译/校验错误时读取。
---

# 做 mod（Escape From Duckov）

一个 mod 是 `your_mods/<mod名>/` 目录，含：

- `<ModName>.csproj` — 编译配置
- `ModBehaviour.cs` — mod 入口类（继承 `Duckov.Modding.ModBehaviour`）
- `info.ini` — mod 元信息
- `preview.png` — 256×256 预览图（Workshop 上传用，可选）

编译成 DLL，用 `validate_mod` 工具校验。以下路径均相对 workspace 根目录（不是技能目录）。

## 游戏数据速查（先读，别猜）

要查游戏里的**内容数据**（物品 / 武器 / 装备 / 图腾 / 钥匙 / 药品 / 食物；任务 / 地图 / 生物 / 天赋 / 建筑 / 商店 / 增益…）时，**不要靠试** —— 这些数据固定落在固定位置，直接读：

- **`docs/guides/00-overview.md`** —— **先读这份**：数据在哪、`inspect_game_data` 命令与路径语法、**物品分类看 Tag**、名字要 join 本地化 CSV、常见坑。
- **`docs/guides/items.md`** —— 物品与全部子类（含 134 个 Tag 对照表、以及「一次拿全 124 把枪数值」的完整命令）。
- **`docs/guides/world.md`** —— 任务 / 地图 / 生物 / 天赋 / 建筑 / 商店 / 增益（每类：类名 + 字段 + 可复制命令）。

> 以前为回答「武器数值在哪」探索了几十步；现在按上面 **1–2 次工具调用**就能拿到。数值会随版本变 —— **现查，不要抄进代码**。

## 获取游戏数据：用 `inspect_game_data`，不用运行游戏

- 要知道游戏里**是什么 / 是多少**（武器数值、任务条件、商店库存、场景对象…）→ 用 **`inspect_game_data`** 读游戏数据文件；常用命令见 `docs/guides/`。
- 要看**代码 / 类型 / 实现**（该改哪个字段、patch 哪个方法）→ 用 `inspect_game_api`。
- 这些都是**在会话里离线完成**的，**不需要玩家启动游戏**。所以需要数据/清单的 mod，直接在这儿导出、随 mod 一起交给玩家就行（不必让 mod 在运行时去生成数据）。

## 配置文件：优先 INI，且要能直接看懂

做带配置的 mod（如“改武器数值”）时，这么写玩家最好上手：

1. **格式优先 `.ini`**（而不是 JSON/XML/CSV）——玩家要手改，可读性第一；C# 侧自己写解析（几十行），或见 `reference/` 里的样例。
2. **文件顶部写使用说明**（`;` 注释）：这个文件管什么、改完怎么生效（重启还是热重载）、数值单位/取值范围。
3. **每一项都要写注释说清它在游戏里的意义**（中文），例：
   ```ini
   ; ============ 武器数据（改数字即生效）============
   ; 每个段 = 一把武器；typeID 是游戏内部 ID，请不要改（改注释里的中文名即可定位）
   [260]
   ; UP-45 —— 枪械 (Item_S_UAK45_Lv_2)
   Damage  = 9.2    ; 伤害：单发基础伤害（霰弹按弹丸数分摊）
   ShootSpeed = 15.83 ; 射速：每秒射击次数
   Capacity = 25   ; 弹匣容量
   ```
4. **默认值 = 游戏内当前值**（用 `inspect_game_data` 导出填入），玩家只需改想改的那几行；配置里写清“改哪个键影响什么”。
5. **mod 只读配置**，不负责生成/覆盖它；配置随 mod 一起发（放 mod 目录根，与 `<ModName>.dll` 同级）。

## 使用方式与条件分支

- **动手前：先把需求对齐（尤其“做什么 / 产物长什么样”）**：不要一上来就写代码。先一段话说清 **你的理解 + 方案（含你推荐的选项）+ 关键取舍**，然后**只问少数几个影响可行性/兼容性/产物形态的问题**。例：
  - 要改的是哪一类对象、只改一部分还是全部？
  - 配置是**随 mod 附一份现成文件**（玩家人手改）还是**运行时生成**？后者会要求玩家先开一次游戏。
  - 是否需要多平台（macOS + Windows）/ 多语言？
  其余细节用**合理默认**并说明（“我按 X 处理，要改再说”）；玩家回“随便/都行”就按你的推荐走。
- **咨询/可行性**：按目标检索 `docs/api/`、`docs/data/` 与 `docs/mod-api.md`，确认 API/数据依据；只是讨论时不必建目录、也不必准备环境。数据层版本见 `mod-repo.json` 的 `game.version`，不臆测最新补丁行为。`docs/api/` 只是维护者反射 dump 的**公开**签名；要**私有成员 / 实现行为 / 当前版本**时，用 `inspect_game_api` 读托管 DLL（见「参考」）。
- **实际制作/修改**：写入仅限当前 session 绑定目录。无绑定且准备写入时才调 `create_mod_folder`，选择合法 C# 标识符（通常 PascalCase）；有绑定就复用，目录缺失先说明阻塞，不另建第二个绑定。
- **规范与实现**：`reference/example_mod/` 只是**结构骨架**（csproj / info.ini / ModBehaviour 的写法与命名）—— **照抄它的结构**即可，**它不是实现来源**（里面只有一个空壳 log），别把它当作“可复用的功能代码”。
  - **实现前先看 `your_mods/` 里已有的同类 mod**（玩家自己的 / 之前做的）—— 能复用就复用，别从零写。
  - 实现依据来自 `docs/guides/`（数据与命令）、`docs/api/`（公开签名）；不够时用 `inspect_game_api` 反编译游戏 DLL。
  - 保持 info.ini 名称、AssemblyName、RootNamespace 和 DLL 名一致（validate_mod 校验）。
- **产物落点**：编译产物放哪都行 —— `install_mod` 会自动找到 `<name>.dll`（mod 根**或 `bin/` 下**）并把它放到游戏 mod 目录**根**（游戏只从根加载 `<name>.dll`）。所以用默认的 `bin/` 即可，不必手动整理。⚠️ **不要**把 csproj 的 `OutputPath` 指到项目根：SDK 默认排除会把项目根下的 `*.cs` 全排掉 → 产出**空壳 dll**（游戏里能看到、勾不上）；确需那就必须同时设 `EnableDefaultCompileItems=false` + 显式 `Compile`。
- **失败处理**：按错误定位修复；同类失败重复出现时先解决前置条件，不无限"直到 PASS"。

## info.ini

```ini
name = MyMod
displayName = 我的 Mod
description = 这个 mod 做了什么

tags = Quality of Life
version = 1.0
```

- `name`：mod 名，= 命名空间 = dll 文件名，合法 C# 标识符（validate_mod 校验）。
- `displayName`：显示名（validate_mod 校验非空）。
- `description`：描述（validate_mod 校验非空）。
- `tags`：Workshop 标签，逗号分隔，可选值见下。
- `version`：版本号。
- `publishedFileId`：Workshop 的 mod ID（上传后回填）。

### tags 可选值

Weapon / Equipment & Gear / Loot & Economy / Quality of Life / Cheats & Exploits / Visual Enhancements / Sound / Quest & Progression

## csproj 要点

- `TargetFramework = netstandard2.1`
- `AssemblyName` / `RootNamespace` = mod 名（和 info.ini 的 name 一致）
- 引用游戏 DLL（`TeamSoda.*`、`ItemStatsSystem.dll`、`Unity*`，`Private=false`）+ `libs/0Harmony.dll`（`Private=true`）
- 游戏目录用 `$(DUCKOV_DIR)` 环境变量注入（`try_set_game_dir` 发现后写进状态文件，编译时导出）
- ⚠️ **不要写死平台路径**：Managed 目录要按平台二选一 —— 用 MSBuild 条件 `$([MSBuild]::IsOSPlatform('OSX'))` 判 macOS（`Duckov.app/Contents/Resources/Data/Managed`），
  否则 `Duckov_Data/Managed`（照抄 `reference/example_mod/ExampleMod.csproj`）。写死任何一个都会让**另一台机器**编译不过 —— 本 repo 支持 macOS + Windows。

## ModBehaviour

mod 入口类，继承 `Duckov.Modding.ModBehaviour`：

```csharp
using UnityEngine;

namespace MyMod
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        void Awake() { Debug.Log("MyMod loaded!"); }
    }
}
```

命名空间通常跟 mod 名一致，类名通常叫 `ModBehaviour`（游戏按 `<name>.ModBehaviour` 加载）。

## validate_mod 工具用法

调 `validate_mod` 工具，参数 `modDir = your_mods/<mod名>/`。

- `PASS: <名> is valid` 表示本工具所需检查通过；仍不代表游戏内加载/玩法已经验证。
- `FAIL` 表示校验失败；`PARTIAL` 表示有检查未执行（例如缺 SDK 跳过编译）。依据输出文本的 `PASS`/`FAIL`/`PARTIAL` 和 `NEXT:` 行说明实际完成范围，不把旧 DLL 的存在当作本次编译成功。
- 输出含 `FAIL` 时不能声称 mod 已全部验证；最终报告实现效果、实际验证和剩余步骤。
- 编译需要 dotnet 与游戏目录：环境未就绪时先看 `setup-workspace`（`check_runtime` 查 SDK / `try_set_game_dir` 确保路径 / `install_runtime` 给装法）。

## 常见错误（对照修正）

- `FAIL: info.ini: name is missing` → info.ini 缺 name，补上。
- `FAIL: info.ini: name (...) is not a valid namespace` → name 含非法字符，改成合法 C# 标识符（PascalCase）。
- `FAIL: missing ModBehaviour.cs` → 缺入口类，创建继承 `Duckov.Modding.ModBehaviour` 的 `ModBehaviour` 类。
- `FAIL: dotnet build failed: ...` → 编译错误，读报错定位（多半是 API 用法/引用问题）。
- `FAIL: missing <ModName>.dll` → 编译没产出 dll，先跑 dotnet build。
- `WARN: dotnet not found` → 用 `check_runtime` 核实，按 `install_runtime` 返回的指引准备 SDK；再次检查确认后才能编译。工具本身不执行安装。
- `WARN: missing preview.png` → 缺预览图（不影响本地加载，Workshop 上传需要 256×256）。

## 参考

- 完整可编译样例：`reference/example_mod/`。
- API：`docs/mod-api.md`；物品：`docs/items.md`。
- **API 内省**：`docs/api/`（公开签名快照）是第一站；不够时（私有成员 / 实现行为 / 当前版本）用 **`inspect_game_api`** 读托管 DLL —— `search`（找类型/成员）· `members`（全部成员含 private）· `decompile`（反编译 C#）· `il` · `strings`。只读、不执行游戏代码。
- **内容数据（数值 / 场景）**：物品数值 / 任务条件 / 敌人预设，以及**场景**（`levelN` 文件里 GameObject / Transform / Camera 等内置对象与挂在它们上的脚本）都在**游戏数据文件**里 → 用 **`inspect_game_data`** 读（`classes` / `search` / `list` / `dump`；`dump` 加 `follow` 跟随引用，看 `Item.stats` 的 `Damage`、场景对象的 `m_LocalPosition` 等；场景用 `--file levelN`）。**先窄后宽**：别 `list` 整个大类（尤其场景里的 `GameObject` / `Transform`；`list` 最多 500 条且多为 `(no name)`）——要具体的用 `search --pattern` 或 `dump --class <C> --name/--pathid`；确实要枚举时用 `--offset` 翻页。要**一批**（如“全部武器的数值”“全部任务的条件”）用 **`export`**：一类对象 × `--match` 过滤 × `--field` 路径 → **一次调用一张表**（路径可穿 PPtr / 展开数组 / 用 `#class` 按解析后类名筛；见 `tools/data-probe/README.md`）。**结果大就落盘**：`out=<file>` → 写文件、只回预览（数据不进上下文），再用 bash 处理。**只读、只用来"查清楚现在是什么样"**（好让 mod 的 C# 在运行时找到/补丁它们）；**改数值/改行为一律是 mod 运行时 C# 的事**（先 `inspect_game_api` 找字段/patch 点）——不改数据文件，也**不是编辑场景**。

## 相关技能

- 路径/运行时没准备好 → `setup-workspace`。
- 产物要装进游戏 → `mod-installer`。
