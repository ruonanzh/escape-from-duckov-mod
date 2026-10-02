# 03 · 角色（玩家 / NPC / 宠物）

## 做法：YSM 文本模型 + 我们自己的运行时库

- **格式 = YSM**（源自 Minecraft Bedrock 几何）：**骨骼树 + 方块**，纯 JSON 文本。
- **加载/挂载 = 我们自己的运行时库**（做法参考社区方案 DCM，但**不依赖它** —— 玩家不需要装任何第三方前置）。
- **P1 范围**：静态替换角色外观 + **跟随游戏动画**（不自研动画系统）。
- **P2 范围**：挂点 / 配件。自制动画系统与游戏内模型管理界面**不在本阶段**。

```json
{"format_version": "1.12.0",
 "minecraft:geometry": [{
   "bones": [
     {"name": "Root", "pivot": [0, 0, 0]},
     {"name": "Pelvis", "parent": "Root", "pivot": [0, 12, 0],
      "cubes": [{"origin": [-4, 12, -2], "size": [8, 12, 4], "uv": [16, 16]}]},
     {"name": "Head", "parent": "Spine.004", "pivot": [0, 24, 0],
      "cubes": [{"origin": [-4, 24, -4], "size": [8, 8, 8], "uv": [0, 0]}]}
   ],
   "description": {"identifier": "geometry.duck.custom", "texture_width": 64, "texture_height": 64}
 }]}
```

| 字段 | 作用 |
|---|---|
| `bones[].name` / `parent` / `pivot` / `rotation` | 骨骼树与关节 —— 动画作用在这里 |
| `bones[].cubes[]`：`origin` / `size` / `uv` / `inflate` | 方块几何 + 贴图 UV |
| `bones[].locators` | 挂点（手持物 / 饰品） |
| `description.texture_width` / `texture_height` | 贴图尺寸 |

## 已实机验证（2026-10-02 spike：运行时把方块挂到 `Hand.R`）

| 验证点 | 结果 | 证据（日志原文） |
|---|---|---|
| **按骨骼名挂载** | ✅ | `bone 'Hand.R' = Spine.001/…/ForeArm.R/Hand.R` |
| **跟随动画**（核心）| ✅ | 方块局部坐标恒为 `(0,0,0)`；骨骼世界坐标与朝向随走动/挥手持续变化（`(-4.6,0.45,-85.0)` → `(-9.1,0.56,-64.4)`）|
| **渲染层要跟渲染器** | ✅ ⚠️ | 角色渲染器在 **layer 9 / 15**，模型根在 `layer 0`；相机 `cullingMask=13631455` **渲染 9、不渲染 0** → 自建物件必须放到**角色渲染器那一层**（第一版放 0 层，看不见）|
| **材质克隆现成** | ✅ | 身体材质 `Skin`，shader = **`SodaCraft/SodaCharacter`**（自定义 URP shader）→ 自建材质必然找不到，**克隆现成材质**才不粉紫 |
| **socket 备用路径** | ✅ | `CharacterModel.rightHandSocket` 是 **private**；反射可取（`…/Hand.R/Hand.Soket.R/RightHandSocket`）|
| **游戏自己也这么挂** | ✅ | 模型层级里现成挂着 `IG_Helmat_Storm_Lv5(Clone)`→`HelmatSocket`、`IG_Armor_Storm_Lv5(Clone)`→`ArmorSocket`、`IG_Backpack_LV5(Clone)`→`BackpackSocket`、`MeleeWeaponAgent_Knife_04_Karambit(Clone)`→`MeleeWeaponSocket` |

**角色模型运行时结构**（实测）：

```
Character(Clone)/ModelRoot/0_CharacterModel_Custom_Template(Clone)/CustomFaceInstance/DuckBody   ← SkinnedMeshRenderer
  ├─ Armature / Root / Pelvis / Spine.001–004 / Head / Duck_Beak / Duck_Eye.L/R
  ├─ UpperArm.L→Elbow.L→ForeArm.L→Hand.L→Hand.Soket.L→LeftHandSocket
  ├─ UpperArm.R→Elbow.R→ForeArm.R→Hand.R→Hand.Soket.R→RightHandSocket
  ├─ Wings.L/R · ArmorSocket · BackpackSocket · HelmatSocket · FaceMaskSocket · HairSocket · MouthSocket
  └─ 装备以 (Clone) 形式挂在对应 socket 下
```

