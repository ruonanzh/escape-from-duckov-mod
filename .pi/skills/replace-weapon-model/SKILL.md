---
name: replace-weapon-model
description: 用户想换某把武器的模型/外观时用（例："把 MP5 换成这把枪" / "给我这把枪做个皮肤" / "这个武器模型换成这个 glb"）。把 GLB 放进 mod、写一句 config、编译安装即可；模型可以先用 Tripo 生成（文本或图片 → GLB）。
---

# 替换武器模型

**目标**：用户手里那把武器（或指定某把武器）的**外观**换成用户提供的模型。不改数值、不改行为。

## 1. 模型从哪来（顺序：先建骨架 → 再产素材 → 最后装）

一句话也能做。顺序是「**先把 mod 骨架建好 → 再产素材 → 最后装**」—— 目录先存在、也先绑定好，后面直接往里写。

### ① 参考图（免费）

- 用户给了图 → 就用它（聊天里发的图会落盘，拿到的是**绝对路径**，直接传给工具）
- 图里带 **logo / 水印 / UI 文字** → **先裁掉再传**（实测：同一张图带 logo 被 `2008` 拒，去掉 logo 就通过）
- 用户只说了句话 → 跳过这步，直接进 ②
- 给了好几张 → **问用户用哪张**

### ② 先把 mod 骨架建好（免费，编译几秒）

- 照 **`reference/weapon_model/`**（示例 mod = 正确答案）建你自己的 mod 目录：拷它当起点，或按它自己写
- 四处名字要一致（**不一致的症状是 mod 静默不加载 ✗**）→ 明细见 §2
- 建完**先跑一次 `validate_mod`** 确认能编译（它会替你检查 name ↔ namespace 一致 ✓）
- **先建骨架、别等素材** —— 素材工具落盘时会自建目录，别让它抢在你前面把目录建出来
- 图标/模型下一步产出来再放进这个目录即可

### ③ 风格化预览图：**清理 + 居中 + 加粗**（约 5 积分）—— **先给用户看，等他确认**

⚠️ **本作的枪偏“厚实”**（枪管粗、机匣壮、枪托结实），而 Tripo 会**严格照图生成** ✗
→ 只清理不加粗，出来的模型就是“细长”的 ✗（实测中厚/长 0.136，本作原枪 0.226 ✗）。
所以这一步的提示词必须同时做两件事：**清理/居中 + 加粗**。

**⭐ 下面那段 `prompt=` 是实测下来最贴合本作风格的默认配方** —— 它就是基准，使用方式分三种：

| 用户给了什么 | 怎么用这段配方 |
|---|---|
| **只给图**（没说别的）| **直接照用 ✓**（一字不改 ✓）|
| **图 + “再帮我改成…/加点…”** | **在这段配方基础上改** ✓（把用户的要求加进去 / 把冲突那句改掉 ✓）|
| **只给文字描述**（没图）| **在这段配方基础上改** ✓（改成描述这把枪该长什么样 ✓，朝向/居中/加粗这三件保留 ✓）|

```
generate_image(
  image="<用户的图>",                  // 只给文字时省略（→ 纯提示词出图 ✓）
  prompt="Redraw the weapon from the input as a clean, isolated in-game asset: keep ONLY the main item - if the input shows several objects, take the largest, centered one and ignore the rest. The whole weapon alone and complete - keep EVERY part of it (stock, pad, magazine, sights, mounts); remove only what is not the weapon (the background, any floating pieces, text, frames). Put it on a plain uniform white background: no scenery, no props, no shadow, no smoke, no hands, no text. Keep the weapon's own design and colors the same, but render it in a CARTOON / toon-shaded style like the game's own assets (flat stylized colors, clean shading - not photoreal), and make it noticeably BULKIER and THICKER: a chunky barrel, a thick handguard and a stout stock - heavy, sturdy proportions like a stylized low-poly game asset. Side view with the barrel and muzzle pointing LEFT, and perfectly CENTERED in the frame, with a clear margin on all sides.",
  out="your_mods/<mod>/.preview/<名字>.preview.png")   // 中间图放 mod 目录下的 .preview/
```

