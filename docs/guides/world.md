# 任务 / 地图 / 生物 / 天赋 / 建筑 / 商店 / 增益

每一类都给出：**类名**（固定）· 常用字段（实测）· **可直接复制的命令**。数量是撰写时的实测值，仅供对照。

所有命令都省略了公共参数 `--file resources.assets`（下同）；用工具时对应 `file="resources.assets"`。

## 任务 Quests — `Quest`（315）

字段：`id` `displayName` `description` `requireLevel` `requiredItemID` `requiredItemCount` `requireSceneID` `questGiverID` `prerequisit` `tasks`

```
# 全部任务（170 条有等级要求）
action=export, class=Quest, match=["requireLevel>=1"],
field=["id","displayName","requireLevel","requiredItemID","requiredItemCount","requireSceneID"]
```

- 前置/子任务：`prerequisit`、`tasks`（→ 引用其它任务对象；用 `dump` + `follow` 看一层）。
- 任务流程/UI 的类：`QuestManager`(1) / `QuestCollection`(1) / `QuestGiver`(10)。
- `displayName` 是 key，join 本地化 CSV 得到中文（见 `00-overview.md`）。

## 地图 / 场景 Maps

| 数据 | 类 / 位置 | 实测 |
|---|---|---|
| **场景清单** | `SceneInfoCollection`（1 个资产，`entries[]`）| 每个 entry：`id`、`displayName`、`sceneReference.guid` |
| **场景文件** | `Data/levelN` | 63 个 |
| 地图选择入口（UI）| `MapSelectionEntry`（7）| `master` `displayNameText` `conditions` `cost` |

```
# 一次拿到全部场景的 id + 名字（两条平行数组）
action=export, class=SceneInfoCollection,
field=["entries[].id","entries[].displayName"], file="resources.assets"

# 场景里有哪些对象（场景用 file=levelN；level0 = 主菜单，实测 111 个 Button、1011 个 GameObject）
action=classes, file=level0
action=export, class=GameObject, file=level0, match=["m_Name~Button"],
field=["m_Name","m_Component[].component.#class"]          # 第二列 = 这个对象身上挂了哪些组件（类名）
```

> `field=["m_Component[].component.#class"]` 是**场景类问题的常用招式**：一次看出每个对象挂了什么脚本（如 `Transform;SteamManager;SteamWorkshopManager`）。

## 生物 / 敌人 Creatures

| 用途 | 类 | 实测 |
|---|---|---|
| **敌人预设**（随机刷怪、BOSS）| `CharacterRandomPreset` | 156（BOSS **61**）|
| 角色模型 / 外观 | `CharacterModel`(58) / `CharacterSubVisuals`(433) | |

`CharacterRandomPreset` 字段：`nameKey` `isBoss` `team` `lootBoxPrefab` `dropBoxOnDead` `showName` `voiceType` `footstepMaterialType`

```
# 全部 BOSS 预设
action=export, class=CharacterRandomPreset, match=["isBoss=1"],
field=["nameKey","lootBoxPrefab","team","showName"]
```

- `nameKey` 是本地化 key（如 `Cname_SchoolBully`）→ join CSV 得名字。
- 角色行为/属性代码：`CharacterMainControl`、`AICharacterController`（用 `inspect_game_api` 看）。

## 天赋 / 技能树 Perks — `Perk`（163）

字段：`displayName` `quality` `defaultUnlocked` `unlocking` `requirement` `icon` `master`(→ 技能树) `hasDescription`

