# 游戏数据查询指南（总览）—— 先读这份

目的：**让你不用猜**。这些数据固定落在固定位置；下面告诉你「概念 → 数据在哪 → 复制哪条命令」。

| 想查 | 去读 |
|---|---|
| 物品与全部子类（Weapon / Equipment / Totem / Key / Medicine / Food / 材料 / 种子 / 工具 …）| `docs/guides/items.md` |
| 任务 / 地图 / 生物 / 天赋 / 建筑 / 商店 / 增益 | `docs/guides/world.md` |

> 这里写的是**怎么查**，不写死具体数值（数值会随版本变，现查）。

## 两条工具的分工

| 工具 | 读什么 | 什么时候用 |
|---|---|---|
| `inspect_game_data` | **内容数据**：游戏数据文件里的序列化对象（物品数值、任务条件、场景对象…）| 「现在是多少 / 有哪些」|
| `inspect_game_api` | **代码**：托管 DLL 的类型 / 成员 / 实现 | 「怎么实现的 / 该改哪个字段」|

## 数据在哪（文件层面，bash 可能用到）

- **macOS**：`<gameDir>/Duckov.app/Contents/Resources/Data/`
- **Windows**：`<gameDir>/Duckov_Data/`

| 文件 | 内容 |
|---|---|
| `resources.assets` | **绝大多数配置**（物品、任务、天赋、增益、商店…）|
| `sharedassets*.assets` | 更多对象 |
| `levelN`（共 63 个）| **场景**（用 `file=levelN` 读）|
| `StreamingAssets/Localization/*.csv` | **本地化文本**（见下）|

> 不给 `file` 时默认扫**所有** `.assets`；要限定就 `file=resources.assets`。

## 三条「不用猜」的规则

### 1. 先看有哪些类：`classes`

```
action=classes                       # 960 个类 / 12.9 万对象（含 GameObject/Transform 等内置类型）
```

### 2. 物品分类看 **Tag** —— 这是游戏官方的分类法

`Item.tags` 指向一组 `Tag` 资产，`Tag.m_Name` 就是分类名（`Weapon` / `Armor` / `Helmat` / `Food` / `Key` / `Totem` …）。

```
# 枚举全部 134 个 Tag
action=export, class=Tag, file=resources.assets

# 用 tag 过滤任意物品（通用写法）
action=export, class=Item, match=["tags.list[].#name=Key"]
```

### 3. 名字通常是**本地化 key**，要 join CSV

`Item.displayName`、`Quest.displayName`、`Buff.displayName`、`Level_*` 等多是 **key**（如 `Item_ButcherKnife`、`Buff_Pain`），不是玩家看到的文字。

- 文件：`<Data>/StreamingAssets/Localization/ChineseSimplified.csv`（表头 `key,value,version,sheet`；另有 `English/ChineseTraditional/Japanese/...`）
- 实测：`Item_AnimalWeapon,空手`

join 示例（bash + python，可直接改）：

```bash
python3 - <<'PY'
import csv
LOC = "<gameDir>/Duckov.app/Contents/Resources/Data/StreamingAssets/Localization/ChineseSimplified.csv"  # Windows 改路径
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

| 目的 | 调用 |
|---|---|
| 有哪些类 | `action=classes` |
| 找对象 | `action=search`(`pattern`) / `action=list`(`class`) |
| 看一个对象 | `action=dump`(`class` + `name`/`typeid`/`pathid`；`follow` 解引用；`depth` 深度) |
| **批量表** | `action=export`(`class` + `match` 过滤 + `field` 列；**大结果用 `out` 落盘**) |
| 它引用了谁 | `action=refs` |

同一字段可给多个 `field`；`match` 也可给多个（AND）。

**路径语法**（`field` / `match` 共用）：`a.b` 字段 · `a[]` / `a[i]` 展开数组 / 取下标 · `#class` / `#name` 取「解析后对象」的类名/名字 · **PPtr 自动跟随**。

两条**实测定下的读取规则**：

- **结构体要写到叶子**：`dimensions` → **空列**；要 `dimensions.x` / `dimensions.y`（同理 `m_LocalPosition.x`）。
- **PPtr 字段默认只给 `pathID N`**；想要对象的**名字/类名**就加 `.#name` / `.#class`（例：`perks.#name`、`m_Component[].component.#class`）。

**运算符**：`=` · `!=` · `~`（子串，忽略大小写） · `>` `>=` `<` `<=`。

## 常见坑

- `dump` 的 `follow` **只下钻一层** → 想要深层值用 `export` 的路径（如 `stats.list[].key`）。
- `list` / `search` 每次最多 **500** 条（用 `offset` 翻页）；`export` 默认 500 行，**给了 `out` 则不限**。
- 大结果**不要直接进上下文**：`out=<file>` → 只回预览，再用 bash/python 处理那个文件。
- 场景（`levelN`）只有显式 `file=levelN` 才读；默认不扫。
- 结构体字段写全路径（`a.b.x`），写 `a` 会得到空列。
- **嵌套数组会拍平**：`a[].b[].c` 输出的是**一条**平行数组，**不保留 `a` 的分组边界**（例：`StockShopDatabase` 的 merchant→entries）。要保留嵌套用 `dump`（带 `depth`）。
- **别把查到的数值抄进代码**（会随版本变）——用这里的命令现查。

## 文本从哪来：本地化用**游戏自带**的，`docs/data/` 是旧快照

- 优先：`<Data>/StreamingAssets/Localization/{ChineseSimplified,ChineseTraditional,English,Japanese,...}.csv`（**实时、多语言、是超集**；含 `key,value,version,sheet`，sheet 就是 `Items`/`Quests`/`Buffs`/…）。
- `docs/data/*.csv`（repo 里那份）是**维护者导出的快照**：同名 sheet 但**条目少得多**（例：Items 892 vs 本地化 2944），且可能落后于玩家版本 → **能用游戏本体就用本体**；仅当不想开游戏时当参考。
- `docs/data/resources.csv`（`type,name` 资产清单）≈ 用 `classes` / `export --class X` **现查**，比快照准。

## 这份指南没写到怎么办（**别停在这里**）

本指南只覆盖“最高频的那几类”。**没写到的概念，照样有路**：

1. **不知道类名** → `action=classes`（960 个类）grep 关键词；或 `action=search, pattern=关键词`（名/类/typeID 子串）。
2. **知道类名、不知道字段** → 拿一个对象 `action=export, class=<C>, rows=1`（看首行的 `pathID`）→ 再 `action=dump, class=<C>, pathid=<pid>, depth=1` 看它的字段名；然后用 `export` 按路径取。
3. **要看实现/字段含义/怎么改** → `inspect_game_api`（`search` → `members` → `decompile`）；公开签名快照在 `docs/api/`。
4. **要文案** → 本地化 CSV（上节）或 `docs/data/*.csv`（快照）。
5. **场景里的对象/坐标** → `file=levelN` + `class=GameObject`（见 `world.md` 的 Maps 段）。

> 找到了新的固定套路（某类对象的判别方式 + 可复制命令）→ **请补进本目录对应的 `.md`**，下一个 agent 就不用再探索了。