- 这段配方里有 8 件事都**不能丢** ✗：**只留主物品**（图里有多个物体时取**最大最居中**那个 ✓ 其余全丢 ✗）· **整把武器都在**（部件一个不少 ✓）· **只移除不是武器的东西**（背景/飘浮物/文字/边框 ✓）· **纯白均一背景 + 无杂物** ✓ · **保持原设计/颜色 + 卡通渲染** ✓（不是照片感 ✗） · **加粗** ✓ · **朝向 LEFT** ✓ · **居中 + 留边** ✓
- 背景那两句的写法：`plain uniform white background` ✓ 或直接写 `transparent` ✓ **两种都实测有效** ✓（真 alpha、四角透明 ✓；白底那版半透明边缘略窄：0.19% vs 0.39% ✓）
- 改配方时**朝向/居中这两件不要弄丢** ✗（用户只说要改颜色，也别顺手把朝向删了 ✓）；如果用户就是要“细一点”，那是**改配方**（把加粗那句改掉 ✓），不是加一句反向要求就完事 ✗
- 出图后**把文件路径给用户**（工具返回里就是绝对路径 ✓）→ 问他「就要这个吗」
  ⚠️ 图大于 1.5MB 时工具**不会**内联显示 ✗（预览图一般 2.5–3.5MB ✓）→ 所以**必须把路径写出来** ✓，
  不要写成“玩家会自动看到”✗（他看不到 ✓ 会以为没生成 ✓）
- `.preview/` 放在 mod 目录下：`install_mod` 会跳过它（不进游戏），也不会把工作区弄脏
- 不像就改 `prompt=` 再来一次（每次都便宜）—— **没确认前不要做 ④**
- ⚠️ **贴图风格由用户的图决定** ✓：不要用“游戏风格参考图”去**改贴图**（会打破用户给的原有贴图 ✗）

### ④ 3D 模型（约 50 积分，含转换）

```
generate_model(action="generate", image=".preview/<名字>.preview.png",
               out="your_mods/<mod>/<武器名>.glb", faceLimit=3000)   // 目录在 ② 已建好；**模型名按武器起** ✓（如 ak103.glb ✓，别固定叫 gun.glb ✗）
```

- 用 ③ 那张**已确认的预览图**做输入 —— **图 → 3D 比纯文字准得多**（冷门型号尤其）
- 用户只要一句话、没图 → 用 `prompt="…"`（**提示词里也必须写朝向**）
- 用户自带 `.glb` → ③④⑤ 全跳过，直接把他的文件放进 `your_mods/<mod>/`

> **朝向只能靠提示词**：`..., the barrel and muzzle point to the LEFT, the stock is on the right`
> **"to the left" → 枪口落在 +Z = Unity 前向** → 进游戏不用再转
> （实测：提示词写 "points to the right" → 枪口落在 −Z ✗；用户自带的 GLB 没有这个保证 → 用 `config.json` 的 `front` 兜底 ✓）

### ⑤ 图标（约 5 积分；用 ④ 的渲染图则**免费**）

```
generate_image(model="your_mods/<mod>/<武器名>.glb",
               prompt="clean game inventory icon, side profile, centered, the weapon occupies about 80% of the frame width",
               out="your_mods/<mod>/icon.png")
```

- 图**原生就是透明的** ✓；运行时只做**尺寸对齐**（256² + PPU50 ✓）—— **不抠底、不裁方、也不调构图** ✗
  → 所以构图得自己写进提示词 ✓：**居中大致有效** ✓，**占宽控制不了** ✗（flare 总会顶满 —— 别为此反复重生成 ✗）

### 成本一览（合计 ≈ 70 积分）

预览图 `generate_image` **10** · 3D 模型 `generate_model` **50**（含转换）· 图标 `generate_image` **10**（用模型自带的渲染图则**免费**）

> **Tripo key**：放**单独的文件** `~/.gamer-agent-pi/api-keys.json` → `{ "tripo": "tsk_..." }`
> （用户级、所有游戏仓库共用、不进 git；**临时方案** —— 将来由 app 的「管理 API keys」界面接管）
> 没有 key 时工具会抛 `TRIPO_NO_API_KEY` —— 这时**向用户要一次**，写进去即可。
> 余额用 `generate_model(action="balance")` 看；**别反复重试刷积分**，不行就改提示词或问用户。

## 2. 建骨架与 config.json

**骨架自己建**（照 `reference/weapon_model/` —— 它就是"正确答案"的样例）；工具只负责**校验**（`validate_mod`）与**安装**（`install_mod`）。

**四处名字必须一致**（不一致的症状是 **mod 静默不加载**，游戏不报错）：

| 位置 | 取值（本例 `mygun`）|
|---|---|
| 目录名 | `your_mods/mygun/` |
| `info.ini` 的 `name` | `mygun` |
| `ModBehaviour.cs` 的 `namespace` | `mygun`，且类名必须是 `ModBehaviour` ← **`validate_mod` 会替你拦下这条** ✓ |
| `.csproj` 的 `AssemblyName` / `RootNamespace` | `mygun`（DLL 名保持一致，卫生）|

名字规则：**小写字母 + 数字、字母开头**（它会成为 C# 命名空间 —— 大写/下划线/中文都不行）。

**`config.json`**（运行时读它 ✓；两种写法，旧写法仍然有效 ✓）：

