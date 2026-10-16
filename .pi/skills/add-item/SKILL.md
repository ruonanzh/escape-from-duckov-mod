---
name: add-item
description: 玩家想**加一件新物品**时用（例如「给我加一件新物品」「新增一件叫 XXX 的装备」「把这模型做成一件新道具卖给我」「加一个能吃的东西」「加一件能摆到展示台上的收藏品」）。新物品有自己的 typeID 与名字，原物品不变。覆盖五组：消耗品、装备（护甲、头盔、口罩、耳机）、背包与容器、材料与通用物品、可展示收藏品。模型可以先用 Tripo 生成，也可以不带模型（沿用原型的外形）。
---

# 新增物品

**目标**：给游戏加一件**新物品**。它有**自己的 typeID 与名字**，**原物品不变**。

**覆盖这五组**：

| 组 | 包含 | 拿哪个 tag 筛原型 |
|---|---|---|
| ① 消耗品 | 食物、饮料、药品、注射器 | `Food` / `Drink` / `Medic` / `Healing` / `Injector` |
| ② 装备 | 护甲、头盔、口罩、耳机 | `Armor` / `Helmat` / `FaceMask` / `Headset` |
| ③ 背包与容器 | 背包、容器 | `Backpack` / `Continer`（原文拼写如此）|
| ④ 材料与通用物品 | 材料、种子、作物、工具、宝石 | `Material` / `Seed` / `Crop` / `Tool` / `Gem` |
| ⑤ 可展示收藏品 | 摆到展示台上的东西 | `ShowCase` |

## 1. 需要的输入（只有下面两项，问清这两项即可）

其余一律沿用原型。

| 问 | 怎么问 |
|---|---|
| **是否要模型** | 默认带。玩家明确说不用时，接口也支持不带 |
| **获得方式** | 见下。**不得默认** |

### 获得方式：必须问清，不得默认

选项是这五个，**可以多选**：商人买 / 制作台做 / 掉落 / 摆到展示台 / 黑市。

- **选了「商人买」，默认卖到售货机**（`Merchant_Normal`）。玩家指定了别的店就听玩家的。游戏里有 **12 个商店档位**：

  | `merchantID` | 卖家 | 卖什么 |
  |---|---|---|
  | `Merchant_Normal` | 售货机 | 杂货与药品；也是「卖东西换钱」的地方 |
  | `Merchant_Weapon` | 老吴 | 武器与弹药 |
  | `Merchant_Equipment` | 橘子 | 头盔与护甲 |
  | `Merchant_Ming` | 小明 | — |
  | `Merchant_Mud` | 泥巴 | — |
  | `Merchant_Fo` | 佛哥 | — |
  | `Merchant_Myst` / `Merchant_Myst0` / `Merchant_MystIsland` | 神秘商人（三个档位）| — |
  | `Merchant_Island` | 岛的商店 | — |
  | `Merchant_IslandMobile` | 售货机（岛上的移动版）| — |

  **玩家说「随便」时按类别定**：武器给老吴（`Merchant_Weapon`）、装备给橘子（`Merchant_Equipment`）、其余给售货机（`Merchant_Normal`）。
  `config.json` 里**必须显式写 `merchantID`**，不写会被 `validate_mod` 拦下。
- **选了「摆到展示台」，物品必须带 `ShowCase` tag**（见第 2 节）。
- **选了「掉落」，先看第 2 节的掉落检查**：不保证掉得出来。
- **黑市**按 tag 自动选货，不需要额外字段。

**不问的**：数值、品质、堆叠、重量。它们默认**沿用原型**。
玩家以后要改，改 `config.json` 的 `stats`、`quality`、`displayQuality`、`maxStackCount`、`value`、`weight`，
然后重新进一次关卡。

**名字**从玩家的话里取。他没说就顺口问一句。

**原型**：玩家指定了就听玩家的。没指定时，你自己按模型与描述挑一件最接近的现有物品。
不论哪种，**必须在回复里写清**你选了哪件、新 typeID 是多少、卖到哪个店。

**查现有物品**：用 `inspect_game_data`（命令见 `docs/guides/items.md`）。

## 2. 选原型与选 Tag

**要改一件东西的分类之前，先明白两件事**（都是反编译实证，不是猜的）：

