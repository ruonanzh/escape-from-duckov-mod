---
name: replace-gun-model
description: 玩家想换某把**枪械**的模型或外观时用（例如「把 MP5 换成这把枪」「给我这把枪做个皮肤」「这个武器模型换成这个 glb」）。把 GLB 放进 mod，写一句 config，编译安装即可。模型可以先用 Tripo 生成（文本或图片到 GLB）。
---

# 替换枪械模型

**目标**：把玩家手里那把**枪械类**武器（或指定某把枪）的**外观**换成玩家提供的模型。不改数值，也不改行为。

> **先看 prefab，再信库的判据**：`libs/mod-kit` 对「哪块是枪身、哪些是旧外观、哪个是挂点」的判断，是对**实测过的 prefab** 归纳出的**最大可能性**，不保证覆盖你的目标。实测 91 把枪里 13 把的名字里连 `WPN_` 都没有（另有 `WPM_`、`Arrow`、父节点自己带渲染器这类结构）。动手前用 `inspect_game_data action=transform --pattern <目标模型名> --depth 3` 看清渲染器名、父子关系与尺寸（prefab 直接查；场景里的对象要加 `file=levelN`）；库的判断与结构冲突时**以 prefab 为准**，并在 mod 的 README 记一笔。

## 1. 模型从哪来（顺序：先建骨架，再产模型，最后装）

一句话也能做。顺序是「**先建 mod 骨架，再产出模型，最后装**」。目录先存在，也先绑定好，后面直接往里写。

### ① 玩家参考图（免费）

- 玩家给了图，就直接用它。聊天里发的图会落盘，拿到的是**绝对路径**，直接传给工具。
- 图里带 **logo、水印或 UI 文字**时，**必须先裁掉再传**。实测：同一张图带 logo 被 `2008` 拒，去掉 logo 就通过。
- 玩家只说了句话，就跳过这步，直接进 ②。
- 给了好几张时，**问玩家用哪张**。

### ② 先把 mod 骨架建好（免费，编译几秒）

- 照 **`reference/weapon_model/`**（示例 mod 等于正确答案）建你自己的 mod 目录：拷它当起点，或按它自己写。
- 三个名字必须一致：`info.ini` 的 mod 名、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace。不一致的症状是 mod 静默不加载。明细见 §5。
- 建完**先跑一次 `validate_mod`** 确认能编译。它会替你检查 mod 名与 namespace 一致。
- **先建骨架，不得等模型**。模型工具落盘时会自建目录，不得让它抢在你前面把目录建出来。
- 图标与模型下一步产出来，再放进这个目录即可。

### ③ 风格化预览图：**清理、居中、加粗**

**本作的枪偏「厚实」**（枪管粗、机匣壮、枪托结实），而 Tripo 会**严格照图生成**。
只清理不加粗，出来的模型就是「细长」的（实测中厚比长 0.136，本作原枪 0.226）。
所以这一步的提示词必须同时做两件事：**清理并居中，再加粗**。

**下面那段 `prompt=` 是实测下来最贴合本作风格的默认配方**。它就是基准，使用方式分三种：

| 玩家给了什么 | 怎么用这段配方 |
|---|---|
| **只给图**（没说别的）| **直接照用。一字不改。** |
| **图，并说「再帮我改成…」或「加点…」** | **在这段配方基础上改。** 把玩家的要求加进去，并改掉冲突那句。 |
| **只给文字描述**（没图）| **在这段配方基础上改。** 改成描述这把枪该长什么样，并保留朝向、居中、加粗这三件。 |

