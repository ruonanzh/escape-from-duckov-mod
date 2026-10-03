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
- 结构与材质做法同 [`01-weapons.md`](01-weapons.md)；**先克隆同类物品材质**再改色，避免 URP 粉紫。

## 挂载点

- 世界/地面模型走 `Item.itemGraphic`（`ItemGraphicInfo`）那棵树；
- 挂饰/附加件挂 `ItemGraphicInfo.sockets`（**挂点全清单 + 命名变体**见 [`00-shared.md`](00-shared.md) 的「挂点」一节）。


## 地面模型与身上模型：同一个

物品**只有一个图形 prefab** —— 游戏把它挂到**不同父节点**，不是两套模型：

| 场景 | 挂到哪 |
|---|---|
| 掉落 / 摆在地上 | 世界（由 `ItemGraphicInfo.groundPoint` 决定怎么贴合地面）|
| 穿在身上 | 角色的 socket：`ArmorSocket`（护甲）· `HelmatSocket`（头盔）· `BackpackSocket`（背包）|
| 拿在手上 | `ItemAgentUtilities` 绑到 `ItemAgent` |

实测证据：`Item_ArmorLV5_2` → `itemGraphic` = `IG_Armor_Lv5-2`；运行时角色 `ArmorSocket` 下挂的就是 `IG_Armor_Storm_Lv5(Clone)`（同名 IG prefab）；放置入口是 `ItemGraphicInfo.CreateAGraphic(item, parent, snapGround, useSpriteIfNoGraphic)` —— 参数就是「**父节点**」。

**例外（1580 件物品里 11 件）**：地面不用 3D 模型而用**贴图**（`Item.useSpriteForPickup = 1`），另有 `ItemGraphicInfo.spriteGraphicPfb` / `fallbackSprite` 兜底。

→ 做模型时的含义：**一个物品做一份模型就够了**，地面 / 身上 / 手持都由游戏复用同一份；`attach` 只决定默认挂哪。

## 运行时：怎么换物品模型（枪 / 背包 / 箱子…）

> 「武器配件其实不是挂上去的」（`ShowIf_*` / `HideIf_*` 条件零件）与挂点清单见 [`00-shared.md`](00-shared.md) 的「挂点」一节。

物品的模型有**两条独立路径**，位置不同、做法不同：

| 在哪 | 是什么 | 怎么换成我们的 |
|---|---|---|
| **拿在手上** | `Item.ActiveAgent`（`ItemAgent`）| 在实体上**只替换“枪身”那一个零件**：把它 `enabled = false`，在**它的变换下**挂我们的 mesh；**其余零件一律不碰** |
| **掉落 / 展示** | `ItemGraphicInfo`（游戏用 `ItemGraphicInfo.CreateAGraphic(item.ItemGraphic, …)` 实例化）| **克隆 `item.ItemGraphic`** → 换掉克隆里的几何 → **反射写回**私有的 `Item.itemGraphic`（游戏之后实例化的就是我们的）|

```csharp
// ① 手持：就地换（不要重建实体）
var active = item.ActiveAgent;
var body = active.GetComponentsInChildren<MeshRenderer>(true)
                 .First(r => r.gameObject.name.StartsWith("WPN_"));     // 枪身
body.enabled = false;
var go = new GameObject("ModelKit_mesh");
go.transform.SetParent(body.transform, false);                          // 继承枪身的位置/旋转/缩放
go.transform.localScale = Vector3.one * fit;                            // fit = 原枪身尺寸 / 我们模型尺寸
go.AddComponent<MeshFilter>().sharedMesh = ourMesh;
go.AddComponent<MeshRenderer>().sharedMaterial = 克隆枪身材质 + 我们的贴图(_MainTex);

// ② 掉落 / 展示：换 prefab，让游戏去实例化
var clone = Object.Instantiate(item.ItemGraphic);                       // 保留 sockets / groundPoint / 设置
clone.gameObject.SetActive(true);                                       // ⚠️ 必须保持激活
clone.transform.position = new Vector3(0f, -5000f, 0f);                 // 模板挪到世界外
// 同样的方式把 clone 里的几何换成我们的
typeof(Item).GetField("itemGraphic", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(item, clone);
```

