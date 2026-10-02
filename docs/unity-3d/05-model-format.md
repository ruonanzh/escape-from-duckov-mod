# 05 · 模型格式（零件清单 JSON）

做**物品 / 武器 / 配件 / 建筑**的模型时，写这种 JSON：一份**零件清单**，运行时由库生成 mesh 并挂上去。
模型文件放 mod 目录，玩家可自己改参数。角色用 YSM（见 [`03-characters.md`](03-characters.md)）。

## 一份完整的模型文件

```json
{
  "name": "compact_pistol",
  "category": "weapons/pistol",
  "summary": "紧凑手枪：滑套 + 枪管 + 枪身 + 握把 + 弹匣",
  "parts": [
    { "role": "slide",   "shape": "box",      "size": [0.030, 0.032, 0.170], "at": [0, 0.030, 0.010] },
    { "role": "barrel",  "shape": "cylinder", "r": 0.008, "h": 0.075, "segments": 12, "at": [0, 0.030, 0.090], "rot": [90, 0, 0] },
    { "role": "frame",   "shape": "box",      "size": [0.028, 0.020, 0.130], "at": [0, 0.008, 0.000] }
  ],
  "material": { "mode": "clone", "pick": "body" },
  "attach": { "kind": "item_graphic" }
}
```

## 字段

### 顶层

| 字段 | 必填 | 说明 |
|---|---|---|
| `name` | ✅ | 模型名（与文件名一致，小写下划线）|
| `category` | ✅ | `weapons/*` · `items/*` · `buildings/*`（决定挂载方式与被查的样例）|
| `summary` | | 一句话：这是什么、由哪些部分组成 |
| `parts[]` | ✅ | 零件清单（见下）|
| `material` | | `{"mode":"clone","pick":"body"}` = 克隆游戏里同类的现有材质（URP 自定义 shader 必须克隆）|
| `attach` | | `{"kind":"item_graphic"}`（物品）/ `{"kind":"socket","name":"HelmatSocket"}`（挂饰）|

### `parts[]` 每个零件

| 字段 | 说明 |
|---|---|
| `role` | 这个零件是什么（`slide`/`barrel`/`grip`…）—— 便于对照与后续替换 |
| `shape` | `box` · `cylinder` · `cone` · `sphere` · `lathe` · `extrude` |
| `size` | `box` 用：`[宽, 高, 长]`（米）|
| `r` / `r1` / `r2` | `cylinder` 半径 / `cone` 底半径、顶半径 |
| `h` / `depth` | 高 / 拉伸厚度（米）|
| `segments` | 圆周分段（圆柱/圆锥/球/旋转体；常用 12–24）|
| `profile` | `lathe` 用：`[[半径, 高度], …]` 轮廓点 |
| `outline` | `extrude` 用：`[[x, y], …]` 二维轮廓 |
| `at` | 位置 `[x, y, z]`（米），相对模型原点 |
| `rot` | 旋转 `[x, y, z]`（度，等价 `Quaternion.Euler`）|
| `scale` | 可选，`[x, y, z]`（默认 1）|

## 约定

- **单位 = 米**；物品模型大致 **0.1–1 m**。
- **坐标**：`+Z` 朝前（枪口 / 正面）、`+Y` 朝上；模型**居中在原点**。
- **几何一律由参数算出**（顶点/索引/UV/法线由库生成），不写死顶点。
- **多零件可共用一张 mesh**；要分色就分 submesh（按 `role` 分组）。
- **贴合游戏**见 [`00-shared.md`](00-shared.md)（`groundPoint` / sockets / 层 / 材质）。

## 零件与三角面数（低多边形风格的对齐参考）

| 形状 | 顶点 | 三角面 |
|---|---|---|
| `box` | 24 | 12 |
| `cylinder`（n 段，带两端盖）| 4n + 2 | 4n |
| `cone`（n 段，带底盖）| n + 1 | 2n |
| `cone`（n 段，圆台两端盖）| 2n + 2 | 3n |
| `sphere`（经 n × 纬 m）| (m − 1)·n + 2 | 2n·(m − 1) |

> 游戏本体 mesh 顶点数**中位数 402**（平均 729，最大 17,594）—— 简单道具**几百到几千顶点**就够贴合画风。
