# 03 · 角色

## 角色的做法：产 YSM 文本几何（交给 DCM 加载）

游戏里的角色是**骨骼 + 蒙皮**（`SkinnedMeshRenderer`，资产里带 `m_BindPose` / `m_BoneNameHashes` / `m_BonesAABB`）：

```
export --class SkinnedMeshRenderer --field "m_GameObject.#name" --rows 5
→ 73 row(s)；例：Player_Duck_Head / WPN_AHBow / Cone …

search --class GameObject --pattern "CharacterModel"
→ 147 match(es)；例：0_CharacterModel_Custom_Killa / _Tagilla / _Boss_Alex / _Enemy_SnowMan …
```

我们的做法是产 **YSM 模型**（源自 Minecraft Bedrock 几何格式）：**骨骼树 + 方块**，纯 JSON 文本；蒙皮与动画由 DCM 运行库处理。

```json
{"format_version": "1.12.0",
 "minecraft:geometry": [{
   "bones": [
     {"name": "root", "pivot": [0, 0, 0]},
     {"name": "body", "parent": "waist", "pivot": [0, 24, 0],
      "cubes": [{"origin": [-4, 12, -2], "size": [8, 12, 4], "uv": [16, 16]}]},
     {"name": "head", "parent": "body", "pivot": [0, 24, 0],
      "cubes": [{"origin": [-4, 24, -4], "size": [8, 8, 8], "uv": [0, 0]}]},
     {"name": "rightItem", "parent": "rightArm", "pivot": [-6, 15, 1],
      "locators": {"lead_hold": [-6, 15, 1]}}
   ],
   "description": {"identifier": "geometry.humanoid.custom", "texture_width": 64, "texture_height": 64}
 }]}
```

| 字段 | 作用 |
|---|---|
| `bones[].name` / `parent` / `pivot` / `rotation` | 骨骼树与关节 —— **动画作用在这里** |
| `bones[].cubes[]`：`origin` / `size` / `uv` / `inflate` | 方块几何 + 贴图 UV |
| `bones[].locators` | **挂点**（手持物 / 饰品挂上去用） |
| `description.texture_width` / `texture_height` | 贴图尺寸 |

放置路径、目标类型（`built-in:Character` / `Pet` / `AICharacter_*`）与侧车配置（`Scale` / `LocatorMappings` / `LocatorOffsets`）见 [`05-community.md`](05-community.md)。

## 做什么 / 怎么做

| 做什么 | 怎么做 |
|---|---|
| **换整体模型**（玩家 / NPC / 宠物）| 产 YSM 文本几何 → 放进 `ModConfigs/DuckovCustomModel/Models` + 侧车 `<名>.ysm.duckov.json`；需玩家先装 DCM + HarmonyLib |
| **挂饰 / 附件**（帽子、背包挂件、武器挂件）| 在 YSM 里加一个 bone + cube，或用 `locators` 挂点 |
| **换贴图 / 换材质（改色）** | 改 YSM 引用的贴图，或克隆原材质后改色 |

## 从游戏里抄真实结构（提取命令）

```
action=search  class=GameObject          pattern="CharacterModel"
action=export  class=SkinnedMeshRenderer field="m_GameObject.#name"  rows=20
action=dump    class=GameObject          name="0_CharacterModel_Custom_Killa"  follow=true  depth=4
```

## 待办

- [ ] 找一份真实 `.ysm` / `ysm.json` 样例，把字段写成可复制的模板
- [ ] 确认游戏侧 `locators` 与 DCM 侧车 `LocatorMappings` 的对应关系
