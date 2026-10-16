---
name: mod-creator
description: Escape From Duckov 的 C# mod 制作与修改。覆盖目录结构、info.ini 字段、csproj 要点、ModBehaviour 入口类、游戏数据查询（`docs/guides/`）、配置文件写法，以及用 validate_mod 编译校验。当玩家新建、修改或解释一个 mod 怎么写，查游戏数据，或排查编译与校验错误时读取。
---

# 做 mod（Escape From Duckov）

一个 mod 是 `your_mods/<目录名>/` 目录。它包含：

- `<ModName>.csproj` — 编译配置
- `ModBehaviour.cs` — mod 入口类（继承 `Duckov.Modding.ModBehaviour`）
- `info.ini` — mod 元信息
- `preview.png` — 256×256 预览图（Workshop 上传用，可选）

编译成 DLL，用 `validate_mod` 工具校验。以下路径均相对工作区根目录，不是技能目录。

## 游戏数据速查（先读，不得猜）

查游戏里的**游戏数据**时，**不得靠试**。这些数据落在固定位置，直接读。范围包括物品、武器、装备、图腾、钥匙、药品、食物、任务、地图、生物、天赋、建筑、商店、增益：

- **`docs/guides/00-overview.md`**：**先读这份**。它写明数据在哪、`inspect_game_data` 命令与路径语法、**物品分类看 Tag**、名字必须 join 本地化 CSV，以及常见坑。
- **`docs/guides/items.md`**：物品与全部子类。含 134 个 Tag 对照表，以及「一次拿全 124 把枪数值」的完整命令。
- **`docs/guides/world.md`**：任务、地图、生物、天赋、建筑、商店、增益。每类给出类名、字段与可复制命令。

> 以前为回答「武器数值在哪」探索了几十步。现在按上面 **1–2 次工具调用**就能拿到。数值会随版本变，**必须现查，不得抄进代码**。

## 获取游戏数据：用 `inspect_game_data`，不得运行游戏

- 查游戏里的**是什么与是多少**（武器数值、任务条件、商店库存、场景对象）时，用 **`inspect_game_data`** 读游戏数据文件。常用命令见 `docs/guides/`。
- 查**代码、类型与实现**（该改哪个字段、patch 哪个方法）时，用 `inspect_game_api`。
- 这些都在**会话里离线完成**，**不需要玩家启动游戏**。所以需要数据或清单的 mod 可以在这里导出，再随 mod 一起交给玩家。mod 不必在运行时生成数据。

## 配置文件：优先 INI，必须能直接看懂

做带配置的 mod（例如改武器数值）时，按下面的写法，玩家最容易上手：

1. **格式优先 `.ini`**，不选 JSON、XML 或 CSV。玩家要手改，可读性第一。C# 侧自己写解析（几十行），或见 `reference/` 里的样例。
2. **文件顶部写使用说明**（`;` 注释）：这个文件管什么、改完怎么生效（重启还是热重载）、数值的单位与取值范围。
3. **每一项都必须写注释，说清它在游戏里的意义**（中文）。例：
   ```ini
   ; ============ 武器数据（改数字即生效）============
   ; 每个段表示一把武器。typeID 是游戏内部 ID，不得改（改注释里的中文名即可定位）
   [260]
   ; UP-45 —— 枪械 (Item_S_UAK45_Lv_2)
   Damage  = 9.2    ; 伤害：单发基础伤害（霰弹按弹丸数分摊）
   ShootSpeed = 15.83 ; 射速：每秒射击次数
   Capacity = 25   ; 弹匣容量
   ```
4. **默认值等于游戏内当前值**（用 `inspect_game_data` 导出填入）。玩家只需改想改的那几行。配置里写清「改哪个键影响什么」。
5. **mod 只读配置**，不负责生成或覆盖它。配置随 mod 一起发，放在 mod 目录根，与 `<ModName>.dll` 同级。

## 新增物品（数据层）

克隆一个现有物品，再注册成新物品（社区 mod 实测可行）。**优先用库里的 `ItemFactory.CloneAsNewItem(源typeID, 新typeID, 显示名, 简介)`**
（内置 `DontDestroyOnLoad`、名字与简介走本地化、`useSpriteForPickup=false`）。
想加一把新枪就走 `.pi/skills/add-weapon/`（那里有 config 字段表与完整流程）。
想加一件新物品（消耗品 / 装备 / 背包 / 材料 / 可展示收藏品）就走 `.pi/skills/add-item/`。
手写版本：