```
# 全部天赋
action=export, class=Perk,
field=["displayName","quality","defaultUnlocked","requirement"]

# 技能树（分组的根）；`perks` 指向的是**升级项对象**，加 `.#name` 才能看到名字（否则只有 pathID）
action=export, class=PerkTree, field=["perks.#name"], file="resources.assets"
```

相关：`PerkTree`(10) / `PerkTreeIDList`(2) / `ModifierDescriptionCollection`(752，属性/修改器文案)。

## 建筑 Buildings — `Building`（29）

字段：`id`（如 `PetHouse`）`dimensions`（**结构体，要写到 `.x`/`.y`**）`graphicsContainer` `functionContainer` `unlockAchievement`

```
action=export, class=Building, field=["id","dimensions.x","dimensions.y","unlockAchievement"]
# -> PetHouse 2 2 0 ; Merchant_Equipment 7 4 0 ...
```

> 注意：`Building` 资产的 `m_Name` 是空的 —— **要看 `id` 字段**，不是 `name`。

## 商店 Shops — `StockShop`（12）

字段：`merchantID`（如 `Merchant_Normal`）`DisplayNameKey` `sellFactor`（回收价倍率）`refreshAfterTimeSpan` `overrideSellingPrice` `refreshStockOnStart` `returnCash`

```
action=export, class=StockShop,
field=["merchantID","sellFactor","refreshAfterTimeSpan","DisplayNameKey"]
```

相关：`StockShopDatabase`(1) / `StockShopItemEntry`(1) / `StockShopView`(1)。

**商品清单在 `StockShopDatabase.merchantProfiles[].entries[]`**（每个 entry：`typeID` `maxStock` `forceUnlock` `priceFactor` `possibility`）—— 一次拿到**每个商人卖什么、备货多少**：

```
action=export, class=StockShopDatabase, file="resources.assets",
field=["merchantProfiles[].merchantID",
       "merchantProfiles[].entries[].typeID",
       "merchantProfiles[].entries[].maxStock",
       "merchantProfiles[].entries[].priceFactor"]
# merchantID 与 entries 的字段各自平行（同一层内同序）
# ⚠️ 但**跨商人会被拍平**：entries 的所有条目连成一条数组，看不出哪段属于哪个商人
```

> ⚠️ **要保留“哪个商人卖什么”的边界**，改用它（保留嵌套）：
> `action=dump, class=StockShopDatabase, pathid=76730, depth=8`（每个 profile 一块：`merchantID` + `entries[]`）；
> 或拿 `entries[].typeID` 的全量清单，再按 typeID 去 `items.md` 查名字。

> `typeID` 对应 `Item.typeID` —— 要名字就拿着 typeID 去 `items.md` 的本地化 join 里查。

## 增益 / Buff — `Buff`（115）

字段：`id` `displayName` `description` `maxLayers` `exclusiveTag` `limitedLifeTime` `totalLifeTime` `effects` `hide` `icon`

```
action=export, class=Buff,
field=["id","displayName","maxLayers","exclusiveTag","totalLifeTime","hide"]
```

相关：`AddBuff`(54) / `AddBuffAction`(54)（谁给谁上 buff 的动作）。

## 其它可能用到的类

| 概念 | 类 | 实测 |
|---|---|---|
| 成就 | `AchievementDatabase`(1) / `AchievementManager`(1) | |
| 对话 | `Dialogue*`（`docs/data/Dialogues.csv` 有文本）| |
| 本地化 | `LocalizationDatabase`(1) | |

## 制作 / 配方 Crafting — `CraftingFormulaCollection`（1 个资产，`list[]`）—— **269 条配方**

每条配方：`id` · `result.id`（产物 `typeID`）`result.amount` · `cost.money` · `cost.items[].id` `cost.items[].amount`（**材料**）· `tags[]`（如 `WorkBenchAdvanced` = 工作台等级）· `requirePerk` · `unlockByDefault` · `hideInIndex`

```
action=export, class=CraftingFormulaCollection, file="resources.assets",
field=["list[].id","list[].result.id","list[].result.amount","list[].cost.money",
       "list[].cost.items[].id","list[].cost.items[].amount","list[].tags[]"]
