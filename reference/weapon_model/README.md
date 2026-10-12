# weapon_model（示例 mod）—— 替换某把武器的模型

**完整正确做法**：整个 mod 等于一句配置加一个 GLB。

## 用法

```
WeaponModelSwap/                 这是 mod 目录（装进游戏时的目录名）
  ├─ WeaponModelSwap.dll
  ├─ info.ini
  ├─ config.json                 内容为 { "typeIDs": [655], "model": "mp5.glb" }
  └─ mp5.glb                     你的模型（**按武器起名**。Tripo 导出的 GLB。）
```

`config.json`（两种写法都支持）：

```json
// ① 一套模型换一批武器（扁平字段等于一条规则，旧写法）
{ "typeIDs": [655], "model": "mp5.glb" }

// ② 每把武器各换各的（多条规则，按顺序匹配，先命中的生效）
{ "entries": [
    { "targets": ["Item_SMG_MP5_Normal"], "model": "smg.glb", "icon": "smg_icon.png" },
    { "typeIDs": [655], "model": "mp5.glb", "front": "-x" } ] }
```

| 字段（每条规则都能用） | 说明 |
|---|---|
| `targets` | 武器**对象名全等**（数组，不区分大小写。例 `["Item_SMG_MP5_Normal"]`。写错一个字符就不命中）|
| `typeIDs` | 或者直接给 typeID（数组，例如 `[655, 238]`。用 `inspect_game_data` 查）|
| `model` | 本目录下的 GLB（相对路径或绝对路径）|
| `icon` | 图标文件名（默认 `icon.png`）|
| `front` | 仅自带模型需要：`auto`、`+z`、`-z`、`+x`、`-x`。Tripo 出的靠提示词 |
| `slots` | 可选：把游戏挂点摆到我们模型上（**米** `[L,H,D]`，相对**模型自身原点**。键：`Muzzle`、`Stock`、`Grip`、`Scope`、`Tec`，加可选的 `pivot`）|

> 给了 `entries` 就**只看 entries**（扁平字段被忽略）。两者不得混用。

`slots`（**可选**。**默认 config 不得写这段**，只有想调更准时才加）：
把游戏的挂点摆到**我们模型**上，值是**相对模型自身原点的偏移，单位米**（不是包围盒比例 ✗）。
`[L, H, D]` 各表示：L 沿 Z（枪口 +Z）、H 沿 Y、D 沿 X。

```jsonc
// 单位米，相对**模型自身原点**。下面是**形状示例**，数值不通用 —— 用编辑器调。
"slots": { "Muzzle": [ 0.32, 0.01,  0.00], "Stock": [-0.34, 0.00,  0.00],
           "Grip":   [ 0.06,-0.05,  0.00], "pivot": [-0.08,-0.02,  0.00] }
```

- ⚠️ **旧的比例值（0~1）已经作废** ✗ —— 现在会被当成**米**（偏移量大十倍以上 ✗）。
  以前抄过 `[0.995,0.800,0.670]` 之类的地方，要用编辑器重新调。
- 下面这组是**旧的**（比例，已作废）—— 只留作对照，**不得抄**：
  `Muzzle [0.995,0.800,0.670]` · `Stock [0.020,0.600,0.670]` · `pivot [0.320,0.490,0.660]`
- 改完**存盘即生效**（热重载，不用重启）。但**换图标**要重启。
- 建议顺序：先调 `pivot`（整枪在手里的位置），再调 5 个槽位（配件落点）。
- **只有装了对应该槽位的配件才看得出效果**（没装瞄具时改 `Scope` 看不到）。
- `Grip` 是**前握把槽位**，不是手抓的位置。手抓的位置用 `pivot`。
- **不写 `slots` 也完全能跑**（走默认的「按原枪包围盒归一化映射」，只是没那么准）。

## 能力在库里（`libs/mod-kit`）

| 文件 | 干什么 |
|---|---|
| `GltfLoader.cs` | 读 GLB，得到 `Mesh` 与贴图。右手系到左手系：取反 X、反转绕序、UV 翻转。握把归零 |
| `WeaponModel.cs` | 换到枪上。关旧零件（`WPN_*` 与 `HideIf_*`，保留 `ShowIf_*` 与特效），挂到原枪身变换帧，**对齐**（用游戏那把枪的包围盒当基准），克隆游戏材质并换贴图 |
| `GameApi.cs` | 找玩家、手持物、层级 |
| `Json.cs` | 读 `config.json` |

## 编译与验证

```bash
export DUCKOV_DIR="<游戏目录>"
dotnet build -c Release        # 产物 bin/Release/WeaponModelSwap.dll
```

装进游戏后进关卡，**手里拿目标武器**（例如 MP5），模型会在 2 秒内换掉。

日志看 `~/Library/Logs/...` 或游戏 Player.log。`[WeaponModel] …` 会写出命中数、隐藏数、锚点、材质与包围盒。
