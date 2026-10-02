# 01 · 武器（枪械 / 近战 / 配件）

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
  IG_Acc_Grip_ALL_REC_2 / IG_Backpack_LV1 / IG_BaseDeco_Snow / …
```

命名规律：`IG_<类别>_<名字>`（`Acc` 配件、`Backpack` 背包、`BaseDeco` 基地装饰…）。

### C. 渲染器在哪（批量导）

```
export --class CharacterSubVisuals \
  --field "m_GameObject.#name" --field "renderers[].#class" --rows 6
→ 433 行；例：IG_Backpack_LV5 / Crown …  renderers[].#class = MeshRenderer
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

