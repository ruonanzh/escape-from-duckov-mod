---
name: replace-item-model
description: 玩家想换**物品**（背包、防弹衣、头盔、弹匣、任务道具，以及近战武器）在世界里的模型时用（例如「把这个背包换成这个模型」「这个头盔换成 glb」「物品模型替换」）。把 GLB 放进 mod，写一句 config，编译安装即可。
---

# 替换物品模型

**目标**：把某件（或某批）**物品**（**含近战武器**）**在世界里的模型**换成玩家提供的模型。不改数值，不改图标，也不改行为。

> **先看 prefab，再信库的判据**：库对「物品本体、世界图形、手持图形、图标位置」的判断同样只是**最大可能性**。动手前用 `inspect_game_data action=transform --pattern <目标模型名> --depth 3` 看清这件物品自己的渲染器层级与尺寸（`ItemGraphic` 与 `Icon` 是两处，只改一处就只换了一半）；库的判断与结构冲突时**以 prefab 为准**。

## 1. 模型从哪来（顺序：先建骨架，再产模型，最后装）

一句话也能做。顺序是「**先建 mod 骨架，再产出模型，最后装**」。目录先存在，也先绑定好，后面直接往里写。

### ① 玩家参考图（免费）

- 玩家给了图，就直接用它。聊天里发的图会落盘，拿到的是**绝对路径**，直接传给工具。
- 图里带 **logo、水印或 UI 文字**时，**必须先裁掉再传**。
- 玩家只说了句话，就跳过这步，直接进 ②。
- 给了好几张时，**问玩家用哪张**。

### ② 先把 mod 骨架建好（免费，编译几秒）

- 照 **`reference/item_model/`**（示例 mod 等于正确答案）建你自己的 mod 目录。
- 三个名字必须一致：`info.ini` 的 mod 名、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace。不一致的症状是 mod 静默不加载。明细见 §5。
- 建完**先跑一次 `validate_mod`** 确认能编译。它会替你检查 mod 名与 namespace 一致。
- **先建骨架，不得等模型**。模型工具落盘时会自建目录，不得让它抢在你前面把目录建出来。

### ③ 预览图：**清理、居中、加粗**

**本作的美术偏厚实**，而 Tripo 会**严格照图生成**。只清理不加粗，出来的会偏「细」。
所以这一步的提示词同时做三件事：**清理、居中、加粗**。

```
generate_image(
  image="<玩家的图>",                       // 只给文字描述时省略。此时纯提示词出图。
  prompt="Redraw the item from the input as a clean, isolated in-game asset: keep ONLY the main item - if the input shows several objects, take the largest, centered one and ignore the rest. The whole item alone and complete - keep EVERY part of it; remove only what is not the item (the background, any floating pieces, text, frames). Put it on a plain uniform white background: no scenery, no props, no shadow, no smoke, no hands, no text. Keep the item's own design and colors the same, but render it in a CARTOON / toon-shaded style like the game's own assets (flat stylized colors, clean shading - not photoreal), and make it noticeably BULKIER and THICKER: heavy, sturdy proportions like a stylized low-poly game asset, about 0.50 m tall (game scale). Show it from the SIDE (a profile view that shows its thickness and depth), NOT a front view, and perfectly CENTERED in the frame, with a clear margin on all sides.",
  out="your_mods/<目录名>/.preview/<物品名>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有 **8 件事都不得丢**：**只留主物品**（图里有多个物体时取**最大最居中**那个，其余全丢）、**整件物品都在**、**只移除不是它的东西**、**纯白均一背景**、**保持原设计与原颜色，并做卡通渲染**（不是照片感）、**加粗**、**侧视**、**居中留边**。
- `about 0.50 m tall (game scale)` 那句里的数字**按原型自己的大小写**。物品**不按类别自动定尺寸**，护甲、头盔、口罩、耳机、背包也一样：生成的模型多大，游戏里就多大。差得多时再在 `config.json` 里写 `size` 显式指定。
- 出图后**把文件路径给玩家**（工具返回里就是绝对路径），再问他「就要这个吗」。
  **图大于 1.5MB 时工具不会内联显示**，所以**必须把路径写出来**。
- `.preview/` 放在 mod 目录下。`install_mod` 会跳过它（不进游戏）。
- 不像就改 `prompt=` 再来一次。每次都便宜。**没确认前不得做 ④**。

### ④ 3D 模型

```
generate_model(action="generate",
               image="your_mods/<目录名>/.preview/<物品名>.preview.png",   // 用 ③ 那张**已确认的**预览图
               out="your_mods/<目录名>/<物品名>.glb",                      // 模型名**按物品起**（backpack.glb）
               faceLimit=3000)
