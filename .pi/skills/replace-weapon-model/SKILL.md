---
name: replace-weapon-model
description: 用户想换某把武器的模型/外观时用（例："把 MP5 换成这把枪" / "给我这把枪做个皮肤" / "这个武器模型换成这个 glb"）。把 GLB 放进 mod、写一句 config、编译安装即可；模型可以先用 Tripo 生成（文本或图片 → GLB）。
---

# 替换武器模型

**目标**：用户手里那把武器（或指定某把武器）的**外观**换成用户提供的模型。不改数值、不改行为。

## 1. 模型从哪来

> ⭐ **提示词里必须写明朝向** —— 这是唯一可靠的办法 ✓
> Tripo 的 `export_orientation` 参数不可靠 ✗（同一批模型结果不一致）；靠几何判断也不行 ✗（火箭筒这种"两端都细"的没法判）
>
> ```
> …, the barrel and muzzle point to the LEFT, the stock is on the right
> ```
> **"to the left" → 枪口落在 +Z = Unity 前向 ✓**（"to the right" → −Z ✗）→ **进游戏不用再转** ✓
> （完整数据：doc 仓 `docs/unity-3d-assets/03-tripo-api.md` §8.8 / §8.9）

> **卡片图标（可选但推荐）**：`generate_icon(model="your_mods/<名字>/gun.glb")` —— 不带 `style` 时**免费** ✓（用模型自己的渲染图 ✓ 形状与游戏里一致 ✓）
> → 存成 mod 根的 `icon.png` ✓（mod 会把它设成武器卡片图标 ✓；运行时自动对齐游戏的 **256² + PPU50** ✓）
> （想要别的风格时给 `style="clean game inventory icon, 3/4 view"` ✓ 约 10 积分 ✓）

> **Tripo key**：放**单独的文件** `~/.gamer-agent-pi/api-keys.json` → `{ "tripo": "tsk_…" }`（用户级 ✓ 所有游戏仓库共用 ✓ 不进 git ✓；**临时方案** —— 将来由 app 的「管理 API keys」界面接管 ✓）。没有 key 时工具会返回 FAIL 并说明放哪 —— 这时**向用户要一次**，写进去即可 ✓（余额可以用 `generate_model(action="balance")` 看 ✓；一次生成大约 40 积分 —— **别反复重试刷积分** ✗ 不行就改提示词或问用户 ✓）

| 情况 | 做法 |
|---|---|
| 用户给了 `.glb` | 直接用 ✓（`your_mods/<你的mod>/` 里放上它即可）|
| 用户给了一张图 / 一句话 | **调 `generate_model` 工具**（它会把 Tripo 的渲染图存到模型目录的 `.preview/` 下 ✓ **装进游戏前先看那张图** —— 形状对不对一眼就知道 ✓ 该目录不会被 `install_mod` 装进游戏 ✓）（π 工具，内部直连 Tripo HTTP）：`generate_model(action="generate", prompt="PPSh-41 样式的冲锋枪，游戏资产，侧视", out="your_mods/<你的mod>/gun.glb", faceLimit=3000)`；用户有参考图就用 `image="<路径>"` 代替 prompt ✓。工具会**自动建任务→轮询→立刻下载**（URL 5 分钟过期 ✗）并按 **Unity 就绪朝向**导出（枪口/正面 = +Z ✓），所以在游戏里不用再转 ✓ |

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

> **图标这步两种情形**：
> - 模型是 `generate_model` 生成的 → `generate_icon(model="…/gun.glb")` ✓（工具能用它记下的 task id 直接取渲染图 ✓ **不花积分** ✓）
> - 用户自带的 GLB → 同样 `generate_icon(model="…/gun.glb")` ✓（工具会自动 import_model 上传后取渲染图 ✓）



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