```
generate_image(
  image="<玩家的图>",                  // 只给文字时省略。此时纯提示词出图。
  prompt="Redraw the weapon from the input as a clean, isolated in-game asset: keep ONLY the main item - if the input shows several objects, take the largest, centered one and ignore the rest. The whole weapon alone and complete - keep EVERY part of it (stock, pad, magazine, sights, mounts); remove only what is not the weapon (the background, any floating pieces, text, frames). Put it on a plain uniform white background: no scenery, no props, no shadow, no smoke, no hands, no text. Keep the weapon's own design and colors the same, but render it in a CARTOON / toon-shaded style like the game's own assets (flat stylized colors, clean shading - not photoreal), and make it noticeably BULKIER and THICKER: a chunky barrel, a thick handguard and a stout stock - heavy, sturdy proportions like a stylized low-poly game asset. Side view with the barrel and muzzle pointing LEFT, and perfectly CENTERED in the frame, with a clear margin on all sides.",
  out="your_mods/<目录名>/.preview/<名字>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有 8 件事都**不得丢**：**只留主物品**（图里有多个物体时取**最大最居中**那个，其余全丢）、**整把武器都在**（部件一个不少）、**只移除不是武器的东西**（背景、飘浮物、文字、边框）、**纯白均一背景，无杂物**、**保持原设计与原颜色，并做卡通渲染**（不是照片感）、**加粗**、**朝向 LEFT**、**居中并留边**。
- 背景那两句的写法：`plain uniform white background` 或 `transparent`。**两种都实测有效**（真 alpha、四角透明；白底那版半透明边缘略窄：0.19% 对 0.39%）。
- 改配方时**不得弄丢朝向与居中**。玩家只说要改颜色，也不得顺手把朝向删了。如果玩家就是要「细一点」，那是**改配方**（把加粗那句改掉），不是加一句反向要求就完事。
- 出图后**把文件路径给玩家**（工具返回里就是绝对路径），再问他「就要这个吗」。
  **图大于 1.5MB 时工具不会内联显示**（预览图一般 2.5–3.5MB），所以**必须把路径写出来**。
  不得写成「玩家会自动看到」。他看不到，会以为没生成。
- `.preview/` 放在 mod 目录下。`install_mod` 会跳过它（不进游戏），也不会把工作区弄脏。
- 不像就改 `prompt=` 再来一次。每次都便宜。**没确认前不得做 ④**。
- **贴图风格由玩家的图决定**。不得用「游戏风格参考图」去**改贴图**，那会打破玩家给的原有贴图。

### ④ 3D 模型

```
generate_model(action="generate", image=".preview/<名字>.preview.png",
               out="your_mods/<目录名>/<武器名>.glb", faceLimit=3000)   // 目录在 ② 已建好；**模型名按武器起**（如 ak103.glb），不得固定叫 gun.glb
```

- 用 ③ 那张**已确认的预览图**做输入。**图到 3D 比纯文字准得多**（冷门型号尤其）。
- 玩家只要一句话、没图时，用 `prompt="…"`。**提示词里也必须写朝向**。
- 玩家自带 `.glb` 时，③④⑤ 全跳过，直接把他的文件放进 `your_mods/<目录名>/`。

生成前先读 mod 目录里的 `asset_manifest.jsonl`（只看最后几行）：同一个模型不得重复生成，
同一个任务可以用 `generate_model action=convert taskId=<原来的 taskId>` 重新导出，比重新生成便宜。

> **朝向只能靠提示词**：`..., the barrel and muzzle point to the LEFT, the stock is on the right`
> **「to the left」让枪口落在 +Z，也就是 Unity 前向**。进游戏不用再转。
> 实测：提示词写「points to the right」时枪口落在 −Z。玩家自带的 GLB 没有这个保证，用 `config.json` 的 `front` 兜底。

### ⑤ 图标

```
generate_image(model="your_mods/<目录名>/<武器名>.glb",
               prompt="clean game inventory icon, side profile, centered, the weapon occupies about 80% of the frame width",
               out="your_mods/<目录名>/icon.png")
```

- 图**原生就是透明的**。运行时只做**尺寸对齐**（256² 与 PPU50），**不抠底、不裁方、也不调构图**。
  所以构图必须自己写进提示词。**居中大致有效**，**占宽控制不了**（flare 总会顶满）。不得为此反复重生成。

### 成本一览（合计 ≈ 70 积分）

| 步骤 | 工具 | 积分 | 备注 |
|---|---|---|---|
| ③ 预览图 | `generate_image` | **10** | 先给玩家看，**等他确认**再往下走 |
| ④ 3D 模型 | `generate_model` | **50** | 含转换 |
| ⑤ 图标 | `generate_image` | **10** | 用 ④ 的**模型渲染图**则**免费** |
| | | **≈ 70** | **数字只在本表维护**，别处不再重复写 |

> **Tripo key**：放**单独的文件** `~/.gamer-agent-pi/api-keys.json`，内容 `{ "tripo": "tsk_..." }`。
> 它是本机的文件，所有游戏仓库共用，不进 git。**这是临时方案**，将来由 app 的「管理 API keys」界面接管。
> 没有 key 时工具会抛 `TRIPO_NO_API_KEY`。这时**向玩家要一次**，写进去即可。
> 余额用 `generate_model(action="balance")` 看。**不得反复重试刷积分**，不行就改提示词或问玩家。

## 2. 建骨架与 config.json

**骨架自己建**（照 `reference/weapon_model/`，它就是「正确答案」的样例）。工具只负责**校验**（`validate_mod`）与**安装**（`install_mod`）。

**工作区更新后，`reference/` 的模板可能变了。** 用模板覆盖自己的 `ModBehaviour.cs` 之前，
先把原文件备份。覆盖会丢掉你写过的改动。

**名字与类名那几条不一致会静默不加载**。明细见 §5。

**`config.json`**（运行时读它。两种写法，旧写法仍然有效）：

```json
// ① 一套模型换一批武器（扁平字段等于一条规则，旧写法）
{ "typeIDs": [655], "model": "mp5.glb" }

