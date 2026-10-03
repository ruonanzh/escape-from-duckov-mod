# 02 · 物品（背包 / 消耗品 / 装饰 / 家具）

与武器**同一套骨架**（`GameObject + Transform + CharacterSubVisuals + ItemGraphicInfo`），差别只在造型与数量级。

**范围**：只讲**有 3D 图形的物品**（即有 `itemGraphic` 的那些）；做模型 = 换掉这份图形里的几何与贴图。

## 提取命令

```
action=search  class=GameObject  pattern="IG_Backpack"     # 找某一类（IG_BaseDeco / IG_Armor / …）
action=dump    pathid=<id>  follow=true  depth=4           # 看结构（Transform / 渲染器 / 挂点）
action=dump    class=Item  match="typeID=<id>"  depth=2    # 从物品反查 itemGraphic
```

## 参数化生成要点

- **背包**：主体盒体（实测 `Backpack_LV1` 全尺寸 **0.44 × 0.38 × 0.30 m**）+ 背带（细长盒体/圆柱）+ 细节（小盒体、扣具）。
- **消耗品（罐头/药剂）**：圆柱 + 顶盖圆台；贴图无关时用纯色分 submesh 区分。
- **装饰/家具**：静态盒体/旋转体组合，多 submesh 分件上色。
- 结构参考同类的真实物品（`IG_Backpack_*` / `IG_BaseDeco_*` …）；**先克隆同类物品的材质**再改色，避免 URP 粉紫。

## 一个物品只需要一份模型

游戏把**同一个图形 prefab** 挂到**不同父节点**，不是两套模型（入口是
`ItemGraphicInfo.CreateAGraphic(item, parent, snapGround)` —— 参数就是父节点）：

| 场景 | 挂到哪 |
|---|---|
| 掉落 / 摆在地上 | 世界（`ItemGraphicInfo.groundPoint` 决定怎么贴地）|
| 穿在身上 | 角色的 socket：`ArmorSocket`（护甲）· `HelmatSocket`（头盔）· `BackpackSocket`（背包）|
| 拿在手上 | `ItemAgentUtilities` 绑到 `ItemAgent` |

**子物品 / 挂饰**（容器里的东西）：`ItemGraphicInfo.sockets` 是"槽位名 → 三项"的表 ——
`socketPoint`（挂点 Transform）· `showIfPluged` · `hideIfPluged`；游戏按 `slot.Key` 查表后把**子物品自己的图形**
挂到 `socketPoint` 下（局部位置/旋转归零、缩放 1）→ **挂点在哪，子物品就出现在哪**。

→ **一个物品做一份模型就够了**；`attach` 只决定默认挂哪。

## 运行时：怎么换物品模型（背包 / 护甲 / 消耗品 / 容器…）

**一句话：用 JSON 在运行时算出几何，替换掉原版 prefab 里的几何；其余一切照抄原版。**

```
models/*.json（零件清单）
   ↓ MeshKit 算顶点/索引/UV   ↓ TextureKit 按 fills 画贴图
   拼一个 GameObject（MeshFilter + MeshRenderer + 克隆来的材质）
   ↓ 挂进"原版 prefab 的副本"里：旧几何关掉、我们的 mesh 挂上
   ↓ 写回 Item.itemGraphic → 游戏以后实例化的就是这份"换了几何的原版 prefab"
```

**本质 = 借一份游戏原有的 prefab**：克隆 → 只换主体零件的几何+贴图 → 其余照抄（`groundPoint` / `sockets` /
`subGraphics` / 挂到角色 socket 的整套关系都照抄）→ 物品一上来就能正确落地、能当装备挂到身上。**不从零造 prefab。**
（工坊 mod 的差别：它们的几何是事先做好的，我们是**运行时用 JSON 现算**。）

⚠️ **只换主体零件那一个**（背包 = 包围盒最大的那个渲染器），其余零件一概不碰 —— 物品的模型也是一组零件，
除主体外都由游戏管。

### 两处都要换

| 在哪 | 是什么 | 怎么换 |
|---|---|---|
| **掉落 / 展示** | `ItemGraphicInfo`（游戏用 `CreateAGraphic(item.ItemGraphic, …)` 实例化）| **克隆 `item.ItemGraphic`** → 换掉克隆里的几何 → 反射写回 `Item.itemGraphic` |
| **穿在身上 / 拿在手上** | 游戏自己把物品的 `IG_*` 挂到角色 socket | 同上：换了 `itemGraphic`，游戏挂上去的就是我们的 |

### 两步走（都在库 `ItemModelBinder` 里）

1. **改物品模板**：`ItemAssetsCollection.GetPrefab(typeID)` 的 `Item.itemGraphic` → 之后**新生成**的实例
   （掉落/捡起/开局）天生就用我们的几何；同时**清实体缓存** `hashedAgentsCache` 让游戏重建时读到我们的。
2. **已经存在的实例**（游戏不会重建）→ **就地**把旧的 `MeshRenderer` 关掉、把我们的 mesh 挂进原几何的变换下
   （**只改渲染器，不销毁任何东西**）。
   ⚠️ `Item.ItemGraphic` 只有 getter → 反射写私有字段 `itemGraphic`。

### 尺寸与原点

- **尺寸 = 真实米制、`scale` 恒为 1**（实测原游戏物品图形 `IG_*` 99% 是 1、社区包 100% 是 1）→ 不缩放。
- ⚠️ prefab 内部**缩放链不一定是 1** → mesh 挂在**原几何零件**的变换下，`localScale` 取 `1 / 该零件 lossyScale`，
  保证**世界尺度**是真实尺寸（挂在根节点不补缩放会缩到看不见）。
- **原点 = 放置点**：物品按"底部中心"做最省事（游戏用 `groundPoint` 贴地）；由 `pivotOffset`（米）声明。

## 坑

| 现象 | 原因 → 做法 |
|---|---|
| 克隆出的模板 `SetActive(false)` → 实例**全隐形** | Unity `Instantiate` 会继承激活状态 → 模板保持激活 |
| 用 `CreateAgent` 换实体 → 武器**选不中 / 用不了** | 游戏持有旧实体引用 → **就地换几何**，别销毁 |
| "关掉全部渲染器" → 不该藏的零件被藏了 | 物品的模型也是一组零件，**除主体外都由游戏管**，只能动主体 |
| "把 mesh 塞进第一个渲染器" → 只见一个很小的碎片 | 锚点要按语义取**主体零件** |
| 尺寸算不对 | 模型里可能混着几米大的**特效零件**（实测包围盒 8 m）→ 算尺寸只按**主体零件** |

## 可运行的例子

`reference/item_swap/`（**含 README**：工程结构 / `config.json` 字段 / build+装+热重载 / 日志位置；
配置改完保存即生效）。
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
