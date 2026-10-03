# 02 · 物品（背包 / 消耗品 / 装饰 / 家具）

与武器**同一套骨架**（`GameObject + Transform + CharacterSubVisuals + ItemGraphicInfo`），差别只在造型与数量级。

## 真实样例

```
search --class GameObject --pattern "IG_Backpack"   → 12 match(es)
  IG_Backpack_SBossPoison_LowDurResFire  pathID=8455
  IG_Backpack_SBossElec_ResSpace         pathID=8456
  IG_Backpack_SBossFire_ResElec          pathID=8457
  IG_Backpack_PumpkinGhost               pathID=8458
  IG_Backpack_Cube                       pathID=8459
  IG_Backpack_LV1 … LV5

search --class GameObject --pattern "IG_BaseDeco"   → IG_BaseDeco_Snow / _Halloween / _Test / …
```

导出渲染器（真实输出节选）：

```
export --class CharacterSubVisuals --field "m_GameObject.#name" --field "renderers[].#class" --rows 6
→ (空名)  IG_Backpack_LV5   MeshRenderer
   (空名)  Crown             MeshRenderer
```

## 提取命令

```
action=search  class=GameObject  pattern="IG_Backpack"     # 找某一类
action=dump    pathid=8455  follow=true  depth=4           # 看结构（Transform/渲染器/挂点）
action=dump    class=Item  match="typeID=<id>"  depth=2    # 从物品反查 itemGraphic
```

## 参数化生成要点

- **背包**：主体盒体（实测 `Backpack_LV1` 全尺寸 **0.44 × 0.38 × 0.30 m**）+ 背带（细长盒体/圆柱）+ 细节（小盒体、扣具圆柱）。
- **消耗品（罐头/药剂）**：圆柱 + 顶盖圆台；贴图无关时用纯色分 submesh 区分。
- **装饰/家具**：多为静态盒体/旋转体组合，可多 submesh 分件上色。
- 结构参考同类的真实物品（`IG_Backpack_*` / `IG_BaseDeco_*` 等）；**先克隆同类物品的材质**再改色，避免 URP 粉紫。

## 挂载点

- 世界/地面模型走 `Item.itemGraphic`（`ItemGraphicInfo`）那棵树；
- 挂饰/附加件：挂到 `ItemGraphicInfo.sockets` 里的 socket（运行时还可 `DuckovItemAgent.GetSocket(name, createNew)` / `AddSocket(transform)`）；注意游戏里 `Socket` 与 `Soket` 两种拼法都有。


## 地面模型与身上模型：同一个

物品**只有一个图形 prefab** —— 游戏把它挂到**不同父节点**，不是两套模型：

| 场景 | 挂到哪 |
|---|---|
| 掉落 / 摆在地上 | 世界（由 `ItemGraphicInfo.groundPoint` 决定怎么贴合地面）|
| 穿在身上 | 角色的 socket：`ArmorSocket`（护甲）· `HelmatSocket`（头盔）· `BackpackSocket`（背包）|
| 拿在手上 | `ItemAgentUtilities` 绑到 `ItemAgent` |

实测证据：`Item_ArmorLV5_2` → `itemGraphic` = `IG_Armor_Lv5-2`；运行时角色 `ArmorSocket` 下挂的就是 `IG_Armor_Storm_Lv5(Clone)`（同名 IG prefab）；放置入口是 `ItemGraphicInfo.CreateAGraphic(item, parent, snapGround, useSpriteIfNoGraphic)` —— 参数就是「**父节点**」。

### 例外：11 件物品掉在地上时只显示一张**图标贴图**

实测 1581 件物品里 **11 件** `Item.useSpriteForPickup = 1`：

| 类别 | 物品 |
|---|---|
| 基地墙纸（6）| `Item_BaseWallPaper_Rock` / `_NewYear` / `_White` / `_Island` / `_Test` / `_Warehouse` |
| 基地装饰（3）| `Item_BaseDeco_Snow` / `_Halloween` / `_Test` |
| 电池（2）| `Item_BatteryPP3`（typeID 11）· `Item_BatteryR6`（typeID 8）|

机制（反编译 `InteractablePickup.CreateGraphic`）：**掉在地上的可拾取物**按这个字段二选一 ——
`= 1` 时克隆通用贴图 prefab `ItemGraphicInfo.spriteGraphicPfb`，并把 `Item.Icon` 设成它的 sprite（**地上一张图**）；
`= 0` 时才 `ItemGraphicInfo.CreateAGraphic(...)` 克隆物品自己的 `itemGraphic`（3D 模型）。

