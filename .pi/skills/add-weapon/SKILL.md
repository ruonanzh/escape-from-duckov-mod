---
name: add-weapon
description: 玩家想**加一把新枪**时用（例如「给我做一把新枪」「加一把叫 XXX 的武器」「新增一把枪，数值照 MP5 改」「把这把模型做成一把新枪卖给我」）。新枪有自己的 typeID 与名字，原枪不变；玩家能在商人那里买到，也能在制作台做出来。模型可以先用 Tripo 生成，也可以不带模型（沿用原型枪的外形）。
---

# 新增武器

**目标**：给游戏加一把**新枪**。它有**自己的 typeID 与名字**，**原枪不变**。

**与「替换枪械模型」的分界**：

| 玩家真正想要的 | 用哪一份 |
|---|---|
| 原枪换个外观，数值不变 | `replace-gun-model` |
| 原枪的数值变一变，全世界还是同一把枪 | 不做。那是改数值 |
| 原枪不动，多一把数值不同的枪 | 本 SKILL，不带模型 |
| 多一把外形不同的枪 | 本 SKILL，带模型 |

## 1. 需要的输入（只问两件）

**只问这两件**。其余一律沿用原型枪。

| 问 | 默认 |
|---|---|
| **枪的模型** | 默认带。玩家明确说不用时，接口也支持不带 |
| **获得方式** | 上架到地堡的商人。问清哪个商人、卖多少钱、备几件 |

**不问的**：数值、品质、槽位。它们默认**沿用原型枪**（克隆是完整拷贝）。
玩家以后要改数值或品质，改 `config.json` 的 `stats`、`quality`、`displayQuality` 即可。
改完重新进一次关卡，**新拿到**的那件生效。

**名字**从玩家的话里取（例如「做一把叫逻辑与理性的枪」）。他没说就顺口问一句。

**原型枪由你自己挑**：按玩家给的模型与描述，挑一把最接近的现有枪。
不问玩家，但**必须在回复里写清**你选了哪把、新 typeID 是多少。他看到不对会说。

**查现有枪**：用 `inspect_game_data`（命令见 `docs/guides/items.md`）。

## 2. 模型从哪来

**先建骨架，再产模型，最后装** —— 与 `replace-gun-model` 的顺序相同。

本 SKILL 不重写那几段提示词。`generate_image` 与 `generate_model` 的**工具说明**里已经写了朝向、配色、面数、积分与不要连发那些话，照它做即可。

玩家自带 `.glb` 时跳过生成，直接把文件放进 mod 目录。
玩家不要模型时，`config.json` 里**不写 `model`**，新枪就沿用原型枪的外形。

## 3. 执行

### ① 建骨架

照 **`reference/weapon_model/`** 建 mod 目录。它同时支持换外观与新增武器。

三个名字必须一致：`info.ini` 的 mod 名、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace。
不一致时游戏**静默不加载**。建完先跑一次 `validate_mod`。

### ② 写 config.json

一个 mod 可以有**多条规则**，每条自带 `action`。新增武器用 `new_weapon`。

