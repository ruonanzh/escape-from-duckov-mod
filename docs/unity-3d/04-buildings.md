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

