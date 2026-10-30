# mod-kit API（写 mod 时会用到的那些）

> 这是**我们自己的**运行时库（`libs/mod-kit/`）。游戏自身的 API 固定套路另见 `docs/cookbook.md` 第 16 节。
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
| `WeaponModel.Apply(root, mesh, tex, offset, slots, scale)` | **主入口**：把模型换到手持武器上（先选锚点，再关旧外观，然后对齐、搬挂点、换贴图）。关旧外观只留三类：我们自己的、灯与特效、**槽位子树**（判据在 `HidePolicy`）。已挂过的层直接 `NoOp`（判据 `PatchRegistry.IsPatched`）。`slots` 的值 `[x,y,z]` 单位是**米**，在**我们模型的局部空间**里。枪的这个空间经过**握把归零**（`zeroGrip` 默认 `true`，参照 mod 不传它），零点在**握把点**。`x` 沿 X（左右）· `y` 沿 Y（上下）· `z` 沿 Z（枪口方向）。**`pivot` 点落在枪根（=手挂点）的坐标系里**，与锚点（枪身零件）自己的偏移无关 |

| `WeaponIcon.Apply(item, "icon.png")` | 换卡片图标（有图标才调。幂等，内部自己归一化尺寸）|
| `WeaponIcon.ApplyToAllMatching(names, typeIds, pngPath)` | 一次给一批武器换同一个图标。`names` 与 `typeIds` 任一命中即可 |
**不写 `slots` 时**：没有 `pivot` 就按「游戏原枪包围盒的比例映射」对齐，**挂点一个都不动**，配件停在游戏原枪的 `Muzzle/Stock/Scope/…` 上。形状不同的模型通常不贴合（实测 ak103：枪口浮空 3 cm、枪托陷进枪身 26 cm）。要么写 `slots`，要么明确接受这个结果。

### 配件的原点在哪（实测 2026-10-18）

读游戏 `.assets` 里每个网格的 `m_LocalAABB`（相对**配件自己**的原点，单位米）：

| 配件 | z 范围 | 长度 | y 范围 | 原点在 |
|---|---|---|---|---|
| `Muzzle_PST_SIL_1`（消音器） | 0.000 … +0.365 | 0.365 | ±0.044 | **安装面**，朝前长 |
| `Muzzle_AR_SCA_2`（补偿器） | 0.000 … +0.135 | 0.135 | ±0.036 | **安装面**，朝前长 |
| `Stock_ALL_REC_2`（枪托） | −0.338 … +0.007 | 0.345 | −0.118 … +0.044 | **前端面**，朝后长 |
| `Scope_SNP_8X_11`（狙击镜） | ±0.182 | 0.363 | 0.000 … +0.115 | **导轨底面** |
| `Scope_All_RedDot_1`（红点） | ±0.067 | 0.134 | −0.007 … +0.118 | **导轨底面** |

所以 `slots` 该放在**我们模型**的哪个点：

- **枪口**：管轴**末端**。x/y 必须对准枪管中心；z 差几厘米无所谓（配件会套住或伸出去）。
- **枪托**：**机匣后缘**，不是模型屁股。配件枪托从原点朝后长（0.345 米），放在屁股上会整条甩到枪后面。
- **瞄具**：导轨**顶面**；**握把 / 科技件**：护木下 / 侧面导轨。
- 一句话：**x/y 要紧，z 可以粗**。x/y 偏是浮空或扎进枪身，z 偏只是长短。

游戏自己也是「差不多」：AK103 的 `Sockets/Stock` 在 z = −0.035，它自己的枪托网格在 −0.280 … −0.559，
而可换枪托覆盖 −0.373 … −0.028，两者本来就错开。不得追求逐件贴合。

> 待办：等有了 agent 可调用的 editor CLI，让它按这张表自动核对（现在只能人眼看、手调）。

**pivot 落在哪（2026-10-18 修）**：以前 `go.transform.localPosition = extraOffset - target`，而 `go` 的父级是**枪身零件（锚点）**，
于是 pivot 点落到**锚点原点**。锚点在枪根里各有偏移（实测 84 把枪：z 中位 +0.171，最大 +0.728，M14 与 Mosin 都在 +0.71 以上），
同一套 pivot 在不同枪上偏的量都不一样；编辑器把 pivot 点摆在**手挂点**（=枪根），两边必然对不上。
现在改成世界坐标赋值（`go.transform.position = root.TransformPoint(extraOffset - target)`），pivot 点精确落在枪根加 `extraOffset`。

