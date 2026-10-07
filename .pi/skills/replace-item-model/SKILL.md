---
name: replace-item-model
description: 用户想换**物品**（背包 / 防弹衣 / 头盔 / 弹匣 / 任务道具…这类非武器物品）在世界里的模型时用（例："把这个背包换成这个模型" / "这个头盔换成 glb" / "物品模型替换"）。把 GLB 放进 mod、写一句 config、编译安装即可。
---

# 替换物品模型

**目标**：某件（或某批）**物品在世界里的模型**换成用户提供的模型。不改数值、不改图标、不改行为。

## 1. 模型从哪来

素材是同一套 Tripo 流程（**参考图 → 预览图 → 3D 模型**），配方与成本见
`.pi/skills/replace-weapon-model/SKILL.md` §1 —— 那是**唯一一份**，这里不重复 ✓。

**落到物品上，按这三点定素材** ✓：

| 点 | 怎么做 |
|---|---|
| **尺寸** | 按**实物**给：背包 ≈ 0.4 m · 头盔 ≈ 0.3 m · 弹匣 ≈ 0.2 m —— 提示词里写清尺寸，别出 1 m 的巨物 ✗ |
| **朝向** | 一般**不用管** ✓（物品不要求朝向）；摆出来难看再在 config 里加 `front` ✓ |
| **图标** | **本能力不换图标** ✗（只换世界里的模型 ✓ 图标另做 ✓）|

## 2. 建骨架与 config.json

**骨架照 `reference/item_model/`**（它就是"正确答案"的样例 ✓）；工具只负责**校验**（`validate_mod`）与**安装**（`install_mod`）。

**四处名字必须一致**（不一致的症状 = **mod 静默不加载**，游戏不报错 ✗）：
`info.ini` 的 `name` · 目录名 · 程序集名（csproj 的 `AssemblyName`）· 命名空间 + 类 `ModBehaviour`

**`config.json`**（两种写法 ✓ 旧写法仍然有效 ✓）：

```json
// ① 一套素材换一件物品
{ "typeIDs": [36], "model": "backpack.glb" }

// ② 每件物品各换各的（多条规则 ✓ 按数组顺序匹配，**先命中的生效** ✓）
{ "entries": [
    { "typeIDs": [260], "model": "backpack.glb" },
    { "targets": ["Item_BackpackLV3"], "model": "backpack_lv3.glb" } ] }
```

| 字段 | 说明 |
|---|---|
| `targets` | 物品**对象名全等**（数组 ✓ 不区分大小写，例 `["Item_BackpackLV3"]`）|
| `typeIDs` | 或按 typeID（数组 ✓）—— **推荐 ✓**（物品对象名不好记 ✓ 与 `targets` 二选一 ✓）|
| `model` | 放在本 mod 目录里的 GLB（相对名或绝对路径 ✓）|
| `front` | 仅**自带模型**需要：`auto`/`+z`/`-z`/`+x`/`-x`（Tripo 出的通常 `auto` 就行 ✓）|

**查物品名 / typeID**：`docs/data/Items.csv`（`对象名, 英文名, 中文名` ✓ 例 `Item_BackpackLV1`）；
要拿 typeID 就用 `inspect_game_data`（`class=Item` / `name=…` ✓）。

**这个能力要引的 mod-kit 文件（4 个都要 ✓）**：
`Json.cs` · `GltfLoader.cs` · `GameApi.cs` · `ItemModel.cs`

**接口**（完整签名：`rg "public static" libs/mod-kit/ItemModel.cs`）：

```csharp
var loaded = ModelKit.GltfLoader.LoadFile(路径, "auto");     // → Mesh + MainTexture

ModelKit.ItemModel.ApplyByTypeID(36, loaded.Mesh, loaded.MainTexture);  // **推荐**：按 typeID ✓（不用先拿到 Item ✓）
ModelKit.ItemModel.Apply(item, loaded.Mesh, loaded.MainTexture);        // 或按物品：模板 + 场上实例一起换 ✓
ModelKit.ItemModel.ApplyToInstance(item, loaded.Mesh, loaded.MainTexture);  // 只改场上这一个（不动模板 ✓）

// res.Report 里有"关旧外观 N 个 / 挂到 哪个挂点 / 清缓存"；res.Restore() 能全还原（热重载用 ✓）
// ⚠️ 没有 ItemGraphic 的物品（纯图标那种 ✓）会被**跳过** ✓（换不了外观 ✗ 日志里写"跳过" ✓）
```

## 3. 装进游戏

`install_mod` 装好即可 —— **不用重启游戏去改配置** ✓（mod 盯着 `config.json` 的写入时间 ✓ 0.25 秒内热重载 ✓）。

## 4. 怎么确认成功（装好 ≠ 成功）

1. 日志（`[ItemModel] 已换：'Item_Backpack_Lv_3'(typeID=260) ← backpack.glb｜锚点='…'｜关旧零件 N 个…`）；
   失败会打 `WARN`（模型不可用 / 没换成 ✓）。
2. 游戏里：那件物品（**地上 / 手里 / 仓库里** ✓）变成你的模型 ✓。
3. ⚠️ **别指望"立刻就变"** ✓：物品的实体会被游戏缓存 → **重新实例化一次**（丢地上再捡 / 重进关卡 ✓）才看到。
4. 没变的话，按顺序查：① `info.ini` 的 `name` 四处一致吗 ② **匹配条件**对不对（`typeIDs` ✓ / 对象名**全等** ✓ —— 看日志里那条规则 ✓）
   ③ 日志有没有 `[ItemModel] 规则 N 条`（没有 = mod 没加载 ✗）。

## 5. 常见错误（对照修正）

| 症状 | 原因 | 修 |
|---|---|---|
| mod 静默不加载（日志啥也没有）| `info.ini` 的 `name` / 目录名 / 程序集名 / 命名空间**不一致** ✗ | 四处改成同一个名字 |
| 物品**原样没变**（点了没反应）✓ | 命中条件没写对 / 该物品没有 ItemGraphic ✓ | 看日志那条 `[ItemModel] 规则 …`；`typeIDs` 抄错一位就"什么都没命中" ✗ |
| 物品变**透明/看不见** ✗ | 我们的 mesh 没挂上，而旧外观已经被关了 | 看日志 `挂到 '…'` ✓；多半是模型文件坏（`model-check` ✓）或顶点异常 ✓ |
| **整件物品连它原来的外观一起消失** ✗ | 以前那版用 DDOL 模板会这样（实例全生在 DDOL 场景 ✗）| **已修** ✓（现在就地改游戏自己的 prefab ✓ 不克隆 ✗ 不 DDOL ✗）|
| 物品变成**一片纸/贴图糊** | 模型没有合适的 UV 或贴图太大 | 用 `model-check` 看 UV/贴图 |
| 物品**飘着 / 离地** | 模型原点不在"物体中心" | 物品不要求贴地 ✓；太难看就换模型（或调 `front` ✓）|
| 手里那件没变、别的都变了 | 手里那个是**旧实例** ✓ | 丢地上再捡起来 ✓（或重进关卡 ✓）|
| 模型**巨大/极小** | 模型不是真实米制 | 重新生成（提示词写清实物尺寸 ✓）或换模型 |

## 6. 还没做的（别承诺）

- **图标** ✗：本能力只换世界模型；物品图标要用 `inspect_game_data` / 另做（能力单列 ✓）。
- **新增物品**（新 typeID / 名字 / 数值）✗：那是**数据层**，属另一个能力（`new-item`），本能力只动**已有物品的外观** ✓。
