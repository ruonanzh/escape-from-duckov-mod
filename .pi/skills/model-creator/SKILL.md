---
name: model-creator
description: 用代码参数化生成 3D 模型并挂到 Escape From Duckov 的物品/建筑/配件上（不依赖 Unity、不打包 AssetBundle）：模型 prefab 骨架、几何来源（盒/圆柱/圆锥/旋转体/拉伸）、坐标与材质约定、按类别的真实样例句（武器/物品/角色/建筑）、挂载点（`ItemGraphicInfo` / `ItemAgentUtilities` / sockets）与验收方式。当玩家要「改模型 / 换外观 / 加个新造型 / 做个 3D 样子」，或要理解游戏里的模型是怎么拼出来时读取。
---

# 做 3D 模型（Escape From Duckov）

## 走哪条路（先明确，别默认要做 AssetBundle）

游戏里的"模型"就是 prefab 上的 `MeshFilter` / `MeshRenderer`；`Mesh` 是**内存对象**
（`vertices` / `triangles` / `uv` / `normals`），**运行时可以直接构造**。

所以本 repo 的做法是 **① 代码参数化生成**：C# 里拼几何 → 换到游戏现成对象上。**不需要 Unity、不需要 AssetBundle、不需要外部工具**。

遇到这些**直接说清楚不做**（不要硬做、不要偷偷引入 Unity）：
- **导入 FBX/GLB 等外部模型**、**自己打包 AssetBundle** → 本轮不做（要做也要用 Unity，不是这个技能的事）。
- **角色模型**：**不要自己写**——角色/宠物/NPC 换模型走社区框架（下一节），参数化只用来做**挂饰**。

## 动手前先看能不能复用（社区生态）

详见 `docs/unity-3d/05-community.md`。三句话：

| 玩家要的 | 社区现成 | 怎么办 |
|---|---|---|
| **角色 / 宠物 / NPC 换模型** | **DCM（Duckov Custom Model）+ YSM**（`ysm.json` = **文本 JSON 几何**，带动画），放进 `ModConfigs/DuckovCustomModel/Models`，**不需要 AssetBundle** | **复用**：产 YSM 文本模型 + 侧车 `<名>.ysm.duckov.json`；需 DCM + HarmonyLib 前置（玩家没装就先说清）|
| **物品/武器的数据与行为**（数值/口径/开火方式/特效/贴图）| `item.yaml` / `recipe.yaml` 声明式框架（作者明确写了“不会改模型”）| 数据交给它或我们的 C#；**几何**才归这个技能 |
| **物品/武器 模型** | 现成 mod 几乎都自带 AssetBundle（Unity 打）| 我们走 **① 参数化生成**；需要真实感模型才回落 bundle |

## 动手前先查真实样例（不要猜结构）

按类别查 `docs/unity-3d/`：`README.md`（总览 + 提取命令）、`00-shared.md`（通用约定）、
`01-weapons.md`、`02-items.md`、`03-characters.md`、`04-buildings.md`。

要现场看游戏里真实的模型结构，用 **`inspect_game_data`**（只读、不运行游戏）：

```
action=search  class=GameObject  pattern="IG_"        # 物品模型 prefab（374 个）
action=search  class=GameObject  pattern="Pfb_BLD"    # 建筑
action=dump    pathid=<id>  follow=true  depth=4      # 一个 prefab 的完整组件图
action=dump    class=Item  match="typeID=<id>"  depth=2   # 物品 → itemGraphic -> pathID
action=export  class=CharacterSubVisuals  field=["m_GameObject.#name","renderers[].#class"]  rows=20
```

通用骨架（实测）：`GameObject` + `Transform` + `CharacterSubVisuals`（`renderers` / `mainModel`）+ 物品模型再加 `ItemGraphicInfo`（`groundPoint` / `sockets`）。

## 几何怎么生成

用参数拼基本体，再按变换组合（**不要手写顶点**）：

| 基本体 | 参数 | 用途 |
|---|---|---|
| 盒体 | 长/宽/高 | 枪身、枪托、背包、墙体 |
| 圆柱 | 半径/高/分段 | 枪管、消音器、柱、管子 |
| 圆锥/圆台 | 上下半径/高 | 枪口、灯罩、塔尖 |
| 旋转体 | 轮廓点 | 弹匣、瓶子、装饰 |
| 拉伸 | 2D 轮廓+厚度 | 枪机、标牌 |

每个基本体产出顶点/索引/UV/法线 → `Matrix4x4.TRS(位置, 旋转, 缩放)` 变换后拼成一张 mesh；多部件用 **submesh** 分材质。
量级参考：游戏里一个三脚架 1431 顶点 / 45 KB —— 几千顶点足够，拼装完全可控。

**坐标/单位**：米；物品模型 0.1–1 m 量级；生成时**居中、+Z 朝前、+Y 朝上**，贴合交给游戏侧的 `groundPoint` / `sockets`。

## 材质

游戏是 **URP**（`EPOURP.dll`）→ 材质 shader 必须匹配，否则**粉紫**。
**不要从零建材质**：克隆同类物品已有的材质，再改颜色/贴图。无贴图时用纯色先跑通。

## 挂载（把 mesh 装上去）

| 目标 | 做法 |
|---|---|
| 物品的世界/地面模型 | 取 `Item.itemGraphic`（`ItemGraphicInfo`）→ 其 `m_GameObject` 树里的 `MeshRenderer.sharedMesh` / `material` |
| 手持实体 | `ItemStatsSystem.ItemAgentUtilities.GetPrefab(key)` → `CreateAgent(prefab, agentType)` → `BindNewAgent(agent, agentType)` |
| 配件/挂饰 | 挂到 `ItemGraphicInfo.sockets` 里的 socket `Transform` |

## 验收

1. `validate_mod` 编译通过 → `install_mod` 装进游戏；
2. **进游戏看**：模型出现、位置/朝向正确、材质不粉紫、手持/地面显示正常；
3. 位置或比例不对时，回到坐标约定（居中/+Z）与 `groundPoint` / `sockets` 调整 —— 不要靠猜着挪。

## 参考

- 详细资料与真实样例：`docs/unity-3d/`（入口 `README.md`）。
- mod 的整体流程（csproj / info.ini / ModBehaviour / 校验）：`mod-creator`。
- 查游戏数据（数值/场景）：`mod-creator` 的「获取游戏数据」一节。
- 装进游戏：`mod-installer`。
