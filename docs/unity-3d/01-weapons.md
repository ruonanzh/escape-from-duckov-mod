# 01 · 武器（枪械 / 近战 / 配件）

## 分类（`Item.tags`）

| 类 | 标签 | 说明 |
|---|---|---|
| 枪械 | `Gun` | 124 件；全部属于 `Weapon` |
| 正式武器 | `Weapon` | 158 件 = 枪 124 + 正经近战 34 |
| 近战 | `MeleeWeapon` | 48 件（含 14 件工具，它们**没有** `Weapon` 标签）|
| **配件** | `Accessory` + 子类 `Muzzle` / `Stock` / `Magazine` / `Scope` / `Grip` + `GunType_*` | **是物品，不是武器**（标签里没有 `Weapon`）|
| 子弹 | `Bullet` | 145 件，独立类目 |

配件装到枪上：枪上有**槽位**（`SlotCollection.list[].key`，配 `requireTags`/`excludeTags` 筛能装什么），
插进去后由**槽位 key** 决定挂到枪模型上的哪个挂点。

## 槽位与挂点

### `Sockets/<槽位>` 才是挂点

武器 prefab 里有三样东西，别混：

| 对象 | 是什么 |
|---|---|
| ⭐ **`Sockets/<槽位>`** | **真挂点**：`Sockets` 容器下有 5 个子节点 `Scope` · `Tec` · `Muzzle` · `Stock` · `Grip`。装上的配件由游戏实例化后挂到这个 Transform 下（局部位置/旋转归零、缩放 1）→ **挂点在哪，配件就出现在哪** |
| `ShowIf_<槽位>` | 装上该槽位配件后**在枪上随之出现**的那段（转接座/底座那种）|
| `HideIf_<槽位>` | 被它替换掉的**原装件**（属于这把枪本身）|

- ⚠️ **别拿 `ShowIf_<槽位>` 的 Transform 当挂点**：实测 MP5 prefab 里 `ShowIf_Scope` / `ShowIf_Grip` /
  `ShowIf_Tec` / `WPN_MP5` **位置完全相同**（都在枪坐标系原点）——它们的几何是按枪的坐标系摆好的。
- **靠名字绑定**（反编译 `ItemGraphicInfo.AutoSet`）：遍历 `Sockets` 的子节点，按 `"ShowIf_" + 子节点名` /
  `"HideIf_" + 子节点名` 去绑 → **槽位名 = `Sockets` 子节点的名字**。
- **`ShowIf_*` 可以缺**（例：枪口槽位全游戏没有一个 `ShowIf_Muzzle` —— 枪口配件直接套枪管）。
- 装配件时游戏做什么（`RefreshSubGraphics`）：默认态 = `ShowIf_*` 关、`HideIf_*` 开；对**有内容**的槽位 →
  把**配件自己的模型**挂到 `socketPoint` 下，并把 `ShowIf_*` 打开、`HideIf_*` 关掉。
- **配件的模型来自它自己的 `itemGraphic`**（命名规律 `IG_Acc_<类型>_<名字>`，例：`Item_Muzzle_PST_DIS_1` →
  `IG_Acc_Muzzle_PST_DIS_1`）。

### 槽位 ≠ 挂点（弹夹就是典型）

`SlotCollection` 列的槽位**可能比 `Sockets` 子节点多**：**只有 `Sockets/<槽位>` 存在时，配件模型才会被挂上去**，
没有的槽位是"纯数值槽"。MP5 实测：

| | key |
|---|---|
| `SlotCollection.list[].key`（6 个）| `Scope` · `Muzzle` · `Grip` · `Stock` · `Tec` · **`Mag`** |
| prefab 的 `Sockets` 子节点（5 个）| 同上但**没有 `Mag`** |

**弹夹（`Mag`）**：52 件弹匣物品（tag 叫 `Magazine`，槽位 key 叫 `Mag`）**全部 `itemGraphic = null`**、
全游戏也没有 `IG_Magazine*` prefab → **换弹匣只改数值和 UI 图标，不改变枪的外观**。

> 顺带：**部分配件本身就没有模型**（`itemGraphic = null`，例：`Muzzle` 34/66、`Mag` 0/52）→
> **装了某件配件看不到变化是正常的**（游戏拿到 null 时连 `ShowIf_*`/`HideIf_*` 都不切）。

### 配件装在哪 = 把挂点摆到我们模型上（模型文件 `slots`）

```
"slots": {
  "Scope":  [0, -0.005, -0.038],   // 机匣顶部导轨（米、模型坐标系）
  "Muzzle": [0, -0.035, 0.22],     // 枪管口
  "Stock":  null                    // null = 这把枪没有这个挂点 → 槽位与占位件都关掉
}
```
- **可选**、每项可为 `null`、**不同枪槽位集合不同**（UZI ≠ MP5），只写有的那几个。
- 语义 = **槽位在我们模型上的位置**（米、模型自身坐标系，原点 = 模型原点）。
- ⭐ **多数模型不用写**：不写就自动按**语义零件**算（枪口 = `barrel` 前端、顶部 = `receiver` 顶面…，
  规则在 `MeshKit.TryGuessSlot`）；只在观感上要覆盖时才写 `slots`。
