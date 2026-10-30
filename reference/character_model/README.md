# 角色替换（参考 mod：`CharacterModelSwap`）

验收记录见 [`scripts/reference-acceptance.md`](../../scripts/reference-acceptance.md)。本 reference 是研究起点，不代替本次加载、行为与边界验收。

把玩家角色的身体换成你自己的 GLB 模型。模型必须先绑好骨（Tripo 的 `rig` 动作，人形 23 关节）。

搬运、隐藏、重申的逻辑全在 `libs/mod-kit/`。这份 mod 只读配置、找主角色、每帧喂 `Tick`。

当前运行时重定向版本尚未完成游戏内验收。离线脚本只检查数学性质与已知回退，不证明游戏内外观正确。

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
   这一版保留源骨架与源权重，手臂跟随游戏骨骼的旋转增量。物品仍在游戏挂点上。
4. 量一次这个模型，把建议的 `scale` 写进配置：

   ```bash
   node scripts/measure-skeleton-gap.mjs /path/to/model.glb /tmp/duckbody.txt
   ```

   脚本最后一行给「建议 scale」，它按头骨高度占网格高度的比例两侧对比来算。实测鸭猫 1.20、
   穿靴子的猫 0.97。这个值随模型变，不得当常数。
5. 看三件事：
   - 形状是否完整（不拧、不涂抹）。
   - 大小是否合适；不合适就微调 `scale`。
   - 手里与背上的物品（枪、背包、护甲）位置是否可接受。它们由**游戏挂点**决定，模型影响不了。
7. 每次改完确认日志里这几行：`config:`（配置读到了）、`APPLIED:`（搬运成功，含缩放与包围盒）、
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

`CharacterRetarget` 保留源模型的骨架、骨长和权重。它每帧读取游戏骨骼相对 bind pose 的
世界旋转增量，再把增量施加到源关节的 bind pose 上。它不复制游戏骨骼的位置，也不使用 IK。
原鸭子骨骼继续由 Animator 驱动，身体渲染器改用源骨骼。

## 四个机制（为什么这么做）

1. **地标对齐。** 只用两个身体方向建坐标架：骨盆到头的方向为「上」，左髋到右髋为「左」。
   源骨架根使用统一旋转、缩放和平移。逐骨旋转由运行时重定向单独计算。

2. **尺寸用真实顶点跨度。** 高度取的是顶点在「上」方向上的最大减最小。
   **不得**用包围盒尺寸投影到上轴：上轴一倾斜，投影偏大，算出的缩放偏小，模型反而缩小。

3. **隐藏靠钩子，循环只兜底。** 挂在 `CharacterModel` 的 `AddSubVisuals`、`SetFaceFromData`、
   `SetFaceFromPreset`、`OnHelmatSlotContentChange`、`OnFaceMaskSlotContentChange` 上，部件一造出来就藏。
   `Tick` 在换好后的十几秒内逐帧扫，之后每秒一次，兜住不走这些路径的部件。
   **保留名单**里的东西一律不藏：手部挂点、背包、护甲、粒子。武器与背包要留着。

4. **源骨架运行时重定向。** 原鸭子骨架负责动画，源骨架保留模型比例并接收旋转增量。

## 已知限制

- 只重定向旋转。源模型与鸭子的骨长、肩宽不同，手部与游戏物品挂点仍可能存在位置差。
- 加载器只保留第一套 UV，不保留顶点色。
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
