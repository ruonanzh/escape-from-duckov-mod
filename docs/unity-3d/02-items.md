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

- **背包**：主体盒体（实测 `Backpack_LV1` 全尺寸 **0.44 × 0.38 × 0.30 m**）+ 背带（细长盒体/圆柱）+ 细节（小盒体、扣具圆柱）。
- **消耗品（罐头/药剂）**：圆柱 + 顶盖圆台；贴图无关时用纯色分 submesh 区分。
- **装饰/家具**：多为静态盒体/旋转体组合，可多 submesh 分件上色。
- 结构与材质做法同 [`01-weapons.md`](01-weapons.md)；**先克隆同类物品材质**再改色，避免 URP 粉紫。

## 挂载点

- 世界/地面模型走 `Item.itemGraphic`（`ItemGraphicInfo`）那棵树；
- 挂饰/附加件挂 `ItemGraphicInfo.sockets`。


## 例子（照着写）

### 背包（`backpack`）
**168 顶点 / 84 三角面** · 包围盒 **0.400 × 0.405 × 0.325 m**（含 `mirror` 展开后的数量）

```json
{
  "name": "backpack",
  "category": "items/backpack",
  "summary": "背包：主体 + 上盖 + 双肩带 + 扣具（肩带用 mirror 只写一边）",
  "parts": [
    {
      "role": "body",
      "shape": "box",
      "size": [
        0.4,
        0.34,
        0.26
      ],
      "at": [
        0,
        0.0,
        0.0
      ]
    },
    {
      "role": "flap",
      "shape": "box",
      "size": [
        0.4,
        0.06,
        0.26
      ],
      "at": [
        0,
        0.2,
        0.0
      ]
    },
    {
      "role": "strap",
      "shape": "box",
      "size": [
        0.06,
        0.3,
        0.03
      ],
      "at": [
        0.14,
        0.05,
        -0.14
      ],
      "rot": [
        -8,
        0,
        0
      ],
      "mirror": "x"
    },
    {
      "role": "buckle",
      "shape": "box",
      "size": [
        0.05,
        0.04,
        0.02
      ],
      "at": [
        0.14,
        -0.04,
        -0.15
      ],
      "mirror": "x"
    },
    {
      "role": "pouch",
      "shape": "box",
      "size": [
        0.18,
        0.15,
        0.07
      ],
      "at": [
        0.0,
        -0.1,
        -0.16
      ]
    }
  ],
  "material": {
    "mode": "clone",
    "pick": "body"
  },
  "attach": {
    "kind": "item_graphic"
  }
}
```

### 罐头（`can_food`）
**174 顶点 / 168 三角面** · 包围盒 **0.070 × 0.118 × 0.068 m**

```json
{
  "name": "can_food",
  "category": "items/food",
  "summary": "罐头：罐身 + 顶盖 + 底环",
  "parts": [
    {
      "role": "body",
      "shape": "cylinder",
      "r": 0.035,
      "h": 0.1,
      "segments": 14,
      "at": [
        0,
        0.0,
        0.0
      ]
    },
    {
      "role": "lid",
      "shape": "cylinder",
      "r": 0.033,
      "h": 0.01,
      "segments": 14,
      "at": [
        0,
        0.055,
        0.0
      ]
    },
    {
      "role": "base",
      "shape": "cylinder",
      "r": 0.033,
      "h": 0.008,
      "segments": 14,
      "at": [
        0,
        -0.054,
        0.0
      ]
    }
  ],
  "material": {
    "mode": "clone",
    "pick": "body"
  },
  "attach": {
    "kind": "item_graphic"
  }
}
```

### 手办（`figurine`）
**210 顶点 / 136 三角面** · 包围盒 **0.160 × 0.360 × 0.110 m**

```json
{
  "name": "figurine",
  "category": "items/collectible",
  "summary": "手办：底座 + 腿 + 躯干 + 双臂（mirror）+ 头 + 头发",
  "parts": [
    {
      "role": "base",
      "shape": "cylinder",
      "r": 0.05,
      "h": 0.012,
      "segments": 16,
      "at": [
        0,
        0.006,
        0.0
      ]
    },
    {
      "role": "legs",
      "shape": "box",
      "size": [
        0.07,
        0.1,
        0.045
      ],
      "at": [
        0,
        0.062,
        0.0
      ]
    },
    {
      "role": "torso",
      "shape": "box",
      "size": [
        0.1,
        0.12,
        0.06
      ],
      "at": [
        0,
        0.172,
        0.0
      ]
    },
    {
      "role": "arm",
      "shape": "box",
      "size": [
        0.03,
        0.1,
        0.04
      ],
      "at": [
        0.065,
        0.172,
        0.0
      ],
      "mirror": "x"
    },
    {
      "role": "head",
      "shape": "box",
      "size": [
        0.1,
        0.1,
        0.1
      ],
      "at": [
        0,
        0.282,
        0.0
      ]
    },
    {
      "role": "hair",
      "shape": "box",
      "size": [
        0.11,
        0.04,
        0.11
      ],
      "at": [
        0,
        0.34,
        0.0
      ]
    }
  ],
  "material": {
    "mode": "clone",
    "pick": "body"
  },
  "attach": {
    "kind": "item_graphic"
  }
}
```

### 储物箱（`storage_crate`）
**120 顶点 / 60 三角面** · 包围盒 **0.520 × 0.340 × 0.370 m**

```json
{
  "name": "storage_crate",
  "category": "items/container",
  "summary": "储物箱：箱体 + 盖 + 两道包边（mirror）+ 锁扣",
  "parts": [
    {
      "role": "body",
      "shape": "box",
      "size": [
        0.5,
        0.3,
        0.35
      ],
      "at": [
        0,
        0.0,
        0.0
      ]
    },
    {
      "role": "lid",
      "shape": "box",
      "size": [
        0.51,
        0.04,
        0.36
      ],
      "at": [
        0,
        0.17,
        0.0
      ]
    },
    {
      "role": "band",
      "shape": "box",
      "size": [
        0.52,
        0.03,
        0.02
      ],
      "at": [
        0,
        0.0,
        0.09
      ],
      "mirror": "z"
    },
    {
      "role": "lock",
      "shape": "box",
      "size": [
        0.06,
        0.05,
        0.02
      ],
      "at": [
        0,
        0.12,
        -0.18
      ]
    }
  ],
  "material": {
    "mode": "clone",
    "pick": "body"
  },
  "attach": {
    "kind": "item_graphic"
  }
}
```
