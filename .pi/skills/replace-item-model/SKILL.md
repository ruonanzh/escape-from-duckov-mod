---
name: replace-item-model
description: 用户想换**物品**（背包 / 防弹衣 / 头盔 / 弹匣 / 任务道具…这类非武器物品）在世界里的模型时用（例："把这个背包换成这个模型" / "这个头盔换成 glb" / "物品模型替换"）。把 GLB 放进 mod、写一句 config、编译安装即可。
---

# 替换物品模型

**目标**：某件（或某批）**物品在世界里的模型**换成用户提供的模型。不改数值、不改图标、不改行为。

## 1. 模型从哪来（顺序：先建骨架 → 再产素材 → 最后装）

一句话也能做。顺序是「**先把 mod 骨架建好 → 再产素材 → 最后装**」—— 目录先存在、也先绑定好，后面直接往里写。

### ① 参考图（免费）

- 用户给了图 → 就直接用（聊天里发的图会落盘，拿到的是**绝对路径**，直接传给工具）
- 图里带 **logo / 水印 / UI 文字** → **先裁掉再传**
- 用户只说了句话 → 跳过这步，直接进 ②
- 给了好几张 → **问用户用哪张**

### ② 先把 mod 骨架建好（免费，编译几秒）

- 照 **`reference/item_model/`**（示例 mod = 正确答案）建你自己的 mod 目录
- **四处名字要一致**（`info.ini` 的 name / 目录名 / 程序集名 / 命名空间+类 `ModBehaviour`）→ 不一致的症状是 **mod 静默不加载**
- 建完**先跑一次 `validate_mod`** 确认能编译
- **先建骨架、别等素材** —— 素材工具落盘时会自建目录，别让它抢在你前面把目录建出来

### ③ 预览图：**清理 + 居中 + 加粗**（约 5 积分）—— **先给用户看，等他确认**

⚠️ 本作的美术**偏厚实**，而 Tripo 会**严格照图生成** ✗ → 只清理不加粗，出来的会偏"细" ✗
所以这一步的提示词同时做三件事：**清理 + 居中 + 加粗** ✓

```
generate_image(
  image="<用户的图>",                       // 只给文字描述时省略（→ 纯提示词出图 ✓）
  prompt="Redraw the item from the input as a clean, isolated in-game asset: keep ONLY the main item - if the input shows several objects, take the largest, centered one and ignore the rest. The whole item alone and complete - keep EVERY part of it; remove only what is not the item (the background, any floating pieces, text, frames). Put it on a plain uniform white background: no scenery, no props, no shadow, no smoke, no hands, no text. Keep the item's own design and colors the same, but render it in a CARTOON / toon-shaded style like the game's own assets (flat stylized colors, clean shading - not photoreal), and make it noticeably BULKIER and THICKER: heavy, sturdy proportions like a stylized low-poly game asset, about 0.55 m tall (game scale). Show it from the SIDE (a profile view that shows its thickness and depth), NOT a front view, and perfectly CENTERED in the frame, with a clear margin on all sides.",
  out="your_mods/<mod>/.preview/<物品名>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有 **8 件事都不能丢** ✗：**只留主物品**（图里有多个物体时取**最大最居中**那个 ✓ 其余全丢 ✗）· **整件物品都在** · **只移除不是它的东西** · **纯白均一背景** · **保原设计与颜色 + 卡通渲染**（不是照片感 ✗）· **加粗** · **侧视** · **居中留边**
- ⚠️ `about 0.55 m tall (game scale)` 那句里的数字**按物品类别换** ✓（见 §1 末尾的尺寸表 ✓）
- 出图后**把文件路径给用户**（工具返回里就是绝对路径 ✓）→ 问他「就要这个吗」
  ⚠️ 图大于 1.5MB 时工具**不会**内联显示 ✗ → 所以**必须把路径写出来** ✓
- `.preview/` 放在 mod 目录下：`install_mod` 会跳过它（不进游戏）✓
- 不像就改 `prompt=` 再来一次（每次都便宜）—— **没确认前不要做 ④**

### ④ 3D 模型（约 50 积分，含转换）

```
generate_model(action="generate",
               image="your_mods/<mod>/.preview/<物品名>.preview.png",   // 用 ③ 那张**已确认的**预览图 ✓
               out="your_mods/<mod>/<物品名>.glb",                      // 模型名**按物品起** ✓（backpack.glb ✓）
               faceLimit=3000)
