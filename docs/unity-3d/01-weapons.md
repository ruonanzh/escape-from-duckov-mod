# 01 · 武器（枪械 / 近战 / 配件）

## 游戏怎么分类（`Item.tags`，实测）

| 类 | 标签 | 数量 | 说明 |
|---|---|---|---|
| 枪械 | `Gun` | 124 | 100% 属于 `Weapon` |
| 正式武器 | `Weapon` | **158** | = `Gun` 124 + 正经近战 34（**不含**配件与工具）|
| 近战 | `MeleeWeapon` | 48 | 含 14 件工具（它们**没有** `Weapon` 标签）|
| **配件** | **`Accessory`** + 子类：`Muzzle` 67 · `Stock` 51 · `Magazine` 53 · `Scope` 31 · `Grip` 23，再加适配枪型的 `GunType_*`（如 `GunType_SMG`）| — | **是物品，不是武器** —— 标签里**没有** `Weapon` |
| 子弹 | `Bullet` | 145 | 独立类目 |

**配件怎么装到枪上**：配件物品带 `ItemSetting_Accessory` 组件；枪上有 **slot**（`ItemStatsSystem.Items.Slot`，用 `key` 标识、`requireTags`/`excludeTags` 筛能装什么），插进去后由 `AccessoryBase.socketName` 决定挂到**枪模型上的 socket**。

**槽位就 5 个**（实测）：`Scope`(48) · `Tec`(39) · `Muzzle`(36) · `Stock`(32) · `Grip`(28)。
它们在武器模型里体现为**条件零件**，由游戏按「装了哪些配件」开关：

| 命名 | 含义 |
|---|---|
| `ShowIf_<槽位>` | 装了该槽位配件时**显示**的模型（配件本身；实测 MP5 的 `ShowIf_Scope` 用的是 M700 的材质，即借来的配件网格）|
| `HideIf_<槽位>` | 装了该槽位配件时**隐藏**的「原装件」—— **它属于这把枪本身** |

→ 换枪模型时：`WPN_*`（枪身）+ `HideIf_*`（自带件）**全关掉**，`ShowIf_*` 与特效保留（见本文「运行时：怎么把新枪模型装进游戏」）。

**配件装在哪 = 我们自己声明**（模型文件的 `slots` 字段，米、模型自身坐标系）：

```
"slots": {
  "Scope":  [0, 0.005, -0.03],   // 机匣顶部导轨
  "Muzzle": [0, -0.035, 0.23],   // 枪管口
  "Stock":  null                  // null = 这把枪没有该挂点 → 隐藏对应的 ShowIf_
}
```
- `slots` **可选**；每项可为 `null`；**不同枪槽位集合不同**（UZI ≠ MP5），只写有的那几个即可。
- 运行时把 `ShowIf_<槽位>` 零件挪到声明的位置（只改位置，保留它自己的旋转/缩放）。

**配件的模型**：就是它自己的 `itemGraphic`（命名规律 `IG_Acc_<类型>_<名字>`，例：消音器 `Item_Muzzle_PST_DIS_1` → `IG_Acc_Muzzle_PST_DIS_1`）。
（`ItemSetting_Accessory.accessoryPfb` 这个字段在 262 个资产里**全是 null** —— 不是模型来源。）

**装在枪上的样子不是「挂 prefab」**：武器模型里自带**条件零件**，由游戏按「装了哪些配件」开关 —— 实测 `ShowIf_Tec` 38 / `HideIf_Muzzle` 36 / `ShowIf_Scope` 34 …。两类含义不同：

- `ShowIf_<槽位>` = **配件物品的模型**（装了才显示；实测 MP5 的 `ShowIf_Scope` 借用了 M700 的材质）；
- `HideIf_<槽位>` = **枪自带的默认件**（装了该槽位配件就隐藏）—— **它属于这把枪本身**。

→ 换枪模型时：`WPN_*` + `HideIf_*`（旧枪的全部几何）都要关掉，`ShowIf_*` 与特效保留。

