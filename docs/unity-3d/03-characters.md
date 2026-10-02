# 03 · 角色（玩家 / NPC / 宠物）

## 做法：YSM 文本模型，按骨骼名挂到游戏骨骼

角色模型 = **骨骼树 + 方块**，用 **YSM 格式**（源自 Minecraft Bedrock 几何），纯 JSON 文本：

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

挂载方式：运行时按 `bones[].name` 找到**同名游戏骨骼**，把该 bone 的几何挂上去。**动画由游戏自己的 Animator 驱动**（走/跑/射击/受击全都有），不需要写动画系统。

## 挂载要点

| 要点 | 做法 |
|---|---|
| **骨骼名** | YSM 里**直接用游戏骨骼名**（`Spine.002` / `UpperArm.R` / `Hand.R`…）→ 按名字 1:1 挂载；导入别人做的 YSM（骨骼名是 `root`/`body`/`leftArm` 那套）才需要映射表，缺失的骨骼保留原挂点 |
| **层（Layer）** | 挂上去的物件要放在**角色渲染器所在层** —— 角色模型根节点在 `layer 0`，但**相机不渲染 `layer 0`**（`Main Camera` 的 `cullingMask` 渲染 `layer 9`，NPC 鸭子模型的角色渲染器在 `layer 9`，另有 `layer 15`）；放错层就整块看不见 |
| **材质** | **克隆游戏现有材质**再改颜色/贴图：身体材质 `Skin`、shader `SodaCraft/SodaCharacter`（自定义 URP shader，自建材质找不到它 → 粉紫）|
| **挂点（socket）** | `HelmatSocket`·`ArmorSocket`·`BackpackSocket`·`MeleeWeaponSocket`·`LeftHandSocket`·`RightHandSocket`·`FaceMaskSocket`·`HairSocket`·`MouthSocket`；`CharacterModel` 的 socket 字段是 `private`，用反射取 |
| **替换原有外观** | `CharacterSubVisuals.SetRenderersHidden(true)` 隐藏原渲染器、`AddRenderer(renderer)` 登记自己的渲染器 |
| **找骨骼的入口** | `CharacterMainControl.characterModel` → `CharacterModel.transform` 往下按名字遍历（`FindDeepChild` 式递归）|

## 运行时层级（游戏里长这样）

```
Character(Clone)/ModelRoot/0_CharacterModel_Custom_Template(Clone)/CustomFaceInstance/DuckBody   ← SkinnedMeshRenderer（layer 9）
  ├─ Armature / Root / Pelvis / Spine.001–004 / Head / Duck_Beak / Duck_Eye.L/R
  ├─ UpperArm.L → Elbow.L → ForeArm.L → Hand.L → Hand.Soket.L → LeftHandSocket
  ├─ UpperArm.R → Elbow.R → ForeArm.R → Hand.R → Hand.Soket.R → RightHandSocket
  ├─ Wings.L/R · ArmorSocket · BackpackSocket · HelmatSocket · FaceMaskSocket · HairSocket · MouthSocket
  └─ 装备以 (Clone) 形式挂在对应 socket 下：
     IG_Helmat_Storm_Lv5(Clone)→HelmatSocket、IG_Armor_Storm_Lv5(Clone)→ArmorSocket、
     IG_Backpack_LV5(Clone)→BackpackSocket、MeleeWeaponAgent_Knife_04_Karambit(Clone)→MeleeWeaponSocket
```

## 骨骼家族（映射基础）

导出真实骨骼名：

```
action=export  class=SkinnedMeshRenderer \
  --field "m_GameObject.#name" --field "m_Bones[].m_GameObject.#name" --out /tmp/smr.tsv
```

72 个蒙皮网格 → 20 种骨骼集合，主力家族：

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

按家族建映射即可：**6 套左右**覆盖绝大多数（NPC 鸭子 / 玩家鸭子 / 蜘蛛·机械 / 兽类 / 载具 / 无人机）。

## 做什么 / 怎么做

| 做什么 | 怎么做 |
|---|---|
| **换整体模型**（玩家 / NPC / 宠物）| 产 YSM（骨骼名用游戏那一套）→ 运行时把几何挂到同名骨骼上 |
| **挂饰 / 附件**（帽子、背包挂件、武器挂件）| 挂到对应 socket（`HelmatSocket` / `BackpackSocket` / `LeftHandSocket`…），与游戏自己挂 `IG_*` 的方式一致 |
| **换贴图 / 换材质（改色）** | 改 YSM 引用的贴图，或克隆原材质后改色 |

## 可运行的参考实现

`reference/bone_probe/`：找骨骼 → 建几何 → 挂上去（含层与材质处理）的可编译示例，运行后在角色手上显示一个跟随动作的方块。

## 从游戏里抄真实结构

```
action=search  class=GameObject          pattern="CharacterModel"
action=export  class=SkinnedMeshRenderer field="m_GameObject.#name"  rows=20
action=dump    class=GameObject          name="0_CharacterModel_Custom_Killa"  follow=true  depth=4
```
