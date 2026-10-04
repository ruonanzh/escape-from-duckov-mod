# ItemSwap —— 可运行的起点（换模型 / 新增物品）

**这是"模型写完之后，怎么变成能跑的 mod"的参考实现**：复制这个目录当起点即可。

- 把物品的模型换成 `models/*.json` 算出来的几何（**掉落在地上 / 拿在手里都换**）
- 可选：**新增一件物品**（克隆现有物品 + 新 typeID + 我们的模型）并发放给你
- **改 `config.json` 或 `models/*.json` 存盘即生效** —— 不用重编译、不用重启游戏

## 工程结构

```
item_swap/
  ItemSwap.csproj            # 唯一工程文件（netstandard2.1）：引用游戏 DLL + 编译 ../mod-kit/*.cs
  ModBehaviour.cs            # 全部逻辑：读 config、热重载、调库、打日志
  config.json                # 运行时可改的配置（字段见下）
  models/*.json              # 模型零件清单（旧格式；说明已归档到 doc 仓）
  info.ini                   # mod 元数据（name / displayName / description / version）
  bin/Release/ItemSwap.dll   # 产物
```

⚠️ **`mod-kit` 不是 nuget 包，是共享源码** —— csproj 里逐个
`<Compile Include="../mod-kit/ItemModelBinder.cs" />` 编译进来（这样在任何机器上都能 build，不需要先发包）。

## build / 安装

```bash
export DUCKOV_DIR="<游戏安装目录>"            # 必需：不设会直接报错（防止编出空壳 dll）
dotnet build -c Release .                     # 在 item_swap/ 目录里
```

走产品侧工具更省事：**`mod-lint`**（校验）→ **`mod-install`**（装进游戏）。

## `config.json` 字段

| 字段 | 作用 |
|---|---|
| `model` | 用哪个模型文件（`models/` 下）|
| `match` / `typeIDs` | **换哪些物品**：两个条件**同时**生效（`typeIDs` 非空就必须命中它；`match` 非空名字还得包含它）|
| `inPlaceHeldSwap` | 已经拿在手里那个也**就地换几何**（默认 `true`；开局手里那把必须就地换，游戏不会重建它）|
| `debugMarkers` | 每个挂点放一个 2cm 小球（**调挂点位置时用**，平时关掉）|
| `newItem` | **新增一件物品**：`cloneFrom`（源物品 typeID）/ `typeID`（新的，选空闲的）/ `displayName` / `give` = `drop`（掉脚边）\| `pickup`（进背包）\| `false` |

## 改完怎么生效（迭代方式，重要）

| 改什么 | 怎么生效 |
|---|---|
| `models/*.json`（尺寸/配色/`slots` 挂点）| **存盘即生效**（demo 监视模型文件时间戳）|
| `config.json` | **存盘即生效** |
| `ModBehaviour.cs` / `../mod-kit/*.cs` | 要**重编译 + 重启游戏**（Unity 只在启动时加载 mod dll）|

## 日志

`/tmp/item_swap.log`：每次换模型打一行（锚点、关掉的渲染器数、槽位与坐标、手里的 mesh 子网格/材质），
失败也在这里 —— 例如 `反射写 itemGraphic 失败`、`AddDynamicEntry 返回 false`、`BuildGraphicClone 返回 null`。

## 拿它当起点做自己的 mod

1. 复制目录 → 改 `info.ini` 的 `name`/`displayName`，csproj 的 `AssemblyName` 改成你的 mod 名
2. 换 `models/*.json` 为你的模型（模型格式说明已归档到 **doc 仓** `docs/archive/unity-3d-exploration/unity-3d-docs/`）
3. 定目标物品：`typeIDs` 填 typeID（用 `inspect_game_data` 查）
4. `model-check` 干跑一遍（几何/UV/绕序/挂点建议）→ 进游戏看 → 需要时开 `debugMarkers` 调挂点
5. 要"新增物品"就填 `newItem`（`cloneFrom` 选一把最接近的现有枪）

## 相关文档与实现

- 模型格式 / 坐标约定 / 武器与物品做法：已归档到 doc 仓 `docs/archive/unity-3d-exploration/unity-3d-docs/`
- 库实现（`../mod-kit/`）：`ItemModelBinder`（换图形：克隆 → 只换主体零件 → 写回 `itemGraphic`）·
  `ItemFactory`（新增物品：`CloneAsNewItem`，内置常驻/名字/`IsGun`）· `MeshKit`（几何）· `TextureKit`（贴图）·
  `GameApi`（找角色 / 发放物品）
- 角色模型：见 doc 仓的归档（方块路线已放弃）