## 样例（用 `inspect_game_data` 从游戏里读出的原文）

### A. 一个真实物品模型 prefab 的完整结构

`Item.itemGraphic -> pathID 77253`（`ItemGraphicInfo`）→ `m_GameObject -> pathID 14367`：

```
=== GameObject IG_Acc_Muzzle_PST_DIS_1 (classID 1, pathID 14367) ===
  m_Component
    [ref] Transform            (pathID 36989)  m_LocalPosition = (0,0,0)  m_LocalScale = (1,1,1)  m_Children = []
    [ref] CharacterSubVisuals  (pathID 84491)  renderers[] / particles[] / lights[] / mainModel
    [ref] ItemGraphicInfo      (pathID 77253)  groundPoint -> pathID 46681   sockets[]   fallbackSprite = null
  m_Name = IG_Acc_Muzzle_PST_DIS_1
```

→ **武器配件 = GameObject + Transform + CharacterSubVisuals(渲染器集合) + ItemGraphicInfo(挂点)**。这就是要照着造的骨架。

### B. 规模：物品模型 prefab 有多少

```
search --class GameObject --pattern "IG_"     → 374 match(es)
  IG_Acc_Grip_ALL_REC_2 / IG_Acc_Muzzle_PST_DIS_1 / IG_Acc_Sight_ALL_REC_1 / …
```

命名规律：`IG_Acc_<类型>_<名字>`（`Acc` = 配件；另有 `Backpack` / `BaseDeco` 等其它类别，见各自文档）。

### C. 渲染器在哪（批量导）

```
export --class CharacterSubVisuals \
  --field "m_GameObject.#name" --field "renderers[].#class" --rows 6
→ 433 行；例：IG_Acc_Muzzle_PST_DIS_1 / IG_Acc_Grip_ALL_REC_2 …  renderers[].#class = MeshRenderer
```

即：**模型的实际渲染器是 `CharacterSubVisuals.renderers` 里的 `MeshRenderer`**（数组，元素为 PPtr）。

## 提取命令（做新类时照抄，改 pattern）

```
# 1) 找一类模型的 prefab
action=search  class=GameObject  pattern="IG_Acc"
# 2) 看某个 prefab 的完整结构
action=dump    pathid=<上面给的 id>  follow=true  depth=4
# 3) 从物品反查它的模型 prefab
action=dump    class=Item  match="typeID=<物品 id>"  depth=2      # 看 itemGraphic -> pathID
action=dump    pathid=<那个 ItemGraphicInfo 的 id>  follow=true  # 拿到 m_GameObject
# 4) 批量导渲染器/名字
action=export  class=CharacterSubVisuals  field=["m_GameObject.#name","renderers[].#class"]  rows=50
```

## 参数化生成要点

- **枪管/消音器**：圆柱（半径 0.01–0.03 m，长 0.1–0.4 m，分段 12–24），沿 +Z。
- **枪身/枪托**：盒体（0.05×0.1×0.3 m 量级）+ 倒角（用多条不同尺寸盒体叠加代替倒角）。
- **弹匣**：旋转体或斜置盒体，插在枪身下方。
- **瞄具/握把/枪口**：更小的圆柱/盒体组合，挂在 `sockets` 上（不要自己猜位置）。
- 组合：每个基本体算完顶点后 `Matrix4x4.TRS(位置, 旋转, 缩放)` 变换再拼接；一张 mesh 顶多加几个 submesh。
- **材质**：克隆同类物品的材质改色（避免 URP shader 找不到 → 粉紫）。

## 挂载点


| 目标 | 做法 |
|---|---|
| 世界/地面显示 | 换 `ItemGraphicInfo` 那棵树里 `MeshRenderer.sharedMesh` |
| 手持显示 | `ItemAgentUtilities.GetPrefab/CreateAgent/BindNewAgent` |
| 配件 | 挂到 `ItemGraphicInfo.sockets` 的 socket Transform |


## 运行时：怎么把新枪模型装进游戏

