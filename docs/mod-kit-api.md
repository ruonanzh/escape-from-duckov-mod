# mod-kit API（写 mod 时会用到的那些）

> 这是**我们自己的**运行时库（`libs/mod-kit/`）—— 游戏自身的 API 另见 [`mod-api.md`](mod-api.md)。
> ⭐ 本文只写「**是什么 / 什么时候用**」，**不抄签名**（签名会变，抄了必过期）；
> 要签名就用现查：`rg "public static" libs/mod-kit/<文件>.cs`（例 `ItemModel.cs`）。

## 0. 三条硬约定（先看这个）

- **可还原**：任何"改外形"的调用都返回 `Result`，它带 `Restore()`。**热重载/卸载时必须调**，否则会残留我们的 mesh。
- **有借有还**：`Result` 里记着这次改过的东西（关掉的旧零件、挂上的实例、搬过的挂点、顺手做的 `Nested`）——
  只调 `Restore()` 就全还回去，不用自己记。
- **层不动**：不自己挑层。游戏设好的 `Character(9)` 照用，其余一律 `Default(0)`（`UI(5)` 会让模型发白）。

## 1. 读模型

| API | 什么时候用 |
|---|---|
| `GltfLoader.LoadFile(path, front)` | 读一个 `.glb` → `Mesh` + 主贴图。`front` 一般 `"auto"`；用户自带的模型按 config 传 `"-z"`/`"+x"`/`"-x"` |
| `GltfLoader.Load(byte[], front)` | 同上，但手上已有字节（很少用） |
| `GltfLoader.MuzzleAtPositiveZ(pts)` / `GuessGrip(pts)` | 自己需要判朝向 / 找握把时才用（换模型时 kit 内部已经做了） |

## 2. 换武器外观

| API | 什么时候用 |
|---|---|
| `WeaponModel.Apply(anchorRoot, mesh, tex, offset, slots, scale)` | **主入口**：把模型换到手持武器上（选锚点 / 藏旧零件 / 对齐 / 搬挂点 / 换贴图） |
| `WeaponIcon.Apply(item, "icon.png")` | 换卡片图标（有图标才调；幂等，内部自己归一化尺寸） |
| `WeaponIcon.ApplyToAllMatching(names, pngPath)` | 一次给一批武器换同一个图标 |

## 3. 换物品外观

| API | 什么时候用 |
|---|---|
| `ItemModel.Apply(item, mesh, tex, handheld, size)` | **主入口**：换一件物品（有图形就地改实例；没图形自动"克隆物品 + 注册动态条目"） |
| `ItemModel.ApplyHandheld(item, mesh, tex, scale)` | 只换"拿在手里"那条 |
| `ItemModel.ApplyToInstance(item, mesh, tex, scale)` | 只换"场上那一个"（手里/身上），不动模板 |
| `ItemModel.ApplyToTransform(root, …)` / `ApplyToGraphicClone(clone, …)` | 你已经拿到"要改的那一层"时直接用；后者专给**场上的图形副本**（掉落/展示） |
| `ItemModel.IsPatched(layer)` | 判"这一层我们挂过没有"—— **唯一判据**，别用别的（`Marked`/名字/扫子树都会判错） |
| `ItemModel.MarkPatched(layer)` / `UnmarkPatched(layer)` | 别的能力（例如武器侧）挂完也要登记 / 自己 `Restore()` 时注销 |
| `ItemModel.HasOurMeshUnder(t)` | 这一层（含子树）里有没有我们的 mesh；给"别重复挂"用 |

## 4. 尺寸档位（config `size` 的上游）

| API | 什么时候用 |
|---|---|
| `ModelSize.For(typeID, explicitSize)` | 这件物品的目标长度（米）；`explicitSize` = config 的 `size`。武器按 `GunType_*` 自动分档，其它返回 `null`（不缩放） |
| `ModelSize.FactorFor(typeID, size, mesh)` | 一步拿到"该乘多少 scale"（mod 侧最常用） |
| `ModelSize.Factor(mesh, target)` / `Longest(mesh)` | 只要系数 / 只要最长边时用 |

## 5. 缓存与路径

| API | 什么时候用 |
|---|---|
| `ModelCache.ModDir()` | 本 mod 的目录（DLL 所在处）—— 找 `config.json` / 素材文件 |
| `ModelCache.Get(cache, path, front, out loaded)` | 带指纹的模型缓存：文件没变就不重读（指纹**不含 `size`**，改 `size` 不必重读） |
| `ModelCache.Fingerprint(path, front)` | 自己要判"该不该重读"时用 |

## 6. 游戏查询 / 操作

| API | 什么时候用 |
|---|---|
| `GameApi.AllItems()` | 枚举场上所有物品（含模板）—— 按 `typeIDs` 找目标 |
| `GameApi.NameMatches(item, "MP5")` | 名字匹配（**全等**，不是子串） |
| `GameApi.DeclaredRenderers(root)` | 游戏**自己声明**的渲染器清单（找"本体"最准，胜过"取最大"） |
| `GameApi.ApplyOurTexture(mat, tex)` | 把我们的贴图换到一份游戏材质上（并清掉对不上的其它贴图槽） |
| `GameApi.EnsureHandheldAgent(item, graphicPrefab)` / `SetAgentPrefab(…, "Handheld", …)` | 让物品"拿在手里"时用我们的模型 |
| `GameApi.FindMainCharacter()` / `AllCharacters()` / `FindCharacter(name)` | 找玩家 / 角色 |
| `GameApi.VisibleLayer` / `HiddenLayer` | 游戏那两个层（`Character` / `SpecialCamera`） |
| `GameApi.WriteGraphic(item, graphic)` / `ClearAgentCache(item)` | 写回 `itemGraphic` / 让游戏下次重建实体时读新 prefab |

## 7. 配置读取

| API | 什么时候用 |
|---|---|
| `Json.Parse(text)` → `GetStr/GetInt/GetFloat/GetBool(key, fallback)` | 读 `config.json`（不用引第三方 JSON 库） |
| `Json.Strings(j, "targets")` | 读字符串数组字段 |

## 8. 排查用

| API | 什么时候用 |
|---|---|
| `ItemModel.DebugOn` + `ItemModel.DumpNewObjects()` | 打开只读诊断：把场上我们造的每个物体打一行（层 / 位置 / 缩放 / 祖先链 / 材质+shader / 同门兄弟数） |
| `Result.Report` | 每次换完都有一句人话报告；`Applied` / `NoOp` 用来决定"要不要打日志" |