- **平时几乎见不到**：`CreateGraphic()` 只在物品**没有自己的 pickup 显示 prefab** 时才被调用
  （`ItemExtensions.CreatePickupAgent` 先取 `AgentUtilities.GetPrefab("Pickup")`，取不到才用通用 `PickupAgentPrefab`）。
  想看效果：把一块电池丢到地上（电池没有自己的 pickup prefab → 走这条路 → 显示图标）。
- 另一处兜底在 `ItemGraphicInfo.CreateAGraphic`：物品**根本没有** `itemGraphic` 时，同样用 `spriteGraphicPfb` + `Icon` 显示图标。
- ⚠️ **只影响"掉在地上的样子"**：拿在手里 / 穿在身上 / 背包图标仍用 3D 模型（这 11 件都有 `itemGraphic`）→
  **换模型不用管这个字段**；但如果要让这些物品在地上显示新模型，得**先清掉 `useSpriteForPickup`**。

→ 做模型时的含义：**一个物品做一份模型就够了**，地面 / 身上 / 手持都由游戏复用同一份；`attach` 只决定默认挂哪。

## 运行时：怎么换物品模型（背包 / 护甲 / 消耗品 / 容器…）

**一句话：用 JSON 在运行时算出「几何」，替换掉原版 prefab 里的几何；prefab 的其余一切照抄原版。**

```
models/*.json（零件清单）
   ↓ MeshKit 算顶点/索引/UV          ↓ TextureKit 按 fills 画贴图
   ↓
拼出一个 GameObject（MeshFilter + MeshRenderer + 克隆来的材质）
   ↓ 挂进“原版 prefab 的副本”里：旧几何关掉，我们的 mesh 挂上
   ↓ 写回 Item.itemGraphic（模板级）→ 游戏以后实例化的就是这份“换了几何的原版 prefab”
```

**本质 = 借一份游戏原有的 prefab**：**克隆 -> 只换主体零件的几何 + 贴图 -> 其余照抄**；**不从零造 prefab**。

为什么要这样：`ItemGraphicInfo` 上的 `groundPoint` / `sockets` / `subGraphics`、以及它挂到角色 socket 的整套关系
—— 全部照抄原版，所以物品一上来就能正确落地、能当装备挂到身上。
（工坊物品 mod 的差别：它们的几何是事先做好的（Unity/美术/AI），我们是**运行时用 JSON 现算**。）

⚠️ **只换主体零件那一个**（背包 = 包围盒最大的那个渲染器），其余零件一概不碰 —— 物品的模型也是一组零件，
除主体外都由游戏管（碰了会出现「有的零件该藏没藏 / 该显示没显示」）。

### 物品的两处模型

| 在哪 | 是什么 | 怎么换成我们的 |
|---|---|---|
| **掉落 / 展示** | `ItemGraphicInfo`（游戏用 `ItemGraphicInfo.CreateAGraphic(item.ItemGraphic, …)` 实例化）| **克隆 `item.ItemGraphic`** → 换掉克隆里的几何 → 反射写回 `Item.itemGraphic` |
| **穿在身上 / 拿在手上** | 游戏自己把物品的 `IG_*` 挂到角色 socket（`ArmorSocket` / `HelmatSocket` / `BackpackSocket` / `RightHandSocket`…）| 同上：换了 `itemGraphic`，游戏挂上去的就是我们的 |

### 两步走（都在库 `ItemModelBinder` 里）

1. **改物品模板**：`ItemAssetsCollection.GetPrefab(typeID)` 的 `Item.itemGraphic` → 之后**新生成**的实例（掉落/捡起/开局）
   天生就用我们的几何；同时**清实体缓存** `hashedAgentsCache`（社区做法），让游戏重建时读到我们的。
2. **已经存在的实例**（游戏不会重建）→ **就地**把旧的 `MeshRenderer` 关掉、把我们的 mesh 挂进原几何的变换下
   （**只改渲染器，不销毁任何东西**）。
   ⚠️ `Item.ItemGraphic` 只有 getter → 反射写私有字段 `itemGraphic`（社区顺序：先试可写属性 `ItemGraphic`，再退字段）。

### 尺寸与原点

- **尺寸 = 真实米制、`scale` 恒为 1**（实测：原游戏物品图形 99.5% 是 1、社区 mod 包 100% 是 1）→ 不缩放。
- ⚠️ prefab 内部**缩放链不一定是 1** —— 实测（沿父链相乘得到"世界缩放"）：

  | 样本 | 世界缩放 = 1 | 非 1 |
  |---|---|---|
  | 原游戏物品图形 `IG_*`（371）| 369（99%）| 2（0.85：`IG_Helmat_Battery2` / `IG_Helmat_Displayer`）|
  | 工坊物品 mod 的包（三角洲合集 200）| **100%** | 0 |

  → mesh 挂在**原几何零件**的变换下，`localScale` 取 `1 / 该零件 lossyScale`，保证**世界尺度**是真实尺寸
  （挂在根节点而不补缩放，实测会缩小到看不见）。
