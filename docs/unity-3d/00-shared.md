# 00 · 通用：几何来源、坐标约定、挂载

## 1. 几何从哪来（唯一来源：参数化代码）

几何 = **数字**：顶点位置 + 三角形索引 + UV + 法线（+ 切线/颜色）。一律**由代码按参数算出**（先用基本体拼再组合）：

| 基本体 | 参数 | 典型用途 |
|---|---|---|
| 盒体 Box | 长/宽/高 | 枪身、枪托、背包主体、墙体 |
| 圆柱 Cylinder | 半径/高/分段 | 枪管、消音器、把手、柱子 |
| 圆锥 / 圆台 Cone | 上下半径/高 | 枪口、灯罩、塔尖 |
| 球 / 半球 Sphere | 半径 | 关节、按钮、灯 |
| 旋转体 Lathe | 轮廓点数组 | 弹匣、瓶子、装饰件 |
| 拉伸 Extrude | 2D 轮廓 + 厚度 | 枪机、标牌、把手 |

组合方式：每个基本体产出 (vertices, triangles, uv, normals)，再各自 `Matrix4x4` 平移/旋转/缩放后拼成一张 mesh（多 submesh 可分材质）。

**量级感（真实数据）**：游戏里 902 个 mesh 的顶点数**中位数 402**（平均 729，最大 17,594）—— 简单道具几千顶点就够贴合这款游戏的画风。

## 2. 坐标 / 单位约定

- 单位 = Unity 米；物品模型普遍在 **0.1–1 m** 量级（枪 ~0.5–1 m，配件 ~0.05–0.2 m）。
- **`scale` 恒为 1、运行时不缩放**：实测原游戏物品图形 99.5% 是 1、社区 mod 包 100% 是 1 → 模型按真实尺寸做。
- **原点 = 游戏放置点**：手持 = 手握住的地方（武器 = 握把）；物品/掉落 = 底部中心（游戏按 `groundPoint` 贴地）。
- 模型 prefab 根的 Transform 常是单位变换（`m_LocalScale = 1`），**朝向由游戏侧决定**；生成时**居中 + 枪口朝 +Z、上方向 +Y**最省事。
- 需要贴合手持/地面时，看 prefab 里的 **`groundPoint`**（`ItemGraphicInfo.groundPoint`）与 **`sockets`**（挂点）。

## 3. 材质

- 游戏是 **URP**（Managed 里 `EPOURP.dll` / `Unity.RenderPipelines.Universal.*`）→ 材质要用 URP shader，否则**粉紫**。
- 最稳的做法：**克隆游戏里同类物品的材质**再改颜色/贴图（省掉 shader 找不到的坑）。
- 无贴图时用**纯色**（先跑通链路，再谈贴图）。

## 4. 挂载（把生成的 mesh 装上去）

| 目标 | 做法 |
|---|---|
| 物品的世界/地面模型 | 取 `Item.itemGraphic`（`ItemGraphicInfo`）→ 其 `m_GameObject` 树里找 `MeshFilter`/`MeshRenderer` → 换 `sharedMesh` / `material`|
| 手持实体 | `ItemAgentUtilities.GetPrefab(key)` → `CreateAgent(prefab, agentType)` → `BindNewAgent(agent, agentType)` |
| 配件/挂饰 | 找 `ItemGraphicInfo.sockets` 里的 socket Transform，把自己生成的物件挂上去 |

## 5. 挂点（sockets）

挂点是模型上的**命名 Transform**：游戏按名字找它来放东西。实测清单（括号里是游戏里出现的次数）：

| 挂点 | 放什么 |
|---|---|
| `ArmorSocket`(55) · `HelmatSocket`(55) · `BackpackSocket`(53) | 角色身上的护甲 / 头盔 / 背包 |
| `RightHandSocket`(55) · `LeftHandSocket`(28) · `Hand.Soket.L/R`(28) | 手上拿的东西（武器 / 手持物）|
| `MeleeWeaponSocket`(64) · `MeleeWeaponSocketFixed`(28) | 近战武器 |
| `FaceMaskSocket`(28) · `HairSocket`(28) · `MouthSocket`(28) · `FaceSocket`(4) | 面罩 / 头发 / 嘴部 |
| `FootRSocket`(28) · `FootLSocket`(28) | 脚部 |
| `TailSocket`(28) | 尾巴 |
| `PopTextSocket`(59) | 伤害数字飘字 |
| `VehicleSocket`(5) | 载具 |
| `Sockets`(111) | 上面这些挂点的**容器节点**，它本身不是挂点 |

### 两种「挂」（都是**增加**，不动原来的）

| 场景 | 怎么做 |
|---|---|
| 角色身上的装备 / 饰品 | **游戏自己挂**：装了什么就把对应的 `IG_*` 挂到对应 socket（实测见 [`02-items.md`](02-items.md) 的「地面模型与身上模型」）|
| 我们生成的几何 / 挂件 | 找到 socket 的 `Transform` → 把我们的物体 `SetParent` 上去（局部位置/旋转/缩放归零）。运行时取挂点：`ItemGraphicInfo.sockets`（模型自带挂点列表）、`DuckovItemAgent.GetSocket(name, createNew)` / `AddSocket(transform)`；YSM 模型用 `bones[].locators` |

⚠️ **命名有变体**：游戏里既写 `Socket` 也写 **`Soket`**（如 `Hand.Soket.L`）——按名字找挂点时两种都要试（实测踩过）。

### 武器配件：**不是「挂」上去的**

武器配件的外观是**武器模型里自带的条件零件**，由游戏按「你装了哪些配件」开关：

| 命名 | 含义 | 实测数量 |
|---|---|---|
| `ShowIf_<槽位>` | 装了该槽位配件时**显示**的模型（如瞄具本身）| `ShowIf_Tec` 38 · `ShowIf_Scope` 34 · `ShowIf_Grip` 21 · `ShowIf_Stock` 4 |
| `HideIf_<槽位>` | 装了该槽位配件时**隐藏**的「原装件」| `HideIf_Muzzle` 36 · `HideIf_Stock` 28 · `HideIf_Scope` 14 · `HideIf_Grip` 7 |

→ 换武器模型时**只动枪身**（`WPN_*`），这些条件零件**一律不碰**（见 [`02-items.md`](02-items.md) 的「运行时：怎么换物品模型」）。

## 6. 分工（谁负责什么）

- **本目录负责**：静态几何 —— 顶点 / 索引 / UV / 法线 / 切线 + submesh，以及它在游戏对象上的挂载。
- **动画**：角色的骨骼动画由**游戏自己的 Animator**驱动 —— 我们的运行时库把几何**按骨骼名挂到游戏骨骼**上，运动就跟着走（见 [`03-characters.md`](03-characters.md)）。
- **其它渲染要素**（顶点动画 / 形态键 `m_Shapes`、LOD 组、碰撞体烘焙 `m_BakedConvexCollisionMesh`）：沿用游戏侧现成 prefab 与组件提供的。
