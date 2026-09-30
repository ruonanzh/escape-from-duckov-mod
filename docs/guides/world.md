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

# 某个场景里有哪些对象（场景用 file=levelN）
action=classes, file=levelN                     # 例：level0 = 主菜单
action=export, class=GameObject, file=level0, match=["m_Name~Door"]
```

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

# 技能树（分组的根）
action=export, class=PerkTree, field=["displayName","perks"]
```

相关：`PerkTree`(10) / `PerkTreeIDList`(2) / `ModifierDescriptionCollection`(752，属性/修改器文案)。

## 建筑 Buildings — `Building`（29）

字段：`id`（如 `PetHouse`）`dimensions` `graphicsContainer` `functionContainer` `unlockAchievement`

```
action=export, class=Building, field=["id","dimensions","unlockAchievement"]
```

> 注意：`Building` 资产的 `m_Name` 是空的 —— **要看 `id` 字段**，不是 `name`。

## 商店 Shops — `StockShop`（12）

字段：`merchantID`（如 `Merchant_Normal`）`DisplayNameKey` `sellFactor`（回收价倍率）`refreshAfterTimeSpan` `overrideSellingPrice` `refreshStockOnStart` `returnCash`

```
action=export, class=StockShop,
field=["merchantID","sellFactor","refreshAfterTimeSpan","DisplayNameKey"]
```

相关：`StockShopDatabase`(1) / `StockShopItemEntry`(1) / `StockShopView`(1)。
**商品清单**在 `StockShopDatabase` / 各 StockShop 的库存引用里 —— 用 `dump` + `follow` 或 `refs` 跟着看。

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

## 通用提示

- **不确定类名** → 先 `action=classes`（960 个类）grep 关键词；再 `action=search, pattern=...`。
- **要数值/条件**（不是文案）→ 都在 `resources.assets` 的这类对象字段里，用 `export`。
- **要文案**（中/英）→ join `Data/StreamingAssets/Localization/*.csv`。
- **要行为/实现** → `inspect_game_api`（decompile）。
- 大结果用 `out=<file>` 落盘（只回预览），再用 bash/python 处理。