## 3. 共用登记表（枪和物品两条路共用）

`WeaponModel`（枪、近战）与 `ItemModel`（装备、物品）是**平级**的两条路。
一件东西可能两边都碰，谁先挂上另一个得看得见，否则扫描会把同一个实例**再挂一次**。

| API | 什么时候用 |
|---|---|
| `PatchRegistry.IsPatched(layer)` | 判「这一层我们挂过没有」。**唯一判据**。不得用别的（`Marked`、名字、扫子树都会判错）。 |
| `PatchRegistry.Mark(layer)` / `Unmark(layer)` | 别的能力（例如武器侧）挂完也要登记，自己 `Restore()` 时注销 |
| `PatchRegistry.HasOurMeshUnder(t)` | 这一层（含子树）里有没有我们的 mesh。它给「不得重复挂」用。 |

## 3.1 关旧外观的判据（两条路共用）

替换模型时「哪些 Renderer 要关」只有一份实现：`libs/mod-kit/HidePolicy.cs`。`WeaponModel`（换枪）与 `ItemModel`（换物品 / 新增物品 / 掉落的枪）都调它，所以判据不会两边写反。

| API | 什么时候用 |
|---|---|
| `HidePolicy.Hide(root, hiddenOut, out underSocket, out effect)` | **唯一的关旧外观入口**。关掉 root 下除「我们自己的 / 灯与特效 / 槽位子树」以外的渲染器，把关掉的追加进 `hiddenOut`（调用方拿它 `Restore()`）。两个 out 参数是保留计数（日志与验收用） |
| `HidePolicy.IsOurs(name)` | 判「这是我们挂的」：`ModelKit_*`（含旧版武器实例的固定名 `WeaponModel`） |
| `HidePolicy.IsEffectLike(name)` | 判灯/特效：`MuzzleFlash` / `Particle` / `Light` / `Glow` / `Point` / `Fx` / `VFX` / `Flash` / `Trail` |
| `HidePolicy.Sockets(root)` | 找槽位点（`Sockets/<槽位>`，没有容器时退化成名字正好等于槽位名）。槽位**子树不关**：配件的模型是游戏 `ItemGraphicInfo.OnSlotContentChanged` 里 `CreateAGraphic(content, socketPoint)` 现造的 |

> `ShowIf_*` / `HideIf_*` 是 prefab 自带的 `showIfPluged` / `hideIfPluged` 两块「装了 / 没装」的外观，属旧外观，**一并关**。
> 判据**不看枪身名字**：实测 91 把枪里有 13 把一个 `WPN_` 都没有（`Pfb_WPM_Glock_02`、`Arrow`、`pCylinder6`…）。

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
| `GameApi.NameMatches(item, "MP5")` | 按名字命中（大小写不敏感的**子串**）|
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

## 10. 新增武器（数据层）

`ItemFactory`（`libs/mod-kit/ItemFactory.cs`）。做一件游戏里**本来没有**的东西时用。
**换外观**走上面第 2 节与第 4 节。这里是**加新东西**。

**物品也走这一节**（数据层不分武器与物品）：`action` 写 `new_item`。
物品多三样要给的：**Tag**（`ApplyTags`）、**堆叠/价值/重量**（`ApplyItemNumbers`）、
**卡片图标**（`AttachIcon`）。做一件新物品的完整流程见
[`.pi/skills/add-item/SKILL.md`](../.pi/skills/add-item/SKILL.md)。

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
| `ApplyTags(item, add, remove, missing)` | 加 tag 或减 tag。**追加**，不改原型的分类。按名字找 `Tag` 用 `GameplayDataSettings.Tags.AllTags` |
| `ApplyItemNumbers(item, maxStackCount, value, weight)` | 堆叠上限、价值、重量。重量是**私有字段**，内部反射写；缓存在读的时候自己会失效 |
| `AttachIcon(item, pngPath)` | 把 png 设成卡片图标（`Item.Icon`）。包了一层 `WeaponIcon`（名字带 Weapon 是历史原因）|
| `PutOnSale(merchantID, typeID, maxStock, priceFactor)` | 上架到商人的货架。**物品这条路必须给 `merchantID`**（缺了就失败，不默认商店）|
| `AddRecipe(id, typeID, amount, money, materials, tags)` | 加一条制作台配方 |
| `RemoveAdded()` | 把我们加过的条目、配方、动态条目全部撤掉（热重载与卸载用） |