// ② 每把武器各换各的（一个 mod **多条规则**，按数组顺序匹配，**先命中的生效**）
{ "entries": [
    { "targets": ["Item_SMG_MP5_Normal"], "model": "smg.glb", "icon": "smg_icon.png" },
    { "typeIDs": [655], "model": "mp5.glb", "front": "-x" } ] }
```

> 给了 `entries` 就**只看 entries**（扁平字段被忽略）。两者不得混用。

| 字段（每条规则都能用） | 说明 |
|---|---|
| `targets` | 要换的武器**对象名全等**（数组，不区分大小写。例 `["Item_SMG_MP5_Normal"]`）|
| `typeIDs` | 或按 typeID（数组。例 `[238, 655]`）。**推荐**，与 `targets` 二选一 |
| `model` | GLB 文件名（**按武器起名**，如 `ak103.glb`，不得固定叫 `gun.glb`。相对 mod 目录，也可绝对路径）。**文件还没产出也没关系**，后面放进来即可 |
| `icon` | 图标文件名（默认 `icon.png`）。没有就只换模型，不换图标 |
| `front` | **只有玩家自带的模型**才需要：用 `-z`、`+x` 或 `-x` 声明枪口朝向。`generate_model` 出的不用 |
| `size` | 修改大小（可选）。不写就用默认：手枪 0.44 ／ SMG 0.85 ／ 步枪 1.00 ／ 霰弹 1.05 ／ 战斗步枪 1.25 ／ 狙 1.35 ／ 机枪 1.45 |

**查准确名字与 typeID**：用 `inspect_game_data`（`class=Item` 与 `name=…`）。**建议直接用 `typeIDs`**。
名字写错一个字符就**什么都不命中**。启动日志会打 `targets=[…] typeIDs=[…]`，对一眼就知道。

**`slots` 与 `pivot`（可选。默认不写，留给玩家自己调）**

我们模型的尺寸和游戏那把不同。配件落点与手抓点只能大致对上。要更准就写 `slots`。

```jsonc
// 单位米，在我们模型的局部空间里。下面是形状示例，数值不通用。
"slots": { "muzzle": [ 0.32, 0.01,  0.00], "stock": [-0.34, 0.00,  0.00],
           "grip":   [ 0.06,-0.05,  0.00], "pivot": [-0.08,-0.02,  0.00] }
