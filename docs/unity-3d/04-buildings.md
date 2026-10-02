# 04 · 建筑 / 场景物件

**静态几何，最适合参数化生成**（无骨骼、无动画）。

## 真实样例

```
search --class GameObject --pattern "Bld"   → 2 match(es)
  Pfb_BLD_Showcase  pathID=18315
  Pfb_BLD_ATM       pathID=19488
```

命名规律：`Pfb_BLD_<名字>`。`Pfb_BLD_ATM` 根节点只有 **`Transform` + `MeshFilter` + `MeshRenderer`**（碰撞体在子物体上）。

## 碰撞与占位

`Duckov.Buildings.Building`（用 `inspect_game_api` decompile 读出的原文）：

```csharp
internal void SetupPreview()
{
    functionContainer.SetActive(false);
    Collider[] componentsInChildren = graphicsContainer.GetComponentsInChildren<Collider>();
    for (int i = 0; i < componentsInChildren.Length; i++)
        componentsInChildren[i].enabled = false;
}

private void CreateAreaMesh()
{
    if (areaMesh == null)
    {
        areaMesh = Object.Instantiate(GameplayDataSettings.Prefabs.BuildingBlockAreaMesh, transform);
        areaMesh.transform.localScale = new Vector3((float)dimensions.x - 0.02f, 1f, (float)dimensions.y - 0.02f);
        areaMesh.transform.SetParent(functionContainer.transform, worldPositionStays: true);
    }
}
```

| 事实 | 做模型时的含义 |
|---|---|
| prefab 分 **`graphicsContainer`（视觉）** 与 **`functionContainer`（功能）** 两个容器 | 模型挂到 `graphicsContainer` 下 |
| **碰撞体就在 `graphicsContainer` 子树里**（放置预览时被临时禁用）| **换模型要一起提供碰撞体**（或沿用现行 prefab 的）；例：`Interact_ATM` 上有一个 `BoxCollider`（pathID 52286）|
| **放置合法性/占位靠网格**：`BuildingManager.GetOccupyingCoords(dimensions, rotation, coord)` | **不要改 `dimensions`** —— 改了占位与 areaMesh 会错位 |
| `areaMesh` = `buildingBlockAreaMesh` 按 `dimensions` 缩放后的实例 | 区域指示由游戏生成，模型不用管 |

游戏里碰撞体总量（`resources.assets`）：`BoxCollider` 556 / `MeshCollider` 107 / `SphereCollider` 57 / `CapsuleCollider` 91。

## 提取命令

```
action=search  class=GameObject  pattern="Pfb_BLD"          # 找建筑 prefab
action=dump    pathID=19488  follow=true  depth=5           # 看结构（含子物体与碰撞体）
action=export  class=BoxCollider  field="m_GameObject.#name" --rows 20     # 看碰撞体挂在谁身上
action=search  class=GameObject  pattern="Building"          # 相关对象（放置/需求/管理器）
```

绑定关系：`BuildingInfo.prefabName` → `BuildingDataCollection.GetPrefab(prefabName)`（`info.prefabName` 就是 `Pfb_BLD_*`）。

## 参数化生成要点

- **墙 / 地板 / 台阶**：盒体直接拼；重复结构用循环批量生成（一栋楼几十个盒体，几千顶点，量级完全可控）。
- **柱 / 管道 / 路灯**：圆柱 + 圆台。
- **屋顶 / 装饰**：旋转体 + 盒体。
- **碰撞体**：随模型一起给（盒体为主；不规则形状用 `MeshCollider`）。
- 多部件用 **submesh 分材质**（墙/窗/门不同色），材质仍用「克隆游戏现有 URP 材质改色」的做法。


## 例子（照着写）

### 墙（`wall`）
**72 顶点 / 36 三角面** · 包围盒 **3.060 × 2.680 × 0.240 m** · 贴图 512²（自动密度 77 px/m）

```json
{
  "name": "wall",
  "category": "buildings/structure",
  "summary": "墙：墙体 + 上下压条（3 m 长）",
  "parts": [
    { "role": "body", "shape": "box", "size": [3.0, 2.6, 0.2], "at": [0, 1.3, 0.0] },
    { "role": "trim_top", "shape": "box", "size": [3.06, 0.08, 0.24], "at": [0, 2.64, 0.0] },
    { "role": "trim_bottom", "shape": "box", "size": [3.06, 0.1, 0.24], "at": [0, 0.05, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "body": "#1E2124", "trim_top": "#A8A8A2", "trim_bottom": "#8E8E88" } },
  "attach": {"kind": "building"}
}
```

### 地板（`floor`）
**120 顶点 / 60 三角面** · 包围盒 **3.060 × 0.120 × 3.060 m** · 贴图 512²（自动密度 42 px/m）

```json
{
  "name": "floor",
  "category": "buildings/structure",
  "summary": "地板：面板 + 四边包边（mirror 两轴，只写两条）",
  "parts": [
    { "role": "panel", "shape": "box", "size": [3.0, 0.12, 3.0], "at": [0, -0.06, 0.0] },
    { "role": "edge", "shape": "box", "size": [3.06, 0.06, 0.06], "at": [0, -0.03, 1.5], "mirror": "z" },
    { "role": "edge_side", "shape": "box", "size": [0.06, 0.06, 3.06], "at": [1.5, -0.03, 0.0], "mirror": "x" }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "panel": "#8A8A86", "edge": "#9A9A96", "edge_side": "#9A9A96" } },
  "attach": {"kind": "building"}
}
```

### 柱子（`pillar`）
**98 顶点 / 72 三角面** · 包围盒 **0.400 × 2.900 × 0.400 m** · 贴图 512²（自动密度 143 px/m）

```json
{
  "name": "pillar",
  "category": "buildings/structure",
  "summary": "柱子：柱身 + 柱础 + 柱帽",
  "parts": [
    { "role": "shaft", "shape": "cylinder", "r": 0.15, "h": 2.8, "segments": 12, "at": [0, 1.4, 0.0] },
    { "role": "base", "shape": "box", "size": [0.4, 0.12, 0.4], "at": [0, 0.06, 0.0] },
    { "role": "cap", "shape": "box", "size": [0.36, 0.1, 0.36], "at": [0, 2.85, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "shaft": "#A5A5A0", "base": "#7E8288", "cap": "#B0B0AA" } },
  "attach": {"kind": "building"}
}
```

### 自助机器（`vending_machine`）
**96 顶点 / 48 三角面** · 包围盒 **1.240 × 2.200 × 1.065 m** · 贴图 512²（自动密度 84 px/m）

```json
{
  "name": "vending_machine",
  "category": "buildings/interactable",
  "summary": "自助机器：柜体 + 面板屏 + 顶部灯箱 + 底座（可交互物件同 ATM/售货机）",
  "parts": [
    { "role": "body", "shape": "box", "size": [1.2, 1.8, 1.0], "at": [0, 0.9, 0.0] },
    { "role": "panel", "shape": "box", "size": [1.0, 0.6, 0.05], "at": [0, 1.35, -0.52] },
    { "role": "sign", "shape": "box", "size": [1.24, 0.4, 1.04], "at": [0, 2.0, 0.0] },
    { "role": "base", "shape": "box", "size": [1.24, 0.1, 1.04], "at": [0, 0.05, 0.0] }
  ],
  "material": {"mode": "clone", "pick": "body"},
  "texture": { "size": [512, 512], "source": "generated", "fills": { "body": "#1E2124", "panel": "#8A8A86", "sign": "#C8D0D8", "base": "#7E8288" } },
  "attach": {"kind": "building"}
}
```