```json
// ① 一套素材换一批武器（扁平字段 = 一条规则 ✓ 旧写法 ✓）
{ "typeIDs": [655], "model": "mp5.glb" }

// ② 每把武器各换各的（一个 mod **多条规则** ✓ 按数组顺序匹配，**先命中的生效** ✓）
{ "entries": [
    { "targets": ["Item_SMG_MP5_Normal"], "model": "smg.glb", "icon": "smg_icon.png" },
    { "typeIDs": [655], "model": "mp5.glb", "front": "-x" } ] }
```

> ⚠️ 给了 `entries` 就**只看 entries** ✗（扁平字段被忽略 ✓）；两者不要混用 ✓

| 字段（每条规则都能用 ✓） | 说明 |
|---|---|
| `targets` | 要换的武器**对象名全等**（数组 ✓ 不区分大小写，例 `["Item_SMG_MP5_Normal"]`）|
| `typeIDs` | 或按 typeID（数组 ✓ 例 `[238, 655]`）—— **推荐 ✓**（与 `targets` 二选一 ✓）|
| `model` | GLB 文件名（**按武器起名** ✓ 如 `ak103.glb` —— 不要固定叫 `gun.glb` ✗；相对 mod 目录 ✓ 也可绝对路径）；**文件还没产出也没关系**，后面放进来即可 |
| `icon` | 图标文件名（默认 `icon.png`）；没有就只换模型、不换图标 |
| `front` | **只有用户自带的模型**才需要：`-z`/`+x`/`-x` 声明枪口朝向（`generate_model` 出的不用）|
| `size` | 修改大小（可选）。不写就用默认：手枪 0.44 ／ SMG 0.85 ／ 步枪 1.00 ／ 霰弹 1.05 ／ 战斗步枪 1.25 ／ 狙 1.35 ／ 机枪 1.45 |

**查准确名字 / typeID**：用 `inspect_game_data`（`class=Item` / `name=…` ✓）→ **建议直接用 `typeIDs`** ✓
（名字写错一个字符就**什么都不命中** ✗ —— 启动日志会打 `targets=[…] typeIDs=[…]` ✓ 对一眼就知道 ✓）

**`slots` / `pivot`（可选 ✓ 默认不写 ✓ 留给玩家自己调）**

我们模型的**比例**和游戏那把不一样 ✓ → 配件落点 / 手抓点只能**大致对** ✓，想更准就加 `slots` ✓：

```json
"slots": { "Muzzle": [0.995, 0.800, 0.670], "Stock": [0.020, 0.600, 0.670],
           "Grip": [0.600, 0.637, 0.670], "pivot": [0.320, 0.490, 0.660] }   // 其余槽位同理（Scope / Tec）
```

- `[L, H, D]` = **0~1 的比例**（相对模型自己的包围盒 ✓）：**L 沿 Z**（0=枪托 → 1=枪口）· **H 沿 Y**（0=底 → 1=顶）· **D 沿 X**（0.5=中间）
- **只有装了对应配件才看得出效果** ✓（没瞄具时改 `Scope` 看不到 ✓）；**存盘立刻生效** ✓（热重载 ✓）
- 顺序建议 ✓ **先 `pivot`**（整枪在手里的位置 ✓）→ 再调槽位 ✓ · 日志可对照 `对齐=slots.pivot；挂点已搬 N 个；槽位(局部米)=…` ✓
- **不写也完全能跑** ✓（走默认"按原枪包围盒映射" ✓ 只是没那么准 ✓）

**要换多把武器？先分清是哪种 ✓：**

- **同一套素材**换多把（同族皮肤 ✓）→ 写**多个** `typeIDs`（例 `[238, 239]` ✓）或 `targets`（全等名 ✓）✓
  —— `targets` **只做全等** ✓ **不能命中一批** ✗（原来那套子串匹配会误伤 ✗）—— **不用 entries** ✓
- **各换各的**（不同模型/图标 ✓）→ 用 `entries` ✓，**一把枪一个 `model` + 一个 `icon`** ✓
  ⚠️ 这意味着素材要做 **N 套** ✓（每套：模型 50~60 积分 + 图标 5 ✓）→ **先跟用户确认要换几把、再开工** ✓
  ⚠️ 各条规则的 `model` 是**同一个 mod 目录里的不同文件名** ✓（`ak.glb` / `mp5.glb` ✓ 互不覆盖 ✓）

**这个能力要引的 mod-kit 文件（5 个都要）**：

| 文件 | 干嘛的 |
|---|---|
| `libs/mod-kit/GltfLoader.cs` | 读 GLB → `Mesh` + `Texture2D`（坐标/绕序/UV 转换、握把归零、按 `front` 转朝向）|
| `libs/mod-kit/WeaponModel.cs` | 把模型换到**手持武器**上（选锚点 / 藏旧零件 / 对齐 / 克隆游戏材质换贴图）|
| `libs/mod-kit/WeaponIcon.cs` | 换武器图标（抠白底 + 居中 + 缩 256² + PPU50）|
| `libs/mod-kit/GameApi.cs` | 找玩家与手持物（`FindMainCharacter` / `EnsureHandheldAgent` …）|
| `libs/mod-kit/Json.cs` | 读 `config.json`（不用引第三方 JSON 库）|

