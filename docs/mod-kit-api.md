# mod-kit API（写 mod 时会用到的那些）

> 这是**我们自己的**运行时库（`libs/mod-kit/`）。游戏自身的 API 另见 [`mod-api.md`](mod-api.md)。
> 本文只写「**是什么与什么时候用**」，**不抄签名**。签名会变，抄了必过期。
> 要签名就现查：`rg "public static" libs/mod-kit/<文件>.cs`（例如 `ItemModel.cs`）。

## 0. 三条硬约定（先看这个）

- **可还原**：任何「改外形」的调用都返回 `Result`，它带 `Restore()`。**热重载/卸载时必须调**，否则会残留我们的 mesh。
- **有借有还**：`Result` 里记着这次改过的东西（关掉的旧零件、挂上的实例、搬过的挂点、顺手做的 `Nested`）。只调 `Restore()` 就全还回去。
- **层不动**：不自己挑层。游戏设好的 `Character(9)` 照用，其余一律 `Default(0)`（`UI(5)` 会让模型发白）。

## 1. 读模型

| API | 什么时候用 |
|---|---|
| `GltfLoader.LoadFile(path, front)` | 读一个 `.glb`，得到 `Mesh`、主贴图与**法线**。`front` 一般用 `"auto"`。玩家自带的模型按 config 传 `"-z"`、`"+x"` 或 `"-x"` |
| `GltfLoader.Load(byte[], front)` | 同上，但手上已有字节（很少用） |
| `GltfLoader.GuessGrip(pts)` | 自己需要找握把时才用。这是**枪的规则**，只用于**换枪**（`WeaponModel` 内部已经做了）。**物品、装备与近战不得用**。 |
| **限制** | `GltfLoader` **不读 glTF 的节点层级与变换**（`nodes` / `scenes` 的 TRS）。它只把每个 mesh 的图元合并。Tripo 的产物是单位矩阵节点，所以不受影响。玩家自带的 GLB 若节点带缩放、旋转或位移，几何会错位。 |

## 2. 换武器外观

| API | 什么时候用 |
|---|---|
| `WeaponModel.Apply(anchorRoot, mesh, tex, offset, slots, scale)` | **主入口**：把模型换到手持武器上（选锚点、藏旧零件、对齐、搬挂点、换贴图）。`slots` 的值 `[x,y,z]` 单位是**米**，在**我们模型的局部空间**里。枪的这个空间经过**握把归零**（`zeroGrip` 默认 `true`，参照 mod 不传它），零点在**握把点**。`x` 沿 X（左右）· `y` 沿 Y（上下）· `z` 沿 Z（枪口方向） |
| `WeaponIcon.Apply(item, "icon.png")` | 换卡片图标（有图标才调。幂等，内部自己归一化尺寸）|
| `WeaponIcon.ApplyToAllMatching(names, typeIds, pngPath)` | 一次给一批武器换同一个图标。`names` 与 `typeIds` 任一命中即可 |

## 3. 共用登记表（枪和物品两条路共用）

`WeaponModel`（枪、近战）与 `ItemModel`（装备、物品）是**平级**的两条路。
一件东西可能两边都碰，谁先挂上另一个得看得见，否则扫描会把同一个实例**再挂一次**。

| API | 什么时候用 |
|---|---|
| `PatchRegistry.IsPatched(layer)` | 判「这一层我们挂过没有」。**唯一判据**。不得用别的（`Marked`、名字、扫子树都会判错）。 |
| `PatchRegistry.Mark(layer)` / `Unmark(layer)` | 别的能力（例如武器侧）挂完也要登记，自己 `Restore()` 时注销 |
| `PatchRegistry.HasOurMeshUnder(t)` | 这一层（含子树）里有没有我们的 mesh。它给「不得重复挂」用。 |

## 4. 换物品外观

| API | 什么时候用 |
|---|---|
| `ItemModel.Apply(item, mesh, tex, handheld, size)` | **主入口**：换一件物品（有图形就改实例；没图形就自动克隆物品并注册动态条目）|
| `ItemModel.ApplyHandheld(item, mesh, tex, scale)` | 只换「拿在手里」那条。物品**有** `ItemGraphic` 时 `Apply` 不会调它，必须自己调，并把返回值塞进世界那条结果的 `Nested` |
| `ItemModel.ApplyToInstance(item, mesh, tex, scale)` | 只换「场上那一个」（手里、身上），不动模板 |
| `ItemModel.ApplyToTransform(root, …)` / `ApplyToGraphicClone(clone, …)` | 你已经拿到「要改的那一层」时直接用；后者专给**场上的图形副本**（掉落、展示） |
| `ItemModel.ItemShaderName` | 常量，等于 `"SodaCraft/SodaLit"`。只影响本来没有 `itemGraphic` 的物品（例如糖果）。给它们借材质时**固定用这个 shader**。 |
| `ItemModel.CurrentNormalMap` | **调 `Apply*` 之前必须设**，等于 `CachedModel.NormalTexture`。它决定附在材质上的法线图。确实没有时就显式设 `null`。**只给物品**，武器不设。 |

## 5. 尺寸（config `size` 的上游）

| API | 什么时候用 |
|---|---|
| `ModelSize.For(typeID, explicitSize)` | 这件物品的目标长度（米）。`explicitSize` 等于 config 的 `size`。武器按 `GunType_*` 自动给出游戏内尺寸，其它返回 `null`（不缩放） |
| `ModelSize.FactorFor(typeID, size, mesh)` | 一步拿到「该乘多少 scale」（mod 侧最常用） |
| `ModelSize.Factor(mesh, target)` / `Longest(mesh)` | 只要系数或只要最长边时用 |

