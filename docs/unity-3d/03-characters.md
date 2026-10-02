# 03 · 角色（玩家 / NPC / 宠物）

## 做法：产 YSM 文本几何，交给 DCM 加载

游戏里的角色是**骨骼 + 蒙皮**（`SkinnedMeshRenderer`，资产里带 `m_BindPose` / `m_BoneNameHashes` / `m_BonesAABB`）：

```
export --class SkinnedMeshRenderer --field "m_GameObject.#name" --rows 5
→ 73 row(s)；例：Player_Duck_Head / WPN_AHBow / Cone …

search --class GameObject --pattern "CharacterModel"
→ 147 match(es)；例：0_CharacterModel_Custom_Killa / _Tagilla / _Boss_Alex / _Enemy_SnowMan …
```

我们的做法是产 **YSM 模型**（源自 Minecraft Bedrock 几何格式）：**骨骼树 + 方块**，纯 JSON 文本；蒙皮与动画由 **DCM** 运行库处理。

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

## 放置与目标

- **放置**：`.ysm` 文件或含 `ysm.json` 的文件夹 → `<游戏>/ModConfigs/DuckovCustomModel/Models`（安装目录只读时 DCM 自动切到用户数据目录）；**不需要 AssetBundle**。
- **前置**：玩家需要 **DCM（Duckov Custom Model）+ HarmonyLib**。
- **目标类型**：`built-in:Character`（角色）/ `built-in:Pet`（宠物）/ `built-in:AICharacter_*`（某个或全部 AI）；不填默认角色 + 全部 AI。
- **游戏内**：`\` 开模型界面选模型；模型自带**动作轮盘**（按 `Z`，分页/分类/循环/打断）；支持 Molang（`query.*`、`ysm.food_level` 等）。
- **侧车配置**（`<模型名>.ysm.duckov.json`，与模型同目录）：
  ```json
  { "Name": "自定义模型", "ModelTarget": "player/main",
    "TargetTypes": ["built-in:Character", "built-in:AICharacter_*"],
    "Scale": 1.0, "HeadPitchLimit": 45, "HeadYawLimit": 85,
    "LocatorMappings": { "RightHandLocator": "模型内实际定位器名" } }
  ```
  `LocatorMappings` 用**模型内的真实定位器名**（缺失时保留原角色挂点）；`LocatorOffsets` 可给各挂点配 `Position` / `Rotation`。

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

**结论：不用为 59 个模型各建一套映射** —— 按**骨架家族**建 **6 套左右**就覆盖绝大多数（NPC 鸭子 / 玩家鸭子 / 蜘蛛·机械 / 兽类 / 载具 / 无人机）；怪物与 Mixamo 那几套可后置。

### 两条命名策略（重要）

| 情形 | 做法 |
|---|---|
| **我们自己生成的模型** | YSM 里的 `bones[].name` **直接用游戏那一家族的骨骼名**（如 `Spine.002` / `Arm.Upper.R`）→ **按名字 1:1 挂载，无需映射表** |
| **导入社区现成 YSM 模型** | 它的骨骼名是作者自己的（`root`/`body`/`leftArm`…）→ 需要**映射表**（YSM 名 → 游戏骨骼名），缺失时保留原挂点 |

## 做什么 / 怎么做

| 做什么 | 怎么做 |
|---|---|
| **换整体模型**（玩家 / NPC / 宠物）| 产 YSM 文本几何 → 放进 `ModConfigs/DuckovCustomModel/Models` + 侧车 `<名>.ysm.duckov.json` |
| **挂饰 / 附件**（帽子、背包挂件、武器挂件）| 在 YSM 里加一个 bone + cube，或用 `locators` 挂点 |
| **换贴图 / 换材质（改色）** | 改 YSM 引用的贴图，或克隆原材质后改色 |

## 从游戏里抄真实结构（提取命令）

```
action=search  class=GameObject          pattern="CharacterModel"
action=export  class=SkinnedMeshRenderer field="m_GameObject.#name"  rows=20
action=dump    class=GameObject          name="0_CharacterModel_Custom_Killa"  follow=true  depth=4
```

## 参考

- 框架：<https://github.com/Duckov-Custom-Model/DuckovCustomModel>（**MIT**）｜文档站 <https://duckov-custom-model.ritsukage.com>｜SDK <https://github.com/Duckov-Custom-Model/DuckovCustomModel-SDK>
- YSM 使用说明：DCM 仓库 `docs/YSM_USAGE.md`

## 待办

- [ ] 找一份真实 `.ysm` / `ysm.json` 样例（社区模型）把字段写成可复制模板
- [ ] 确认游戏侧 `locators`（如 `Hand.Soket.L`）与侧车 `LocatorMappings` 的对应关系
- [ ] 实机验证：把自建几何挂到游戏骨骼上，看是否跟随动画（可用日志验：定时打骨骼世界坐标）
