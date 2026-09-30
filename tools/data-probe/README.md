# data-probe — 读 Unity3D 游戏的内容数据（给 agent 用）

读取游戏数据文件里**序列化对象**的**字段与值**，用来回答"现在是什么样"：物品价格 `Item.value`、
武器数值 `Item.stats`（`Damage`…）、任务条件 `Quest.requiredItemCount`、敌人预设
`CharacterRandomPreset.isBoss`、以及**场景**（`levelN` 文件里的 GameObject / Transform / Camera 等
对象与挂在它们上的脚本）。

- **只读**：不修改任何文件（改数值/改行为是 mod 运行时 C# 的事，不是本工具的活）。它读场景是为了
  "查清楚运行时有什么"（好让 mod 的 C# 去找/补丁），**不是关卡编辑器**。
- **离线**：依赖已 vendored 在 `lib/`，不联网。

## 为什么能读到自定义字段

成品游戏的数据文件里**没有 type tree**。本工具用游戏**自己的托管 DLL**现算脚本类的字段布局
（`AssetsTools.NET.MonoCecil` 的 `MonoCecilTempGenerator`），加一份内置类型库
（`lib/classdata.tpk`），才能把二进制解成字段。

## 用法

```bash
dotnet run --project tools/data-probe -- \
  --managed "<game>/.../Managed" --data "<game>/.../Data" \
  --action <classes|search|list|dump|refs> [options]
```

| action | 选项 | 作用 |
|---|---|---|
| `classes` | — | 列出资产**类名 + 数量**（含内置类型：`GameObject`/`Transform`/`Camera`…）|
| `list` | `--class <C>` | 列出某类的资产（name / typeID / pathID；**每次最多 500 条**，用 `--offset` 翻页）|
| `search` | `--pattern <p>` `[--class C]` | 按名称/类/typeID 找资产（**每次最多 500 条**，用 `--offset` 翻页）|
| `dump` | `--class <C>` ＋ (`--name`\|`--typeid`\|`--pathid`) `[--depth d] [--follow]` | dump 该资产的**字段 + 值**；`--follow` **跟随引用**（如 `Item.stats` → `StatCollection` 里的 `Damage`）|
| `refs` | 同 `dump` 的定位 | 列出该资产**引用了哪些对象**（PPtr）|
| `export` | `--class <C>` `[--match <expr>]…` `[--field <path>]…` `[--rows N]` | **批量表**：一类对象 × 过滤 × 字段路径 → 每行一个资产（TAB 分列；数组用 `;` 连）|

公共：`--file <x.assets|levelN>`（限定单个数据文件；**`levelN` = 场景文件**，格式与 `.assets` 相同）、`--limit N`（截断，默认 2000 行）、`--depth d`（dump 深度，默认 3）、`--offset N`（跳过前 N 条，给 `list`/`search` 翻页用）。

> `list` / `search` 每次最多 500 条；截断时会提示 `--offset <下一页>`——**优先用更窄的查询（`search --pattern` / `dump --name`），确实要枚举时才翻页**。

## `export` 的路径语法（`--field` / `--match` 共用）

一段一段用 `.` 分隔，**PPtr 自动跟随**：

| 段 | 含义 |
|---|---|
| `a` | 字段 `a` |
| `a[]` | 展开数组（每个元素一个值）|
| `a[i]` | 取下标 i |
| `#class` | 当前 PPtr **解析后对象的类名**（如 `ItemSetting_Gun`）|
| `#name` | 解析后对象的名字 |

`--match` 运算符：`=` `!=` `~`（子串，忽略大小写）`>` `>=` `<` `<=`；可重复（AND；数组路径=任一元素命中）。

```bash
# 一次拿全 124 把枪的数值（“是武器”靠 m_GameObject 组件里有没有 ItemSetting_Gun 判定）
data-probe ... --action export --class Item \
  --match "m_GameObject.m_Component[].component.#class=ItemSetting_Gun" \
  --field displayName --field value --field "stats.list[].key" --field "stats.list[].baseValue"
# → name  typeID  pathID  displayName  value  columns...
#   stats.list[].key 与 .baseValue 是**平行数组**，按 `;` 切开后一一对应
```

同类可换任意类型：`--class Quest --match "requireLevel>=1"`、`--class CharacterRandomPreset --match "isBoss=1"`。

## 例

```bash
# 找武器
data-probe ... --action search --pattern UAK45 --class Item
#   Item  Item_S_UAK45_Lv_2  typeID=260  pathID=84067

# 读它的数值（跟随 stats）
data-probe ... --action dump --class Item --name Item_S_UAK45_Lv_2 --follow
#   ... stats -> StatCollection
#         key = Damage          baseValue = 9.2
#         key = ShootSpeed      baseValue = 15.83
#         key = Capacity        baseValue = 25

# 读场景：levelN 就是场景文件（主菜单、关卡等）
data-probe ... --action classes --file level0           # 153 类 / 5516 对象
data-probe ... --action list --class GameObject --file level0
data-probe ... --action dump --class GameObject --name DevCam --file level0 --follow
#   ... m_Component -> Transform ... m_LocalPosition x/y/z
```

## 依赖（vendored，来自 UABEA 包，MIT）

`lib/`：`AssetsTools.NET.dll`、`AssetsTools.NET.MonoCecil.dll`、`Mono.Cecil.dll`、
`Mono.Cecil.Rocks.dll`、`classdata.tpk`。