```

- 每个值是 `[x, y, z]`，单位是**米**，在**我们模型的局部空间**里。可以写负数。
  `x` 沿 X（左右）· `y` 沿 Y（上下）· `z` 沿 Z（枪口方向）。
- **枪的这个空间经过握把归零**。零点在**握把点**，不在 glb 的原点。
  游戏侧由 `GltfLoader` 的 `zeroGrip` 决定。枪默认 `true`，参照 mod 不传它。
  只有 mod 自己传 `zeroGrip: false` 时，零点才是 glb 的原点。近战与物品固定不做归零。
- 只有装了对应配件，才看得出那个槽位的效果。存盘立刻生效。
- 顺序：先调 `pivot`（整枪在手里的位置），再调槽位。日志里看 `align=slots.pivot`。
- 不写 `slots` 也可以跑。这时走「按原枪包围盒映射」，只是没那么准。

**要换多把武器时，先分清是哪种：**

- **同一套模型**换多把（同族皮肤）：写**多个** `typeIDs`（例 `[238, 239]`）或 `targets`（全等名）。
  `targets` **只做全等**，**不得命中一批**（原来那套子串命中会误伤）。**不用 entries**。
- **各换各的**（不同模型与图标）：用 `entries`，**一把枪一个 `model` 与一个 `icon`**。
  这意味着要做 **N 套**（每套的成本见「成本一览」）。**先跟玩家确认要换几把，再开工**。
  各条规则的 `model` 是**同一个 mod 目录里的不同文件名**（`ak.glb`、`mp5.glb`，互不覆盖）。

两种引法（挑一种）：

- **csproj 加一行通配**：`<Compile Include="../../libs/mod-kit/*.cs" />`（照 `reference/weapon_model/` 就行。以后 kit 加文件也不用再改 csproj）
- 或把这些文件**拷进你的 mod 目录**（自包含，路径最省事）

**接口**（完整清单见 [`docs/mod-kit-api.md`](../../../docs/mod-kit-api.md)。签名用 `rg "public static" libs/mod-kit/` 现查）：

| 调用 | 作用 |
|---|---|
| `GltfLoader.LoadFile(path, front)` 返回 `Loaded { Mesh, MainTexture, VertexCount, TriangleCount, Report }` | 读 GLB。`front` 用 `"auto"`；玩家自带的模型按 `config.json` 传 `"-z"`、`"+x"` 或 `"-x"` |
| `WeaponModel.Apply(anchorRoot, mesh, texture)` 返回 `Result { Applied, Instance, Report }` | 换到手持武器上。**`reference/weapon_model/ModBehaviour.cs` 里有完整用法，照抄即可** |
| `WeaponIcon.Apply(item, "icon.png")`（有图标时才调）| 换图标（幂等，内部自己归一化尺寸）|
| `Json.Parse(text)` 返回 `GetStr/GetInt/GetFloat(key, fallback)` | 读 `config.json` 字段 |

**要用 Harmony**（自己改数值或行为时）：再加 `libs/0Harmony.dll` 的引用。照 `reference/example_mod/ExampleMod.csproj`（它是 `<Private>true</Private>`，会随 mod 自带）。

## 3. 装进游戏

用 `install_mod`（会校验，再拷到游戏的 Mods 目录）。装完**按 §4 确认**。装好不等于成功。

**装好之后改 `config.json` 不用重启游戏**（mod 会盯着它的写入时间）：

- `target` 与 `front` 改完存盘，立刻生效。
- `size` 改完存盘，立刻生效。
- `model` 换成**另一个文件名**，会重新读模型。
- `model` **同名**换了文件内容（例如重新生成同名 `ak103.glb`），**不会**重读。必须重启游戏。
- **图标**不会重设。换图标必须重启游戏。

## 4. 怎么确认成功（装好不等于成功）

`validate_mod` 与 `install_mod` 已经替你查了**名字、类名与目录**。剩下的只有一件事：**让玩家看**。

- **让玩家做**：进游戏，背包切到目标武器，再**拿出来**（模型应立刻变）。
  装的时候游戏开着的话，先退出再进。
  要看：形状对、贴图在、**枪口朝前**、手握在握把。
- 玩家说「变了、对的」就完成。
- 玩家说「没变」或「不对」时，先问清**哪儿不对**（没变、形状、贴图、朝向），再对 §5 表。

## 5. 常见错误（对照修正）

> 工具的错误信息里**通常已经带了下一步**。**先照它说的做**。下面只列**工具没说**的（能力特有的）。

- `code=2008`（内容审核）：**先试把图里的 logo、水印与 UI 文字裁掉再传**（实测同一张图去掉 logo 就通过）。仍被拒时，去掉 `image=` 改用 `prompt=` 纯文字，或换一张图，或用玩家自带的 `.glb`。不得改措辞反复重试。
- `TRIPO_TASK_FAILED`：生成失败。改提示词或换玩家参考图重来。
- `TRIPO_FILE_NOT_FOUND`：**它指的那个文件不存在**（`image=` 或 `model=` 传进来的路径写错了）。
- 形状对但**握把偏**：让玩家说偏多少。`WeaponModel.Apply(..., extraOffset)` 可微调（一般不用）。
- **贴图没上**：GLB 里没有 baseColor 贴图。重新 `texture_model`，或直接用带贴图的 GLB。
- **模型整块看不见**：绕序或 UV 问题（加载器已按左手系转换）。改提示词或换玩家参考图重生成。
- **前后反了**：提示词朝向写反了（写成了 "left"）。**玩家自带的模型**没这个保证，在 `config.json` 里写 `"front": "-z"`（或 `+x`、`-x`）。
- **什么都没变化**：三个名字**不一致**（`info.ini` 的 name、csproj 的 `AssemblyName`、`ModBehaviour.cs` 的 namespace），或类名不是 `ModBehaviour`。改成同一个名字。`validate_mod` 会拦。namespace 是合法 C# 标识符，通常 PascalCase。照 `reference/weapon_model/` 抄最省事。

## 6. 把模型路径交给玩家（让他自己调位置）

装完之后，在回话里给出**模型文件的路径**（`your_mods/<mod 目录>/<模型文件>.glb`），并说明：

> 你可以用 3D 编辑器打开它，自己调两件事：模型相对游戏角色的落点（`pivot`），以及各配件挂点（`slots`）。改完点「把结果发给 agent」，让 agent 重新安装就生效。

- **不写 `slots` 会怎样**：配件停在**游戏原枪**的挂点上。模型形状不同就会浮空或陷进枪身。
- 玩家提「瞄具往前一点」这类要求时，改的是 `slots` 里那一行的米数，不用重新生成模型。
