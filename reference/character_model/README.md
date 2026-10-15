# 角色替换（参考 mod：`CharacterModelSwap`）

把玩家角色的身体换成你自己的 GLB 模型。模型必须先绑好骨（Tripo 的 `rig` 动作，人形 23 关节）。

搬运、隐藏、重申的逻辑全在 `libs/mod-kit/`。这份 mod 只读配置、找主角色、每帧喂 `Tick`。

## 用法

1. 编译本目录（`dotnet build CharacterModelSwap.csproj -c Release`）。
2. 把编译出的 `CharacterModelSwap.dll`、`0Harmony.dll`、`info.ini`、`config.json` 复制到
   `<游戏目录>/Duckov.app/Contents/Mods/<mod 名>/`。
3. 把你的模型命名为 `model.glb`，放在同一个目录。
4. 需要时改 `config.json` 里的 `scale`，存盘即生效（不必重启）。

模型要求：

- 带骨架（`skins` 非空），关节名是人形那一套（`Root` 与 `mixamorig:*`，共 23 个）。
- 加载一律用 `front=auto`、`zeroGrip=false`。参考实现已经这么写，不得改：加载期的旋转或位移会让
  顶点与骨架错位。

## 推荐流程（拿一个模型得到可接受的结果）

1. 准备模型：人形 23 关节（Tripo 的 `rig`，`spec=mixamo`）。
2. 放进本 mod 目录，命名为 `model.glb`。
3. 先用默认配置跑一次（只需一个 `scale`，默认 1.2）。
   这一版形状一定保真：手臂不摆，物品在自己的位置上。
4. 看三件事：
   - 形状是否完整（不拧、不涂抹）。
   - 大小是否合适；不合适就调 `scale`，常用范围 0.8 到 1.5。
   - 手里与背上的物品（枪、背包、护甲）位置是否可接受。它们由**游戏挂点**决定，模型影响不了。
6. 每次改完确认日志里这几行：`config:`（配置读到了）、`APPLIED:`（搬运成功，含缩放与包围盒）、
   `character model hooks`（五个钩子挂上了）、`model hook hid N`（部件被钩子藏掉）。

## config 字段

| 字段 | 默认 | 作用 |
|---|---|---|
| `model` | `model.glb` | 模型文件名，相对本 mod 目录 |
| `front` | `auto` | 朝向声明。角色不得改 |
| `scale` | `1.2` | 额外倍数，乘在自动算出的缩放上 |

`scale` 的取值依据：自动缩放只让**包围盒**与角色一样高。鸭子是高瘦、模型常常矮胖，所以视觉上还会偏小。
实测这对模型：鸭子的头骨在网格高度的 76%，鸭猫的头关节在 58%，比值 0.72。让**头顶**对齐约需 `1.38`，
`1.2` 是折中。

`BindArmBones` 默认 `false`（现阶段恒为 false）：搬运是**刚性**的，鸭子手臂骨的转轴不在模型的
手臂网格里，绑上去以后动画一摆，手臂就离开身体。代价是手臂不再独立摆动。

那是目前最稳的一版。

**形状保真是首要目标。** 所以不做「把每个部件搬到角色对应骨上」那套（逐骨落位）：量化过，
它的代价是形状被改变（边长比最大 4.12，刚性是 1.000），不限躯干时挂点物品还与身体分家，
只限制到手臂时肩部接缝仍被拉。完整测量与结论见设计仓
`docs/unity-3d-assets/10-character-replacement-research.md`。

手臂与手的权重默认并到躯干（`BindArmBones` 默认 false，现阶段恒为 false）。原因是模型的
手臂不在游戏手臂骨的转轴上，绑上去后动画一摆就脱开。

## 四个机制（为什么这么做）

1. **地标对齐加刚性搬运。** 只用两个身体方向建坐标架：骨盆到头的方向为「上」，左髋到右髋为「左」。
   两边用同一个构造，所以只能得到旋转，不会拧、不会镜像。
   **不得**改回逐骨重定姿态：实测两副骨架的骨轴约定平均差 139 到 143 度（用
   `scripts/measure-skeleton-gap.mjs` 可复量），逐骨等于把每个部件各转一个角度，模型拧成一团。

2. **尺寸用真实顶点跨度。** 高度取的是顶点在「上」方向上的最大减最小。
   **不得**用包围盒尺寸投影到上轴：上轴一倾斜，投影偏大，算出的缩放偏小，模型反而缩小。

3. **隐藏靠钩子，循环只兜底。** 挂在 `CharacterModel` 的 `AddSubVisuals`、`SetFaceFromData`、
   `SetFaceFromPreset`、`OnHelmatSlotContentChange`、`OnFaceMaskSlotContentChange` 上，部件一造出来就藏。
   `Tick` 在换好后的十几秒内逐帧扫，之后每秒一次，兜住不走这些路径的部件。
   **保留名单**里的东西一律不藏：手部挂点、背包、护甲、粒子。武器与背包要留着。

4. **手臂并到躯干。** 见上：`BindArmBones` 默认 false。

## 已知限制

- 模型保留**自己的绑定姿势**，游戏的动画增量叠在它上面。所以姿势不像鸭子那样精确。
  要让姿势也完全对齐，需要逐骨求出两副骨架的坐标架对应关系（试过一版，仍拉伸，未成立）。
- 只搬 `vertices`、`uv`、`triangles`、`boneWeights`。源模型若有第二套 UV 或顶点色会丢。
- 只读第一个 skin 与第 0 组关节权重。

## 排查

日志前缀：

| 前缀 | 含义 |
|---|---|
| `[CharacterModel] APPLIED` | 搬运成功，后面是顶点数、映射关节数、缩放、包围盒 |
| `[CharacterModel] FAILED` | 失败原因写在冒号后（缺地标、没找到身体渲染器等） |
| `[CharacterSwap] character model hooks` | 五个钩子哪几个挂上了 |
| `[CharacterSwap] model hook hid N` | 钩子当场藏了几个部件 |
| `[CharacterSwap] re-applied` | 游戏重建了角色，自动重来一次 |

设计依据与实测数据在设计仓 `docs/unity-3d-assets/10-character-replacement-research.md`。