1. **Tag 决定功能**。物品的「能不能修、能不能卖、能不能插进某个槽」都是从 tag 算出来的：
   `Item.Repairable` 判 `Tags.Contains("Repairable")`，`Item.Sticky` 判 `Sticky`，
   `Item.CanBeSold` 判 `NotSellable`；插槽准入是 `Slot.CheckAbleToPlug` 里的
   `item.Tags.Check(requireTags, excludeTags)`。
   所以**品类 tag 选错不是分类难看，而是功能会错**（修不了、卖不了、插不进槽、上不了展示台）。
2. **Tag 决定掉落**。掉落不是固定物品单，而是**按 tag 池加权随机**：
   掉落箱上的 `LootBoxLoader.tags.entries[]` 就是 tag 池，抽哪件物品看**物品自己的 tag**。

**品类 tag 从原型继承**（克隆是完整拷贝），这一般不用动。
**`config.json` 的 `tags` 是「额外标记」——追在原型分类之上，不是替换。**

| 想要的 | 写哪个 tag |
|---|---|
| 能摆到展示台上 | `ShowCase`（展示台的 12 个槽全都要求它）|
| 只是归类 | `Collection` / `Luxury` / `Daily` |

**要有两件事一起成立：**

1. **槽要放行**：物品必须带 `ShowCase`（展示台的 12 个槽全都要求它）。
2. **要有东西可显示**：展示台按物品自己的 3D 图形造一份显示件。物品没有 3D 图形时，摆上去什么都不显示。

> `Collection` / `Luxury` / `Daily` 只是归类，不改变游戏行为，**不得写成有副作用**。

**筛原型的固定命令**（`tags.list[].#name` 换成上表的 tag）：

```
action=export, class=Item, file=resources.assets,
match=["tags.list[].#name=Food"],
field=["typeID","displayName","maxStackCount","weight"]
```

### 掉落可达性检查（固定命令，不得靠玩家发现）

**先查实到的掉落池**：

```
action=export, class=LootBoxLoader, file=resources.assets,
field=["tags.entries[].value.#name","tags.entries[].weight","activeChance"]
```

截至编写时，实到的**非空** tag 池只有两种：`Medic;Drink;Injector`（权重 `10;1;2`）与 `Computer`（权重 3）。

报告三件事：**会不会掉、从哪种箱子掉、不会掉就明说「只能靠商人或制作台」**。
**第②到第⑤组现在没有任何掉落池覆盖**，不得让玩家以为一定会掉。

**查某个 tag 挂在多少件物品上**：

```
action=refs, class=Tag, name=<Tag 名>, by=Item
```

## 3. 模型从哪来

**先建骨架，再产模型，最后装。**

### 风格化预览图：**清理、居中、加粗**

**本作的美术偏厚实**，而 Tripo 会**严格照图生成**。只清理不加粗，出来的会偏「细」。
所以这一步的提示词同时做三件事：**清理、居中、加粗**。

**下面那段 `prompt=` 是实测下来最贴本作风格的默认配方**，就是基准。使用方式分三种：

| 玩家给了什么 | 怎么用这段配方 |
|---|---|
| **只给图**（没说别的） | **直接照用，一字不改** |
| **图，并说「再帮我改成……」或「加点……」** | 在这段配方基础上改，把玩家的要求加进去，并改掉冲突那句 |
| **只给文字描述**（没图） | 在这段配方基础上改，按描述写清这件物品该长什么样，并保留清理、侧视、加粗、居中留边这几件 |

```
generate_image(
  image="<玩家的图>",                       // 只给文字描述时省略，此时纯提示词出图
  prompt="Redraw the item from the input as a clean, isolated in-game asset: keep ONLY the main item - if the input shows several objects, take the largest, centered one and ignore the rest. The whole item alone and complete - keep EVERY part of it; remove only what is not the item (the background, any floating pieces, text, frames). Put it on a plain uniform white background: no scenery, no props, no shadow, no smoke, no hands, no text. Keep the item's own design and colors the same, but render it in a CARTOON / toon-shaded style like the game's own assets (flat stylized colors, clean shading - not photoreal), and make it noticeably BULKIER and THICKER: heavy, sturdy proportions like a stylized low-poly game asset, about 0.50 m tall (game scale). Show it from the SIDE (a profile view that shows its thickness and depth), NOT a front view, and perfectly CENTERED in the frame, with a clear margin on all sides.",
  out="your_mods/<目录名>/.preview/<物品名>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有 **8 件事都不得丢**：**只留主物品**、**整件物品都在**、**只移除不是它的东西**、**纯白均一背景**、**保持原设计与原颜色并做卡通渲染**、**加粗**、**侧视**、**居中留边**。
- `about 0.50 m tall (game scale)` 那句里的数字**按原型自己的大小写**。物品**不按类别自动定尺寸**：生成的模型多大，游戏里就多大。差得多时再在 `config.json` 里写 `size` 调。
- 出图后**把文件路径给玩家**（工具返回里就是绝对路径），再问他「就要这个吗」。
  图大于 1.5MB 时工具不内联显示，所以**必须把路径写出来**。
- `.preview/` 放在 mod 目录下。`install_mod` 会跳过它（不进游戏）。
- 不像就改 `prompt=` 再来一次。每次都便宜。**没确认前不得做下一步。**

### 3D 模型

```
generate_model(action="generate",
               image="your_mods/<目录名>/.preview/<物品名>.preview.png",   // 用上面那张**已确认的**预览图
               out="your_mods/<目录名>/<物品名>.glb",                      // 模型名**按物品起**（backpack.glb）
               faceLimit=3000)