**一句话：用 JSON 在运行时算出「几何」，替换掉原枪 prefab 里的几何；prefab 的其余一切照抄原版**
（sockets、配件槽位、特效节点 `MuzzleFlash`、组件 `ItemAgent_Gun`、动画）→ 所以新枪一上来就能跟手、能装配件、有枪口火焰。

```
models/*.json（零件清单）
   ↓ MeshKit 算顶点/索引/UV        ↓ TextureKit 按 fills 画贴图
   ↓
拼出一个 GameObject（MeshFilter + MeshRenderer + 克隆枪身材质）
   ↓ 挂进“原枪 prefab 的副本”里：旧枪几何关掉，我们的 mesh 挂上
   ↓ 写回 Item.itemGraphic（模板级）→ 游戏以后实例化的就是这份“换了几何的原枪 prefab”
```

### 两条必须分开处理的情况

| 情况 | 做法 |
|---|---|
| 游戏**会自己重建**的实例（掉落 / 捡起 / 切枪 / 新生成）| ① **改物品模板** `ItemAssetsCollection.GetPrefab(typeID)` 的 `Item.itemGraphic` + ④ **清实体缓存** `hashedAgentsCache` → 游戏重建时就用我们的 |
| **已经拿在手里**（游戏不会重建）的那个 | ③ **就地换几何**：关掉旧枪渲染器，在**原枪身零件的变换下**挂我们的 mesh（只改渲染器，**不销毁任何东西**）|

⚠️ `Item.ItemGraphic` 只有 getter → 反射写私有字段 `itemGraphic`（社区顺序：先试可写属性 `ItemGraphic`，再退字段）。

### 枪的几何 = 四类零件

| 零件 | 是什么 | 替换时 |
|---|---|---|
| `WPN_<枪名>` | **枪身本体** | **关掉** |
| `HideIf_<槽位>` | **枪自带的默认件**（枪口 / 枪托 / 镜座 / 握把）| **关掉** —— 也是这把旧枪的几何 |
| `ShowIf_<槽位>` | **配件物品的模型**（装了才显示；实测 MP5 的 `ShowIf_Scope` 用的是 M700 材质）| **保留** |
| `MuzzleFlash` / `Particle*` | 特效 | 保留 |

**配件装在哪**由模型文件的 `slots` 声明（上位「配件怎么装到枪上」一节）：`ShowIf_<槽位>` 零件会被挪到声明的挂点。

### 尺寸与原点

- **尺寸 = 真实米制、`scale` 恒为 1**（实测：原游戏物品图形 99.5% 是 1、社区 mod 包 100% 是 1）→ 不缩放。
- ⚠️ 但 prefab 内部的**缩放链不一定是 1** —— 实测（沿父链相乘得到"世界缩放"）：

  | 样本 | 世界缩放 = 1 | 非 1 |
  |---|---|---|
  | 原游戏枪身 `WPN_*`（99）| 67（68%）| **32（32%）**：`WPN_M14` 1.039（本地=1，**父级带缩放**）· `WPN_Minotaur` 1.225 · `WPN_ASVAL_Lightsaber` 15.5 |
  | 工坊武器 mod 的包（优香MPX 26）| **100%** | 0 |

  → mesh 要挂在**原枪身零件**的变换下，并把 `localScale` 取 `1 / 该零件 lossyScale`，让它在**世界尺度上是真实尺寸**。
  （挂在根节点而不补缩放，会被父链带偏 —— 实测表现是**缩小到看不见**。）
- **原点 = 握把**（手握住的地方），由模型文件的 `pivotOffset`（米）声明。

### 坑（都实测踩过）

