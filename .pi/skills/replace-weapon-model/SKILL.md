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

- **先调 `create_mod_folder`**（建目录 + 把这个会话绑上去；绑定之后才写得动里面的文件）：
  `create_mod_folder(name="mygun")`
- 再建骨架：`create_mod(kind="replace-weapon-model", name="mygun", target="MP5", model="gun.glb")`
  （`model=` 只是写进 config 的文件名，**文件还没有也没关系**；参数表见 §2）
- ⚠️ **名字只能用小写字母 + 数字、且字母开头**（如 `mygun`）—— 大写会被 `create_mod_folder` 拒，下划线会被 `create_mod` 拒。
- 图标/模型下一步产出来再放进去即可。

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

### 成本一览

| 步 | 工具 | 约 |
|---|---|---|
| ② 骨架 | `create_mod` | 0 |
| ③ 预览图 | `generate_image` | 5 |
| ④ 3D 模型 | `generate_model` | 50 |
| ⑤ 图标 | `generate_image` | 5（或 0）|

> **Tripo key**：放**单独的文件** `~/.gamer-agent-pi/api-keys.json` → `{ "tripo": "tsk_..." }`
> （用户级、所有游戏仓库共用、不进 git；**临时方案** —— 将来由 app 的「管理 API keys」界面接管）
> 没有 key 时工具会抛 `TRIPO_NO_API_KEY` —— 这时**向用户要一次**，写进去即可。
> 余额用 `generate_model(action="balance")` 看；**别反复重试刷积分**，不行就改提示词或问用户。

## 2. 做 mod（**一条工具调用**）

```
create_mod(kind="replace-weapon-model", name="mygun", target="MP5", file="gun.glb")
```
它会：拷模板 → **把四处名字改成一致**（目录 / `info.ini` 的 name / `.csproj` 的 AssemblyName+RootNamespace /
`ModBehaviour.cs` 的 namespace —— 游戏要求 `<mod名>.ModBehaviour` 类型 ✓）→ 写 `config.json` → 编译 ✓

| 参数 | 说明 |
|---|---|
| `name` | mod 名：**小写字母+数字、字母开头**（如 `mygun` —— 大写会被 `create_mod_folder` 拒 ✗，下划线会被本工具拒 ✗）；会成为命名空间/程序集名 |
| `target` | 要换的武器名一段（`MP5` 会匹配 `SMG_MP5_Normal`）；或 `typeIDs=[655]` 精确匹配 |
| `file` | GLB 文件名（默认 `gun.glb`；先把模型放到它说的目录里 ✓）|
| `icon` | 图标文件名（默认 `icon.png` ✓）—— 用 `generate_icon` 生成后放在 mod 根 ✓（运行时缩到 256²+PPU50 与游戏图标一致 ✓）；没有这个文件就只换模型不换图标 ✓ |
| `front` | **只有用户自带的模型**才需要（声明朝向：`-z`/`+x`/`-x`；`generate_model` 出的不用 ✓）|
| `build=false` | 不想立刻编译时（默认会编 ✓ 需要已记录游戏目录 ✓）|

> **为什么必须用它**：手抄模板时最容易漏两处 —— ① 四处名字不一致（症状是"mod 静默不加载"✗）
> ② 模板里指向 `libs/mod-kit` 的 `<Compile Include>` 是按**模板自身的目录**算的相对路径 ✓ → mod 放到别处（或目录层数不同）就编不过 ✗；工具会**按 mod 实际位置重算** ✓

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
- `TRIPO_FILE_NOT_FOUND` → 路径不对（`create_mod` 的 `file=` 或 `generate_model` 的 `model=`）。
- `MOD_NAME_TAKEN` → 这个名字已经被别的会话占了（或目录已存在）→ 换个名字重来（别硬用旧名）。
- `HELPER_READ_ONLY` → 这是 Game Helper 会话，不能建 mod → 让用户开一个 **mod 会话**。
- 工具不存在 `create_mod_folder` → 这是在仓库里直接开的开发会话（没经 app）→ 直接 `create_mod` 即可（没有写隔离）。
- 形状对但**握把偏** → 看日志两侧包围盒；`WeaponModel.Apply(..., extraOffset)` 可微调（一般不用）。
- **贴图没上** → GLB 里没有 baseColor 贴图（重新 `texture_model` 或直接用带贴图的 GLB）。
- **模型整块看不见** → 绕序/UV 问题（加载器已按左手系转换）→ 改提示词或换参考图重生成。
- **前后反了** → 提示词朝向写反了（写 "left"）；**用户自带的模型**没这个保证 → 在 `config.json` 里写 `"front": "-z"`（或 `+x`/`-x`）。
- 日志没有 `[WeaponModel]` → mod 没装成功或名字对不上（`<mod名>.ModBehaviour`）→ 用 `validate_mod` / `install_mod` 的返回确认。
