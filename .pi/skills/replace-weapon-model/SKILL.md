---
name: replace-weapon-model
description: 用户想换某把武器的模型/外观时用（例："把 MP5 换成这把枪" / "给我这把枪做个皮肤" / "这个武器模型换成这个 glb"）。把 GLB 放进 mod、写一句 config、编译安装即可；模型可以先用 Tripo 生成（文本或图片 → GLB）。
---

# 替换武器模型

**目标**：用户手里那把武器（或指定某把武器）的**外观**换成用户提供的模型。不改数值、不改行为。

## 1. 模型从哪来

| 情况 | 做法 |
|---|---|
| 用户给了 `.glb` | 直接用 ✓ |
| 用户给了一张图 / 一句话 | 用 Tripo 生成：`text_to_model` / `image_to_model`（低面数用 P1 系列，`face_limit` 3000 左右）→ 完成后**立刻下载**（URL 5 分钟过期 ✗）→ 需要时 `convert_model(format="GLTF", export_orientation="-x", with_animation=false, texture_size=1024, texture_format="PNG")` |

> `export_orientation="-x"` 让枪口/正面落到 **+Z = Unity 前向** ✓（实测；不用它也能换，运行时/工具会校正朝向）

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
| **模型整块看不见** | 典型是绕序/朝向 ✗ 用 `convert_model(export_orientation="-x")` 重导 ✓ |
| 日志没有 `[WeaponModel]` | mod 没装成功或名字对不上（`<mod名>.ModBehaviour`）→ 用 `validate_mod` / `install_mod` 的返回确认 |
