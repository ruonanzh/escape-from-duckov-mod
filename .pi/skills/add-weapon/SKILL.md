---
name: add-weapon
description: 玩家想**加一把新枪**时用（例如「给我做一把新枪」「加一把叫 XXX 的武器」「新增一把枪，数值照 MP5 改」「把这把模型做成一把新枪卖给我」）。新枪有自己的 typeID 与名字，原枪不变；玩家能在商人那里买到，也能在制作台做出来。模型可以先用 Tripo 生成，也可以不带模型（沿用原型枪的外形）。
---

# 新增武器

**目标**：给游戏加一把**新枪**。它有**自己的 typeID 与名字**，**原枪不变**。

> **克隆原型之前，先看原型枪自己的 prefab**：库对「枪身、零件、挂点、手持实体」的判断是**最大可能性**。同类的枪身命名有 `WPN_*`、`Pfb_WPN_*`、`WPM_*`、`Arrow` 等多种，还有父节点自己带渲染器的结构。动手前用 `inspect_game_data action=transform --pattern <原型模型名> --depth 3` 看清结构（prefab 直接查；场景里的对象要加 `file=levelN`）；冲突时**以 prefab 为准**。

## 1. 需要的输入（只有下面两项，问清这两项即可）

其余一律沿用原型枪。

| 问 | 默认 |
|---|---|
| **枪的模型** | 默认带。玩家明确说不用时，接口也支持不带 |
| **获得方式** | 上架到地堡的商人。问清哪个商人、卖多少钱、备几件 |

**不问的**：数值、品质、槽位。它们默认**沿用原型枪**（克隆是完整拷贝）。
玩家以后要改数值或品质，改 `config.json` 的 `stats`、`quality`、`displayQuality` 即可。
改完重新进一次关卡，**新拿到**的那件生效。

**名字**从玩家的话里取（例如「做一把叫测试步枪的枪」）。他没说就顺口问一句。

**原型枪**：玩家指定了就听玩家的。没指定时，你自己按模型与描述挑一把最接近的现有枪。
不问玩家，但**必须在回复里写清**你选了哪把、新 typeID 是多少。他看到不对会说。

**查现有枪**：用 `inspect_game_data`（命令见 `docs/guides/items.md`）。

## 2. 模型从哪来

**先建骨架，再产模型，最后装。**

### 风格化预览图：**清理、居中、加粗**

**本作的枪偏「厚实」**（枪管粗、机匣壮、枪托结实），而 Tripo 会**严格照图生成**。
只清理不加粗，出来的模型就是「细长」的（实测中厚比长 0.136，本作原枪 0.226）。
所以这一步的提示词必须同时做三件事：**清理、居中、加粗**。

**下面那段 `prompt=` 是实测下来最贴本作风格的默认配方**，就是基准。使用方式分三种：

| 玩家给了什么 | 怎么用这段配方 |
|---|---|
| **只给图**（没说别的） | **直接照用，一字不改** |
| **图，并说「再帮我改成……」或「加点……」** | 在这段配方基础上改，把玩家的要求加进去，并改掉冲突那句 |
| **只给文字描述**（没图） | 在这段配方基础上改，改成描述这把枪该长什么样，并保留朝向、居中、加粗三件 |