## 游戏里的骨骼家族（映射基础，实测）

导出命令（拿真实骨骼名）：

```
action=export  class=SkinnedMeshRenderer \
  --field "m_GameObject.#name" --field "m_Bones[].m_GameObject.#name" --out /tmp/smr.tsv
```

实测：**72 个蒙皮网格 → 20 种不同骨骼集合**，但主力只有几个家族（按网格数排）：

| 家族 | 网格数 | 骨骼数 | 骨骼命名（节选） | 代表 |
|---|---|---|---|---|
| **NPC 鸭子** | **28** | 24 | `Root;Pelvis;Spine.001–004;Head;UpperArm.L;Elbow.L;ForeArm.L;Hand.L;Hand.Soket.L;Thigh.R/L;Foot;Tail` | `DuckBody` |
| **玩家鸭子** | **14** | **37** | `Root;Hip;Spine.001–003;Head;HairTip;Arm.Root/Uper/Fore.R;Hand.R;Finger.*;Leg.Upper/Lower/Foot;Tail.001/002` | `Player_Duck_Head` |
| **蜘蛛 / 机械腿** | 7 | 20 | `Root;Bottom;Body;Gun;Leg_1…3_F/B_L/R;Leg_Target_*` | `Leg_3_B_L` |
| 兽类（狼/兔/鸟）| 4+ | 9–12 | `root;body;ear.L/R;tail.01/02;leg.F/B.L/R;(wing.L/R)` | `Mesh_LOD1` |
| 载具 / 马 | 2 | 19 | `dian;Spine;L_qian_tui;…;Head;Tail01` | `Vehicle_Horse` |
| 无人机 | 1 | 7 | `Root;Body;Head;Arm_XP/XN/YP/YN` | `Drone` |
| 怪物（自动命名）| 3 | 2–21 | `Bone001…`（无人体语义）| `Monster_T2` |
| Mixamo 骨架（个别模型）| 1 | 52 | `mixamorig:*` | `Tagilla` |
| 武器上的蒙皮网格 | 4 | 4–6 | `Root;Arrow;Spring…` | `WPN_AHBow` |

**结论：不用为 59 个模型各建一套映射** —— 按**骨架家族**建 **6 套左右**就覆盖绝大多数；怪物与 Mixamo 可后置。

### 两条命名策略（重要）

| 情形 | 做法 |
|---|---|
| **我们自己生成的模型** | YSM 里的 `bones[].name` **直接用游戏那一家族的骨骼名**（如 `Spine.002` / `UpperArm.R`）→ **按名字 1:1 挂载，无需映射表** |
| **导入社区现成 YSM 模型** | 它的骨骼名是作者自己的（`root`/`body`/`leftArm`…）→ 需要**映射表**，缺失时保留原挂点 |

## 做什么 / 怎么做

| 做什么 | 怎么做 |
|---|---|
| **换整体模型**（玩家 / NPC / 宠物）| 产 YSM 文本几何 → 我们的运行时库按骨骼名挂到角色骨骼上（游戏动画照常）|
| **挂饰 / 附件**（帽子、背包挂件、武器挂件）| 挂到对应 socket（`HelmatSocket` / `BackpackSocket` / `LeftHandSocket`…），与游戏自己挂 `IG_*` 的方式一致 |
| **换贴图 / 换材质（改色）** | 改 YSM 引用的贴图，或克隆原材质后改色 |

## 从游戏里抄真实结构（提取命令）

```
action=search  class=GameObject          pattern="CharacterModel"
action=export  class=SkinnedMeshRenderer field="m_GameObject.#name"  rows=20
action=dump    class=GameObject          name="0_CharacterModel_Custom_Killa"  follow=true  depth=4
```

## 参考

- 社区同类方案（**只参考思路，不作为前置**）：<https://github.com/Duckov-Custom-Model/DuckovCustomModel>（MIT）｜文档站 <https://duckov-custom-model.ritsukage.com>

## 待办

- [ ] 把 spike 的挂载逻辑整理成正式运行时库（骨骼名映射 + 层/材质处理）
- [ ] 找一份真实 `.ysm` 样例，把字段写成可复制模板
- [ ] 玩家鸭子（37 根）与 NPC 鸭子（24 根）两套骨架的 YSM 模板各一份