```

- **图到 3D 比纯文字准得多**。
- 玩家自带 `.glb` 时，③④ 全跳过，直接把他的文件放进 mod 目录。

生成前先读 mod 目录里的 `asset_manifest.jsonl`（只看最后几行）：同一个模型不得重复生成，
同一个任务可以用 `generate_model action=convert taskId=<原来的 taskId>` 重新导出，比重新生成便宜。
- 玩家只要了一句话、没图时，用 `prompt="…"` 出模型。

### ⑤ 图标

这份流程只换**世界里的模型**，**不换图标**。背包格子与「使用时」那张图是游戏自带的。

### 尺寸：**照游戏内尺寸做**（实测。**不按现实尺寸**）

| 类别 | 典型最长边 |
|---|---|
| 背包 | **0.50 m** |
| 防弹衣（身体）| **0.67 m** |
| 头盔（头部）| **0.65 m** |
| 面具（面部）| **0.63 m** |
| 耳机 | **0.78 m** |

- 这行数字**写进 ③ 的 `prompt=` 里**（就是 `about 0.50 m tall (game scale)` 那句）。
- 这些数**比实物大**（头盔实物约 0.3 m，游戏里 0.65 m），所以必须**按游戏内尺寸**定。
- **建模三条**：① **原点随便**（运行时按「包围盒中心」对齐）② **朝向正着**（Y 上，Z 前）③ **尽量左右对称**。

### 成本一览（合计 ≈ 60 积分）

| 步骤 | 工具 | 积分 | 备注 |
|---|---|---|---|
| ③ 预览图 | `generate_image` | **10** | 先给玩家看，**等他确认**再往下走 |
| ④ 3D 模型 | `generate_model` | **50** | 含转换 |
| | | **≈ 60** | **数字只在本表维护**，别处不再重复写 |

## 2. 建骨架与 config.json

**骨架照 `reference/item_model/`**（它就是「正确答案」的样例）。工具只负责**校验**（`validate_mod`）与**安装**（`install_mod`）。
**名字与类名那几条不一致会静默不加载**。明细见 §5。

**工作区更新后，`reference/` 的模板可能变了。** 用模板覆盖自己的 `ModBehaviour.cs` 之前，
先把原文件备份。覆盖会丢掉你写过的改动。

**`config.json`**（两种写法）：

```json
// ① 一套模型换一件物品（最常用）
{ "typeIDs": [36], "model": "backpack.glb", "world": true }

// ② 每件物品各换各的（多条规则，按数组顺序匹配，**先命中的生效**）
{ "entries": [
    { "typeIDs": [260], "model": "backpack.glb" },
    { "targets": ["Item_Candy"], "model": "candy.glb", "handheld": true } ] }
```

| 字段 | 默认 | 说明 |
|---|---|---|
| `world` | **开** | 换**世界里的模型**（地上、展示、穿戴着）。纯外观，不改行为 |
| `handheld` | **关** | **只针对非装备物品**：连**拿在手里**那条也换。**会改行为**（物品变成「可拿在手里」与「界面上可以选它」）。要开就先问玩家一句 |
| `targets` | — | 物品**对象名全等**（数组，不区分大小写。例 `["Item_BackpackLV3"]`）|
| `typeIDs` | — | 或按 typeID（数组）。**推荐**。物品对象名不好记。与 `targets` 可以同时给，任一命中即可 |
| `model` | — | 放在本 mod 目录里的 GLB（相对名或绝对路径）|
| `size` | — | 修改大小（可选）。不写就不改大小 |
| `front` | `auto` | 仅**自带模型**需要：`auto`、`+z`、`-z`、`+x`、`-x`。Tripo 出的通常 `auto` 就行 |

> **三层模型**：① 世界（`world`）② 拿在手里（`handheld`）③ **2D 图标**（游戏自带。我们从不删，但可以替换）。
> 第 ③ 层那张图（背包格子与「使用时」出现的图）是游戏自带的。**我们不删**。

**查物品名与 typeID**：用 `inspect_game_data` 现查（`export, class=Item, file=resources.assets,
field=["typeID","displayName"]`，例 `Item_BackpackLV1`）。**中文名**去游戏自带的本地化里按 key 查
（见 `docs/cookbook.md` 第 14 节）。
要拿 typeID 就用 `inspect_game_data`（`class=Item` 与 `name=…`）。

**这个能力要引的 mod-kit 文件（4 个都要）**：
`Json.cs`、`GltfLoader.cs`、`GameApi.cs`、`ItemModel.cs`

**接口**（完整清单见 [`docs/mod-kit-api.md`](../../../docs/mod-kit-api.md)。签名用 `rg "public static" libs/mod-kit/ItemModel.cs` 现查）：

```csharp
var loaded = ModelKit.GltfLoader.LoadFile(路径, "auto");     // 返回 Mesh 与 MainTexture

