# 03 · 角色（玩家 / NPC / 宠物）

## 做法：YSM 文本模型，按骨骼名挂到游戏骨骼

角色模型 = **骨骼树 + 方块**，用 **YSM 格式**（源自 Minecraft Bedrock 几何），纯 JSON 文本：

```json
{
  "format_version": "1.12.0",
  "minecraft:geometry": [{
    "bones": [
      {"name": "Root", "pivot": [0, 0, 0]},
      {"name": "Pelvis", "parent": "Root", "pivot": [0, 2.42, 0], "cubes": [{"origin": [-4, 2.42, -3], "size": [8, 6, 6], "uv": [0, 0]}]},
      {"name": "Spine.004", "parent": "Pelvis", "pivot": [0, 9.53, 0]},
      {"name": "Head", "parent": "Spine.004", "pivot": [0, 12.73, -0.26], "cubes": [{"origin": [-4, 12.5, -4.26], "size": [8, 8, 8], "uv": [24, 0]}]}
    ],
    "description": {"identifier": "geometry.duck.custom", "texture_width": 128, "texture_height": 128}
  }]
}
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

## 运行时：怎么用（库 API）

逻辑都在 `reference/mod-kit/`，mod 里只剩"挑谁 + 每帧调一次"：

```csharp
var ysm = ModelLoader.LoadYsm(Path.Combine(ModelLoader.ModDir(), "models/duck_hip.json"));

var replacer = new CharacterModelReplacer(ysm)
{
    ReplaceBody   = true,    // true = 替换（关掉角色本体）｜ false = 只增加（挂件/饰品，不动本体）
    KeepEquipment = true,    // 保留装备/武器（挂在 *Socket* 下的东西）
};

replacer.Attach(who);        // 找骨骼 → 建几何 → 挂上（玩家或任意 NPC）
replacer.Tick();             // 每帧：替换本体 / 保留装备 / 模型被重建就重挂
```

挑选目标：

| 目标 | 怎么写 |
|---|---|
| **玩家** | `GameApi.FindMainCharacter()` —— 内部用 **`IsMainCharacter`** 判（`FindObjectsOfType` 的第一个可能是 NPC；主菜单里一个都没有）|
| **特定 NPC** | `FindObjectsOfType<CharacterMainControl>()` 里取 `!IsMainCharacter`，再按**模型名**筛（`c.characterModel.name.IndexOf("Jeff")`）；对象名同理 |

先用 `inspect_game_data` 确认目标用哪套骨架（dump 它的 Transform 子树看骨骼名），**再选对应模板**（见下节）。

## 运行时的坑（都踩过）

| 现象 | 原因 | 正确做法 |
|---|---|---|
| 挂上一半骨骼就没了 | 模板骨架与目标角色**不是同一套命名** | 按目标角色实际骨架选模板（玩家 = `Pelvis`/`UpperArm` 那套；boss Jeff = `Hip`/`Arm.Root` 那套）。选错会**静默不挂** |
| 方块挂上了但看不见 | 挂到了**别的角色**（第一个 `CharacterMainControl` 常常是 NPC） | 用 `IsMainCharacter` 挑玩家 |
| 原版角色又出现 | 用"改 layer"当隐藏 —— 游戏刷新会把层改回 `Character` | **`enabled = false`** 才能拦住（`gameObject.layer` 只作备用） |
| 背包/枪一起消失 | 装备判定看名字前缀（枪叫 `Knife04`，不是 `IG_*`） | 判定改成"**挂在名字含 `Socket`/`Soket` 的挂点下**"（`MeleeWeaponSocket`/`HelmatSocket`/`ArmorSocket`/`BackpackSocket`/`Hand.Soket.L`…）|
| 角色卡在 **T-pose** | Unity `Animator` 默认 `CullUpdateTransforms`：**下面渲染器全被禁用就停止更新骨骼** | 挂载时把角色的 Animator 设成 **`AlwaysAnimate`** |
| 变成**白模** | 找不到材质源（有的模型没有蒙皮网格，或有别的渲染器） | 材质源回退：优先蒙皮材质，否则取**任意一个有材质的渲染器**；都没有才用兜底材质 |
| 挂上去一会儿又没了 | 游戏**重建角色模型**（进关卡 / 换装备 / NPC 对象池） | 每帧 `Tick()` 检测（模型根变了 / 方块被销毁）→ **重挂** |

## 玩法不受影响（边界）

我们只换**外观**：血量、碰撞体、AI、阵营、掉落**一点没动** —— 所以被替换的 NPC 照样能被打、也会打你。要改这些是**数据/逻辑层**的事（`mod-creator`），不是模型能力。

## 可运行的例子

`reference/cube_person/`：把 `models/duck_hip.json`（或 `duck_pelvis.json`）挂到目标角色上。
mod 目录里的 `config.json` 决定目标，**改完保存即生效**（不用重编译）：

```
{ "model": "models/duck_hip.json", "target": "npc", "match": "Jeff", "replaceBody": true }
```

- `target`：`player` ｜ `npc`；`match`：NPC 的模型名/对象名片段
- `replaceBody`：`true` 替换 ｜ `false` 只增加
- 日志：`/tmp/cube_person.log`（挂了几个方块、缺哪些骨骼、本体关了几个、装备保留几个、材质源）

## 骨骼家族（映射基础）

导出真实骨骼名：

```
action=export  class=SkinnedMeshRenderer \
  --field "m_GameObject.#name" --field "m_Bones[].m_GameObject.#name" --out /tmp/smr.tsv
