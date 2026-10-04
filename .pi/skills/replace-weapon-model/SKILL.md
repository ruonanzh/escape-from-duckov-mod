---
name: replace-weapon-model
description: 用户想换某把武器的模型/外观时用（例："把 MP5 换成这把枪" / "给我这把枪做个皮肤" / "这个武器模型换成这个 glb"）。把 GLB 放进 mod、写一句 config、编译安装即可；模型可以先用 Tripo 生成（文本或图片 → GLB）。
---

# 替换武器模型

**目标**：用户手里那把武器（或指定某把武器）的**外观**换成用户提供的模型。不改数值、不改行为。

## 1. 模型从哪来

> ⭐ **提示词里必须写明朝向** —— 这是**唯一可靠**的办法 ✓
> （实测：Tripo 自己的 `export_orientation` 参数对同一批模型结果都不一致 ✗；
>   靠几何判定也不行 —— 火箭筒这种"两端都细"的没法判 ✗）
>
> ```
> …, the barrel and muzzle point to the LEFT, the stock is on the right
> ```
> 实测映射：**"to the left" → 枪口落在 +Z = Unity 前向 ✓**（"to the right" → −Z ✗）
> → **朝向在生成阶段就定好，进游戏不用再转** ✓
>
> **为什么不能靠别的**：Tripo 的 `export_orientation` 参数对同一批模型结果都不一致 ✗（有时翻有时不翻）；
> "几何判定哪端是枪口"对**火箭筒**这种两端都细的必然失效 ✗ → 提示词是唯一可靠的手段 ✓
> （完整实测见 doc 仓 `docs/unity-3d-assets/03-tripo-api.md` §8.8 / §8.9）

> **Tripo key**：放**单独的文件** `~/.gamer-agent-pi/api-keys.json` → `{ "tripo": "tsk_…" }`（用户级 ✓ 所有游戏仓库共用 ✓ 不进 git ✓；**临时方案** —— 将来由 app 的「管理 API keys」界面接管 ✓）。没有 key 时工具会返回 FAIL 并说明放哪 —— 这时**向用户要一次**，写进去即可 ✓（余额可以用 `generate_model(action="balance")` 看 ✓；一次生成大约 40 积分 —— **别反复重试刷积分** ✗ 不行就改提示词或问用户 ✓）

| 情况 | 做法 |
|---|---|
| 用户给了 `.glb` | 直接用 ✓（`your_mods/<你的mod>/` 里放上它即可）|
| 用户给了一张图 / 一句话 | **调 `generate_model` 工具**（π 工具，内部直连 Tripo HTTP）：`generate_model(action="generate", prompt="PPSh-41 样式的冲锋枪，游戏资产，侧视", out="your_mods/<你的mod>/gun.glb", faceLimit=3000)`；用户有参考图就用 `image="<路径>"` 代替 prompt ✓。工具会**自动建任务→轮询→立刻下载**（URL 5 分钟过期 ✗）并按 **Unity 就绪朝向**导出（枪口/正面 = +Z ✓），所以在游戏里不用再转 ✓ |

## 2. 做 mod（照 `reference/weapon_model/` 抄）

1. 在 `your_mods/<你的mod名>/` 建目录，把 `reference/weapon_model/` 的 4 个文件抄过来
2. **把三处名字改成 `<你的mod名>`**：目录名 / `info.ini` 的 `name` / `.csproj` 的 `AssemblyName` 与 `ModBehaviour.cs` 的 `namespace`（游戏要求 `<mod名>.ModBehaviour` 这个类型）
3. 把 GLB 放进去，写 `config.json`：`{ "target": "MP5", "model": "gun.glb" }`
   - 不知道武器名/typeID → 用 `inspect_game_data` 查（`docs/guides/items.md`）

## 3. 编译 + 装进游戏

```bash
export DUCKOV_DIR="<游戏安装目录>"
dotnet build -c Release
```
用 `install_mod` 工具装进游戏（会校验 + 装到 Mods 目录）。

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