```json
{
  "entries": [
    {
      "action": "new_weapon",
      "cloneFromItemId": 1629,
      "displayName": "逻辑与理性",
      "description": "千年科学学园・研讨会会计早濑优香的专属配枪。",
      "model": "mpx.glb",
      "merchantID": "Merchant_Weapon",
      "maxStock": 1,
      "priceFactor": 1,
      "stats": { "Damage": 13.0, "Capacity": 25 },
      "quality": 9,
      "crafting": { "money": 0, "materials": { "662": 5 } }
    }
  ]
}
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `action` | 是 | 固定写 `new_weapon` |
| `cloneFromItemId` | 是 | 原型枪的 typeID。数值、插槽、品质全部继承它 |
| `displayName` | 是 | 新枪的名字 |
| `description` | 否 | 简介 |
| `model` | 否 | 本 mod 目录里的 glb。不写就沿用原型外形 |
| `newTypeId` | 否 | 不写就取游戏自己的下一个空闲号。**回复里要写出来** |
| `merchantID` | 否 | 默认 `Merchant_Weapon`，也就是卖枪那个商人 |
| `maxStock` | 否 | 备货件数，默认 1 |
| `priceFactor` | 否 | 价格倍数，默认 1 |
| `onSale` | 否 | 写 `false` 就不上架 |
| `stats` | 否 | 覆盖数值，用**绝对值**。键用游戏自己的名字 |
| `quality` | 否 | 整数。不写就沿用原型 |
| `displayQuality` | 否 | `None` 到 `Q8` 之一。不写就沿用原型 |
| `slots` | 否 | 写法与替换武器那套完全一样 |
| `crafting` | 否 | `{ money, materials, tags }`。写了就加一条制作台配方 |

**数值的键从哪来**：用 `inspect_game_data` 导出（命令见 `docs/guides/items.md` 的「数值」一节）。
**不得手写猜的键** —— 键不存在时那一条不会生效，日志里会列出来。

**同一份 config 里可以混装多种能力**：换外观一条、新增武器一条、新增物品一条，各写各的 `action`。

### ③ 装进游戏

按 `mod-installer` 那份做。

## 4. 验收（装好不等于成功）

让玩家做这几件，逐条确认：

- 地堡武器商人的货架上有这把新枪，**能买**
- 写了 `crafting` 时，**制作台能做出**同一把枪
- 能装备、能开火、能换弹
- **原枪没变**（外形、数值、品质都不变）
- 名字与简介显示正确，**不是** `*字面量*`
- 改 `config.json` 的 `stats`、`quality`、`displayQuality`，存盘后**新拿到**的那件生效（已经买到手的那件保留它自己的数值。游戏按**实例**存档）
- 存档后重进：新枪还在，商人还有货
- 不带模型时外形等于原型枪；带模型时外形是我们的 glb

**上架与制作台的时机**：两者都必须在**进关卡之前**做。
改完 config 后，**重新进一次关卡**才能看到货架变化。游戏不用重启。

## 5. 常见错误（对照修正）

| 现象 | 原因 | 怎么办 |
|---|---|---|
| 名字显示成 `*某段文字*` | 名字被当成了本地化键 | 不要手改 dll。`displayName` 写文本，代码会自己建键 |
| 商人那里看不到新枪 | 上架发生在进关卡之后 | 注册与上架都在进关卡之前。**重新进一次关卡** |
| 数值没生效 | `stats` 的键写错了；或者你试的是**早就拿到手**的那一件 | 键必须与游戏的 `Stat.Key` 完全一致，日志会列出没找到的键。实例已经有的数值不会被后来的 config 改动覆盖 |
| **原枪也变了** | 图形没克隆，直接改了共享的那一份 | 这是 bug，报给我们。正常情况下不得发生 |
| 配方一直是锁的 | 靠 `unlockByDefault` 解锁 | 游戏只认 `unlockedFormulaIDs`。代码已经用 `UnlockFormula` 处理 |
| 新枪装上就没了 | typeID 撞了 | 换一个 `newTypeId`，或删掉它让游戏给号 |
| 拿在手里还是旧样子 | 手持实体没换 | 代码里 `AttachModel` 默认处理手持。报给我们 |

## 接口一览

完整清单见 [`docs/mod-kit-api.md`](../../docs/mod-kit-api.md)。
签名用 `rg "public static" libs/mod-kit/ItemFactory.cs` 现查。

| 调用 | 干什么 |
|---|---|
| `ItemFactory.CloneAsNewItem(原型id, 新id, 名字, 简介)` | 克隆成新 typeID，写名字与简介，注册成动态条目 |
| `ItemFactory.NextFreeTypeID()` | 游戏自己的下一个空闲 typeID |
| `ItemFactory.IsTypeIDTaken(id)` | 这个 typeID 被占用了吗 |
| `ItemFactory.AttachModel(item, mesh, tex, size, handheld, sizeTypeID)` | 换图形。内部克隆图形，不动原型 |
| `ItemFactory.ApplyStats(item, 字典, missing)` | 覆盖数值。找不到的键收进 `missing` |
| `ItemFactory.ApplyQuality(item, quality, displayQuality)` | 覆盖品质 |
| `ItemFactory.PutOnSale(merchantID, typeId, maxStock, priceFactor)` | 上架到商人 |
| `ItemFactory.AddRecipe(id, typeId, count, money, materials, tags)` | 加一条制作台配方 |
| `ItemFactory.RemoveAdded()` | 撤销上面加过的东西（热重载与卸载用） |
| `GameApi.GiveItemToPlayer(item)` | 直接发给玩家（自测用，必须在进关卡之后） |
