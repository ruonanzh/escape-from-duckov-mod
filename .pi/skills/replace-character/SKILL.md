---
name: replace-character
description: 把玩家角色的模型换成自己做的一个。触发：玩家说「把角色换成……」，给了参考图或一段描述，或者已经有一个带骨架的 GLB。流程是：先做风格化的 Tripo 预览图，再产出带骨架的 3D 模型，最后装进游戏。不适用：换武器或物品模型（用 replace-gun-model 或 replace-item-model），以及只做图标（用 generate_image）。
---

# 替换玩家角色模型

一句话：**先做风格化的 Tripo 预览图，再产出带骨架的 3D 模型，最后装进游戏**。

**首要目标是形状保真。** 模型必须看起来像玩家给的那只角色。与它冲突的做法一律不选：
右手掌心由两骨 IK 对齐握把；左手并入躯干，不独立摆臂。手掌不主动旋转。

与另外四个 SKILL 一样，本 SKILL 不重写工具说明里已经写过的朝向、面数、积分与不得连发那些话。

> **先看目标角色自己的骨架与子外观**：库对「骨架层级、子外观、挂点」的判断是**最大可能性**。玩家、NPC、宠物的层级与命名各不相同，动手前用 `inspect_game_data action=dump --class CharacterModel` 与 `action=dump --class CharacterSubVisuals` 看清实际结构；冲突时**以 prefab 为准**。

## 1. 需要的输入（只有下面两项，问清这两项即可）

| 输入 | 说明 |
|---|---|
| 玩家的图，或一段文字描述 | 图更好。它是**表面**的来源：颜色、材质、五官、花纹 |
| 角色叫什么 | 用来给文件命名，例如 `duckcat`。文件就叫 `duckcat.glb` 与 `duckcat_rigged.glb` |

## 2. 模型从哪来（顺序：先建骨架，再产模型，最后装）

### ① 两张参考图（免费）

角色替换比武器、物品多一张参考图，这是本 SKILL 与它们的差别：

| 参考图 | 定什么 | 从哪来 |
|---|---|---|
| **玩家的图** | **表面**：颜色、材质、花纹、五官 | 玩家给。没有就只给文字描述 |
| **游戏参考图** | **轮廓、比例、姿态、整体风格** | 随本 SKILL 附带：`references/game-duck-front.png`（游戏里鸭子的正面照） |

**两张图的分工不得混。** 游戏参考图给形状与风格，玩家的图给表面。实测：把游戏鸭子的轮廓
与玩家那只猫的表面合起来，出来的是一只保持游戏比例、但长着猫的花纹与五官的角色。

### ② 先把 mod 骨架建好（免费，编译几秒）

从 `reference/character_model` 复制到你的 mod 目录，改 `info.ini` 的 `name` 与 `displayName`，
再改 `config.json` 里的 `model` 与 `scale`。先编译一次确认能过，再花钱生成模型。

### ③ 风格化预览图：把玩家的画风拉成游戏的画风

Tripo 会严格照图生成。这一步要把两张参考图合成一张**站立、正面、四肢分开**的预览图。

**下面那段 `prompt=` 是实测下来最贴合的默认配方**，就是基准。使用方式分三种：

| 玩家给了什么 | 怎么用这段配方 |
|---|---|
| **只给图**（没说别的） | **直接照用，一字不改** |
| **图，并说「再帮我改成……」** | 在这段配方基础上改，把玩家的要求加进去，并改掉冲突那句 |
| **只给文字描述**（没图） | 在这段配方基础上改，把描述写进外形与表面那两句，并保留站立、正面、居中三件 |

