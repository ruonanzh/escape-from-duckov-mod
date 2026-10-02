# 02 · 物品（背包 / 消耗品 / 装饰 / 家具）

与武器**同一套骨架**（`GameObject + Transform + CharacterSubVisuals + ItemGraphicInfo`），差别只在造型与数量级。

## 真实样例

```
search --class GameObject --pattern "IG_Backpack"   → 12 match(es)
  IG_Backpack_SBossPoison_LowDurResFire  pathID=8455
  IG_Backpack_SBossElec_ResSpace         pathID=8456
  IG_Backpack_SBossFire_ResElec          pathID=8457
  IG_Backpack_PumpkinGhost               pathID=8458
  IG_Backpack_Cube                       pathID=8459
  IG_Backpack_LV1 … LV5

search --class GameObject --pattern "IG_BaseDeco"   → IG_BaseDeco_Snow / _Halloween / _Test / …
```

导出渲染器（真实输出节选）：

```
export --class CharacterSubVisuals --field "m_GameObject.#name" --field "renderers[].#class" --rows 6
→ (空名)  IG_Backpack_LV5   MeshRenderer
   (空名)  Crown             MeshRenderer
```

## 提取命令

```
action=search  class=GameObject  pattern="IG_Backpack"     # 找某一类
action=dump    pathid=8455  follow=true  depth=4           # 看结构（Transform/渲染器/挂点）
action=dump    class=Item  match="typeID=<id>"  depth=2    # 从物品反查 itemGraphic
```

## 参数化生成要点

- **背包**：主体盒体（~0.3×0.15×0.4 m）+ 背带（细长盒体/圆柱）+ 细节（小盒体、扣具圆柱）。
- **消耗品（罐头/药剂）**：圆柱 + 顶盖圆台；贴图无关时用纯色分 submesh 区分。
- **装饰/家具**：多为静态盒体/旋转体组合，可多 submesh 分件上色。
- 结构与材质做法同 [`01-weapons.md`](01-weapons.md)；**先克隆同类物品材质**再改色，避免 URP 粉紫。

## 挂载点

- 世界/地面模型走 `Item.itemGraphic`（`ItemGraphicInfo`）那棵树；
- 挂饰/附加件挂 `ItemGraphicInfo.sockets`。