**手上已经有 `Sprite` 时**（不是 png 路径）：直接写 `item.Icon = sprite`。`Item.Icon` 是 `[get/set] Sprite`（签名见 `docs/api/ItemStatsSystem.md`），游戏会读它。`AttachIcon` 只是「从 png 生成再设进去」的包壳。

**取值**：商店档位**不写死**，以游戏的 `StockShopDatabase.merchantProfiles` 为准（**12 个**）。
名单只有一份，在 `.pi/skills/add-item/SKILL.md` 第 1 节；`Merchant_Test` 只以 `StockShop` 对象存在、`merchantProfiles` 里没有它，**不得上架**（`PutOnSale` 会报 `no merchant profile named ...`）。
另有一套独立**黑市**（按 tag 选货，不是 `merchantID`）。
**物品必须显式写 `merchantID`**（校验会拦）；武器不写仍默认 `Merchant_Weapon`（老约定）。
现查：`action=export, class=StockShopDatabase, field=["merchantProfiles[].merchantID"]`（返回 12 个，分号连接）。
品质枚举是 `None`、`White`、`Green`、`Blue`、`Purple`、`Orange`、`Red`、`Q7`、`Q8`。

**已有的能力不得重写**：手持那条走 `GameApi.EnsureHandheldAgent`（`AttachModel` 内部已经调了）；
发给玩家走 `GameApi.GiveItemToPlayer`（内部先调官方的 `ItemUtilities.SendToPlayer`，反射兜底；**必须在进关卡之后**）。

两条机制的细节见 [`cookbook.md`](cookbook.md) 第 6 节与第 7 节。

## 11. 角色替换

把玩家角色换成自己的模型。完整做法与已知限制见 `.pi/skills/replace-character/SKILL.md`，
config 的 `action` 是 `replace_character`（字段：`model`、`scale`）。这里只列接口。

| API | 什么时候用 |
|---|---|
| `CharacterSwap.Apply(character, skin)` | **主入口**：把 `GltfLoader.Loaded` 搬到这个角色的骨架上。模型必须以 `front=auto, zeroGrip:false` 加载，并具有 `UpperArm.R`、`ForeArm.R`、`Hand.R` |
| `CharacterSwap.Tick(now, interval)` | 每帧调。开头 4 秒连着重新隐藏与重挂，之后按 `interval` 节流 |
| 手臂行为 | 右手三根源关节由内部两骨 IK 驱动，掌心对齐 `Hand.Soket.R`；左手三根骨并入 `Spine.004`。手掌不主动旋转。`CharacterArmIk` 是内部实现 |
| `CharacterSwap.ExtraScale` | 在自动尺寸之上再乘的倍数，默认 `1` |
| `CharacterSwap.Result` | `Applied`、`Report`、`Body`、`MappedJoints`、`UnmappedJoints`、`UnweightedVertices`、`Reasserted`；方法 `Reassert(now, interval)` 与 `Restore()`。`Restore()` 后不得再 `Reassert()` |

钩子是**第一次 `Apply` 时自己挂的**（`if (!_hooked)` 里调 `ItemHooks.Install()` 与 `ModelHooks.Install()`）。
mod 侧不用调，也没有对外的 `Install`。

骨骼映射表（`CharacterRig.cs`，名字对错由 `scripts/check-character-rig.mjs` 机器检查）：

| API | 什么时候用 |
|---|---|
| `CharacterRig.TryMap(mixamoBoneName, out duckBone)` | Mixamo 关节名得到游戏骨骼名。映射不上返回 `false` |
| `CharacterRig.StripPrefix(boneName)` | 去掉 `mixamorig:` 之类的前缀 |
| `CharacterRig.DuckBones` / `CharacterRig.DuckSlotCount` | 游戏 26 个骨骼槽的真实名单，顺序即下标 |
| `CharacterRig.ExtraDuckBones` | **离线核对**用的名单：鸭子比 Mixamo 多出来的骨，以及它挂到哪一根。**区分大小写**；运行时用不到（整体换身体时这几根拿不到权重是无害的），只有 `Verify()` 读它 |
| `CharacterRig.Verify()` | 自检：映射表里每个名字都在真实名单里 |