```
generate_image(
  image="<玩家的图>",                       // 只给文字时省略，此时纯提示词出图
  styleRef=".pi/skills/replace-character/references/game-duck-front.png",   // 游戏参考图：轮廓与风格
  prompt="A full-body front-view game character concept, standing, centered, plain transparent background. \
image[1] defines the BODY SILHOUETTE and proportions: a chubby upright cartoon duck - rounded egg-shaped body, \
very short thick legs, the head merged into the body with no neck, a small rounded crest on top of the head, \
simple mitten hands, both arms hanging down naturally at the sides. \
image[2] defines the SURFACE only: orange tabby cat fur with darker stripes, two big round black cat eyes, \
a small cat nose and mouth with whiskers, two small triangular cat ears, cat paws instead of the duck feet. \
Match the game-art style: clean low-poly game asset, flat cartoon shading, soft matte material, simple rounded shapes, \
no weapons, no clothes, no accessories. \
Use image[1] as the subject and silhouette reference; image[2] is the colour and surface reference.",
  out="your_mods/<目录名>/.preview/<角色名>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有五件事**不得丢**：**站姿**、**正面**、**居中**、**四肢与身体分开**、**身体轮廓照游戏参考图**。
- 姿势决定绑骨质量：坐姿、蜷成一团、四肢黏在一起的图**不得**送到下一步。
- 换个角色时只改「表面」那两句与外形描述，游戏参考图那句不改。
- 出图后**把文件路径给玩家**（工具返回里是绝对路径），再问他「就要这个吗」。
  图大于 1.5MB 时工具不内联显示，所以**必须写出路径**，不得写成「玩家会自动看到」。
- `.preview/` 放在 mod 目录下，`install_mod` 会跳过它。
- 不像就改 `prompt=` 再来一次，每次都便宜。**没确认前不得做 ④。**

### ④ 3D 模型

```
generate_model action=generate image=".preview/<角色名>.preview.png" out=".preview/<角色名>.glb" faceLimit=2000
```

判据：工具回报里出现 `bounds`，且尺寸与游戏角色同一个量级（0.5 米到 1.5 米）。
差得远就回第 ③ 步重画预览图，不得靠后处理硬拉。

### ⑤ 绑骨（先检查，再绑）

```
generate_model action=rig-check taskId=<上一步的 taskId> rigType=biped
generate_model action=rig       taskId=<上一步的 taskId> rigType=biped spec=mixamo out="<角色名>_rigged.glb"
```

三个必守项：

1. `rigType=biped`。人形以外的类型给不出人形关节名。
2. `spec=mixamo`。它给出标准 Mixamo 关节名（`mixamorig:Hips`、`mixamorig:Spine` 等）。
3. **结果的关节名必须逐个核对**。`rig-check` 只是建议，不代表绑骨成功。

判据：工具回报里列出关节名，必须能看到 `mixamorig:` 前缀，数量在 20 个上下。
看到 `tripo::` 名字说明绑的不是人形骨架，本次作废，回第 ③ 步重做。

### ⑥ 设置初始尺寸

运行库会按模型与游戏角色的真实身体高度自动缩放。
`config.json` 里的 `scale` 是乘在自动缩放上的额外倍率。
初次安装时使用 `scale: 1.2`。
这个经验值来自游戏角色高度与 Tripo 常见的约 1 米高模型，不是当前模型的实测结果。

玩家验收后觉得角色偏小或偏大时，再调整 `scale`。
骨架差距测量只用于尺寸异常的排查。
需要测量时，由维护者运行 `scripts/measure-skeleton-gap.mjs`。
正常制作流程不得要求玩家或 agent 执行该脚本。

### 省钱：哪些能复用，哪些不能

- **同一个模型**再要一次：直接复用已有的 `<角色名>_rigged.glb`，花费 0。不得重新生成。
- **同一个任务**要重新导出：用 `generate_model action=convert taskId=<原来的 taskId>`，比重新生成便宜。
- **生成前先读** mod 目录里的 `asset_manifest.jsonl`（只看最后几行），里面记着已有的模型与任务号。
- **骨架不得跨模型复用。** 骨架与骨权重是绑在**那个网格**上的，换一个新模型必须重新绑骨
  （约 25 积分）。可以省的是生成与导出那几步，不是这一笔。

### 成本一览

| 步骤 | 工具 | 积分 |
|---|---|---|
| ③ 风格化预览图 | `generate_image` | 约 10 |
| ④ 3D 模型（含格式转换） | `generate_model action=generate` | 约 50 |
| ⑤ 绑骨 | `generate_model action=rig` | 约 25 |
| | | **约 85** |

数字只在本表维护，别处不再重复写。

## 3. 建骨架与 config.json

在 mod 目录里放：`<mod名>.dll`、`0Harmony.dll`、`info.ini`、`config.json`、
`<角色名>_rigged.glb`（第 ⑤ 步绑好骨的那个）。
`<mod名>` 必须与第 ② 步确定的 mod 名一致。

```json
{
  "entries": [
    { "action": "replace_character", "model": "duckcat_rigged.glb", "scale": 1.20 }
  ]
}
```

`model` 写第 ⑤ 步产出的文件名。
初次安装时，`scale` 使用经验值 `1.2`。
玩家验收后觉得尺寸不合适时，再调整 `scale`。

## 4. 整理目录并装进游戏

安装前必须按运行时引用整理目录。不得按文件名判断文件是否需要。

1. `config.json` 或 mod 代码引用的文件必须留在原来的相对路径。
2. 未被运行时引用的中间模型、参考图和渲染图必须移入 `.preview/`。
3. `<角色名>.glb` 与 `<角色名>.raw.glb` 只有在未被运行时引用时才能移入 `.preview/`。
4. 移动已有生成文件后，必须同步更新 `asset_manifest.jsonl` 里对应的 `files` 路径。
5. 整理后运行 `validate_mod`，再运行 `install_mod`。

`.preview/` 里的文件仍可供以后重新生成、转换和排查使用。
`install_mod` 会跳过 `.preview/`。
`install_mod` 是整个目录替换。config 引用的模型缺失时，安装后的 mod 会缺少模型。

## 5. 验收（装好不等于成功）

`validate_mod` 与 `install_mod` 已经替你查了 `action`、字段与目录。剩下的只有一件事：**让玩家看**。

- 验收时分三项报告：游戏能加载本 mod；玩家角色的形状、尺寸与手臂符合要求；枪、背包与护甲仍显示。

- **让玩家做**：进游戏，走到有光的地方，**动一动、转个身**。
  装的时候游戏开着的话，先退出再进。
- 要看：**形状**像玩家给的那只角色（不拧、不涂抹）、**大小**与旁边的角色差不多、
  **手臂贴身不飘**。
- 手上与背上物品的位置由**游戏挂点**决定，模型影响不了，看着偏移是几何差，不是错位。
- 玩家说「对了」就完成。玩家说「不对」时，先问清**哪儿不对**（形状、大小、手臂、五官），再对 §6 表。

## 6. 常见错误（对照修正）

| 现象 | 原因与修正 |
|---|---|
| 模型没变 | 装的时候游戏开着。退出游戏再进一次 |
| 形状拧成一团 | 用的是逐骨重定姿态。当前实现是刚性放置，不得改回去 |
| 右手臂与身体脱开或掌心偏离 | 检查右手三根关节、`Hand.Soket.R` 与 `[CharacterArmIk]` 日志；不得把左手也接入握把 IK |
| 五官、头发一直露着 | 钩子没挂上，或保留名单写错。看日志里 `character model hooks` 与 `model hook hid` 两行 |
| 背包漂到身体后面 | 有位移作用在躯干上。当前实现不做逐骨落位，出现这一现象说明改动跑偏了 |
| 看起来偏小或偏大 | 先小幅调整 `scale`，再重新安装并验收。需要分析头部比例时，由维护者测量骨架差距 |
| 关节映射数很低（日志里 `joints mapped`） | 关节名不是 `mixamorig:*`。回第 ⑤ 步，确认 `rigType=biped` 与 `spec=mixamo` |
| 预览图不像玩家要的 | 改 `prompt=` 重出，不得直接进第 ④ 步 |

## 已知限制：这是**参考 mod 与框架**的当前限制

下面几条是**现在这套实现**的限制，不是不能解决的问题。**你若有更好的办法，写进回复**：
说清要改哪一段、为什么这样改。`libs/` 由维护者管理，agent 不得直接改。

1. **左右手臂行为不同。** 右手使用两骨 IK 对齐掌心握把；左手并入 `Spine.004`，不独立摆臂。
   两手使用同一握把 IK 会让左手在部分动画中翻转。
2. **姿势不完全对齐。** 模型保留自己的绑定姿势，游戏的动画增量叠在它上面。
3. **只搬顶点、法线、UV、三角面与骨权重。** 源模型若有第二套 UV 或顶点色会丢。
4. **挂在游戏挂点上的物品**（枪、背包、护甲）位置由游戏决定，模型侧无法调整。
