# Unity 3D 资产（不依赖 Unity 的做法）

**用途**：做「改 / 加模型」的 mod 时，agent 从这里挑一个**最接近的真实样例**照着做，而不是猜。

## 结论先行：模型两条路（本目录只管**模型**；数值/行为/特效等**数据层**看 `mod-creator`）

| # | 玩家要的 | 路线 | 依赖 |
|---|---|---|---|
| **①** | **物品 / 武器 / 配件 / 建筑 / 收藏品**的外观 | **代码参数化生成**（本目录主线）：C# 运行时拼 `Mesh` | 无 |
| **②** | **角色 / 宠物 / NPC** 的外观 | **复用 DCM + YSM**（`ysm.json` 文本几何）——见 [`05-community.md`](05-community.md) | DCM + HarmonyLib |

**为什么 ① 可行**：游戏里的"模型"就是 prefab 上的 `MeshFilter` / `MeshRenderer`；`Mesh` 本身是**内存对象**（`vertices` / `triangles` / `uv` / `normals`），运行时可以直接构造。
「几何」= 一堆数字，可参数化表达（例：枪管 = 圆柱 (半径, 长度, 分段数)）。

## 怎么用（agent）

1. 判断类别（武器 / 物品 / 附件 / 角色 / 建筑…）→ 打开对应 `NN-*.md`；
2. **照着真实样例的组件结构**，用参数化模板生成 mesh（几何用代码拼，不要手抄顶点）；
3. 按该篇的「挂载点」把 mesh 装到游戏对象上；
4. 几何来源与坐标约定见 [`00-shared.md`](00-shared.md)。

## 模型 prefab 的通用结构（真实 dump，`IG_Acc_Muzzle_PST_DIS_1`）

```
GameObject  IG_Acc_Muzzle_PST_DIS_1        ← 模型 prefab 根
├─ Transform                                (位置/旋转/缩放 + m_Children)
├─ CharacterSubVisuals                      (renderers / particles / lights / mainModel)
└─ ItemGraphicInfo                          (groundPoint / sockets)   ← 物品模型入口
```

物品资产里怎么找到它：`Item.itemGraphic -> ItemGraphicInfo`（该组件的 `m_GameObject` 就是 prefab 根）。
**挂载点**：`ItemStatsSystem.ItemAgentUtilities.GetPrefab(key)` / `CreateAgent(prefab, agentType)` / `BindNewAgent(agent, agentType)`。

## 查真实样例（都用 `inspect_game_data`，不用运行游戏）

| 想查 | 命令要点 |
|---|---|
| 某类模型 prefab | `action=search class=GameObject pattern="IG_"`（物品模型）/ `"Pfb_BLD"`（建筑）|
| 一个 prefab 的完整结构 | `action=dump pathid=<id> follow=true depth=4` |
| 批量导组件/名字 | `action=export class=CharacterSubVisuals field=["m_GameObject.#name","renderers[].#class"] rows=20` |
| 某物品的模型入口 | `action=dump class=Item match="typeID=<id>" depth=2` → 看 `itemGraphic -> pathID` |

## 类别索引

| 文件 | 覆盖 | 模型是否可参数化 |
|---|---|---|
| [`00-shared.md`](00-shared.md) | 几何来源、坐标/单位约定、通用挂载 | — |
| [`01-weapons.md`](01-weapons.md) | 枪械 / 近战 / **配件**（瞄具、握把、枪口） | ✅ 适合 |
| [`02-items.md`](02-items.md) | 背包 / 消耗品 / 装饰 / 家具类物品模型 | ✅ 适合 |
| [`03-characters.md`](03-characters.md) | 角色模型（`0_CharacterModel_Custom_*`） | ⚠️ 参数化不适合（骨骼/蒙皮）→ **走 DCM + YSM** |
| [`04-buildings.md`](04-buildings.md) | 建筑 / 场景物件（`Pfb_BLD_*`） | ✅ 适合（静态几何）|
| [`05-community.md`](05-community.md) | **角色/宠物/NPC 的社区方案**：DCM + YSM（文本几何、带动画、需前置），以及模型 mod 的 bundle 现实 | — |

> ⚠️ 本目录是**参考与配方**：真实样例是 dump 出来的事实；参数化模板属骨架，**首次实机验证前不要当成"已验证可用"**。