```

- **图 → 3D 比纯文字准得多** ✓
- 用户自带 `.glb` → ③④ 全跳过，直接把他的文件放进 mod 目录 ✓
- 用户只要了一句话、没图 → 用 `prompt="…"` 出模型 ✓

### ⑤ 图标 —— **本能力不需要** ✗

只换世界里的模型 ✓ **不换图标**（背包格子 / "使用时"那张图是 UI 画的 ✓ 属另一条能力 ✓）

### 尺寸：**照游戏里那一档做** ✓（实测 ✓ **不是实物尺寸** ✗）

| 类别 | 典型最长边 |
|---|---|
| 背包 | **0.55 m** |
| 防弹衣（身体）| **0.80 m** |
| 头盔（头部）| **0.65 m** |
| 面具（面部）| **0.65 m** |
| 耳机 | **0.78 m** |

- 这行数字**写进 ③ 的 `prompt=` 里** ✓（就是 `about 0.55 m tall (game scale)` 那句 ✓）
- ⚠️ 这些数**比实物大**（头盔实物 ~0.3 m，游戏里 0.65 m）→ **照游戏那一档** ✓
- **建模三条** ✓：① **原点随便**（运行时按"包围盒中心"对齐 ✓）② **朝向正着**（Y 上 · Z 前）③ **尽量左右对称** ✓

### 成本一览（合计 ≈ 60 积分）

预览图 `generate_image` **10** · 3D 模型 `generate_model` **50**（含转换）

## 2. 建骨架与 config.json

**骨架照 `reference/item_model/`**（它就是"正确答案"的样例 ✓）；工具只负责**校验**（`validate_mod`）与**安装**（`install_mod`）。

**四处名字必须一致**（不一致的症状 = **mod 静默不加载**，游戏不报错 ✗）：
`info.ini` 的 `name` · 目录名 · 程序集名（csproj 的 `AssemblyName`）· 命名空间 + 类 `ModBehaviour`

**`config.json`**（两种写法 ✓）：

```json
// ① 一套素材换一件物品（最常用 ✓）
{ "typeIDs": [36], "model": "backpack.glb", "world": true }

// ② 每件物品各换各的（多条规则 ✓ 按数组顺序匹配，**先命中的生效** ✓）
{ "entries": [
    { "typeIDs": [260], "model": "backpack.glb" },
    { "targets": ["Item_BackpackLV3"], "model": "backpack_lv3.glb", "handheld": true } ] }
```

| 字段 | 默认 | 说明 |
|---|---|---|
| `world` | **开** ✓ | 换**世界里的模型** ✓（地上 / 展示 / 穿戴着 ✓）—— 纯外观 ✓ 不改行为 ✓ |
| `handheld` | **关** ✓ | 连**拿在手里**那条也换 ✓ ⚠️ **会改行为**（物品变成“可拿在手里 + UI 可选中” ✓）→ 要开就先问玩家一句 ✓ |
| `targets` | — | 物品**对象名全等**（数组 ✓ 不区分大小写，例 `["Item_BackpackLV3"]`）|
| `typeIDs` | — | 或按 typeID（数组 ✓）—— **推荐 ✓**（物品对象名不好记 ✓ 与 `targets` 可同时给 ✓ 任一命中即可 ✓）|
| `model` | — | 放在本 mod 目录里的 GLB（相对名或绝对路径 ✓）|
| `size` | — | 修改大小（可选）。不写就不改大小 |
| `front` | `auto` | 仅**自带模型**需要：`auto`/`+z`/`-z`/`+x`/`-x`（Tripo 出的通常 `auto` 就行 ✓）|

> **三层模型** ✓：① 世界（`world` ✓）② 拿在手里（`handheld` ✓）③ **2D 图标**（游戏自带 ✓ 我们从不删 ✗ 但可以替换 ✓）。
> 第 ③ 层那张图（背包格子 / “使用时”出现的图）是 **UI 画的** ✗ → 不属本能力（要变它只能**改图标** ✓）。

**查物品名 / typeID**：`docs/data/Items.csv`（`对象名, 英文名, 中文名` ✓ 例 `Item_BackpackLV1`）；
要拿 typeID 就用 `inspect_game_data`（`class=Item` / `name=…` ✓）。

**这个能力要引的 mod-kit 文件（4 个都要 ✓）**：
`Json.cs` · `GltfLoader.cs` · `GameApi.cs` · `ItemModel.cs`

**接口**（⭐ **完整清单见 [`docs/mod-kit-api.md`](../../docs/mod-kit-api.md)** ✓ —— 签名用 `rg "public static" libs/mod-kit/ItemModel.cs` 现查 ✓）：

```csharp
var loaded = ModelKit.GltfLoader.LoadFile(路径, "auto");     // → Mesh + MainTexture

// 主入口：一件物品（含它的模板 ✓）
var res = ModelKit.ItemModel.Apply(item, loaded.Mesh, loaded.MainTexture, handheld: 开关);