- **原点 = 放置点**：物品按“底部中心”做最省事（游戏用 `groundPoint` 贴地）；由模型文件的 `pivotOffset`（米）声明。

## 这些坑：哪些是游戏事实、哪些是做法不对

| 坑 | 性质 |
|---|---|
| 克隆出的模板一旦 `SetActive(false)`，游戏实例化出来的**全是隐形的** | **游戏事实**：Unity `Instantiate` 会继承激活状态 |
| 用 `CreateAgent` 换实体 → 武器**选不中 / 用不了** | **游戏事实**：游戏持有旧实体的引用 |
| 模型里混着几米大的特效零件（实测包围盒 **8 m**）| **游戏事实**：算尺寸只能按**主体零件** |
| 物品 prefab 的比例和真实尺寸不一致 | **游戏事实**：要按原模型对齐 |
| “关掉全部渲染器” → 不该藏的零件被藏了 | **做法不对**：物品的模型也是一组零件，**除主体外都由游戏管**，只能动主体 |
| “把 mesh 塞进第一个渲染器” → 只见一个很小的碎片 | **做法不对**：锚点要按语义取**主体零件**（不能随便取第一个）|
| “改 layer 当隐藏”被游戏改回去 | **游戏事实**（角色那条踩过）；物品这边直接用 `enabled = false` |

## 可运行的例子

`reference/item_swap/`（配置见 `config.json`：`model` / `match` / `typeIDs` / `bindNew`，改完保存即生效）。
库实现：`reference/mod-kit/ItemModelBinder.cs`。

## 例子（照着写）

### 背包（`backpack`）
**168 顶点 / 84 三角面** · 包围盒 **0.4 × 0.405 × 0.325 m** · 贴图 512²（自动密度 388 px/m）

```json
{
  "name": "backpack",
  "category": "items/backpack",
  "summary": "背包：主体 + 上盖 + 双肩带 + 扣具（肩带用 mirror 只写一边）",
  "parts": [
    { "role": "body", "shape": "box", "size": [0.4, 0.34, 0.26], "at": [0, 0.0, 0.0] },
    { "role": "flap", "shape": "box", "size": [0.4, 0.06, 0.26], "at": [0, 0.2, 0.0] },
    { "role": "strap", "shape": "box", "size": [0.06, 0.3, 0.03], "at": [0.14, 0.05, -0.14], "rot": [-8, 0, 0], "mirror": "x" },
    { "role": "buckle", "shape": "box", "size": [0.05, 0.04, 0.02], "at": [0.14, -0.04, -0.15], "mirror": "x" },
    { "role": "pouch", "shape": "box", "size": [0.18, 0.15, 0.07], "at": [0.0, -0.1, -0.16] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "body": "#1E2124", "flap": "#43533A", "strap": "#37432E", "buckle": "#2A2A2A", "pouch": "#556B45" } },
  "attach": {"kind": "item_graphic"}
}
```

### 罐头（`can_food`）
**420 顶点 / 168 三角面** · 包围盒 **0.07 × 0.118 × 0.068 m** · 贴图 512²（自动密度 1024 px/m）

```json
{
  "name": "can_food",
  "category": "items/food",
  "summary": "罐头：罐身 + 顶盖 + 底环",
  "parts": [
    { "role": "body", "shape": "cylinder", "r": 0.035, "h": 0.1, "segments": 14, "at": [0, 0.0, 0.0] },
    { "role": "lid", "shape": "cylinder", "r": 0.033, "h": 0.01, "segments": 14, "at": [0, 0.055, 0.0] },
    { "role": "base", "shape": "cylinder", "r": 0.033, "h": 0.008, "segments": 14, "at": [0, -0.054, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "body": "#1E2124", "lid": "#C0C4C8", "base": "#7E8288" } },
  "attach": {"kind": "item_graphic"}
}
```

### 手办（`figurine`）
**304 顶点 / 136 三角面** · 包围盒 **0.16 × 0.36 × 0.11 m** · 贴图 512²（自动密度 754 px/m）

