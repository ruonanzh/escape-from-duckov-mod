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


## 例子（照着写）

坐标与单位约定见 [`00-shared.md`](00-shared.md)，格式见 [`05-model-format.md`](05-model-format.md)。

### 紧凑手枪（`pistol_compact`）
**218 顶点 / 132 三角面** · 包围盒 **0.030 × 0.159 × 0.203 m**

```json
{
  "name": "pistol_compact",
  "category": "weapons/pistol",
  "summary": "紧凑手枪：滑套 + 枪管 + 枪身 + 握把 + 扳机护圈 + 弹匣底 + 前后准星",
  "parts": [
    {
      "role": "slide",
      "shape": "box",
      "size": [
        0.03,
        0.032,
        0.17
      ],
      "at": [
        0,
        0.03,
        0.005
      ]
    },
    {
      "role": "barrel",
      "shape": "cylinder",
      "r": 0.008,
      "h": 0.075,
      "segments": 12,
      "at": [
        0,
        0.03,
        0.085
      ],
      "rot": [
        90,
        0,
        0
      ]
    },
    {
      "role": "frame",
      "shape": "box",
      "size": [
        0.028,
        0.022,
        0.135
      ],
      "at": [
        0,
        0.006,
        0.0
      ]
    },
    {
      "role": "grip",
      "shape": "box",
      "size": [
        0.026,
        0.095,
        0.038
      ],
      "at": [
        0,
        -0.048,
        -0.045
      ],
      "rot": [
        -12,
        0,
        0
      ]
    },
    {
      "role": "guard",
      "shape": "box",
      "size": [
        0.008,
        0.02,
        0.032
      ],
      "at": [
        0,
        -0.02,
        -0.022
      ]
    },
    {
      "role": "magazine",
      "shape": "box",
      "size": [
        0.028,
        0.01,
        0.042
      ],
      "at": [
        0,
        -0.098,
        -0.054
      ],
      "rot": [
        -12,
        0,
        0
      ]
    },
    {
      "role": "sight_front",
      "shape": "box",
      "size": [
        0.004,
        0.006,
        0.006
      ],
      "at": [
        0,
        0.049,
        0.078
      ]
    },
    {
      "role": "sight_rear",
      "shape": "box",
      "size": [
        0.02,
        0.006,
        0.008
      ],
      "at": [
        0,
        0.049,
        -0.055
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

### 紧凑冲锋枪（`smg_compact`）
**170 顶点 / 108 三角面** · 包围盒 **0.060 × 0.241 × 0.570 m**

```json
{
  "name": "smg_compact",
  "category": "weapons/smg",
  "summary": "紧凑冲锋枪：机匣 + 枪管 + 折叠托 + 握把 + 弹匣 + 照门",
  "parts": [
    {
      "role": "receiver",
      "shape": "box",
      "size": [
        0.06,
        0.09,
        0.3
      ],
      "at": [
        0,
        0.02,
        0.0
      ]
    },
    {
      "role": "barrel",
      "shape": "cylinder",
      "r": 0.01,
      "h": 0.14,
      "segments": 12,
      "at": [
        0,
        0.035,
        0.2
      ],
      "rot": [
        90,
        0,
        0
      ]
    },
    {
      "role": "stock",
      "shape": "box",
      "size": [
        0.04,
        0.06,
        0.16
      ],
      "at": [
        0,
        0.0,
        -0.22
      ]
    },
    {
      "role": "grip",
      "shape": "box",
      "size": [
        0.03,
        0.1,
        0.04
      ],
      "at": [
        0,
        -0.07,
        -0.05
      ],
      "rot": [
        -10,
        0,
        0
      ]
    },
    {
      "role": "magazine",
      "shape": "box",
      "size": [
        0.025,
        0.14,
        0.04
      ],
      "at": [
        0,
        -0.09,
        0.06
      ],
      "rot": [
        -6,
        0,
        0
      ]
    },
    {
      "role": "sight_rear",
      "shape": "box",
      "size": [
        0.03,
        0.008,
        0.01
      ],
      "at": [
        0,
        0.075,
        -0.06
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

### 突击步枪（`rifle_assault`）
**246 顶点 / 192 三角面** · 包围盒 **0.055 × 0.308 × 0.965 m**

```json
{
  "name": "rifle_assault",
  "category": "weapons/rifle",
  "summary": "突击步枪：机匣 + 长枪管 + 消焰器 + 枪托 + 握把 + 弹匣 + 光学瞄具",
  "parts": [
    {
      "role": "receiver",
      "shape": "box",
      "size": [
        0.055,
        0.08,
        0.36
      ],
      "at": [
        0,
        0.02,
        0.0
      ]
    },
    {
      "role": "barrel",
      "shape": "cylinder",
      "r": 0.009,
      "h": 0.34,
      "segments": 12,
      "at": [
        0,
        0.03,
        0.35
      ],
      "rot": [
        90,
        0,
        0
      ]
    },
    {
      "role": "muzzle",
      "shape": "cylinder",
      "r": 0.019,
      "h": 0.09,
      "segments": 12,
      "at": [
        0,
        0.03,
        0.56
      ],
      "rot": [
        90,
        0,
        0
      ]
    },
    {
      "role": "stock",
      "shape": "box",
      "size": [
        0.045,
        0.07,
        0.2
      ],
      "at": [
        0,
        0.0,
        -0.26
      ]
    },
    {
      "role": "grip",
      "shape": "box",
      "size": [
        0.03,
        0.1,
        0.045
      ],
      "at": [
        0,
        -0.075,
        -0.06
      ],
      "rot": [
        -10,
        0,
        0
      ]
    },
    {
      "role": "magazine",
      "shape": "box",
      "size": [
        0.03,
        0.16,
        0.05
      ],
      "at": [
        0,
        -0.1,
        0.05
      ],
      "rot": [
        -8,
        0,
        0
      ]
    },
    {
      "role": "scope",
      "shape": "cylinder",
      "r": 0.02,
      "h": 0.22,
      "segments": 12,
      "at": [
        0,
        0.105,
        -0.02
      ],
      "rot": [
        90,
        0,
        0
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

### 消音器（`suppressor`）
**116 顶点 / 112 三角面** · 包围盒 **0.038 × 0.038 × 0.195 m**

```json
{
  "name": "suppressor",
  "category": "weapons/muzzle",
  "summary": "消音器：筒身 + 两端接环",
  "parts": [
    {
      "role": "body",
      "shape": "cylinder",
      "r": 0.019,
      "h": 0.18,
      "segments": 16,
      "at": [
        0,
        0,
        0.09
      ],
      "rot": [
        90,
        0,
        0
      ]
    },
    {
      "role": "thread",
      "shape": "cylinder",
      "r": 0.011,
      "h": 0.03,
      "segments": 12,
      "at": [
        0,
        0,
        0.0
      ],
      "rot": [
        90,
        0,
        0
      ]
    }
  ],
  "material": {
    "mode": "clone",
    "pick": "body"
  },
  "attach": {
    "kind": "socket",
    "name": "MuzzleSocket"
  }
}
```

### 铁锤（`hammer_melee`）
**108 顶点 / 92 三角面** · 包围盒 **0.100 × 0.375 × 0.260 m**

```json
{
  "name": "hammer_melee",
  "category": "weapons/melee",
  "summary": "铁锤：锤头 + 木柄 + 柄尾",
  "parts": [
    {
      "role": "head",
      "shape": "box",
      "size": [
        0.1,
        0.1,
        0.26
      ],
      "at": [
        0,
        0.32,
        0.0
      ]
    },
    {
      "role": "handle",
      "shape": "cylinder",
      "r": 0.016,
      "h": 0.3,
      "segments": 10,
      "at": [
        0,
        0.15,
        0.0
      ]
    },
    {
      "role": "butt",
      "shape": "cylinder",
      "r": 0.02,
      "h": 0.03,
      "segments": 10,
      "at": [
        0,
        0.01,
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
