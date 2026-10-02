# 04 · 建筑 / 场景物件

**静态几何，最适合参数化生成**（无骨骼、无动画）。

## 真实样例

```
search --class GameObject --pattern "Bld"   → 2 match(es)
  Pfb_BLD_Showcase  pathID=18315
  Pfb_BLD_ATM       pathID=19488
```

（命名规律：`Pfb_BLD_<名字>`；相关资产还有 `BuildingManager`(GameObject 6611)、`BuildingGridBlocked`(Material 359)、`BuildingRequired`(GameObject 系列)。）

## 提取命令

```
action=search  class=GameObject  pattern="Pfb_BLD"          # 找建筑 prefab
action=dump    pathID=19488  follow=true  depth=4           # 看结构
action=export  class=CharacterSubVisuals --field "m_GameObject.#name" --field "renderers[].#class" --rows 20
action=search  class=GameObject  pattern="Building"          # 相关对象（放置/需求/管理器）
```

## 参数化生成要点

- **墙 / 地板 / 台阶**：盒体直接拼；重复结构用循环批量生成（一栋楼几十个盒体，几千顶点，量级完全可控）。
- **柱 / 管道 / 路灯**：圆柱 + 圆台。
- **屋顶 / 装饰**：旋转体 + 盒体。
- **碰撞**：先不做（游戏侧可能按 prefab 现成碰撞体走）；若需要，后续再补。
- 多部件用 **submesh 分材质**（墙/窗/门用不同色），仍然只克隆游戏现有 URP 材质改色。

## 挂载点

- 建筑 prefab 的模型树同样是 `GameObject + Transform + CharacterSubVisuals(renderers)`；
- 换模型 = 换该树里 `MeshRenderer.sharedMesh`；
- 建造/放置逻辑（`BuildingDataCollection.GetPrefab`）**不动**。

## 待办

- [ ] `Pfb_BLD_ATM` 的完整 dump 摘录（层级 + 渲染器数 + 大致尺寸）
- [ ] 建筑 prefab 与 `BuildingDataCollection` 的绑定关系（哪条数据指向这个 prefab）
