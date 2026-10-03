---
name: model-creator
description: 给 Escape From Duckov 做 3D 模型：代码参数化生成的几何来源（盒/圆柱/圆锥/旋转体/拉伸）、模型 prefab 骨架、坐标与材质约定、按类别的真实样例句（武器/物品/建筑）、挂载点（`ItemGraphicInfo` / `ItemAgentUtilities` / sockets）与怎么确认做好了，以及角色/宠物/NPC 的 YSM 文本模型（骨骼树 + 方块，运行时按骨骼名挂到游戏骨骼，跟随游戏动画）。当玩家要「改模型 / 换外观 / 加个新造型 / 做个 3D 样子 / 换角色模型」，或要理解游戏里的模型是怎么拼出来时读取。
---

# 做 3D 模型（Escape From Duckov）

## 两条做法

| # | 做什么 | 做法 | 需要 |
|---|---|---|---|
| **①** | **物品 / 武器 / 配件 / 建筑 / 收藏品**的外观 | **代码参数化生成**（本技能主线）：C# 运行时拼 `Mesh`，换到游戏现成对象上 | dotnet |
| **②** | **角色 / 宠物 / NPC** 的外观 | **YSM 文本几何 + 我们自己的运行时库**：产 `ysm.json`（骨骼树 + 方块），运行时按骨骼名挂到游戏骨骼上（跟随游戏动画）| 无 |

**① 为什么可行**：游戏里的"模型"就是 prefab 上的 `MeshFilter` / `MeshRenderer`；`Mesh` 是**内存对象**
（`vertices` / `triangles` / `uv` / `normals`），运行时直接构造即可 —— 不需要 Unity、不需要 AssetBundle。

**② 的细节**（YSM 字段、骨骼家族与映射、层与材质的实机结论）见 `docs/unity-3d/03-characters.md`。

## 动手前先查真实样例（照着写，不猜结构）

按类别查 `docs/unity-3d/`：`README.md`（总览 + 提取命令）、`00-shared.md`（通用约定）、
`01-weapons.md`、`02-items.md`、`03-characters.md`、`04-buildings.md`。

要现场看游戏里真实的模型结构，用 **`inspect_game_data`**（只读、不运行游戏）：

```
action=search  class=GameObject  pattern="IG_"        # 物品模型 prefab（374 个）
action=search  class=GameObject  pattern="Pfb_BLD"    # 建筑
action=dump    pathid=<id>  follow=true  depth=4      # 一个 prefab 的完整组件图
action=dump    class=Item  match="typeID=<id>"  depth=2   # 物品 → itemGraphic -> pathID
action=export  class=CharacterSubVisuals  field=["m_GameObject.#name","renderers[].#class"]  rows=20
```

通用骨架：`GameObject` + `Transform` + `CharacterSubVisuals`（`renderers` / `mainModel`）+ 物品模型再加 `ItemGraphicInfo`（`groundPoint` / `sockets`）。

## 几何怎么生成

几何一律**由代码按参数算出**：先用基本体拼，再按变换组合。

| 基本体 | 参数 | 用途 |
|---|---|---|
| 盒体 | 长/宽/高 | 枪身、枪托、背包、墙体 |
| 圆柱 | 半径/高/分段 | 枪管、消音器、柱、管子 |
| 圆锥/圆台 | 上下半径/高 | 枪口、灯罩、塔尖 |
| 旋转体 | 轮廓点 | 弹匣、瓶子、装饰 |
| 拉伸 | 2D 轮廓+厚度 | 枪机、标牌 |

每个基本体产出顶点/索引/UV/法线 → `Matrix4x4.TRS(位置, 旋转, 缩放)` 变换后拼成一张 mesh；多部件用 **submesh** 分材质。
量级参考：游戏里一个三脚架 1431 顶点 / 45 KB —— 几千顶点足够，拼装完全可控。

**坐标/单位**：米；物品模型 0.1–1 m 量级；生成时**居中、+Z 朝前、+Y 朝上**，贴合交给游戏侧的 `groundPoint` / `sockets`。

## 材质

游戏是 **URP**（`EPOURP.dll`）→ 材质 shader 必须匹配，否则**粉紫**。
做法：**克隆同类物品已有的材质**，再改颜色/贴图（省掉 shader 找不到的坑）；无贴图时先用纯色跑通。

## 挂载（把 mesh 装上去）

| 目标 | 做法 |
|---|---|
| 物品的世界/地面模型 | 取 `Item.itemGraphic`（`ItemGraphicInfo`）→ 其 `m_GameObject` 树里的 `MeshRenderer.sharedMesh` / `material` |
| 手持实体 | `ItemStatsSystem.ItemAgentUtilities.GetPrefab(key)` → `CreateAgent(prefab, agentType)` → `BindNewAgent(agent, agentType)` |
| 配件/挂饰 | 挂到 `ItemGraphicInfo.sockets` 里的 socket `Transform` |

## 换物品模型（枪 / 背包 / 箱子…）

物品有两处模型，位置不同、做法也不同：