- 取值问工具（按语义零件打印、可直接抄）：
  ```
  dotnet tools/model-check/bin/Release/net8.0/model-check.dll --file models/smg_compact.json --slots
  ```
- 工具会校验**槽位名**（只认那 5 个）+ **数值离模型太远就 WARN**（超 5cm，多半填反轴/单位写错）；
  但"接在哪"是几何 + 观感的取舍，工具不替你决定。

## 提取命令（做新类时照抄，改 pattern）

```
# 1) 找一类模型的 prefab
action=search  class=GameObject  pattern="IG_Acc"
# 2) 看某个 prefab 的完整结构（Transform / 渲染器 / 挂点）
action=dump    pathid=<上面给的 id>  follow=true  depth=4
# 3) 从物品反查它的模型 prefab
action=dump    class=Item  match="typeID=<物品 id>"  depth=2      # 看 itemGraphic -> pathID
action=dump    pathid=<那个 ItemGraphicInfo 的 id>  follow=true  # 拿到 m_GameObject
# 4) 批量导名字 / 渲染器
action=export  class=CharacterSubVisuals  field=["m_GameObject.#name","renderers[].#class"]  rows=50
```

## 参数化生成要点

- ⭐ **零件的 `role` 要用语义名**（`barrel` / `receiver` / `handguard` / `stock` / `grip` / `magazine` / `sight_rear`…，
  按前缀匹配）：**自动挂点按它找面**（枪口 = `barrel` 前端）、`model-check --slots` 也靠它给建议值。
- **枪管**：圆柱（半径 0.01–0.03 m，长 0.1–0.4 m，分段 12–24）。圆柱默认**竖着**（沿 +Y），要沿 +Z 就用 `"rot": [90,0,0]`。
- **枪身/枪托**：盒体（0.05×0.1×0.3 m 量级）；倒角 = 多条不同尺寸的盒体叠出来。
- **弹匣**：斜置盒体/旋转体，做**枪身 mesh 的一部分**（游戏不给弹匣挂模型）。
- **瞄具/握把/枪口/枪托这些是"配件"**：模型属于**配件物品自己**（`IG_Acc_*`），**做枪时不用画**；
  要保证的是"枪上挂配件的位置有实体"（挂点摆哪见上文）。
- 组合：每个基本体算完顶点后按 `(位置, 旋转, 缩放)` 变换再拼接；一张 mesh 顶多加几个 submesh。
- **材质**：克隆同类物品的材质改色（避免 URP shader 找不到 → 粉紫）。

## 运行时：怎么把新枪模型装进游戏

**一句话：用 JSON 算出的几何，替换掉原枪图形 prefab 里的几何；其余一切照抄原版**，所以新枪一上来就能跟手、
能装配件、有枪口火焰。

```
models/*.json（零件清单）
   ↓ MeshKit 算顶点/索引/UV   ↓ TextureKit 按 fills 画贴图
   拼一个 GameObject（MeshFilter + MeshRenderer + 克隆枪身材质）
   ↓ 挂进"原枪 prefab 的副本"：旧枪几何关掉、我们的 mesh 挂上
   ↓ 写回 Item.itemGraphic → 游戏以后实例化的就是这份"换了几何的原枪 prefab"
```

**本质 = 借一份游戏原有的 prefab**：克隆 → 只换主体零件的几何+贴图 → 其余照抄。**不从零造 prefab。**

| 哪份 prefab | 管什么 | 我们怎么换 |
|---|---|---|
| **物品图形** `Item.itemGraphic`（`ItemGraphicInfo`）| 掉落 / 展示 / 手上 | 克隆 `ItemAssetsCollection.GetPrefab(typeID)` 里这份 → 换几何 → 反射写回（模板级）+ 清实体缓存 `hashedAgentsCache` |
| **手持实体** `ItemAgent`（`item.ActiveAgent`）| 已经拿在手里的那把 | **由图形 prefab 派生**（`ActiveAgent = IG_Gun_Mp5(Clone)(ItemAgent_Gun)`）；已持有的游戏**不会重建** → **就地换几何**（关旧枪渲染器、挂我们的 mesh，**只改渲染器、不销毁任何东西**）|

⚠️ `Item.ItemGraphic` 只有 getter → 反射写私有字段 `itemGraphic`。

**只换主体零件（`WPN_*` / 最大的非配件零件）那一个**，其余一概不碰（枪上其它零件由游戏按状态开关）：