```json
{
  "name": "figurine",
  "category": "items/collectible",
  "summary": "手办：底座 + 腿 + 躯干 + 双臂（mirror）+ 头 + 头发",
  "parts": [
    { "role": "base", "shape": "cylinder", "r": 0.05, "h": 0.012, "segments": 16, "at": [0, 0.006, 0.0] },
    { "role": "legs", "shape": "box", "size": [0.07, 0.1, 0.045], "at": [0, 0.062, 0.0] },
    { "role": "torso", "shape": "box", "size": [0.1, 0.12, 0.06], "at": [0, 0.172, 0.0] },
    { "role": "arm", "shape": "box", "size": [0.03, 0.1, 0.04], "at": [0.065, 0.172, 0.0], "mirror": "x" },
    { "role": "head", "shape": "box", "size": [0.1, 0.1, 0.1], "at": [0, 0.282, 0.0] },
    { "role": "hair", "shape": "box", "size": [0.11, 0.04, 0.11], "at": [0, 0.34, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "base": "#7E8288", "legs": "#4A4E7A", "torso": "#E8E2D8", "arm": "#E8E2D8", "head": "#6E7276", "hair": "#3A2E2A" } },
  "attach": {"kind": "item_graphic"}
}
```

### 储物箱（`storage_crate`）
**120 顶点 / 60 三角面** · 包围盒 **0.52 × 0.34 × 0.37 m** · 贴图 512²（自动密度 295 px/m）

```json
{
  "name": "storage_crate",
  "category": "items/container",
  "summary": "储物箱：箱体 + 盖 + 两道包边（mirror）+ 锁扣",
  "parts": [
    { "role": "body", "shape": "box", "size": [0.5, 0.3, 0.35], "at": [0, 0.0, 0.0] },
    { "role": "lid", "shape": "box", "size": [0.51, 0.04, 0.36], "at": [0, 0.17, 0.0] },
    { "role": "band", "shape": "box", "size": [0.52, 0.03, 0.02], "at": [0, 0.0, 0.09], "mirror": "z" },
    { "role": "lock", "shape": "box", "size": [0.06, 0.05, 0.02], "at": [0, 0.12, -0.18] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "body": "#1E2124", "lid": "#C0C4C8", "band": "#4A4A4A", "lock": "#C8A94A" } },
  "attach": {"kind": "item_graphic"}
}
```

### 防弹衣（`armor_vest`）
**168 顶点 / 84 三角面** · 包围盒 **0.42 × 0.47 × 0.28 m** · 贴图 512²（自动密度 309 px/m）

```json
{
  "name": "armor_vest",
  "category": "items/armor",
  "summary": "防弹衣：衣身 + 前插板 + 肩带（mirror）+ 腰带 + 弹匣袋（mirror）",
  "parts": [
    { "role": "shell", "shape": "box", "size": [0.34, 0.4, 0.22], "at": [0, 0.0, 0.0] },
    { "role": "plate", "shape": "box", "size": [0.26, 0.26, 0.04], "at": [0, 0.02, -0.12] },
    { "role": "shoulder", "shape": "box", "size": [0.1, 0.08, 0.18], "at": [0.16, 0.2, 0.0], "mirror": "x" },
    { "role": "belt", "shape": "box", "size": [0.36, 0.06, 0.24], "at": [0, -0.2, 0.0] },
    { "role": "pouch", "shape": "box", "size": [0.1, 0.1, 0.06], "at": [0.12, -0.12, -0.13], "mirror": "x" }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "shell": "#3E4A3A", "plate": "#2E362B", "shoulder": "#3E4A3A", "belt": "#2A2E28", "pouch": "#556B45" } },
  "attach": {"kind": "socket", "name": "ArmorSocket"}
}
```

### 头盔（`helmet`）
**144 顶点 / 72 三角面** · 包围盒 **0.3 × 0.245 × 0.34 m** · 贴图 512²（自动密度 421 px/m）

```json
{
  "name": "helmet",
  "category": "items/helmet",
  "summary": "头盔：盔体 + 帽檐 + 面罩 + 耳罩（mirror）+ 顶部导轨",
  "parts": [
    { "role": "dome", "shape": "box", "size": [0.24, 0.2, 0.28], "at": [0, 0.06, 0.0] },
    { "role": "brim", "shape": "box", "size": [0.26, 0.04, 0.3], "at": [0, -0.04, 0.02] },
    { "role": "visor", "shape": "box", "size": [0.22, 0.1, 0.06], "at": [0, 0.02, -0.14] },
    { "role": "earpiece", "shape": "box", "size": [0.04, 0.1, 0.1], "at": [0.13, 0.02, 0.02], "mirror": "x" },
    { "role": "rail", "shape": "box", "size": [0.06, 0.03, 0.16], "at": [0, 0.17, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "dome": "#3A443A", "brim": "#333B33", "visor": "#1A1E22", "earpiece": "#2A302A", "rail": "#22261F" } },
  "attach": {"kind": "socket", "name": "HelmatSocket"}
}
```