**要记住的几点**

- **手里是“零件组合”，不是一个 mesh**：除枪身外还有配件槽位（`ShowIf_*` / `HideIf_*`）、弹匣、枪机…**这些由游戏按状态开关** → **只能动枪身**。
- **不要重建实体**（例如 `ItemAgentUtilities.CreateAgent`）：它会销毁旧实体，而游戏（`ItemAgentHolder` 等）还持有引用 → 那件武器会**选不中 / 用不了**，直到你丢地上再捡起来。
- **对齐尺寸**：物品 prefab 的比例与真实尺寸不一致（我们 0.2 m 的手枪要塞进 MP5 的 0.86 m 槽位）→ 按**原枪身包围盒**等比缩放。
- **材质**：克隆**枪身**的材质，把我们的贴图塞进 `_MainTex`（武器 shader `SodaCraft/SodaLit` 的主贴图槽就是 `_MainTex`）；别从零建材质。
- **每帧重申**：物品会被反复实例化（开局 / 掉落 / 拾取 / 切枪）→ 看到没换过的实例就换。
- **优先写“物品模板”**（`ItemAssetsCollection.GetPrefab(typeID)`）：以后每次实例化都对（开局就生效）；
  活实例再补一次，兜住“改之前就已经生成”的那些。
- **给全新物品模型**：同 ② —— 反射写 `itemGraphic` 就是“给这个物品一份模型”。

## 与社区做法的对照（都是反编译社区 mod 得到的）

| 环节 | 社区做法（三角洲合集 / 优香MPX）| 我们 |
|---|---|---|
| 写物品图形 | 反射：**先试可写属性 `ItemGraphic`，再退到私有字段 `itemGraphic`** | 同（已采纳这个顺序）|
| **写到哪** | **写到物品模板 `ItemAssetsCollection.GetPrefab(typeID)`** → 以后每次实例化都对 | 也写模板（`BindGraphicOnPrefab`）+ 补一次活实例 |
| 图形 prefab 怎么造 | 用 **bundle 里的 prefab** + `AddComponent<ItemGraphicInfo>()` + 子物体塞进 `ModelPivot` | **克隆物品已有的图形**（保留 sockets / groundPoint / 各设置），只换几何 |
| 手持实体 | `SetAgentPrefab`（写 `ItemAgentUtilities.agents`）+ **`ClearAgentCache`**（清 `hashedAgentsCache`）让游戏重建 | **就地换几何**（不重建 → 不破坏游戏持有的引用）|
| 材质 / shader | 自带材质 + **`FixModelShaders`**：把所有材质 shader 统一换成 `SodaCraft/SodaLit`（退 URP Lit/Unlit/Standard），保留贴图与颜色 | **克隆游戏现成材质**（因此不需要修 shader）|

**结论**：两条路殊途同归，**优先写"物品模板"**（`ItemAssetsCollection.GetPrefab`）—— 这样新实例天生就对，
不用每帧重申；活实例的补绑只是兜底（处理"改之前就已经生成"的那些）。

## 这些坑：哪些是游戏事实、哪些是做法不对

| 坑 | 性质 |
|---|---|
| 克隆出的模板一旦 `SetActive(false)`，游戏实例化出来的**全是隐形的** | **游戏事实**：Unity `Instantiate` 会继承激活状态 |
| 用 `CreateAgent` 换实体 → 武器**选不中 / 用不了** | **游戏事实**：游戏持有旧实体的引用 |
| 物品里混着几米大的特效零件（实测包围盒 **8 m**）| **游戏事实**：算尺寸只能按枪身 |
| 物品 prefab 的比例和真实尺寸不一致 | **游戏事实**：要按原模型对齐 |
| “关掉全部渲染器” → 配件 / 弹匣消失 / 只有几个配件 | **做法不对**：手里是零件组合，除枪身外都由游戏管 |
| “把 mesh 塞进第一个渲染器” → 只见一个小黑管 | **做法不对**：要按语义取**枪身**（`WPN_*` 或最大的非配件零件）|
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