- 克隆：`Instantiate(ItemAssetsCollection.GetPrefab(源typeID).gameObject)`。必须调用 `DontDestroyOnLoad`。
  然后调用 `GetComponent<Item>()`。再调用 `SetTypeID(新typeID)`。
- 名字：`LocalizationManager.SetOverrideText(key, 文本)` 与 `DisplayNameRaw = key`。
  直接塞原文，游戏会把它当**翻译键**，显示成带星号的样子（`*超大纤维束*`）。
- 注册：`ItemAssetsCollection.AddDynamicEntry(item)`。同一个 typeID 会覆盖原版并打警告。失败时必须销毁克隆体。
- **模型**由模型侧提供：`ItemFactory.AttachModel(item, mesh, 贴图, size)`（内部**克隆图形**，不动原型）与 `ItemModel.WriteGraphic(item, graphic)`。
  `itemGraphic` 是数据层与模型层唯一的连接点。
- **运行时造的对象必须 `DontDestroyOnLoad`**（图形克隆、新物品模板、手持实体）。否则换场景时（菜单到关卡）这些对象被销毁。`itemGraphic` 变成「已销毁引用」（Unity 的 `== null` 判 true），物品回退成图标。
- 注意：注册通常发生在**进关卡之前**（那时没有玩家），发放或掉落到地上必须等进关卡后再做。新物品还需要**获得途径**（工作台配方、掉落或发放）。

## 使用方式与条件分支

- **动手前：先确认需求再动手**。先跟玩家对齐「这个 mod 是什么」（改什么、覆盖范围、产物的形态）。给出**一份 mod 内容描述**。玩家确认后再开始写代码。
- **咨询与可行性**：按目标检索 `docs/api/` 与 `docs/mod-api.md`，确认 API 与数据依据；游戏数据用 `inspect_game_data` 现查。
- **实际制作与修改**：目录名与 mod 名是两个名字。目录名给 `create_mod_folder`，用小写 snake_case（小写字母、数字、下划线，字母开头）。mod 名写进 `info.ini` 的 `name`，是合法 C# 标识符，通常 PascalCase。写入仅限当前 session 绑定的目录。无绑定且准备写入时才调 `create_mod_folder`。有绑定就复用。目录缺失时先说明阻塞，不得另建第二个绑定。
- **规范与实现**：`reference/example_mod/` 只作**骨架参考**（csproj、info.ini、ModBehaviour 的写法与命名）。保持 info.ini 的 mod 名、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace 一致（validate_mod 校验）。
- **产物落点**：编译产物放哪都行。`install_mod` 会自动找到 `<name>.dll`（mod 根**或 `bin/` 下**），并把它放到游戏 mod 目录**根**。游戏只从根加载 `<name>.dll`。所以用默认的 `bin/` 即可，不必手动整理。**不得**把 csproj 的 `OutputPath` 指到项目根。SDK 的默认排除会把项目根下的 `*.cs` 全排掉，产出**空壳 dll**（游戏里能看到，却勾不上）。确需如此时必须同时设 `EnableDefaultCompileItems=false`，并加显式 `Compile`。
- **失败处理**：按错误定位修复。同类失败重复出现时先解决前置条件。不得无限「直到 PASS」。

## info.ini

```ini
name = MyMod
displayName = 我的 Mod
description = 这个 mod 做了什么

tags = Quality of Life
version = 1.0
```

- `name`：mod 名，等于命名空间，等于 dll 文件名。必须是合法 C# 标识符（validate_mod 校验）。
- `displayName`：显示名（validate_mod 校验非空）。
- `description`：描述（validate_mod 校验非空）。
- `tags`：Workshop 标签，逗号分隔，可选值见下。
- `version`：版本号。
- `publishedFileId`：Workshop 的 mod ID（上传后回填）。

### tags 可选值

- Weapon
- Equipment & Gear
- Loot & Economy
- Quality of Life
- Cheats & Exploits
- Visual Enhancements
- Sound
- Quest & Progression

## csproj 要点

- `TargetFramework = netstandard2.1`
- `AssemblyName` 与 `RootNamespace` 等于 mod 名（和 info.ini 的 name 一致）
- 引用游戏 DLL（`TeamSoda.*`、`ItemStatsSystem.dll`、`Unity*`，`Private=false`）与 `libs/0Harmony.dll`（`Private=true`）
- 游戏目录用 `$(DUCKOV_DIR)` 环境变量注入（`try_set_game_dir` 发现后写进状态文件，编译时导出）
- **不得写死平台路径**。Managed 目录必须按平台二选一：用 MSBuild 条件 `$([MSBuild]::IsOSPlatform('OSX'))` 判 macOS（`Duckov.app/Contents/Resources/Data/Managed`），否则 `Duckov_Data/Managed`（照抄 `reference/example_mod/ExampleMod.csproj`）。写死任何一个都会让**另一台机器**编译不过。本工作区支持 macOS 与 Windows。
- 要用共用库 `libs/mod-kit/` 时，用通配引用：

  ```xml
  <Compile Include="../../libs/mod-kit/*.cs" />
  ```

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