| 现象 | 原因 → 做法 |
|---|---|
| 武器**选不中 / 用不了** | 用 `ItemAgentUtilities.CreateAgent` 去"替换"活实体会**销毁**它，游戏引用失效 → **就地换几何**，别销毁 |
| 改完开局还是原版 | 改 prefab **不会重建已经拿在手里的实例** → 那个实例要**就地换** |
| 新模型看不见 / 巨大 | 挂在了 prefab 里缩放不为 1 的节点下没补缩放，或把"某个小零件"当锚点 → 用**原枪身零件**当锚点 + 补 `1/lossyScale` |
| 配件消失 / 只剩几个 | 把 `ShowIf_*`（配件模型）或 `HideIf_*` 处理错 → `WPN_*`+`HideIf_*` 关掉、`ShowIf_*` 保留 |
| 找不到"原版是不是 1 倍" | 数一数：原游戏物品图形 99.5% 是 `scale=1`、社区包 100% → **按真实尺寸建模，不要缩放去凑** |

## 例子（照着写）

坐标与单位约定见 [`00-shared.md`](00-shared.md)，格式见 [`05-model-format.md`](05-model-format.md)。

### 紧凑手枪（`pistol_compact`）
**288 顶点 / 132 三角面** · 包围盒 **0.03 × 0.159 × 0.203 m** · 贴图 512²（自动密度 1024 px/m）

```json
{
  "name": "pistol_compact",
  "category": "weapons/pistol",
  "summary": "紧凑手枪：滑套 + 枪管 + 枪身 + 握把 + 扳机护圈 + 弹匣底 + 前后准星",
  "parts": [
    { "role": "slide", "shape": "box", "size": [0.03, 0.032, 0.17], "at": [0, 0.03, 0.005] },
    { "role": "barrel", "shape": "cylinder", "r": 0.008, "h": 0.075, "segments": 12, "at": [0, 0.03, 0.085], "rot": [90, 0, 0] },
    { "role": "frame", "shape": "box", "size": [0.028, 0.022, 0.135], "at": [0, 0.006, 0.0] },
    { "role": "grip", "shape": "box", "size": [0.026, 0.095, 0.038], "at": [0, -0.048, -0.045], "rot": [-12, 0, 0] },
    { "role": "guard", "shape": "box", "size": [0.008, 0.02, 0.032], "at": [0, -0.02, -0.022] },
    { "role": "magazine", "shape": "box", "size": [0.028, 0.01, 0.042], "at": [0, -0.098, -0.054], "rot": [-12, 0, 0] },
    { "role": "sight_front", "shape": "box", "size": [0.004, 0.006, 0.006], "at": [0, 0.049, 0.078] },
    { "role": "sight_rear", "shape": "box", "size": [0.02, 0.006, 0.008], "at": [0, 0.049, -0.055] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "slide": "#3A3F44", "barrel": "#22262A", "frame": "#33383D", "grip": "#4A4038", "guard": "#2C3136", "magazine": "#2E3236", "sight_front": "#1E2124", "sight_rear": "#1E2124" } },
  "attach": {"kind": "item_graphic"}
}
```

### 紧凑冲锋枪（`smg_compact`）
**240 顶点 / 108 三角面** · 包围盒 **0.06 × 0.241 × 0.57 m** · 贴图 512²（自动密度 678 px/m）

```json
{
  "name": "smg_compact",
  "category": "weapons/smg",
  "summary": "紧凑冲锋枪：机匣 + 枪管 + 折叠托 + 握把 + 弹匣 + 照门",
  "parts": [
    { "role": "receiver", "shape": "box", "size": [0.06, 0.09, 0.3], "at": [0, 0.02, 0.0] },
    { "role": "barrel", "shape": "cylinder", "r": 0.01, "h": 0.14, "segments": 12, "at": [0, 0.035, 0.2], "rot": [90, 0, 0] },
    { "role": "stock", "shape": "box", "size": [0.04, 0.06, 0.16], "at": [0, 0.0, -0.22] },
    { "role": "grip", "shape": "box", "size": [0.03, 0.1, 0.04], "at": [0, -0.07, -0.05], "rot": [-10, 0, 0] },
    { "role": "magazine", "shape": "box", "size": [0.025, 0.14, 0.04], "at": [0, -0.09, 0.06], "rot": [-6, 0, 0] },
    { "role": "sight_rear", "shape": "box", "size": [0.03, 0.008, 0.01], "at": [0, 0.075, -0.06] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "receiver": "#3A3F44", "barrel": "#22262A", "stock": "#3E3833", "grip": "#4A4038", "magazine": "#2E3236", "sight_rear": "#1E2124" } },
  "attach": {"kind": "item_graphic"}
}
```