| 零件 | 是什么 | 替换时 |
|---|---|---|
| `WPN_<枪名>` | 枪身本体 | **关掉** |
| `HideIf_<槽位>` | 枪自带的默认件（枪口/枪托/镜座/握把）| **关掉**（也是旧枪几何）|
| `ShowIf_<槽位>` | 配件的占位模型 | **保留**（跟着挂点一起平移）|
| `MuzzleFlash` / `Particle*` | 特效 | 保留 |

### 尺寸与原点

- **尺寸 = 真实米制、`scale` 恒为 1**（实测：原游戏物品图形 99.5%、社区包 100% 都是 1）→ 不缩放。
- ⚠️ prefab 内部**缩放链不一定是 1**（实测枪身 `WPN_*` 有 32% 不是 1，例 `WPN_M14` 1.039、`WPN_ASVAL_Lightsaber` 15.5）
  → mesh 挂在**原枪身零件**的变换下，`localScale` 取 `1 / 该零件 lossyScale`，保证**世界尺度**是真实尺寸。
- **原点 = 握把**（手握住的地方），由模型文件的 `pivotOffset`（米）声明。

### 坑（都实测踩过）

| 现象 | 原因 → 做法 |
|---|---|
| 武器**选不中 / 用不了** | 用 `CreateAgent` 去"替换"活实体会**销毁**它、游戏引用失效 → **就地换几何**，别销毁 |
| 改完开局还是原版 | 改 prefab **不会重建已经拿在手里的实例** → 那个实例要**就地换** |
| 新模型看不见 / 巨大 | 锚点用了缩放不为 1 的节点，或把"小零件"当锚点 → 用**原枪身零件** + 补 `1/lossyScale` |
| 配件消失 / 只剩几个 | `ShowIf_*`（配件模型）或 `HideIf_*` 处理错 → `WPN_*`+`HideIf_*` 关掉、`ShowIf_*` 保留 |

## 新增一把枪

**模型层只提供 `itemGraphic`**（数据层与模型层唯一的连接点）；物品本身的创建属于**数据层**
（新 typeID / 名字 / 数值 / `AddDynamicEntry`，见 `mod-creator` SKILL）。

```csharp
// 数据层（库 ItemFactory 内置：常驻 / 名字走本地化 / useSpriteForPickup=false / 枪自动打 IsGun）
var item = ItemFactory.CloneAsNewItem(源typeID, 新typeID, "显示名");
// 模型层：给这个新物品一份"换成我们几何"的图形
binder.WriteGraphicTo(item, binder.BuildGraphicClone(源typeID));
// 注册
ItemAssetsCollection.AddDynamicEntry(item);
```

三条坑：

- **名字必须是"键 + 本地化表"**（`LocalizationManager.SetOverrideText(key, 文本)` + `DisplayNameRaw = key`）——
  塞字面量会被显示成 `*字面量*`。
- **注册通常发生在进关卡之前**（那时还没有玩家）→ "发给玩家 / 掉到地上"要**等进关卡后再做**。
- 新物品要有**获得途径**（工作台配方 / 掉落 / 发放），否则只能在代码里造出来。

### ⭐ 我们造的运行时对象必须常驻（`DontDestroyOnLoad`）

我们走"运行时算几何"这条路 → 自己 `Instantiate` 出来的东西（**图形克隆 / 新物品模板 / 手持实体**）都是
**场景对象** → 从主菜单进关卡时场景卸载 → 被销毁 → 物品的 `itemGraphic` 变成"已销毁引用"（Unity `== null` 判 true）
→ 游戏回退成图标贴图（看着像"黑方块/原版图"）。**所以创建后立刻 `DontDestroyOnLoad`：**

```csharp
Object.DontDestroyOnLoad(clone.gameObject);   // ① 图形克隆（写进 itemGraphic 的那份）
Object.DontDestroyOnLoad(itemGo);             // ② 新物品模板
Object.DontDestroyOnLoad(agentGo);            // ③ 手持实体
```

- **判断标准是"有没有宿主"，不是"是不是我们造的"**：挂在游戏对象树下的（`SetParent` 到骨骼 / 原几何零件，
  例如角色的 YSM 方块）**不用**常驻 —— 它跟着宿主生灭；只被字段引用的（**图形克隆 / 物品模板 / 手持实体**）
  没有宿主 → **必须**常驻。
- 常驻对象还要**保持激活**（Unity `Instantiate` 会继承模板激活状态 → 模板 `SetActive(false)` 会让实例全隐形）。
- 别用 `MultiSceneCore.MoveToActiveWithScene` 保活模板（它的激活状态跟着场景走 → 别的关卡里模板变 inactive）。
- 对照：官方路线（Unity prefab + AssetBundle）里这些都是**资产**，天然不被销毁，所以官方文档不用提这件事。

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
