# model-check —— 离线校验模型文件（干跑）

不启动游戏，就能知道模型**做出来长什么样**：顶点数、三角面数、包围盒、贴图排布与自动密度，
以及格式/尺寸/UV/骨架名等规则违例。规范见 `docs/unity-3d/05-model-format.md`。

## 用法

```bash
export PATH="$HOME/.dotnet:$PATH"

# 校验一个模型文件
dotnet tools/model-check/bin/Release/model-check.dll --file my_gun.json

# 校验文档里所有 ```json 块（零件清单 / YSM 骨架）
dotnet tools/model-check/bin/Release/model-check.dll \
  --md docs/unity-3d/01-weapons.md --md docs/unity-3d/02-items.md --md docs/unity-3d/04-buildings.md

# 指定贴图边长（演示自动密度的变化）
dotnet tools/model-check/bin/Release/model-check.dll --file my_gun.json --side 1024
```

退出码：有 `FAIL` → `1`，否则 `0`（`WARN` 不算失败，可用于 CI）。

## 输出

```
PASS  01-weapons.md #1   category=weapons/pistol   parts= 8  verts=  288  tris=  132  bbox=0.03×0.159×0.203m  centerX=0  atlas=512²  density=1024 px/m (0.98 mm/px)
WARN  02-items.md #2    密度只有 42 px/m（24 mm/像素）—— 只够大色块
FAIL  03-characters.md #2    骨骼 'Arm.Uper.R' 的 parent 'Arm.Upper.R' 不存在
```

## 校验哪些内容

**零件清单（物品 / 武器 / 配件 / 建筑）**

| 层 | 检查 |
|---|---|
| 格式 | 必填字段；`shape` 合法；各形状的必填参数（`box` 的 `size`、`cylinder` 的 `r`/`h`、`lathe` 的 `profile`…）；`mirror` 只能是 x/y/z |
| 几何 | 由 `MeshKit` 真算一遍：顶点 / 三角面 / 包围盒 / submesh；UV 不越界、零件矩形互不重叠 |
| 约定 | 贴图边长是 2 的幂；零件是否都配了 `fills` 底色；`source: file` 时提示无法核对图片尺寸 |
| 量级 | 自动密度 < 32 px/m（只够大色块）；顶点数 > 5000（游戏本体中位 402）|
| 分类 | 枪械/配件：长度沿 +Z、以 x=0 居中、尺寸在实测区间（手枪 0.15–0.25 m 等）；建筑：必须有一面贴地（`minY` 或 `maxY` ≈ 0）|

**YSM（角色）**

| 检查 |
|---|
| `minecraft:geometry[0].bones` 非空；`description.texture_width/height` 存在 |
| 骨骼名唯一、`parent` 必须存在、**父子关系不成环** |
| cube 的 `size` 三个数都 > 0、有 `uv`；`uv` 的展开矩形（`2d+2w` × `d+h`）不超出贴图 |
| 骨骼名不在已知骨架里 → WARN（拼写错会导致**静默不挂载**；已内置玩家鸭子 / NPC 鸭子两套名单）|

## 为什么可信：与运行时共用同一份几何核心

`MeshKit.cs` 是本目录与**运行时库**共用的几何核心（不依赖 UnityEngine）：

```
零件清单 JSON ──解析──▶ MeshKit ──┬─▶ mesh 数据 ──▶ 这里：顶点/包围盒/尺度校验
                                  │                运行时：UnityEngine.Mesh + 挂载
                                  └─▶ 图集排布表 ─┬─▶ 这里：UV 越界/重叠校验
                                                  └─▶ 贴图生成 / 玩家照着画模板
```

校验算出来的东西**就是**游戏里会生成的东西 —— 否则"校验通过但游戏里不一样"，等于没校验。