命名空间通常与 mod 名一致，类名通常叫 `ModBehaviour`（游戏按 `<name>.ModBehaviour` 加载）。

## validate_mod 工具用法

调 `validate_mod` 工具，参数 `modDir = your_mods/<目录名>/`。

- `PASS: <名> is valid` 表示本工具所需检查通过。它仍不代表游戏内加载与玩法已经验证。
- `FAIL` 表示校验失败。`PARTIAL` 表示有检查未执行（例如缺 SDK 跳过编译）。依据输出文本的 `PASS`、`FAIL`、`PARTIAL` 与 `NEXT:` 行说明实际完成范围。不得把旧 DLL 的存在当作本次编译成功。
- 输出含 `FAIL` 时不得声称 mod 已全部验证。最终报告实现效果、实际验证与剩余步骤。
- 编译需要 dotnet 与游戏目录。环境未就绪时先看 `setup-workspace`（`check_runtime` 查 SDK，`try_set_game_dir` 确保路径，`install_runtime` 给装法）。

## 常见错误（对照修正）

- `FAIL: info.ini: name is missing`：info.ini 缺 name，补上。
- `FAIL: info.ini: name (...) is not a valid namespace`：name 含非法字符。改成合法 C# 标识符（PascalCase）。
- `FAIL: missing ModBehaviour.cs`：缺入口类。创建继承 `Duckov.Modding.ModBehaviour` 的 `ModBehaviour` 类。
- `FAIL: dotnet build failed: ...`：编译错误。读报错定位（多半是 API 用法或引用问题）。
- `FAIL: missing <ModName>.dll`：编译没产出 dll。先跑 dotnet build。
- `WARN: dotnet not found`：用 `check_runtime` 核实，按 `install_runtime` 返回的指引准备 SDK。再次检查确认后才能编译。工具本身不执行安装。
- `WARN: missing preview.png`：缺预览图（不影响本地加载，Workshop 上传需要 256×256）。

## 参考

- 完整可编译样例：`reference/example_mod/`。
- API：`docs/mod-api.md`。物品：`docs/items.md`。
- **API 内省**：`docs/api/`（公开签名快照）是第一站。不够时（私有成员、实现行为、当前版本）用 **`inspect_game_api`** 读托管 DLL：`search`（找类型与成员）、`members`（全部成员含 private）、`decompile`（反编译 C#）、`il`、`strings`。只读，不执行游戏代码。
- **游戏数据（数值与场景）**：物品数值、任务条件、敌人预设，以及**场景**（`levelN` 文件里的内置对象（GameObject、Transform、Camera）与挂在它们上的脚本）都在**游戏数据文件**里。用 **`inspect_game_data`** 读：`classes`、`search`、`list`、`dump`。`dump` 加 `follow` 跟随引用，看 `Item.stats` 的 `Damage`，以及场景对象的 `m_LocalPosition`。场景用 `--file levelN`。**先窄后宽**：不得 `list` 整个大类（尤其场景里的 `GameObject` 与 `Transform`；`list` 最多 500 条，且多为 `(no name)`）。要具体对象时用 `search --pattern` 或 `dump --class <C> --name/--pathid`。确实要枚举时用 `--offset` 翻页。要**一批**（例如全部武器的数值、全部任务的条件）时用 **`export`**：一类对象乘 `--match` 过滤乘 `--field` 路径，**一次调用一张表**（路径可以穿 PPtr、展开数组、用 `#class` 按解析后类名筛。见 `tools/data-probe/README.md`）。**结果大就落盘**：`out=<file>` 写文件、只回预览（数据不进上下文），再用 bash 处理。**它只读，只用来「查清楚现在是什么样」**（好让 mod 的 C# 在运行时找到或补丁它们）。**改数值与改行为一律是 mod 运行时 C# 的事**（先 `inspect_game_api` 找字段或 patch 点）。不得改数据文件，也不得编辑场景。

## 相关技能

- 路径或运行时没准备好时，用 `setup-workspace`。
- 产物要装进游戏时，用 `mod-installer`。
