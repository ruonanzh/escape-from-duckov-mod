# 游戏数据查询指南（总览）—— 先读这份

目的：**让你不用猜**。这些数据固定落在固定位置。下面告诉你：概念对应哪份数据，以及要复制哪条命令。

| 想查 | 去读 |
|---|---|
| 物品与全部子类（武器、装备、图腾、钥匙、药品、食物、材料、种子、工具）| `docs/guides/items.md` |
| 任务、地图、生物、天赋、建筑、商店、增益 | `docs/guides/world.md` |

> 这里写的是**怎么查**。文中出现的数字是**撰写时实测值，仅供对照**，会随版本变，精确值一律现查。

## 三条工具的分工

| 工具 | 读什么 | 什么时候用 |
|---|---|---|
| `inspect_game_data` | **游戏数据**：游戏数据文件里的序列化对象（物品数值、任务条件、场景对象）| 「现在是多少」或「有哪些」|
| `inspect_game_api` | **代码**：托管 DLL 的类型、成员与实现 | 「怎么实现的」或「该改哪个字段」|
| `inspect_game_code` | **实现**：dump 里的声明与调用位置 | 「谁调用了它」|

## 数据在哪（文件层面，bash 可能用到）

- **macOS**：`<gameDir>/Duckov.app/Contents/Resources/Data/`
- **Windows**：`<gameDir>/Duckov_Data/`

| 文件 | 内容 |
|---|---|
| `resources.assets` | **绝大多数配置**（物品、任务、天赋、增益、商店）|
| `sharedassets*.assets` | 更多对象 |
| `levelN`（共 63 个）| **场景**（用 `file=levelN` 读）|
| `StreamingAssets/Localization/*.csv` | **本地化文本**（见下）|

> 不给 `file` 时默认扫**所有** `.assets`。要限定就写 `file=resources.assets`。

## 三条「不用猜」的规则

### 1. 先看有哪些类：`classes`

```
action=classes                       # 960 个类，12.9 万对象（含内置类型，例如 GameObject、Transform）
```

### 2. 物品分类看 **Tag** —— 这是游戏官方的分类法

`Item.tags` 指向一组 `Tag` 资产。`Tag.m_Name` 就是分类名（`Weapon`、`Armor`、`Helmat`、`Food`、`Key`、`Totem`）。

```
# 枚举全部 134 个 Tag
action=export, class=Tag, file=resources.assets

# 用 tag 过滤任意物品（通用写法）
action=export, class=Item, match=["tags.list[].#name=Key"]
```

### 3. 名字通常是**本地化 key**，要 join CSV

`Item.displayName`、`Quest.displayName`、`Buff.displayName`、`Level_*` 这几类多是 **key**（例如 `Item_ButcherKnife`、`Buff_Pain`），不是玩家看到的文字。

- 文件：`<Data>/StreamingAssets/Localization/ChineseSimplified.csv`（表头 `key,value,version,sheet`）。共 10 个语言，名单与规则只在 `docs/cookbook.md` 第 14 节
- 实测：`Item_AnimalWeapon,空手`

join 示例（bash 与 python，可直接改）：

```bash
# 先导出（第 4 列 = displayName；不给 field= 就只有 3 列，下面的 c[3] 会越界）
# inspect_game_data: action=export, class=Item, file=resources.assets, field=["displayName"], out="/tmp/items.tsv"
python3 - <<'PY'
import csv
LOC = "<gameDir>/Duckov.app/Contents/Resources/Data/StreamingAssets/Localization/ChineseSimplified.csv"  # macOS
# Windows 改用：<gameDir>/Duckov_Data/StreamingAssets/Localization/ChineseSimplified.csv
loc = {}
with open(LOC, encoding='utf-8-sig') as f:
    for row in csv.DictReader(f):
        loc[row['key']] = row['value']
# /tmp/items.tsv 是 export --out 出来的（第 4 列 = displayName）
for line in open('/tmp/items.tsv', encoding='utf-8'):
    if line.startswith('#') or line.startswith('name\t'): continue
    c = line.rstrip('\n').split('\t')
    print(c[0], '->', loc.get(c[3], c[3]))
PY
```

## `inspect_game_data` 命令速查

**本节只讲机制**（参数含义、路径语法、上限与落盘）。**「我要查 X 用哪条」的任务清单只有一份，在 `docs/cookbook.md` 第 13 节。**