| 在哪 | 怎么换成我们的 |
|---|---|
| **拿在手上**（`Item.ActiveAgent`）| **只替换“枪身”那一个零件**：`WPN_*`（或最大的非配件零件）`enabled = false`，在**它的变换下**挂我们的 mesh｜**其余零件一律不碰** |
| **掉落 / 展示**（`ItemGraphicInfo`）| **克隆 `item.ItemGraphic`** → 换掉克隆里的几何 → 反射写回私有字段 `Item.itemGraphic`（游戏之后实例化的就是我们的）|

- **优先写“物品模板”** `ItemAssetsCollection.GetPrefab(typeID)`：新实例天生就对（开局就生效）；活实例再补一次兜底。
- **手里是“零件组合”**：配件槽位（`ShowIf_*` / `HideIf_*`）、弹匣、枪机都由**游戏**按状态开关 → 只能动枪身。
- **不要重建实体**（如 `ItemAgentUtilities.CreateAgent`）：它会销毁旧实体，游戏还持有引用 → 武器**选不中 / 用不了**。
- **克隆作模板时必须保持激活**：`SetActive(false)` 会让游戏实例化出来的**全都隐形**。
- **对齐尺寸**：按原枪身包围盒等比缩放（0.2 m 的手枪要撑到 0.86 m 的 MP5 槽位）。
- **材质**：克隆枪身材质，贴图塞进 `_MainTex`；**每帧**检查没换过的实例（开局 / 掉落 / 拾取 / 切枪都会重建）。
- 可复用实现：`reference/mod-kit/ItemModelBinder.cs`；例子：`reference/item_swap/`（`config.json` 可切模型/目标，改完保存即生效）。

## 换角色模型时怎么找角色

```csharp
// 玩家：用 IsMainCharacter 判定 —— 不要拿 FindObjectsOfType 的第一个，那可能是 NPC
var player = Object.FindObjectsOfType<CharacterMainControl>().FirstOrDefault(c => c.IsMainCharacter);
var model  = player.characterModel;          // 进关卡前是 null → 每秒重试
var root   = model.transform;                // 骨骼都在它下面

// 特定 NPC：按 GameObject 名或“模型名”筛
var npc = Object.FindObjectsOfType<CharacterMainControl>()
            .FirstOrDefault(c => c.characterModel != null
                              && c.characterModel.name.StartsWith("0_CharacterModel_Custom_"));

// 替换原外观：把"本体"渲染器直接关掉（改层没用 —— 游戏会改回来）；装备/武器要留着
foreach (var r in model.GetComponentsInChildren<Renderer>(true))
{
    if (IsOurs(r)) continue;                       // 我们自己建的方块
    bool equip = IsUnderSocket(r, model.transform); // 装备都挂在 *Socket* 下（MeleeWeaponSocket/HelmatSocket/…）
    r.enabled = equip;                              // 本体关掉、装备打开 —— enabled 是硬的，游戏不会动它
}
// 每帧重申一次（游戏刷新只改层，不改 enabled）｜隐藏层 SpecialCamera=31，可见层 Character=9

- **必须用 `IsMainCharacter` 挑玩家**：主菜单里一个角色都没有；关卡里一次能找到十几个（玩家 + NPC + 宠物），`FindObjectsOfType` 的第一个常是 NPC。
- **角色模型会被游戏重建**（进关卡、换装备）→ 每秒检查“模型根是否变了 / 方块是否还在”，变了就重新挂。
- **别用“改层”当隐藏**：游戏刷新会把层改回 `Character`（实测踩过）；`enabled = false` 才拦得住。
- **卡 T-pose**：Unity `Animator` 默认 `CullUpdateTransforms` —— 本体渲染器一被禁用就停更骨骼 → 把 Animator 设成 `AlwaysAnimate`。
- **白模**：找不到材质源时（有的模型没有蒙皮网格）取任意有材质的渲染器；都没有才是白模。
- **装备规则**：挂在名字含 `Socket` 的挂点下 = 装备/武器（背包、头盔、手里的枪）→ 保持 `enabled = true`，否则背包和枪会一起消失。
- **模板骨架要跟目标角色对上**：玩家在关卡里用 `Pelvis`/`UpperArm` 那套；boss Jeff 用 `Hip`/`Arm.Root` 那套 —— 选错会**静默不挂**（用 `inspect_game_data` dump 目标模型的骨骼名确认）。
- 可复用实现：`reference/mod-kit/CharacterModelReplacer.cs`（`ReplaceBody` 开关 = 替换 / 只增加；`Attach` + 每帧 `Tick`）+ `GameApi.cs`（找玩家 / 找 NPC）。
- 例子：`reference/cube_person/`（读 mod 目录的 `config.json`，可切玩家/NPC，改完保存即生效）。

## 怎么确认做好了

1. `validate_mod` 编译通过 → `install_mod` 装进游戏；
2. **进游戏看**：模型出现、位置/朝向正确、材质不粉紫、手持/地面显示正常；
3. 位置或比例不对时，回到坐标约定（居中/+Z）与 `groundPoint` / `sockets` 调整。

## 参考

- 详细资料与真实样例：`docs/unity-3d/`（入口 `README.md`；角色类做法见 `03-characters.md`）。
- mod 的**整体流程**（csproj / info.ini / ModBehaviour / 校验 / 装进游戏）：`mod-creator`、`mod-installer`。