```

72 个蒙皮网格 → 20 种骨骼集合，主力家族：

| 家族 | 网格数 | 骨骼数 | 骨骼命名（节选） | 代表 |
|---|---|---|---|---|
| 鸭子 B：`Pelvis`/`UpperArm`（**玩家**在用）| **28** | 24 | `Root;Pelvis;Spine.001–004;Head;UpperArm.L;Elbow.L;ForeArm.L;Hand.L;Hand.Soket.L;Thigh.R/L;Foot;Tail` | `DuckBody` |
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

## 例子（照着写）

两份骨架模板：**骨骼名与 `pivot` 取自游戏真实骨架**（单位是像素，**16 px = 1 m**；`pivot` 是模型空间绝对坐标，与 Bedrock/YSM 一致）。
方块尺寸是最简近似 —— 做具体角色时按需拆分与细化。

### 鸭子骨架 B：`Pelvis` / `UpperArm` 命名（`duck_pelvis`，24 骨）

**23 个骨骼 / 552 顶点 / 276 三角面** · 贴图 128²（UV 已按 box-UV 排布）

```json
{
  "format_version": "1.12.0",
  "minecraft:geometry": [{
    "bones": [
      {"name": "Root", "pivot": [0, 0, 0], "cubes": [{"origin": [-1.3, -0.09, -1.3], "size": [2.6, 2.6, 2.6], "uv": [0, 0]}]},
      {"name": "Pelvis", "parent": "Root", "pivot": [0, 2.42, 0], "cubes": [{"origin": [-2.6, 2.06, -2.1], "size": [5.2, 3.0, 4.2], "uv": [13, 0]}]},
      {"name": "Spine.001", "parent": "Pelvis", "pivot": [0, 4.7, 0], "cubes": [{"origin": [-1.3, 4.05, -1.3], "size": [2.6, 2.6, 2.6], "uv": [32, 0]}]},
      {"name": "Spine.002", "parent": "Spine.001", "pivot": [0, 5.99, 0], "cubes": [{"origin": [-1.3, 5.58, -1.3], "size": [2.6, 2.6, 2.6], "uv": [45, 0]}]},
      {"name": "Spine.003", "parent": "Spine.002", "pivot": [0, 7.76, 0], "cubes": [{"origin": [-1.3, 7.34, -1.3], "size": [2.6, 2.6, 2.6], "uv": [58, 0]}]},
      {"name": "Spine.004", "parent": "Spine.003", "pivot": [0, 9.53, 0], "cubes": [{"origin": [-4.65, 7.27, -2.15], "size": [5.0, 4.4, 4.6], "uv": [71, 0]}]},
      {"name": "Head", "parent": "Spine.004", "pivot": [0, 12.73, -0.26], "cubes": [{"origin": [-2.5, 12.53, -2.56], "size": [5.0, 4.4, 4.6], "uv": [92, 0]}]},
      {"name": "UpperArm.L", "parent": "Spine.004", "pivot": [-4.31, 9.42, 0.29], "cubes": [{"origin": [-6.7, 8.07, -1.04], "size": [2.6, 2.6, 2.6], "uv": [113, 0]}]},
      {"name": "Elbow.L", "parent": "UpperArm.L", "pivot": [-6.49, 9.32, 0.24], "cubes": [{"origin": [-8.05, 6.96, -1.05], "size": [2.6, 2.6, 2.6], "uv": [0, 10]}]},
      {"name": "ForeArm.L", "parent": "Elbow.L", "pivot": [-7.02, 7.19, 0.26], "cubes": [{"origin": [-8.46, 4.76, -1.03], "size": [2.6, 2.6, 2.6], "uv": [13, 10]}]},
      {"name": "Hand.L", "parent": "ForeArm.L", "pivot": [-7.3, 4.93, 0.28], "cubes": [{"origin": [-8.64, 3.37, -1.01], "size": [2.6, 2.6, 2.6], "uv": [26, 10]}]},
      {"name": "Hand.Soket.L", "parent": "Hand.L", "pivot": [-7.39, 4.41, 0.3], "cubes": [{"origin": [-8.69, 3.11, -1.0], "size": [2.6, 2.6, 2.6], "uv": [39, 10]}]},
      {"name": "UpperArm.R", "parent": "Spine.004", "pivot": [4.31, 9.42, 0.29], "cubes": [{"origin": [4.1, 8.07, -1.04], "size": [2.6, 2.6, 2.6], "uv": [52, 10]}]},
      {"name": "Elbow.R", "parent": "UpperArm.R", "pivot": [6.49, 9.32, 0.24], "cubes": [{"origin": [5.46, 6.96, -1.05], "size": [2.6, 2.6, 2.6], "uv": [65, 10]}]},
      {"name": "ForeArm.R", "parent": "Elbow.R", "pivot": [7.02, 7.19, 0.26], "cubes": [{"origin": [5.86, 4.76, -1.03], "size": [2.6, 2.6, 2.6], "uv": [78, 10]}]},
      {"name": "Hand.R", "parent": "ForeArm.R", "pivot": [7.3, 4.93, 0.28], "cubes": [{"origin": [6.04, 3.37, -1.01], "size": [2.6, 2.6, 2.6], "uv": [91, 10]}]},
      {"name": "Hand.Soket.R", "parent": "Hand.R", "pivot": [7.39, 4.41, 0.3], "cubes": [{"origin": [6.09, 3.11, -1.0], "size": [2.6, 2.6, 2.6], "uv": [104, 10]}]},
      {"name": "Thigh.R", "parent": "Pelvis", "pivot": [1.44, 1.42, 0.0], "cubes": [{"origin": [0.15, -0.44, -1.35], "size": [2.6, 2.6, 2.6], "uv": [0, 17]}]},
      {"name": "Foot.R", "parent": "Thigh.R", "pivot": [1.46, 0.3, -0.1], "cubes": [{"origin": [0.16, -1.0, -1.4], "size": [2.6, 2.6, 2.6], "uv": [13, 17]}]},
      {"name": "Thigh.L", "parent": "Pelvis", "pivot": [-1.44, 1.42, 0.0], "cubes": [{"origin": [-2.75, -0.44, -1.35], "size": [2.6, 2.6, 2.6], "uv": [26, 17]}]},
      {"name": "Foot.L", "parent": "Thigh.L", "pivot": [-1.46, 0.3, -0.1], "cubes": [{"origin": [-2.76, -1.0, -1.4], "size": [2.6, 2.6, 2.6], "uv": [39, 17]}]},
      {"name": "Tail", "parent": "Pelvis", "pivot": [0, 1.9, -2.2], "cubes": [{"origin": [-1.3, 0.7, -4.2], "size": [2.6, 2.6, 2.6], "uv": [52, 17]}]},
      {"name": "Tail.001", "parent": "Tail", "pivot": [0, 2.1, -3.6], "cubes": [{"origin": [-1.3, 0.8, -4.9], "size": [2.6, 2.6, 2.6], "uv": [65, 17]}]}
    ],
    "description": {"identifier": "geometry.duck.pelvis", "texture_width": 128, "texture_height": 128}
  }]
}
```

### 鸭子骨架 A：`Hip` / `Arm.Root` 命名（`duck_hip`，37 骨）

**21 个骨骼 / 504 顶点 / 252 三角面**（手指等细节骨骼按需补） · 贴图 128²（UV 已按 box-UV 排布）

```json
{
  "format_version": "1.12.0",
  "minecraft:geometry": [{
    "bones": [
      {"name": "Root", "pivot": [0.0, 0.0, 0.0], "cubes": [{"origin": [-0.6, 0.0, -0.6], "size": [1.2, 1.42, 1.2], "uv": [0, 0]}]},
      {"name": "Hip", "parent": "Root", "pivot": [0.0, 1.42, 0.0], "cubes": [{"origin": [-0.6, 1.42, -0.6], "size": [1.2, 1.6, 1.2], "uv": [5, 0]}]},
      {"name": "Spine.001", "parent": "Hip", "pivot": [0.0, 3.02, 0.0], "cubes": [{"origin": [-0.6, 3.02, -0.6], "size": [1.2, 2.22, 1.2], "uv": [10, 0]}]},
      {"name": "Spine.002", "parent": "Spine.001", "pivot": [0.0, 5.24, 0.0], "cubes": [{"origin": [-0.61, 5.24, -0.61], "size": [1.23, 2.46, 1.23], "uv": [15, 0]}]},
      {"name": "Spine.003", "parent": "Spine.002", "pivot": [0.0, 7.7, 0.0], "cubes": [{"origin": [-0.92, 7.7, -1.07], "size": [1.84, 3.68, 1.84], "uv": [20, 0]}]},
      {"name": "Head", "parent": "Spine.003", "pivot": [-0.0, 11.38, -0.3], "cubes": [{"origin": [-1.0, 10.38, -1.3], "size": [2.0, 2.0, 2.0], "uv": [29, 0]}]},
      {"name": "Arm.Root.R", "parent": "Spine.003", "pivot": [1.84, 9.24, 0.24], "cubes": [{"origin": [1.24, 9.24, -0.36], "size": [1.2, 1.93, 1.2], "uv": [38, 0]}]},
      {"name": "Arm.Upper.R", "parent": "Arm.Root.R", "pivot": [1.84, 11.17, 0.24], "cubes": [{"origin": [1.24, 11.17, -0.36], "size": [1.21, 2.42, 1.21], "uv": [43, 0]}]},
      {"name": "Arm.Fore.R", "parent": "Arm.Upper.R", "pivot": [1.84, 13.59, 0.24], "cubes": [{"origin": [1.24, 13.59, -0.36], "size": [1.2, 1.7, 1.2], "uv": [48, 0]}]},
      {"name": "Hand.R", "parent": "Arm.Fore.R", "pivot": [1.84, 15.29, 0.24], "cubes": [{"origin": [0.84, 14.29, -0.76], "size": [1.2, 1.2, 1.2], "uv": [53, 0]}]},
      {"name": "Arm.Root.L", "parent": "Spine.003", "pivot": [-1.84, 9.24, 0.24], "cubes": [{"origin": [-2.44, 9.24, -0.36], "size": [1.2, 1.93, 1.2], "uv": [58, 0]}]},
      {"name": "Arm.Upper.L", "parent": "Arm.Root.L", "pivot": [-1.84, 11.17, 0.24], "cubes": [{"origin": [-2.45, 11.17, -0.36], "size": [1.21, 2.42, 1.21], "uv": [63, 0]}]},
      {"name": "Arm.Fore.L", "parent": "Arm.Upper.L", "pivot": [-1.84, 13.59, 0.24], "cubes": [{"origin": [-2.44, 13.59, -0.36], "size": [1.2, 1.7, 1.2], "uv": [68, 0]}]},
      {"name": "Hand.L", "parent": "Arm.Fore.L", "pivot": [-1.84, 15.29, 0.24], "cubes": [{"origin": [-2.84, 14.29, -0.76], "size": [1.2, 1.2, 1.2], "uv": [73, 0]}]},
      {"name": "Leg.Upper.R", "parent": "Hip", "pivot": [1.84, 3.23, -0.91], "cubes": [{"origin": [1.24, 3.23, -1.51], "size": [1.2, 1.91, 1.2], "uv": [78, 0]}]},
      {"name": "Leg.Lower.R", "parent": "Leg.Upper.R", "pivot": [1.84, 5.14, -0.91], "cubes": [{"origin": [1.24, 5.14, -1.51], "size": [1.2, 1.27, 1.2], "uv": [83, 0]}]},
      {"name": "Foot.R.001", "parent": "Leg.Lower.R", "pivot": [1.84, 6.41, -0.91], "cubes": [{"origin": [0.84, 5.41, -1.91], "size": [1.2, 1.2, 1.2], "uv": [88, 0]}]},
      {"name": "Leg.Upper.L", "parent": "Hip", "pivot": [-1.84, 3.23, -0.91], "cubes": [{"origin": [-2.44, 3.23, -1.51], "size": [1.2, 1.91, 1.2], "uv": [93, 0]}]},
      {"name": "Leg.Lower.L", "parent": "Leg.Upper.L", "pivot": [-1.84, 5.14, -0.91], "cubes": [{"origin": [-2.44, 5.14, -1.51], "size": [1.2, 1.27, 1.2], "uv": [98, 0]}]},
      {"name": "Foot.L.001", "parent": "Leg.Lower.L", "pivot": [-1.84, 6.41, -0.91], "cubes": [{"origin": [-2.84, 5.41, -1.91], "size": [1.2, 1.2, 1.2], "uv": [103, 0]}]},
      {"name": "Tail.001", "parent": "Hip", "pivot": [-0.0, 4.73, -3.84], "cubes": [{"origin": [-1.0, 3.73, -4.84], "size": [1.2, 1.2, 1.2], "uv": [108, 0]}]}
    ],
    "description": {"identifier": "geometry.duck.hip", "texture_width": 128, "texture_height": 128}
  }]
}
```