```

> ⚠️ 两个数组嵌套（`list` → `cost.items`）会**拍平**：`cost.items[].id` 是**所有配方材料连成一条**，看不出哪几样属于哪条配方。要看单条配方：
> `action=dump, class=CraftingFormulaCollection, pathid=76575, depth=9`（保留嵌套），或用 `--offset` 一条条看。
> `result.id` / `cost.items[].id` 都是 `Item.typeID` —— 去 `items.md` 查名字。

## 掉落 / 战利品 Loot

**掉落不是固定物品单，而是「按 tag 池加权随机」**：

`CharacterRandomPreset.lootBoxPrefab`（PPtr）→ 一个 `InteractableLootbox`（同一个 GameObject 上还有 `LootBoxLoader`），里面：

| 字段 | 含义 |
|---|---|
| `tags.entries[].value.#name` | **掉落的物品 tag 池**（每个 tag 带 `weight` / 百分比）|
| `tags.entries[].weight` | 权重 |
| `excludeTags` | 排除的 tag |
| `activeChance` | 这个箱子激活的概率 |
| `randomCount.x` / `.y` | 随机数量范围 |
| `inventorySize` | 容量 |

```
# 全部掉落箱的 tag 池 + 权重（实测：Medic;Drink;Injector + 10;1;2）
action=export, class=LootBoxLoader, file="resources.assets",
field=["tags.entries[].value.#name","tags.entries[].weight","activeChance","inventorySize"]
```

> **「某敌人掉什么」的完整链路**：`CharacterRandomPreset`（用 `nameKey`/`isBoss` 定位）→ `lootBoxPrefab` 的 pathID → `action=dump, class=LootBoxLoader, pathid=<那个 gameObject 上的 loader>`；或先用上面的 export 把**全部**掉落箱列出来对照。
> 要“哪些物品属于某个 tag 池” → 用 `items.md` 的 `match="tags.list[].#name=<X>"`。

## 跨类 join 速查（问题 → 一条命令）

| 我想知道 | 命令要点 |
|---|---|
| 某任务要交什么？ | `class=Quest` → `requiredItemID` `requiredItemCount`（+ `requireSceneID`）|
| 某配方的材料？ | `class=CraftingFormulaCollection`（单条用 `dump`，见上）|
| 某商人卖什么？ | `class=StockShopDatabase` → `merchantProfiles[].entries[].typeID` |
| 某敌人/BOSS 掉什么？ | `class=CharacterRandomPreset` → `lootBoxPrefab` → `class=LootBoxLoader` |
| 某物品的数值？ | `docs/guides/items.md`（`stats.list[].key/baseValue`）|
| 某场景里有什么？ | `class=GameObject, file=levelN` |
| 某个中文名对应哪个 key？ | 本地化 CSV（`00-overview.md`）|

## 与官方 wiki 的口径差异（实测）

这些数据**已逐条对照过** `escapefromduckov.net`。以下差异是**我们读原始字段、wiki 做了二次加工**，不是我们错：

| 字段 | 我们（原始数据）| wiki（加工后）|
|---|---|---|
| `Quest.questGiverID` | **数字 id**（1/2/3…）| NPC 名（`Jeff`/`Alex`…）|
| `CharacterRandomPreset.team` | **数字**（0/1/3…）| 名（`scav`/`usec`/`lab`…）|
| `Buff.hide=1` / 隐藏项 | **照实列出**（Buff 115）| **过滤掉**（只显 89）|
| `CharacterRandomPreset.isBoss` | **原始字段**（61 个为 1）| 有 12 个标了 false（与数据不符）|

**内部/测试项**（正常工作产物，但一般不想展示）：`DummyEnemyCharacterRandomPresetLv N`、无 `m_Name` 的 `Item`（如 `Formula_Blueprint` 模板）。要排除就在结果里按名字过滤。

> 结论：**以游戏数据为准**；wiki 适合当“中文名/可读视图”参考，不代表原始字段值。

## 通用提示

- **本指南没写到的概念**，按 `00-overview.md` 的「这份指南没写到怎么办」四步走（`classes` → `export rows=1` 拿 pathID → `dump depth=1` 看字段 → `export` 取；要实现用 `inspect_game_api`）。
- **不确定类名** → 先 `action=classes`（960 个类）grep 关键词；再 `action=search, pattern=...`。
- **要数值/条件**（不是文案）→ 都在 `resources.assets` 的这类对象字段里，用 `export`。
- **要文案**（中/英）→ join `Data/StreamingAssets/Localization/*.csv`。
- **要行为/实现** → `inspect_game_api`（decompile）。
- 大结果用 `out=<file>` 落盘（只回预览），再用 bash/python 处理。
