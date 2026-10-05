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
- 用户只说了句话 → 跳过这步，直接进 ②
- 给了好几张 → **问用户用哪张**

### ② 先把 mod 骨架建好（免费，编译几秒）

- 照 **`reference/weapon_model/`**（示例 mod = 正确答案）建你自己的 mod 目录：拷它当起点，或按它自己写
- 四处名字要一致（**不一致的症状是 mod 静默不加载 ✗**）→ 明细见 §2
- 建完**先跑一次 `validate_mod`** 确认能编译（它会替你检查 name ↔ namespace 一致 ✓）
- **先建骨架、别等素材** —— 素材工具落盘时会自建目录，别让它抢在你前面把目录建出来
- 图标/模型下一步产出来再放进这个目录即可

### ③ 风格化预览图（约 5 积分）—— **先给用户看，等他确认**

```
generate_image(
  image="<用户的图>",                  // 没图就省略 -> 纯提示词出图
  styleRef="<游戏内武器的图>",           // 强烈建议：游戏里已有武器的图 -> 出得像本作
  style="..., the barrel and muzzle point to the LEFT, the stock is on the right",
  out=".preview/<名字>.preview.png")    // 中间图一律进 .preview/，不会被装进游戏
```

- 出图后**自己先看**（读这张图）→ 再**给用户看，问他「就要这个吗」**
- 不像就改 `style` 再来一次（每次都便宜）—— **没确认前不要做 ④**

### ④ 3D 模型（约 50 积分，含转换）

```
generate_model(action="generate", image=".preview/<名字>.preview.png",
               out="your_mods/<mod>/gun.glb", faceLimit=3000)   // 目录在 ② 已建好
```

- 用 ③ 那张**已确认的预览图**做输入 —— **图 → 3D 比纯文字准得多**（冷门型号尤其）
- 用户只要一句话、没图 → 用 `prompt="…"`（**提示词里也必须写朝向**）
- 用户自带 `.glb` → ③④⑤ 全跳过，直接把他的文件放进 `your_mods/<mod>/`

> **朝向只能靠提示词**：`..., the barrel and muzzle point to the LEFT, the stock is on the right`
> **"to the left" → 枪口落在 +Z = Unity 前向** → 进游戏不用再转
> （完整数据：doc 仓 `docs/unity-3d-assets/03-tripo-api.md` §8.8 / §8.9 / §11）

### ⑤ 图标（约 5 积分；用 ④ 的渲染图则**免费**）

```
generate_image(model="your_mods/<mod>/gun.glb", styleRef="<游戏里那把武器的卡片图标>",
               style="clean game inventory icon, side profile, pure white background, no shadow, centered",
               out="your_mods/<mod>/icon.png")
```

- **尺寸/抠白/居中由游戏运行时自动处理**（256² + PPU50）→ 不要自己裁
- 不写 `style` → 直接用模型自己的渲染图（免费）；没有 `icon.png` 就只换模型、不换图标

### 成本一览（合计 ≈ 60 积分）

预览图 `generate_image` **5** · 3D 模型 `generate_model` **50**（含转换）· 图标 `generate_image` **5**（用模型自带的渲染图则**免费**）

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

**`config.json`**（运行时读它；键名就是这几个）：

| 字段 | 说明 |
|---|---|
| `target` | 要换的武器名的一段（`MP5` 会匹配 `SMG_MP5_Normal`）|
| `typeIDs` | 或精确匹配，如 `[238]`（与 `target` 二选一）|
| `model` | GLB 文件名（`gun.glb`，相对 mod 目录）；**文件还没产出也没关系**，后面放进来即可 |
| `icon` | 图标文件名（默认 `icon.png`）；没有就只换模型、不换图标 |
| `front` | **只有用户自带的模型**才需要：`-z`/`+x`/`-x` 声明枪口朝向（`generate_model` 出的不用）|

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

> **图标这步**：`generate_image(model="…/gun.glb", out="…/icon.png")` ✓
> - 不给 `style` → 直接用模型自己的渲染图 ✓（免费 ✓ 形状与游戏里一致 ✓）
> - 给 `style` = `styleRef`（游戏内图标）→ 重画一张更像本作的 ✓（约 5 积分 ✓）
> - **尺寸/抠白/居中由游戏运行时自动处理** ✓（256² + PPU50）→ 不用自己裁 ✗



用 `install_mod` 工具（会校验 + 装到 Mods 目录）。

## 4. 怎么确认成功（装好 ≠ 成功）

`install_mod` 返回 PASS 只说明**文件装好了**，不代表游戏里生效。要这样确认：

- **要用户做**：进游戏 → 背包切到目标武器 → 拿出来（2 秒内模型应换掉）。装的时候游戏如果开着，先退出再进。
- **你自己做**：读 `~/Library/Logs/TeamSoda/Duckov/Player.log` 里的 `[WeaponModel]` 行 —— 有这两行才算真生效：
  - `手里的物品：'SMG_MP5_Normal' … 命中`
  - `已换模型：隐藏旧零件 N 个；锚点=…；材质=…`
  看不到 = 没生效 → 去 §5 对照。
- **要用户看**：模型形状、贴图、枪口朝前、手握住握把。

## 5. 常见错误（对照修正）

- `TRIPO_NO_API_KEY` → 向用户要一次 Tripo key，存到 `~/.gamer-agent-pi/api-keys.json`。
- `code=2008`（内容审核）→ 去掉 `image=` 改用 `style=` 纯文字重试；还不行就换一张图，或用用户自带的 `.glb`（别改措辞反复重试）。
- `TRIPO_RATE_LIMIT` / `code=2000` → 发得太密，等 30–60 秒再发（别循环重试）。
- `TRIPO_TASK_FAILED` → 生成失败，改提示词或换参考图重来。
- `TRIPO_FILE_NOT_FOUND` → 路径不对（`generate_model` 的 `out=` 或 `config.json` 的 `model`）。
- 形状对但**握把偏** → 看日志两侧包围盒；`WeaponModel.Apply(..., extraOffset)` 可微调（一般不用）。
- **贴图没上** → GLB 里没有 baseColor 贴图（重新 `texture_model` 或直接用带贴图的 GLB）。
- **模型整块看不见** → 绕序/UV 问题（加载器已按左手系转换）→ 改提示词或换参考图重生成。
- **前后反了** → 提示词朝向写反了（写 "left"）；**用户自带的模型**没这个保证 → 在 `config.json` 里写 `"front": "-z"`（或 `+x`/`-x`）。
- 日志没有 `[WeaponModel]` → mod 没装成功或名字对不上（`<mod名>.ModBehaviour`）→ 用 `validate_mod` / `install_mod` 的返回确认。