### 突击步枪（`rifle_assault`）
**456 顶点 / 192 三角面** · 包围盒 **0.055 × 0.308 × 0.965 m** · 贴图 512²（自动密度 526 px/m）

```json
{
  "name": "rifle_assault",
  "category": "weapons/rifle",
  "summary": "突击步枪：机匣 + 长枪管 + 消焰器 + 枪托 + 握把 + 弹匣 + 光学瞄具",
  "parts": [
    { "role": "receiver", "shape": "box", "size": [0.055, 0.08, 0.36], "at": [0, 0.02, 0.0] },
    { "role": "barrel", "shape": "cylinder", "r": 0.009, "h": 0.34, "segments": 12, "at": [0, 0.03, 0.35], "rot": [90, 0, 0] },
    { "role": "muzzle", "shape": "cylinder", "r": 0.019, "h": 0.09, "segments": 12, "at": [0, 0.03, 0.56], "rot": [90, 0, 0] },
    { "role": "stock", "shape": "box", "size": [0.045, 0.07, 0.2], "at": [0, 0.0, -0.26] },
    { "role": "grip", "shape": "box", "size": [0.03, 0.1, 0.045], "at": [0, -0.075, -0.06], "rot": [-10, 0, 0] },
    { "role": "magazine", "shape": "box", "size": [0.03, 0.16, 0.05], "at": [0, -0.1, 0.05], "rot": [-8, 0, 0] },
    { "role": "scope", "shape": "cylinder", "r": 0.02, "h": 0.22, "segments": 12, "at": [0, 0.105, -0.02], "rot": [90, 0, 0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "receiver": "#3A3F44", "barrel": "#22262A", "muzzle": "#1E2124", "stock": "#3E3833", "grip": "#4A4038", "magazine": "#2E3236", "scope": "#1A1D20" } },
  "attach": {"kind": "item_graphic"}
}
```

### 消音器（`suppressor`）
**280 顶点 / 112 三角面** · 包围盒 **0.038 × 0.038 × 0.195 m** · 贴图 512²（自动密度 1024 px/m）

```json
{
  "name": "suppressor",
  "category": "accessories/muzzle",
  "summary": "消音器：筒身 + 两端接环",
  "parts": [
    { "role": "body", "shape": "cylinder", "r": 0.019, "h": 0.18, "segments": 16, "at": [0, 0, 0.09], "rot": [90, 0, 0] },
    { "role": "thread", "shape": "cylinder", "r": 0.011, "h": 0.03, "segments": 12, "at": [0, 0, 0.0], "rot": [90, 0, 0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "body": "#1E2124", "thread": "#565B61" } },
  "attach": {"kind": "socket", "name": "MuzzleSocket"}
}
```

### 铁锤（`hammer_melee`）
**224 顶点 / 92 三角面** · 包围盒 **0.1 × 0.375 × 0.26 m** · 贴图 512²（自动密度 713 px/m）

```json
{
  "name": "hammer_melee",
  "category": "weapons/melee",
  "summary": "铁锤：锤头 + 木柄 + 柄尾",
  "parts": [
    { "role": "head", "shape": "box", "size": [0.1, 0.1, 0.26], "at": [0, 0.32, 0.0] },
    { "role": "handle", "shape": "cylinder", "r": 0.016, "h": 0.3, "segments": 10, "at": [0, 0.15, 0.0] },
    { "role": "butt", "shape": "cylinder", "r": 0.02, "h": 0.03, "segments": 10, "at": [0, 0.01, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "head": "#6E7276", "handle": "#8A6A3F", "butt": "#6B5330" } },
  "attach": {"kind": "item_graphic"}
}
```