```

- **图到 3D 比纯文字准得多。**
- 判据：工具回报里出现 `bounds`，且尺寸与原型同一个量级。差得远就回上一步改预览图。
- 玩家自带 `.glb` 时跳过这一步，直接把他的文件放进 mod 目录。
- 玩家不需要模型时，`config.json` 里**不写 `model`**，新物品就沿用原型的外形。

生成前先读 mod 目录里的 `asset_manifest.jsonl`（只看最后几行）：同一个模型不得重复生成，
同一个任务可以用 `generate_model action=convert taskId=<原来的 taskId>` 重新导出，比重新生成便宜。

### 图标（新物品必须有）

背包格子里本来没有这件物品的图，所以要出一张：

```
generate_image(model="your_mods/<目录名>/<物品名>.glb",
               prompt="clean game inventory icon, three-quarter view, centered, the item occupies about 80% of the frame width",
               out="your_mods/<目录名>/icon.png")
```

用模型自己的渲染图（`model=`）**不花积分**。`config.json` 的 `icon` 默认就是 `icon.png`。


## 4. 执行

### ① 建骨架

照 **`reference/item_model/`** 建 mod 目录。它同时支持换外观与新增物品。

三个名字必须一致：`info.ini` 的 mod 名、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace。
不一致时游戏**静默不加载**。建完先跑一次 `validate_mod`。

### ② 写 config.json

一个 mod 可以有**多条规则**，每条自带 `action`。新增物品用 `new_item`。

```json
{
  "entries": [
    {
      "action": "new_item",
      "cloneFromItemId": 743,
      "displayName": "超大纤维束",
      "description": "把三十份纤维压成一份。",
      "model": "fiber.glb",
      "merchantID": "Merchant_Normal",
      "maxStock": 3,
      "priceFactor": 1,
      "tags": ["ShowCase"],
      "maxStackCount": 99,
      "value": 500,
      "weight": 0.35,
      "crafting": { "money": 100, "materials": { "743": 30 } }
    }
  ]
}
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `action` | 是 | 固定写 `new_item` |
| `cloneFromItemId` | 是 | 原型的 typeID。数值、插槽、品质、**分类 tag** 全继承它 |
| `displayName` | 是 | 新物品的名字 |
| `merchantID` | **上架时必填** | 卖到哪个店，写第 1 节表里的值。写 `"onSale": false` 就不用它 |
| `description` | 否 | 简介 |
| `model` | 否 | 本 mod 目录里的 glb（物品可以直接写 `fiber.glb`）。不写就沿用原型外形 |
| `size` | 否 | 修改大小（可选）。不写就不改大小 |
| `handheld` | 否 | **只针对非装备物品**：连**拿在手里**那条也换。**会改行为**（物品变成「可拿在手里」与「界面上可以选它」）。要开就先问玩家一句 |
| `icon` | 否 | 卡片图标，默认 `icon.png` |
| `newTypeId` | 否 | 不写就取游戏自己的下一个空闲号。**回复里要写出来** |
| `maxStock` | 否 | 备货件数，默认 1 |
| `priceFactor` | 否 | 价格倍数，默认 1 |
| `onSale` | 否 | 写 `false` 就不上架 |
| `tags` | 否 | **额外标记**（追加，不是替换）。见第 2 节 |
| `maxStackCount` | 否 | 堆叠上限。不写就沿用原型 |
| `value` | 否 | 价值（商人的价格基础）。不写就沿用原型 |
| `weight` | 否 | 重量（千克）。不写就沿用原型。背包上限看它 |
| `quality` | 否 | 整数。不写就沿用原型 |
| `displayQuality` | 否 | `None` 到 `Q8` 之一。不写就沿用原型 |
| `stats` | 否 | 覆盖数值，用**绝对值**。键用游戏自己的名字 |
| `crafting` | 否 | `{ money, materials, tags }`。写了就加一条制作台配方 |