// handheld：要不要**连"拿在手里"那条也换** ✓（⚠️ 它会改行为：变成"可拿在手里 + UI 可选中" ✓ 见 §4）
// res.Report 里写着做了什么（关旧外观 N 个 / 挂到哪个挂点 / 有没有走"克隆+动态条目"）；
// res.Restore() 能全还原（热重载用 ✓）
```

## 3. 装进游戏

`install_mod` 装好即可 —— **不用重启游戏去改配置** ✓（mod 盯着 `config.json` 的写入时间 ✓ 0.25 秒内热重载 ✓）。
改 `size` 也一样当场生效 ✓。

## 4. 怎么确认成功（装好 ≠ 成功）

1. 日志 ✓ —— 两条路看不同的关键字 ✓：
   - 物品**有**图形（背包/装备类 ✓）：`[ItemModel] 已换：'…'｜关旧外观 N 个｜挂到 '…'｜…`
   - 物品**没有**图形（糖果那类 ✓）：`…｜没有 ItemGraphic → **克隆物品预制体 + 注册动态条目**（遮蔽原物品 ✓ 关卡重建冲不掉 ✓）｜…`
   - 开了 `handheld` ✓：会另有一条 `…｜（拿在手里那条 ✓）清掉模板自带视觉 N 个｜…`
   - 没成会写 `[ItemModel] 跳过：'…' <原因>` ✓（比如模型文件坏 ✓）
2. 游戏里：**地上 / 穿戴 / 手里**（开了 `handheld` 才有手里 ✓）变成你的模型 ✓。
3. ⚠️ **别指望“立刻就变”** ✓：物品的实体会被游戏缓存 → **重新实例化一次**（丢地上再捡 / 换下来再穿上 / 重进关卡 ✓）才看到 ✓。
4. 没变的话，按顺序查：① `info.ini` 的 `name` 四处一致吗 ② **匹配条件**对不对（`typeIDs` ✓ / 对象名**全等** ✓ —— 看日志里那条规则 ✓）
   ③ 日志有没有 `[ItemModel] 规则 N 条`（没有 = mod 没加载 ✗）④ 那条规则里 `world=开` ✓ 吗。

## 5. 常见错误（对照修正）

| 症状 | 原因 | 修 |
|---|---|---|
| mod 静默不加载（日志啥也没有）| `info.ini` 的 `name` / 目录名 / 程序集名 / 命名空间**不一致** ✗ | 四处改成同一个名字 |
| 物品**原样没变** ✓ | 匹配条件写错 / 规则里 `world=关` ✗ | 看日志那条 `[ItemModel] 规则 …` ✓；`typeIDs` 抄错一位就“什么都没命中”✗ |
| **地上变了、手里没变** ✓ | 这条物品的 `handheld` 没开 ✓（默认关 ✓）| 加上 `"handheld": true` ✓（⚠️ 会改行为 ✓ 先问玩家 ✓）|
| **手里还是那张 2D 图**（开了 handheld 也一样）✗ | 该物品**没有“拿着”这个表示** ✗ —— 它“使用时”那张图是 **UI 画的** ✓ | 本能力做不到 ✗ → 要变只能**改图标** ✓（另一条能力 ✓）|
| **UI / 背包格子里的图没变** ✓ | 本能力**只换世界模型** ✓，从不改图标 ✗ | 正常 ✓；想改图标走图标能力 ✓ |
| 模型**巨大/极小** | 模型不是真实米制 | 重新生成（提示词写清实物尺寸 ✓）或换模型 ✓ |
| 物品变**一片纸/贴图糊** | 模型没有合适 UV 或贴图太大 | 用 `model-check` 看 UV/贴图 ✓ |
| 模型**穿模 / 悬空** | 模型尺寸/原点不适合（比如把 1 m 的枪装到耳机上 ✓）| 换个尺寸合理的模型 ✓；或等 per-item 的 `scale`/`offset` ✗（还没做 ✓）|

### 接口一览 ✓（⭐ **完整清单见 [`docs/mod-kit-api.md`](../../docs/mod-kit-api.md)** ✓ —— 签名用 `rg "public static" libs/mod-kit/ItemModel.cs` 现查 ✓）

| 调用 | 干什么 · 什么时候用 |
|---|---|
| `ItemModel.Apply(item, mesh, tex, handheld)` | **主入口** ✓ 换一件物品：有图形就**就地改** ✓ 没图形就**自动**克隆物品 + 注册动态条目 ✓ 需要调的就这一个 ✓ |
| `ItemModel.ApplyHandheld(item, mesh, tex)` | 只换"**拿在手里**"那条 ✓（`Apply(..., handheld: true)` 内部会调它 ✓ 一般不用自己调 ✗）|
| `ItemModel.ClearAgentCache(item)` | 清实体缓存 → 游戏下次会用改过的图形重建实体 ✓（`Apply` 内部已调 ✓ 一般不用自己调 ✗）|
| `Result` | 返回值 ✓：`Applied`=真换了 ✓ · `NoOp`=已经换过/没得换 ✓（**别打日志** ✗）· `Report`=一句话说明 ✓ · `Restore()`=还原 ✓ |
| `Result.Hidden` | 被我们关掉的旧外观 ✓（热重载/卸载时 `Restore()` 会把它们开回来 ✓）|
