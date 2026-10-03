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

**一句话：用 JSON 在运行时算出「几何」，替换掉原版 prefab 里的几何；prefab 的其余一切照抄原版。**

```
models/*.json（零件清单）
   ↓ MeshKit 算顶点/索引/UV          ↓ TextureKit 按 fills 画贴图
   ↓
我们拼出一个 GameObject（MeshFilter + MeshRenderer + 克隆来的材质）
   ↓ 挂进“原版 prefab 的副本”里：旧枪的几何关掉，我们的 mesh 挂上
   ↓ 写回 Item.itemGraphic（模板级）→ 游戏以后实例化的就是这份“换了几何的原版 prefab”
```

**为什么要“复制原 prefab + 换几何”，而不是从零重建**：sockets / 配件槽位（`ShowIf_*`）/ 特效节点（`MuzzleFlash`）/
组件（`ItemAgent_Gun`）/ 动画 —— 全部照抄原版，所以一上来就能跟手、能装配件、有枪口火焰。
（工坊武器 mod 的差别只在：他们的几何是 Unity/美术/AI 事先做好的；我们是**运行时用 JSON 现算**。）

### 两条必须分开处理的情况

| 情况 | 做法 |
|---|---|
| **游戏会自己重建的实例**（开局之后掉落 / 捡起 / 切枪 / 新生成）| ① **改物品模板** `ItemAssetsCollection.GetPrefab(typeID)` 的 `Item.itemGraphic` + ④ **清实体缓存** `hashedAgentsCache` → 游戏重建时就用我们的 |
| **已经拿在手里、游戏不会重建**的那个实例 | ③ **就地换几何**：把旧枪的渲染器关掉，在**原枪身零件的变换下**挂我们的 mesh（⭐ 只改渲染器，不销毁任何东西）|

⚠️ **`Item.ItemGraphic` 只有 getter** → 反射写私有字段 `itemGraphic`（社区 mod 的顺序是"先试可写属性 `ItemGraphic`，再退到字段"，更能抗更新）。

### 手里那把枪 = 四类零件（实测 MP5）

| 零件 | 是什么 | 替换时 |
|---|---|---|
| `WPN_<枪名>` | **枪身本体** | **关掉** |
| `HideIf_<槽位>` | **枪自带的默认件**（枪口 / 枪托 / 镜座 / 握把）| **关掉** —— 它们也是**这把旧枪的一部分** |
| `ShowIf_<槽位>` | **玩家装的配件物品的模型**（实测 MP5 的 `ShowIf_Scope` 用的是 M700 材质，即借来的配件网格）| **保留** —— 属于**配件那件物品** |
| `MuzzleFlash` / `Particle*` | 特效 | 保留 |

→ **新枪模型 = 枪身 + 自带件**：换上就把旧枪几何**全部关掉**（`WPN_*` + `HideIf_*`），只留配件与特效。

### 尺寸与原点

- **尺寸 = 真实米制、`scale` 恒为 1**（实测原游戏物品图形 99.5%、社区 mod 包 100% 都是 1）→ 不缩放。
- 但 prefab 内部的**缩放链不一定是 1** → 我们的 mesh 挂在**原枪身零件**的变换下，并把 `localScale` 设成
  `1 / 该零件 lossyScale`，**让它在世界尺度上保持真实尺寸**（实测：挂在根节点会被父级缩放带偏到看不见）。
- **原点 = 游戏放置点**：武器 = 握把；由模型文件的 `pivotOffset`（米）声明。

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