**数值与 tag 的名字从哪来**：用 `inspect_game_data` 导出（命令见 `docs/guides/items.md`）。
**不得手写猜的名字** —— 名字不存在时那一条不会生效，日志里会列出来。

**同一个文件可以放很多条**：一个 `new_item` 只造一件东西。要做一批就写多条（每条一个 `new_item`）。
同一份 config 里也可以混装 `replace_item`，各写各的 `action`。

### ③ 装进游戏

按 `mod-installer` 那份做。

## 5. 验收（装好不等于成功）

让玩家做这几件，逐条确认：

- 新物品在**玩家指定的那个商店**有货，**能买**
- 写了 `crafting` 时，**制作台能做出**同一件
- **能用**：食物能吃、药品能用、装备能穿、背包能装
- **第⑤组的关键一条：能摆到展示台上**（物品带 `ShowCase`）
- **掉落按第 2 节的检查结果对账**：消耗品应能从医疗箱开出来；其余组不掉就明说
- 图标正确；堆叠、价值、重量符合预期
- **原型没变**（名字、数值、分类、图标、商品货架都不变）
- 名字与简介显示的是你要的文字，**不是带星号的原文**（例如 `*超大纤维束*`）
- 存档后重进：新物品还在，商人还有货

**时机**：注册、上架、配方都必须在**进关卡之前**做。改完 config 后，**重新进一次关卡**才能看到变化。游戏不用重启。

## 6. 常见错误（对照修正）

| 现象 | 原因 | 怎么办 |
|---|---|---|
| 名字显示成 `*某段文字*` | 名字被当成了本地化键 | 不得手改 dll。`displayName` 写文本，代码会自己建键 |
| 商人那里看不到 | 没写 `merchantID`，或上架发生在进关卡之后 | 必须写 `merchantID`。改完**重新进一次关卡** |
| 上不了展示台 | 物品没有 `ShowCase` tag | 在 `tags` 里加 `ShowCase` |
| 摆上展示台什么都不显示 | 物品没有 3D 图形 | 给 `model`，或换一个带图形的原型 |
| 卖不掉 | 物品带了 `NotSellable`（`Item.CanBeSold` 判它）| 去掉那个 tag。注意 `NotForSell` 是**没人读**的同类标记 |
| 装备插不进槽 | 品类 tag 不对（插槽按 tag 放行）| 换一个品类正确的原型，或补上该槽要求的 tag |
| 不会掉 | 它的 tag 不在任何掉落池里 | 见第 2 节的检查。掉不了就改成靠商人或制作台 |
| 数值没生效 | `stats` 的键写错了；或者你试的是**早就拿到手**的那一件 | 键必须与游戏的 `Stat.Key` 完全一致，日志会列出没找到的键 |
| **原型也变了** | 图形没克隆，直接改了共享的那一份 | 这是 bug，报给我们。正常情况下不得发生 |
| 配方一直是锁的 | 靠 `unlockByDefault` 解锁 | 游戏只认 `unlockedFormulaIDs`。代码已经用 `UnlockFormula` 处理 |
| 新物品装上就没了 | typeID 撞了 | 换一个 `newTypeId`，或删掉它让游戏给号 |
| 拿在手里还是旧样子 | 手持实体没换 | 多半是这件物品**本来没有图形**：把 `handheld` 写成 `true` |

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
| `ItemFactory.ApplyTags(item, add, remove, missing)` | 加/减 tag。**追加**，不改原型的分类 |
| `ItemFactory.ApplyItemNumbers(item, maxStackCount, value, weight)` | 堆叠上限、价值、重量 |
| `ItemFactory.AttachIcon(item, pngPath)` | 把 png 设成卡片图标 |
| `ItemFactory.PutOnSale(merchantID, typeId, maxStock, priceFactor)` | 上架到商店（物品这条路必须给 `merchantID`）|
| `ItemFactory.AddRecipe(id, typeId, amount, money, materials, tags)` | 加一条制作台配方 |
| `ItemFactory.RemoveAdded()` | 撤销上面加过的东西（热重载与卸载用）|
| `GameApi.GiveItemToPlayer(item)` | 直接发给玩家（自测用，必须在进关卡之后） |
