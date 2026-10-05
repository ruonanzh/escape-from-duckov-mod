---
name: replace-weapon-model
description: 用户想换某把武器的模型/外观时用（例："把 MP5 换成这把枪" / "给我这把枪做个皮肤" / "这个武器模型换成这个 glb"）。把 GLB 放进 mod、写一句 config、编译安装即可；模型可以先用 Tripo 生成（文本或图片 → GLB）。
---

# 替换武器模型

**目标**：用户手里那把武器（或指定某把武器）的**外观**换成用户提供的模型。不改数值、不改行为。

## 1. 模型从哪来（四步资产流程）

用户**只说一句话**也能做 ✓ 但**最省积分、最不容易白花钱**的顺序是这四步：
第 ②④ 步各只要 **5 积分**，只有第 ③ 步贵（约 **50**）→ **先把便宜的做完、和用户确认，再花贵的那步** ✓

### ① 参考图（免费）

- 用户给了图 → 就用它（聊天里发的图会落盘，拿到的是**绝对路径** ✓ 直接传给工具 ✓）
- 用户只说了句话 → 跳过这步，直接进 ②（② 支持纯提示词 ✓）
- 给了好几张 → **问用户用哪张** ✓

### ② 风格化预览图（约 5 积分）—— **先给用户看，等他确认**

```
generate_image(
  image="<用户的图>",                  // 没图就省略 → 纯提示词出图 ✓
  styleRef="<游戏内武器的图>",           // 强烈建议：一张游戏里已有武器的图（工坊图标/截图）→ 出得像本作 ✓
  style="…, the barrel and muzzle point to the LEFT, the stock is on the right",
  out=".preview/<名字>.preview.png")    // 中间图一律进 .preview/ ✓ 不会被装进游戏 ✓
```

- 出图后**自己先看**（读这张图 ✓）→ 再**给用户看，问他「就要这个吗」** ✓
- 不像就改 `style` 再来一次 ✓（每次都便宜 ✓）—— **不要**在没确认前去做 ③ ✓
- 想更像本作：`image` 给用户的图、`styleRef` 给游戏内的图，**两个都给最准** ✓

### ③ 3D 模型（约 50 积分，含转换）

```
generate_model(action="generate", image=".preview/<名字>.preview.png",
               out="your_mods/<mod>/gun.glb", faceLimit=3000)
```

- 用 ② 那张**已确认的预览图**做输入 ✓ —— **图 → 3D 比纯文字准得多** ✓（冷门型号尤其 ✓）
- 用户只要一句话、没图 → 用 `prompt="…"` ✓（**提示词里也必须写朝向** ✓）
- 用户自带 `.glb` → ①②③ 全跳过 ✓ 直接用他的文件 ✓（那时要在 `create_mod` 里声明 `front=` ✓）
- 工具会把 Tripo 的渲染图存到 `.preview/<名字>.preview.png` ✓ 并记下 task id（供 ④ 免费用 ✓）

> **朝向只能靠提示词** ✓（`export_orientation` 参数不可靠 ✗，几何判定对「两端都细」的武器也不可用 ✗）
> ```
> …, the barrel and muzzle point to the LEFT, the stock is on the right
> ```
> **"to the left" → 枪口落在 +Z = Unity 前向 ✓**（"to the right" → −Z ✗）→ **进游戏不用再转** ✓
> （完整数据与端点/坑：doc 仓 `docs/unity-3d-assets/03-tripo-api.md` §8.8 / §8.9 / §11 ✓）

### ④ 图标（约 5 积分；想省钱用 ③ 的渲染图则**免费**）

```
generate_image(
  model="your_mods/<mod>/gun.glb",
  styleRef="<游戏里那把武器的卡片图标>",
  style="clean game inventory icon, side profile, pure white background, no shadow, centered",
  out="your_mods/<mod>/icon.png")
```

- **尺寸/抠白/居中都不用管** ✓ —— 游戏运行时会自动抠成透明、居中、缩到 **256² + PPU50**（与游戏自带图标一致 ✓）
  → **不要**自己裁、不要自己缩放 ✗
- 不写 `style` → 直接拿模型自己的渲染图 ✓（免费 ✓ 形状必然一致 ✓）→ 存成 `icon.png` ✓
- 背景一定写 **`pure white background, no shadow`** ✓（灰底/阴影会被运行时留下淡淡的边 ✗）
- 没有 `icon.png` → 只换模型、不换图标 ✓

### 成本一览

| 步 | 工具 | 约 |
|---|---|---|
| ② 预览图 | `generate_image` | 5 |
| ③ 3D 模型 | `generate_model` | 50 |
| ④ 图标 | `generate_image` | 5（或 0 = 用模型的渲染图 ✓）|
| | 合计 | **≈ 60** ✓ |

> **Tripo key**：放**单独的文件** `~/.gamer-agent-pi/api-keys.json` → `{ "tripo": "tsk_…" }`
> （用户级 ✓ 所有游戏仓库共用 ✓ 不进 git ✓；**临时方案** —— 将来由 app 的「管理 API keys」界面接管 ✓）
> 没有 key 时工具会返回 FAIL 并说明放哪 —— 这时**向用户要一次**，写进去即可 ✓
> 余额用 `generate_model(action="balance")` 看 ✓；**别反复重试刷积分** ✗ 不行就改提示词或问用户 ✓

## 2. 做 mod（**一条工具调用**）

```
create_mod(kind="replace-weapon-model", name="MyGun", target="MP5", file="gun.glb")
```
它会：拷模板 → **把四处名字改成一致**（目录 / `info.ini` 的 name / `.csproj` 的 AssemblyName+RootNamespace /
`ModBehaviour.cs` 的 namespace —— 游戏要求 `<mod名>.ModBehaviour` 类型 ✓）→ 写 `config.json` → 编译 ✓

| 参数 | 说明 |
|---|---|
| `name` | mod 名（字母+数字 ✓ 会成为命名空间/程序集名 ✓）|
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

## 4. 怎么确认成功

1. 进关卡，**手里拿出目标武器**（背包里切到它）
2. 2 秒内模型应该换掉 ✓（`DontDestroyOnLoad`/重挂都由 mod 自己处理 ✓）
3. 日志（`[WeaponModel]`）应包含：
   - `手里的物品：'SMG_MP5_Normal' … 命中 ✓ 会换`
   - `已换模型：隐藏旧零件 N 个；锚点=WPN_MP5；材质=…/SodaCraft/SodaLit；我们的包围盒=…；原枪身包围盒=…`
4. 看画面：**模型形状 ✓ 贴图 ✓ 枪口朝前 ✓ 手握住握把 ✓**

## 5. 出问题时的判据

| 现象 | 原因 / 做法 |
|---|---|
| 形状对但**握把偏** | 看日志两侧包围盒；`WeaponModel.Apply(..., extraOffset)` 可微调（一般不用 ✓）|
| **贴图没上** | GLB 里没有 baseColor 贴图（重新 `texture_model` 或直接用带贴图的 GLB）|
| **模型整块看不见** | 绕序/UV 问题（加载器已按左手系转换 ✓）→ 改提示词或换参考图重生成 ✓ |
| **前后反了** | 提示词朝向写反了 ✓（写 "left" ✓）；**用户自带的模型**没这个保证 → 在 `config.json` 里写 `"front": "-z"`（或 `+x`/`-x`）✓ |
| 日志没有 `[WeaponModel]` | mod 没装成功或名字对不上（`<mod名>.ModBehaviour`）→ 用 `validate_mod` / `install_mod` 的返回确认 |