```
generate_image(
  image="<玩家的图>",                       // 只给文字时省略，此时纯提示词出图
  prompt="Redraw the weapon from the input as a clean, isolated in-game asset: keep ONLY the main item - if the input shows several objects, take the largest, centered one and ignore the rest. The whole weapon alone and complete - keep EVERY part of it (stock, pad, magazine, sights, mounts); remove only what is not the weapon (the background, any floating pieces, text, frames). Put it on a plain uniform white background: no scenery, no props, no shadow, no smoke, no hands, no text. Keep the weapon's own design and colors the same, but render it in a CARTOON / toon-shaded style like the game's own assets (flat stylized colors, clean shading - not photoreal), and make it noticeably BULKIER and THICKER: a chunky barrel, a thick handguard and a stout stock - heavy, sturdy proportions like a stylized low-poly game asset. Side view with the barrel and muzzle pointing LEFT, and perfectly CENTERED in the frame, with a clear margin on all sides.",
  out="your_mods/<目录名>/.preview/<名字>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有 8 件事都**不得丢**：**只留主物品**、**整把武器都在**（部件一个不少）、**只移除不是武器的东西**、**纯白均一背景**、**保持原设计与原颜色并做卡通渲染**、**加粗**、**朝向 LEFT**、**居中留边**。
- 改配方时**不得弄丢朝向与居中**。玩家只说要改颜色，也不得顺手把朝向删了。玩家要「细一点」时，那是**改配方**（把加粗那句改掉），不是加一句反向要求。
- 出图后**把文件路径给玩家**（工具返回里就是绝对路径），再问他「就要这个吗」。
  图大于 1.5MB 时工具不内联显示，所以**必须把路径写出来**，不得写成「玩家会自动看到」。
- `.preview/` 放在 mod 目录下。`install_mod` 会跳过它（不进游戏）。
- 不像就改 `prompt=` 再来一次，每次都便宜。**没确认前不得做下一步。**

### 3D 模型

```
generate_model(action="generate",
               image="your_mods/<目录名>/.preview/<名字>.preview.png",   // 用上面那张**已确认的**预览图
               out="your_mods/<目录名>/<武器名>.glb",                     // 模型名**按武器起**（mp5.glb）
               faceLimit=3000)
```

- **图到 3D 比纯文字准得多。**
- 判据：工具回报里出现 `bounds`，且尺寸与原型枪同一个量级。差得远就回上一步改预览图。
- 玩家自带 `.glb` 时跳过这一步，直接把他的文件放进 mod 目录。
- 玩家不需要模型时，`config.json` 里**不写 `model`**，新枪就沿用原型枪的外形。

生成前先读 mod 目录里的 `asset_manifest.jsonl`（只看最后几行）：同一个模型不得重复生成，
同一个任务可以用 `generate_model action=convert taskId=<原来的 taskId>` 重新导出，比重新生成便宜。

### 图标（新枪必须有）

背包格子里本来没有这把枪的图，所以要出一张：

```
generate_image(model="your_mods/<目录名>/<武器名>.glb",
               prompt="clean game inventory icon, side profile, centered, the weapon occupies about 80% of the frame width",
               out="your_mods/<目录名>/icon.png")
```

用模型自己的渲染图（`model=`）**不花积分**；要新画一张才花。`config.json` 的 `icon` 默认就是 `icon.png`。


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
      "displayName": "测试步枪",
      "description": "为了试新模型做的一把枪。",
      "model": "test_rifle.glb",
      "merchantID": "Merchant_Weapon",
      "maxStock": 1,
      "priceFactor": 1,
      "stats": { "Damage": 13.0, "Capacity": 25 },
      "quality": 6,
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
| `slots` | 否 | 配件落点与手抓点。下面有单独一段讲什么时候写、怎么写 |
| `crafting` | 否 | `{ money, materials, tags }`。写了就加一条制作台配方 |

**`slots` 什么时候写、怎么写**

我们模型的尺寸和原枪不同，配件落点与手抓点只能大致对上。要让配件落在正确的位置，就写 `slots`。

```jsonc
// 单位米，在我们模型的局部空间里。下面是形状示例，数值不通用。
"slots": { "muzzle": [ 0.32, 0.01,  0.00], "stock": [-0.34, 0.00,  0.00],
           "grip":   [ 0.06,-0.05,  0.00], "pivot": [-0.08,-0.02,  0.00] }
