# 00 · 通用：几何来源、坐标约定、挂载

## 1. 几何从哪来（唯一来源：参数化代码）

几何 = **数字**：顶点位置 + 三角形索引 + UV + 法线（+ 切线/颜色）。**不要手抄顶点**；用参数拼基本体再组合：

| 基本体 | 参数 | 典型用途 |
|---|---|---|
| 盒体 Box | 长/宽/高 | 枪身、枪托、背包主体、墙体 |
| 圆柱 Cylinder | 半径/高/分段 | 枪管、消音器、把手、柱子 |
| 圆锥 / 圆台 Cone | 上下半径/高 | 枪口、灯罩、塔尖 |
| 球 / 半球 Sphere | 半径 | 关节、按钮、灯 |
| 旋转体 Lathe | 轮廓点数组 | 弹匣、瓶子、装饰件 |
| 拉伸 Extrude | 2D 轮廓 + 厚度 | 枪机、标牌、把手 |

组合方式：每个基本体产出 (vertices, triangles, uv, normals)，再各自 `Matrix4x4` 平移/旋转/缩放后拼成一张 mesh（多 submesh 可分材质）。

**量级感（真实数据）**：游戏里一个三脚架 `SM_Studio_Tripod_2` = **1431 顶点**，顶点字节 **45,792 B**（存在 `resources.assets.resS`，offset 1136188592）。
→ 简单道具几千顶点足够；「参数化拼装」完全够用，手写顶点不现实。

## 2. 坐标 / 单位约定

- 单位 = Unity 米；物品模型普遍在 **0.1–1 m** 量级（枪 ~0.5–1 m，配件 ~0.05–0.2 m）。
- 模型 prefab 根的 Transform 常是单位变换（`m_LocalScale = 1`），**朝向由游戏侧决定**；生成时**居中 + 枪口朝 +Z、上方向 +Y**最省事。
- 需要贴合手持/地面时，看 prefab 里的 **`groundPoint`**（`ItemGraphicInfo.groundPoint`）与 **`sockets`**（挂点）。

## 3. 材质

- 游戏是 **URP**（Managed 里 `EPOURP.dll` / `Unity.RenderPipelines.Universal.*`）→ 材质要用 URP shader，否则**粉紫**。
- 最稳的做法不是从零建材质，而是**克隆游戏里同类物品的材质**再改颜色/贴图（可省掉 shader 找不到的坑）。
- 无贴图时用**纯色**（先跑通链路，再谈贴图）。

## 4. 挂载（把生成的 mesh 装上去）

| 目标 | 做法 |
|---|---|
| 物品的世界/地面模型 | 取 `Item.itemGraphic`（`ItemGraphicInfo`）→ 其 `m_GameObject` 树里找 `MeshFilter`/`MeshRenderer` → 换 `sharedMesh` / `material`|
| 手持实体 | `ItemAgentUtilities.GetPrefab(key)` → `CreateAgent(prefab, agentType)` → `BindNewAgent(agent, agentType)` |
| 配件/挂饰 | 找 `ItemGraphicInfo.sockets` 里的 socket Transform，把自己生成的物件挂上去 |

## 5. 明确不做

- **骨骼/蒙皮动画**（`SkinnedMeshRenderer` + `m_BoneNameHashes`）：参数化生成做不到，见 [`03-characters.md`](03-characters.md)。
- 顶点动画 / 形态键（`m_Shapes`）、LOD 组、碰撞体烘焙（`m_BakedConvexCollisionMesh`）。