// 主入口：一件物品（含它的模板）
var res = ModelKit.ItemModel.Apply(item, loaded.Mesh, loaded.MainTexture, handheld: 开关);

// handheld：是否**连「拿在手里」那条也换**（它会改行为：变成「可拿在手里」与「界面上可以选它」。见 §4）
// res.Report 用英文写着做了什么：旧外观数、挂到的挂点、有没有走「克隆加动态条目」。
// res.Restore() 能全还原（热重载用）
```

## 3. 装进游戏

`install_mod` 装好即可。**不用重启游戏去改配置**（mod 盯着 `config.json` 的写入时间，0.25 秒内热重载）。
改 `size` 也一样当场生效。

## 4. 怎么确认成功（装好不等于成功）

`validate_mod` 与 `install_mod` 已经替你查了**名字、类名与目录**。剩下的只有一件事：**让玩家看**。

- **让玩家做**：进游戏，背包切到目标，看**地上、穿戴或手里**（开了 `handheld` 才有手里）变成你的模型了没。
  装的时候游戏开着的话，先退出再进。
  物品会被游戏缓存，所以要**重新实例化一次**（丢地上再捡、换下来再穿上、重进关卡）才看到。
- 玩家说「变了、对的」就完成。
- 玩家说「没变」或「不对」时，先问清**哪儿不对**（没变、形状、贴图、尺寸），再对 §5 表。

## 5. 常见错误（对照修正）

| 症状 | 原因 | 修 |
|---|---|---|
| **什么都没变化** | 三个名字**不一致**（`info.ini` 的 name、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace），或类名不是 `ModBehaviour` | 改成同一个名字。`validate_mod` 会拦。namespace 是合法 C# 标识符，通常 PascalCase。照 `reference/item_model/` 抄最省事 |
| 物品**原样没变** | 命中条件写错，或规则里 `world=关` | 确认 `typeIDs` 抄对了（抄错一位就「什么都没命中」），并确认 `world` 没关 |
| **地上变了、手里没变** | 这条物品的 `handheld` 没开（默认关）| 加上 `"handheld": true`。会改行为，先问玩家。|
| **手里还是那张 2D 图** | 这件物品本来没有「拿着」的模型，而 `handheld` **默认是关的** | 加 `"handheld": true`。会改行为，先问玩家。|
| 模型**巨大或极小** | Tripo 会把**最长边归一到 ≈1 m**，不等于游戏内尺寸 | 先用 config 的 **`size`**（米。武器不写就自动按游戏内尺寸，物品不写则不缩放）；不够再重生成 |

### 接口一览（完整清单见 [`docs/mod-kit-api.md`](../../../docs/mod-kit-api.md)。签名用 `rg "public static" libs/mod-kit/ItemModel.cs` 现查）

| 调用 | 干什么、什么时候用 |
|---|---|
| `ItemModel.Apply(item, mesh, tex, handheld)` | **主入口**。换一件物品：有图形就**就地改**，没图形就**自动**克隆物品并注册动态条目。需要调的就这一个 |
| `ItemModel.ApplyHandheld(item, mesh, tex)` | 只换「**拿在手里**」那条。只有「物品**没有** `ItemGraphic`」时，`Apply(..., handheld: true)` 内部才调它。物品**有** `ItemGraphic` 时必须自己调，并把返回值放进世界那条 `Result` 的 `Nested`（否则 `Restore()` 不还原它）|
| `ItemModel.ClearAgentCache(item)` | 清实体缓存，游戏下次会用改过的图形重建实体。（`Apply` 内部已调。一般不用自己调。）|
| `Result` | 返回值：`Applied` 等于真换了、`NoOp` 等于已经换过或没得换（**不得打日志**）、`Report` 等于一句话说明、`Restore()` 等于还原 |
| `Result.Hidden` | 被我们关掉的旧外观。（热重载或卸载时 `Restore()` 会把它们开回来。）|