```

- 每个值是 `[x, y, z]`：`x` 沿 X（左右）、`y` 沿 Y（上下）、`z` 沿 Z（枪口方向）。可以写负数。
- 零点在**握把点**，不在 glb 的原点（枪默认 `zeroGrip: true`）。
- 调整顺序：**先调 `pivot`**（整枪在手里的位置），**再调各个槽位**。
- 只有装了对应配件，才看得出那个槽位的效果。存盘立刻生效。
- 不写 `slots` 也可以跑。这时走「按原枪包围盒映射」，只是没那么准。

**数值的键从哪来**：用 `inspect_game_data` 导出（命令见 `docs/guides/items.md` 的「数值」一节）。
键名是游戏里的原样写法（实测 124 把枪都是 `Damage`、`Capacity`、`ReloadTime`、`BulletSpeed` 这种），
**导出里长什么样就照抄**，不得凭记忆写。代码把这个字符串直接交给游戏的 `GetStat`，**差一个字母就不生效**，
日志里会把它列进没找到的键。

**同一份 config 里可以混装多条**：换武器外观一条、换物品外观一条、新增武器一条，各写各的 `action`。

### ③ 装进游戏

按 `mod-installer` 那份做。

## 4. 验收（装好不等于成功）

让玩家做这几件，逐条确认：

- 地堡武器商人的货架上有这把新枪，**能买**
- 写了 `crafting` 时，**制作台能做出**同一把枪
- 能装备、能开火、能换弹
- **原枪没变**（外形、数值、品质都不变）
- 名字与简介显示的是你要的文字，**不是带星号的原文**（例如 `*超大纤维束*`）
- 改 `config.json` 的 `stats`、`quality`、`displayQuality`，存盘后**新拿到**的那件生效（已经买到手的那件保留它自己的数值。游戏按**实例**存档）
- 存档后重进：新枪还在，商人还有货
- 不带模型时外形等于原型枪；带模型时外形是我们的 glb

**上架与制作台的时机**：两者都必须在**进关卡之前**做。
改完 config 后，**重新进一次关卡**才能看到货架变化。游戏不用重启。

## 5. 常见错误（对照修正）

| 现象 | 原因 | 怎么办 |
|---|---|---|
| 名字显示成 `*某段文字*` | 名字被当成了本地化键 | 不得手改 dll。`displayName` 写文本，代码会自己建键 |
| 商人那里看不到新枪 | 上架发生在进关卡之后 | 注册与上架都在进关卡之前。**重新进一次关卡** |
| 数值没生效 | `stats` 的键写错了；或者你试的是**早就拿到手**的那一件 | 键必须与游戏的 `Stat.Key` 完全一致，日志会列出没找到的键。实例已经有的数值不会被后来的 config 改动覆盖 |
| **原枪也变了** | 图形没克隆，直接改了共享的那一份 | 这是 bug，报给我们。正常情况下不得发生 |
| 配方一直是锁的 | 靠 `unlockByDefault` 解锁 | 游戏只认 `unlockedFormulaIDs`。代码已经用 `UnlockFormula` 处理 |
| 新枪装上就没了 | typeID 撞了 | 换一个 `newTypeId`，或删掉它让游戏给号 |
| 拿在手里还是旧样子 | 手持实体没换 | 代码里 `AttachModel` 默认处理手持。报给我们 |

## 6. 把模型路径交给玩家（让他自己调位置）

装完之后，在回话里给出**模型文件的路径**（`your_mods/<mod 目录>/<模型文件>.glb`），并说明：

> 你可以用 3D 编辑器打开它，自己调两件事：模型相对游戏角色的落点（`pivot`），以及各配件挂点（`slots`）。改完把数字写进 `config.json` 就生效。

- **不写 `slots` 会怎样**：配件停在**游戏原枪**的挂点上。模型形状不同就会浮空或陷进枪身（实测 ak103：枪口浮空 3 cm、枪托陷进枪身 26 cm）。
- 编辑器里切到「配件挂点」，点「按模型几何重算」可以一次拿到 5 个点的几何建议值（枪口取管轴末端、枪托取尾部截面中心、瞄具取顶部、握把取下方、科技件取侧面），再按玩家的喜好微调。
- 玩家提「瞄具往前一点」这类要求时，改的是 `slots` 里那一行的米数，不用重新生成模型。

## 接口一览

完整清单见 [`docs/mod-kit-api.md`](../../../docs/mod-kit-api.md)。
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
| `ItemFactory.AddRecipe(id, typeId, amount, money, materials, tags)` | 加一条制作台配方 |
| `ItemFactory.RemoveAdded()` | 撤销上面加过的东西（热重载与卸载用） |
| `GameApi.GiveItemToPlayer(item)` | 直接发给玩家（自测用，必须在进关卡之后） |