## 6. 缓存与路径

| API | 什么时候用 |
|---|---|
| `ModelCache.ModDir()` | 本 mod 的目录（DLL 所在处）—— 找 `config.json` 与模型文件 |
| `ModelCache.Get(cache, path, front, out loaded)` | 带指纹的模型缓存（`CachedModel` 带 `Mesh`、`Texture`、`NormalTexture`）。文件没变就不重读。指纹**不含 `size`**，改 `size` 不必重读。 |
| `ModelCache.Fingerprint(path, front)` | 自己要判「该不该重读」时用 |

## 7. 游戏查询 / 操作

| API | 什么时候用 |
|---|---|
| `GameApi.AllItems()` | 枚举场上所有物品（含模板）—— 按 `typeIDs` 找目标 |
| `GameApi.NameMatches(item, "MP5")` | 名字匹配（大小写不敏感的**子串**）|
| `GameApi.DeclaredRenderers(root)` | 游戏**自己声明**的渲染器清单（找「本体」最准，胜过「取最大」） |
| `GameApi.ApplyOurTexture(mat, tex)` | 把我们的贴图换到一份游戏材质上（并清掉对不上的其它贴图槽） |
| `GameApi.ApplyNormalMap(mat, normal, tag)` | 只接**法线**（`_BumpMap` 或 `_NormalMap` 加关键字）。只在 shader 名以 `SodaCraft/` 开头时才接。其余（雾效、精灵、UI）**一律跳过**。 |
| `GameApi.EnsureHandheldAgent(item, graphicPrefab)` / `SetAgentPrefab(…, "Handheld", …)` | 让物品「拿在手里」时用我们的模型 |
| `GameApi.FindMainCharacter()` / `AllCharacters()` / `FindCharacter(name)` | 找玩家 / 角色 |
| `GameApi.VisibleLayer` / `HiddenLayer` | 游戏那两个层（`Character` / `SpecialCamera`） |
| `ItemModel.WriteGraphic(item, graphic)` / `ClearAgentCache(item)` | 写回 `itemGraphic` / 让游戏下次重建实体时读新 prefab |

## 8. 配置读取

| API | 什么时候用 |
|---|---|
| `Json.Parse(text)` 返回 `GetStr/GetInt/GetFloat/GetBool(key, fallback)` | 读 `config.json`（不需要引第三方 JSON 库） |
| `Json.Strings(j, "targets")` | 读字符串数组字段 |

## 9. 排查用

| API | 什么时候用 |
|---|---|
| `ItemModel.DebugOn` + `ItemModel.DumpNewObjects()` | 打开只读诊断：把场上我们造的每个物体打一行（层、位置、缩放、祖先链、材质与 shader、同门兄弟数）|
| `Result.Report` | 每次换完都有一句人话报告。`Applied` 与 `NoOp` 用来决定「是否打日志」。 |

## 10. 新增武器 / 新增物品（数据层）

`ItemFactory`（`libs/mod-kit/ItemFactory.cs`）。做一件游戏里**本来没有**的东西时用。
**换外观**走上面第 2 节与第 4 节。这里是**加新东西**。

**时机**：全部在**进关卡之前**（通常就是 `ModBehaviour.Start()`）。两个原因：
商人的 `Awake` 会把商品条目抄进商店自己的列表；发东西需要玩家，而那时还没有玩家。

| API | 什么时候用 |
|---|---|
| `CloneAsNewItem(源typeID, 新typeID, 显示名, 简介)` | **主入口**：克隆一件现有物品成新 typeID，写名字与简介，注册成动态条目。名字走本地化，键是 `<mod名>.<新typeID>.name`；描述键由游戏派生，是它加 `_Desc` |
| `NextFreeTypeID()` | 取游戏自己的下一个空闲 typeID（config 没写 `newTypeId` 时用） |
| `IsTypeIDTaken(id)` | 这个号被占了吗。**必须在注册之前查** |
| `AttachModel(item, mesh, tex, size, handheld, sizeTypeID)` | 把我们 GLB 的网格装到这个新物品上。内部**克隆图形**，不动原型。缩放参照 `sizeTypeID`，一般传原型枪的号 |
| `ApplyStats(item, 字典, missing)` | 按名字覆盖数值，用**绝对值**。找不到的键收进 `missing`，不静默跳过 |
| `ApplyQuality(item, quality, displayQuality)` | 覆盖品质。`quality` 是整数，-1 表示不改；`displayQuality` 是枚举名 |
| `PutOnSale(merchantID, typeID, maxStock, priceFactor)` | 上架到商人的货架 |
| `AddRecipe(id, typeID, count, money, materials, tags)` | 加一条制作台配方 |
| `RemoveAdded()` | 把我们加过的条目、配方、动态条目全部撤掉（热重载与卸载用） |

**取值**：三个商人是 `Merchant_Normal`、`Merchant_Ming`、`Merchant_Weapon`，默认用最后那个（卖枪的）。
品质枚举是 `None`、`White`、`Green`、`Blue`、`Purple`、`Orange`、`Red`、`Q7`、`Q8`。

**已有的能力不要重写**：手持那条走 `GameApi.EnsureHandheldAgent`（`AttachModel` 内部已经调了）；
发给玩家走 `GameApi.GiveItemToPlayer`（内部先调官方的 `ItemUtilities.SendToPlayer`，反射兜底；**必须在进关卡之后**）。

两条机制的细节见 [`cookbook.md`](cookbook.md) 第 6 节与第 7 节。