两种引法（挑一种）：
- 抄 `reference/weapon_model/WeaponModelSwap.csproj` 里那 5 行 `<Compile Include="...">`，路径按**你的 mod 目录**算
- 或把这 5 个文件**拷进你的 mod 目录**（自包含，路径最省事）

**接口**（完整签名：`rg "public static" libs/mod-kit/`）：

| 调用 | 作用 |
|---|---|
| `GltfLoader.LoadFile(path, front)` → `Loaded { Mesh, MainTexture, VertexCount, TriangleCount, Report }` | 读 GLB；`front` 用 `"auto"`，用户自带的模型按 `config.json` 传 `"-z"`/`"+x"`/`"-x"` |
| `WeaponModel.Apply(anchorRoot, mesh, texture)` → `Result { Applied, Instance, Report }` | 换到手持武器上 —— **`reference/weapon_model/ModBehaviour.cs` 里有完整用法，照抄即可** |
| `WeaponIcon.Apply(item, "icon.png")`（有图标时才调）| 换图标（幂等，内部自己归一化尺寸）|
| `Json.Parse(text)` → `GetStr/GetInt/GetFloat(key, fallback)` | 读 `config.json` 字段 |

**要用 Harmony**（自己改数值/行为时）：再加 `libs/0Harmony.dll` 的引用 —— 照 `reference/example_mod/ExampleMod.csproj`（它是 `<Private>true</Private>`，会随 mod 自带）。

## 3. 装进游戏

用 `install_mod`（会校验 + 拷到游戏的 Mods 目录）。装完**按 §4 确认** —— 装好 ≠ 成功。

**装好之后改 `config.json` 不用重启游戏** ✓（mod 会盯着它的写入时间 ✓）：
- ✓ `target` / `front` 改完存盘 → 立刻生效
- ✓ `size` 改完存盘 → 立刻生效
- ✓ `model` 换成**另一个文件名** → 会重新读模型
- ✗ `model` **同名**换了文件内容（例如重新生成同名 `ak103.glb`）→ **不会**重读 → 要重启游戏
- ✗ **图标**不会重设 → 换图标要重启游戏

## 4. 怎么确认成功（装好 ≠ 成功）

`install_mod` 返回 PASS 只说明**文件装好了**，不代表游戏里生效。要这样确认：

- **要用户做**：进游戏 → 背包切到目标武器 → 拿出来（2 秒内模型应换掉）。装的时候游戏如果开着，先退出再进。
- **你自己做**：读游戏日志里的 `[WeaponModel]` 行 —— 有这两行才算真生效：
  - macOS：`~/Library/Logs/TeamSoda/Duckov/Player.log`
  - Windows：`%USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log`
  - `手里的物品：'SMG_MP5_Normal' … 命中`
  - `已换模型：隐藏旧零件 N 个；锚点=…；材质=…`
  看不到 = 没生效 → 去 §5 对照。
- **要用户看**：模型形状、贴图、枪口朝前、手握住握把。

## 5. 常见错误（对照修正）

> 工具的错误信息里**通常已经带了下一步** ✓ —— **先照它说的做**；下面只列**工具没说**的（能力特有的）✓

- `code=2008`（内容审核）→ **先试：把图里的 logo / 水印 / UI 文字裁掉再传**（实测同一张图去掉 logo 就通过）；仍被拒再去掉 `image=` 改用 `prompt=` 纯文字，或换一张图，或用用户自带的 `.glb`（别改措辞反复重试）。
- `TRIPO_TASK_FAILED` → 生成失败，改提示词或换参考图重来。
- `TRIPO_FILE_NOT_FOUND` → **它指的那个文件不存在**（`image=` / `model=` 传进来的路径写错了）。
- 形状对但**握把偏** → 看日志两侧包围盒；`WeaponModel.Apply(..., extraOffset)` 可微调（一般不用）。
- **贴图没上** → GLB 里没有 baseColor 贴图（重新 `texture_model` 或直接用带贴图的 GLB）。
- **模型整块看不见** → 绕序/UV 问题（加载器已按左手系转换）→ 改提示词或换参考图重生成。
- **前后反了** → 提示词朝向写反了（写 "left"）；**用户自带的模型**没这个保证 → 在 `config.json` 里写 `"front": "-z"`（或 `+x`/`-x`）。
- 日志没有 `[WeaponModel]` → mod 没装成功或名字对不上（`<mod名>.ModBehaviour`）→ 用 `validate_mod` / `install_mod` 的返回确认。