| 目的 | 调用 |
|---|---|
| 找对象 | `action=search`(`pattern`) 或 `action=list`(`class`) |
| 看一个对象 | `action=dump`(`class` 加 `name`、`typeid` 或 `pathid`。`follow` 解引用。`depth` 深度) |
| **批量表** | `action=export`(`class` 加 `match` 过滤加 `field` 列。**大结果用 `out` 落盘**) |
| **完整结果落盘** | 任何 action 都能加 `out=<file>` 来**流式写文件**（内存恒定，数据一条不少）。不给 `out` 则**显示**到 `limit` 行就停，并提示你落盘。落盘默认上限 **100 万行**（`dump --depth 7 --follow` 是指数展开，实测写出过 1.4 亿行与几 GB）。要控制行数：`export` 用 `rows=<N>`（带 `out` 时 `limit` 会被忽略）；其它 action 用 `limit=<N>`。 |
| **反查：谁用了这个对象** | `action=refs` 加 `by=<类名>`（例如 `by=MeshFilter`）。正向 `refs` 只有「我引用了谁」。跨文件时 `file` 要给**两个**（逗号分隔，例如 `file=sharedassets4.assets,level4`）|
| **按 mesh 名一步拿尺寸** | `action=transform` 加 `mesh=<mesh 名>`（可以用 `exact` 严格相等，子串会撞名；用 `has=<组件>` 过滤）|
| **模型真实尺寸与挂点位置** | `action=transform`(`name` 或 `pathid` 加 `depth`。**必须加 `file=levelN`**，场景与 prefab 在那里)。Unity 的 Transform **每级都是 local**，所以尺寸与挂点只能**沿父链累计**才拿得到。挂了 `MeshFilter` 时顺带给出 `mesh AABB × 累计缩放`，也就是**游戏内真实尺寸（米）**（例如 `Rifle02` 得到 **0.998 m**）|

同一字段可给多个 `field`。`match` 也可给多个（AND）。

**路径语法**（`field` 与 `match` 共用）：`a.b` 字段，`a[]` 或 `a[i]` 展开数组或取下标，`#class` 或 `#name` 取「解析后对象」的类名或名字。**PPtr 自动跟随**。

两条**实测定下的读取规则**：

- **结构体要写到叶子**：只写 `dimensions` 会得到**空列**。要写 `dimensions.x` 或 `dimensions.y`（同理 `m_LocalPosition.x`）。
- **PPtr 字段默认只给 `pathID N`**。想要对象的**名字或类名**就加 `.#name` 或 `.#class`（例如 `perks.#name`、`m_Component[].component.#class`）。

**运算符**：`=`、`!=`、`~`（子串，忽略大小写）、`>`、`>=`、`<`、`<=`。

## 常见坑

- `dump` 的 `follow` **只下钻一层**。想要深层值就用 `export` 的路径（例如 `stats.list[].key`）。
- `list` 与 `search` 每次最多 **500** 条（用 `offset` 翻页）。`export` 不给 `out` 只回 500 行；给了 `out` 落盘，上限 **100 万行**（`tools/data-probe/Program.cs` 的 `Out` 构造函数）。
- 大结果**不得直接进上下文**：用 `out=<file>`，工具只回预览。再用 bash 或 python 处理那个文件。
- 场景（`levelN`）只有显式 `file=levelN` 才读。默认不扫。
- 结构体字段要写全路径（`a.b.x`）。写 `a` 会得到空列。
- **嵌套数组会拍平**：`a[].b[].c` 输出的是**一条**平行数组，**不保留 `a` 的分组边界**（例如 `StockShopDatabase` 的 merchant 与 entries）。要保留嵌套就用 `dump`（带 `depth`）。
- **不得把查到的数值抄进代码**（会随版本变）。用这里的命令现查。
- **空结果不等于「没有」**：看退出码。`0` = 成功（合法空结果会带 `# WARNING: … matched nothing`）、`1` = 目标不存在（类名/名字/pathID 查不到，会带 `# ERROR` 与近邻建议）、`2` = 调用方式错（未知 flag、flag 缺值、`inspect_game_api` 没给 `dll=`）。判据见 `tools/data-probe/README.md` 与 `tools/api-probe/README.md` 的「出口码与静默边界」。

## 文本从哪来：只认游戏自带的那份

文案一律读游戏自带的 `<Data>/StreamingAssets/Localization/<语言>.csv`（实时、多语言、是超集）。

**规则、sheet 归属的坑、两条「不得」只有一份，在 `docs/cookbook.md` 第 14 节。** 这里不复述。

资产清单也一样：用 `classes` / `search` / `export --class X` **现查**（任务清单见 `docs/cookbook.md` 第 13 节）。

## 这份指南没写到怎么办（**不得停在这里**）

本指南只覆盖「最高频的那几类」。**没写到的概念，照样有路**：

1. **不知道类名**时，用 `action=classes`（960 个类）grep 关键词，或用 `action=search, pattern=关键词`（名、类或 typeID 子串）。
2. **知道类名、不知道字段**时，先拿一个对象 `action=export, class=<C>, rows=1`（看首行的 `pathID`），再 `action=dump, class=<C>, pathid=<pid>, depth=1` 看它的字段名。然后用 `export` 按路径取。
3. **要看实现、字段含义或怎么改**时，先读 `docs/api/`。一个类型的私有成员、IL 或字符串用 `inspect_game_api`。全库声明与调用位置、一个类型或成员的实现用 `inspect_game_code`，见 `docs/guides/code.md`。
   类型可能住在多个 DLL 里：`search` 查不到时，用 `dll=` 指定程序集（例如 `TeamSoda.*` 或 `*`），不得急着下结论说「没有这个类型」。
4. **要文案**时，读游戏自带的本地化 CSV（上节）。
5. **场景里的对象与坐标**时，用 `file=levelN` 与 `class=GameObject`（见 `world.md` 的 Maps 段）。

> 找到了新的固定套路（某类对象的判别方式与可复制命令）时，**补进本目录对应的 `.md`**。下一个 agent 就不用再探索了。
