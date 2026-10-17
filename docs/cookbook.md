# Cookbook（做法速查）

> 每条都注明依据：**反编译**（`tools/api-probe`）、**数据 dump**（`tools/data-probe`）
> 或**运行时实测**。改结论前先复看依据。依据没了（代码改了、也测不了），就把那条删掉，
> 不得留没人验过的说法。
>
> 写到游戏行为时，先问一句「这条有依据吗」。没有就先查，再写。

## 先查这里：按现象找节号

| 现象 | 去哪节 |
|---|---|
| 模型跟着物品跑 / 改了影响别的物品 | §1、§8 |
| 换的模型位置不对 / 旧零件没关掉 | §3 |
| 配件挂不上（不知道挂点叫什么）| §5 |
| 拿在手里没挂点 / 手里东西不对 | §10 |
| 货架上没货 / 配方一直是锁的 | §6、§7 |
| 模型发白 / 渲染在最前 / 看不见 | §4 |
| 同一个号被撞 / 存档认不回来 | §9 |
| 新增武器与新增物品差在哪 | §12 |
| 越玩越卡 | §11 |
| 中文名查不到 | §14 |
| 音效放不出来 | §15 |
| 要调游戏侧 API | §16 |
| 我要查某个游戏数据 | §13 |

## 1. 新物品的模型跟着物品跑 / 换场景变成 2D 图标

**症状**：模型「跟着玩家跑」或「跟着物品掉在地上」；换个场景，物品退回 2D 图标。

**做**：
1. 克隆**物品的 prefab** 当模板（只在物品没有 `itemGraphic`、需要给它造一份时）。
2. 模板 `SetParent(null)` 独立放置，`transform.position = (0, -5000, 0)`。
3. 对**物品 prefab 模板**调 `DontDestroyOnLoad`。
4. **不得**把它挂成物品的子物体。

**依据**：反编译 `ItemGraphicInfo.CreateAGraphic`（签名见 `docs/api/ItemStatsSystem.md:10`）、`ItemModel.cs:595-615`、`ItemFactory.cs:274-280`。归零写在 `if (parent != null)` 里（反编译原文）—— 调用方传 `null` 时副本会停在 `-5000`；不 DDOL 则换场景被销毁，`itemGraphic` 变成已销毁引用，于是退回 2D 图标。
**只对模板**：从零 `new` 出来的图形**不** DDOL（`ItemModel.MakeGraphicForItem`，靠每秒复查再造）；`reference/item_model/README.md:96` 记过「把**克隆出来的图形** DDOL 再写回」，结果是整件物品连原外观一起消失。

## 2. 换了外观却「拿不起来」/ 2D 图标被删了

**症状**：想让它能拿在手里（或相反，不想改行为）；或者背包里的 2D 图标没了。

**做**：一个「外观」是三层，**按层改，别一起改**：

| 层 | 是什么 | 开关 |
|---|---|---|
| ① **世界模型** | 地上、展示、**穿戴**的样子；纯外观 | `world`，**默认开** |
| ② **拿在手里** | 手里/装备的样子；**会改行为**（变成可拿、界面可选）| `handheld`，**默认关** |
| ③ **2D 图标** | 背包格子、使用时那张图 | **游戏自带**，从不删，可替换 |

**依据**：`libs/mod-kit/ModConfig.cs:311-312`（`World = j.GetBool("world", true)` / `Handheld = j.GetBool("handheld", false)`）。
**只有物品这条路读它们**：武器替换走 `WeaponModel`，只改手里那把活枪与场上实例；枪的掉落/展示图形保持原样，是**已知限制**（见 `reference/weapon_model/ModBehaviour.cs`）。
**新增那条只看一半**：`new_item` / `new_weapon` 规则总要挂模型，`world: false` 只打一条 warning 然后照样挂；`handheld` 则真的生效，但只对 `new_item`（`reference/item_model/ModBehaviour.cs:191-196`、`reference/weapon_model/ModBehaviour.cs:270-273`）。
第 ③ 层是 `Duckov.UI.UsageUtilitiesDisplay`；物品**没有** 3D 图形时（糖果那类）才给它造一份。新建一把枪的手持实体见第 10 节。

## 3. 换上去的模型位置不对 / 旧零件没被关掉

**症状**：新模型挂在奇怪的位置；或新旧两个模型同显（枪最常见）。

**做**（找「该换的那个 mesh」，按顺序）：
1. 优先游戏自己声明的集合 `CharacterSubVisuals.renderers` —— 但 `SetRenderers()` 会**重扫所有子渲染器**，灯与特效可能混进来（实测：头盔被 `SodaPointLight` 骗走过）。
2. 只认 `MeshRenderer` / `SkinnedMeshRenderer`（Sprite、灯、粒子一律排除）。
3. 武器：优先名字**含 `WPN_`** 的（子串，不是前缀）。这只是**挑锚点的提示**，关旧件不靠它 —— 见下面「关哪些」。
4. 还有多个时取**包围盒最大**（库里有 `< 5f` 的尺寸上限，超过就当没找到）。
5. 集合为空或没有该组件时，才回退到「遍历、排灯与特效、比大小」。

**关哪些（武器替换）**：**全关**，只留三类 —— 我们自己的（`ModelKit_*`）、灯/特效（`MuzzleFlash` / `Particle` / `Light` / `Glow` / `Point` / `Fx` / `VFX` / `Flash` / `Trail`）、**槽位子树**（`Scope` / `Muzzle` / `Tec` / `Stock` / `Grip` 下面挂的渲染器）。
判据不认枪身名字：实测 91 把枪、297 个渲染器，其中 13 把**一个 `WPN_` 都没有**（`Pfb_WPM_Glock_02`、`Arrow`、`MachineGun01`、`pCylinder6`…），按名字关必然漏。反编译实证：配件模型是 `ItemGraphicInfo.OnSlotContentChanged` 里 `CreateAGraphic(content, socketPoint)` **现造**并挂到 socket 下的，所以 socket 子树**不得关**；`ShowIf_*` / `HideIf_*` 是原枪 prefab 自带的“装了/没装”两块外观（`showIfPluged` / `hideIfPluged`），替换时**要一起关**。

**依据**：`WeaponModel.cs`（Apply 的 ② 段）、`ItemModel.cs:181-236`、`ItemGraphicInfo.OnSlotContentChanged`（反编译）。层一律不动，见第 4 节。

## 4. 模型发白 / 渲染在最前 / 干脆看不见

**症状**：换上去的模型发白、渲染在所有东西前面，或者看不见。

**做**：**不得动 `layer`**，保持 `Default(0)`。

| 层 | 号 | 谁在用 |
|---|---|---|
| **Default** | 0 | 世界里掉落与展示（我们的模型保持这里）|
| **Character** | 9 | 穿在身上 / 拿在手里（游戏自己切）|
| **SpecialCamera** | 31 | 隐藏（只有特写相机看得见，常规视角等于隐身）|
| **SodaLight** | 15 | 灯效 |
| **UI** | 5 | 界面（**不是**给世界模型用的；实测 `UI(5)` 会发白）|

**依据**：游戏用**层**开关视觉（不是 `SetActive`）：显示切 `Character`、隐藏切 `SpecialCamera`。我们**试过**动层，那期间出现过「发白并渲染在最前」与「看不见」两种现象（这是「试过什么」，不是因果结论）。
我们**不挑层**：保持 `0`；就地改时**只有锚点在 `Character(9)` 时继承层**，其余（含 `UI(5)`）一律压回 `0`（`ItemModel.cs:270-276`）。游戏原有渲染器的显隐由游戏自己那套层负责（`GameApi.HideCharacterSkin`），**不是**我们违规改层。

## 5. 挂点对不上 / 配件挂不上

**症状**：配件挂错位置或挂不上；头盔与耳机不重合。

**做**：按**运行时实测**的路径挂（下表）。游戏数据不是权威来源。

| 挂点字段 | 运行时的路径 | 实测时已装备 |
|---|---|---|
| `rightHandSocket` | `.../Hand.R/Hand.Soket.R/RightHandSocket` | 手里的枪 |
| `helmatSocket` | `.../Spine.004/Head/HelmatSocket` | 帽子、耳机 |
| `faceSocket` | `.../Spine.004/Head/FaceMaskSocket` | 无 |
| `armorSocket` | `.../Spine.004/ArmorSocket` | 无 |
| `backpackSocket` | `.../Spine.004/BackpackSocket` | 背包 |
| `meleeWeaponSocket` | `.../Hand.R/.../MeleeWeaponSocketFixed` | 无 |
| `lefthandSocket` | `.../Hand.L/Hand.Soket.L/LeftHandSocket` | 无 |

耳机用 `helmatSocket`；实测中帽子 `IG_Helmat_Hat(Clone)` 与耳机 `IG_Headset_LV1V2(Clone)` 是 `HelmatSocket` 下的兄弟节点，世界坐标相同。
运行时玩家模型是 `0_CharacterModel_Custom_Template(Clone)`，它的挂点偏移与鸭子 prefab 不同，所以 3D editor 的挂点标记必须按运行时数值校准。

**依据**：运行时实测。数据侧的字段名见 `docs/api/TeamSoda.Duckov.Core.md:713-725`（`FaceMaskSocket` 等；本表的 `faceSocket` 是数据字段名）。`editors/duckov_3d_editor/editor.js:257-263` 记的是 **prefab 侧**的父链（`Spine.003`），与运行时（`Spine.004`）不同，以运行时为准。

## 6. 货架上没有新东西

**症状**：装好、启用了、进了关卡，商人货架上没有你要的东西。

**做**：
1. **在进关卡之前**往 `StockShopDatabase` 的商人档案追加条目。商人 `Awake` 跑 `InitializeEntries()` 时会把条目**抄**进商店自己的列表，抄完再改就晚了。
2. 条目类型是 `StockShopDatabase.ItemEntry`，字段 `typeID`、`maxStock`、`forceUnlock`、`priceFactor`、`possibility`、`lockInDemo`，有无参构造。
3. **`possibility` 写 1**。`DoRefreshStock()` 只对 `0 < possibility < 1` 的条目掷骰子，掷不中就隐藏并清零。
4. 新物品必须**先注册**（商人会调 `GetMetaData(typeID)`）。

**验收**：`validate_mod` 通过；启动游戏后 `grep "\[NewItem\]\|\[NewWeapon\]" "$HOME/Library/Logs/TeamSoda/Duckov/Player.log"` 里能看到 `put typeID=… on 'Merchant_…' (maxStock=…, priceFactor=…, possibility=1)`；再进关卡看货架。症状表见 `.pi/skills/add-item/SKILL.md` 第 6 节。

**依据**：`libs/mod-kit/ItemFactory.cs:596-635`（`PutOnSale`）。存档只按 typeID 存存货数量（`GenerateSaveData` / `SetupSaveData`），新 typeID 在旧存档里整体跳过。商家资产是能读的（`list --class StockShop` 有 12 个，`dump` 能拿到 `merchantID`），但那是**数据**；条目在 `Awake` 里已经被抄进运行时商店，所以只能改数据库。

## 7. 配方一直是锁的

**症状**：工作台上能看到配方，但一直锁着。

**做**：清单在 `CraftingFormulaCollection` 的**私有** `list` 里（公开面只有只读 `Entries`，追加要反射）。**不得自己反射**，直接调 `ItemFactory.AddRecipe(id, typeID, amount, money, materials, tags)`。自己写时：`unlockByDefault` 必须写 `false`，再调 `CraftingManager.UnlockFormula(id)`。

**验收**：同第 6 节。成功时 `[NewItem]` 的 Report 是 `added recipe '{id}' -> typeID=… x…`；`the recipe '…' is already there` 是**幂等/没加**那条，别拿它当成功。

**依据**：`libs/mod-kit/ItemFactory.cs:502-507,660-690`。`IsFormulaUnlocked` 只看 `unlockedFormulaIDs`；那个列表只在 `CraftingManager.Awake()` 到 `Load()` 之间按 `unlockByDefault` 填，而我们追加配方**晚于**它；`UnlockFormula` 遇到 `unlockByDefault == true` 会**直接报错返回**，于是配方永远是锁的。`CraftingManager.Save()` 存 `unlockedFormulaIDs`，所以存档后重进仍解锁。

## 8. 改一件物品，别的物品跟着变

**症状**：改一件物品的外观，同型号其它物品也跟着变；或者改完完全没生效。

**做**：先问一句「它是不是这件物品自己 GameObject 上的组件」。是，每件各有一份；不是，就是共享资产。

| 东西 | 是什么 | 改它会不会影响别人 |
|---|---|---|
| 数值（`StatCollection`）| 物品自己那个 GameObject 上的组件（数据 dump 实证：它的 `m_GameObject` 就是物品那个）| **不会**。克隆各有一份 |
| 插槽 `slots`、`modifiers` | 同上 | **不会** |
| 手持实体表 `agents` | `ItemAgentUtilities` 每件一个（`private ItemAgentUtilities agentUtilities = new ItemAgentUtilities();`），`agents` 是 `[SerializeField]` | **不会** |
| **世界图形 `itemGraphic`** | 指向**一个共享资产**（枪的 prefab 里就是 `itemGraphic -> pathID …`）| **会。** 同型号那把枪共用同一个 |

再看你在走哪条路：
- **新建**（新 typeID / 动态条目）：克隆图形、改克隆、写回物品（`ItemFactory.AttachModel`）。
- **替换已有物品**：**不得**写回共享 prefab。只改场上实例并登记（`ItemModel.Apply`），prefab 原封不动。

**依据**：`libs/mod-kit/ItemModel.cs:703-711`（当前做法：只改实例 + 扫描，共享 prefab 原封不动）；`ItemModel.cs:14-18` 记的是**为什么不能克隆写回**（模板被塞进 `DontDestroyOnLoad`，之后每个实例都生在 DDOL 场景，整件物品连原外观一起消失）。注意同文件 `:18` 那句「直接改游戏自己的 prefab」是更早的写法，已被 `:703-711` 取代；克隆写回只出现在 `ItemFactory.cs:270-291`（新建 typeID）与 `ItemModel.cs:530-640`（无图形物品 + 动态条目）。
**标记键不得自己设**：`IsGun`、`IsBullet` 这种是物品上 `ItemSetting_*` 组件在**自己的 `Awake`** 里打的（`ItemSettingBase.Awake()` 调 `SetMarkerParam(Item)`，枪那份是 `SetBool("IsGun", true)`）；`Instantiate` 出来的对象是激活的，`Awake` 当场就跑，自己再设一遍是多余。

## 9. 同一个号被撞 / 存档里的东西认不回来

**症状**：一个 config 里两条「自动给号」的规则撞号；或者重进游戏后，存档里那把枪不认得。

**做**：`AddDynamicEntry(prefab)` 只写 `dynamicDic[typeID] = { typeID, prefab }`，**不碰** `entries`，也不推进 `NextTypeID`。所以：
1. 自己分号时必须**本地去重**（撞游戏、也撞自己分过的号，都往后挪）。
2. 注册要在**进关卡之前**（商品表在商人 `Awake` 里抄走，见第 6 节；图形与标记键在 `Instantiate` 那一刻定下，见第 8 节）。
3. 每次进游戏要用**同一个号**重新注册，存档才认。

**依据**：`ItemFactory.cs:80-97`（`AllocateTypeID` 跳过 `IsTypeIDTaken` 与本地 `_assigned`）；`NextTypeID` 是 `entries.Max(...)` 起算的缓存值，而 `entries` 里只有主游戏物品，所以同一会话它一直返回同一个号；`ItemTreeData.DataEntry` 按 `typeID` 存档；`DynamicEntry.MetaData` 是懒算，注册后 `GetMetaData(号)` 立即可用。

## 10. 手里没挂点 / 手里显示的是别的东西

**症状**：新枪拿在手里没有挂点（配件挂不上），或者手里显示的是别的东西。

**做**：**不得预造手持实体**，交给游戏在第一次被拿时建：`ItemExtensions.CreateHandheldAgent(item)` 里枪走 `ItemAgent_Gun.BuildAgent(item.ItemGraphic.gameObject)`，它 `Instantiate` **我们那份图形**、补 `Sockets`/`Muzzle`/`Tec`、设 `handheldSocket = normalHandheld` 与 `handAnimationType = gun`。自己走「克隆通用模板 + 清空子物体 + 挂网格」那条路，手里就只剩一个网格，`Sockets` 没了。

**例外**：本来没有图形的物品才需要预造（那条走通用模板）。

**依据**：`libs/mod-kit/ItemFactory.cs:235-245`、`libs/mod-kit/ItemModel.cs:655`。物品 prefab 的 `agents` 为空是正常的；`HasHandHeldAgent` 在没拿出来之前是假，不是错。

## 11. 越玩越卡 / 只能全场景扫描

**症状**：帧率随游玩时间下降；或者想拿某个对象时只能全场扫。

**做**：`FindObjectsOfType` 与 `Resources.FindObjectsOfTypeAll` 都是**整场景枚举**，不得放进每帧或高频循环。三个高频需求都有现成入口：

**① 找玩家**：首选 `LevelManager.Instance.MainCharacter`（getter 就是读缓存字段）；同类 `ControllingCharacter` / `PetCharacter`；事件 `LevelManager.OnControllingCharacterChanged`（静态 `Action<CharacterMainControl>`）；等关卡用静态 `LevelInited` / `AfterInit`。菜单阶段 `MainCharacter` 是空，这时才退回扫描。

**② 等时机**：需要「进关卡之后做一次」时用静态标志 `LevelInited` / `AfterInit` / `LevelInitializing`，不得每帧问。

**③ 枚举物品实例**：游戏**没有**「全部物品实例」的注册表，只能按来源枚举——某个背包用 `Inventory.Content`（`List<Item>`）或 `Inventory.GetEnumerator()`，战利品箱用静态 `LevelManager.LootBoxInventories`。按来源枚举会漏，漏掉的物品保持旧外观。

**④ 图形创建：挂钩子，不得轮询**：`libs/mod-kit/ItemHooks` patch 的是 **`ItemGraphicInfo.Setup(Item)`**（世界图形）与 **`ItemAgent.Initialize(Item, ItemAgent.AgentTypes)`**（手持那条）。只挂上 Setup 时手持那条仍走兜底扫描（`_nextHeldSweep` = +1s、`_nextCloneSweep` / `_nextApply` = +5s），安装日志会打 `partial`。自己在别处另挂 patch 是额外依赖，不是必需。

**依据**：`libs/mod-kit/ItemHooks.cs:55-78`、`libs/mod-kit/GameApi.cs:26-45`、`docs/api/TeamSoda.Duckov.Core.md:5385-5411`。`reference/item_model/ModBehaviour.cs` 的注释记了「每帧 `ApplyAll` 导致越玩越卡」，节流是 `58f3bef` 加的。`ItemGraphicInfo.Setup` 会写反向索引 `ItemRefrence`（从图形找回物品），但它替代不了枚举。
**代价**：`ItemHooks` 需要 `libs/0Harmony.dll`；三个模型样板都引了并带 `CopyHarmonyToModRoot` 目标，没引这个依赖的 mod 要重编。

## 12. 新增武器和新增物品差在哪

**症状**：照物品的做法做枪（或反过来），某些字段或步骤对不上。

**做**：数据层是**同一套**（`ItemFactory`），克隆、typeID、名字、注册、上架、配方都不分武器与物品，差别在调用方多给几样。完整流程见 `.pi/skills/add-item/SKILL.md`。

| | 新增武器 | 新增物品 |
|---|---|---|
| 上架到哪 | 不写就默认 `Merchant_Weapon`（老约定）| **必须显式写 `merchantID`**（校验会拦）|
| 模型层 | 手里那把活枪 + 场内实例 | 世界图形 + 卡片图标 |
| 图标 | `icon.png` 设成 `item.Icon` | 同一套 |
| 额外几样 | 插槽偏移 `slots` | `tags`、`maxStackCount`、`value`、`weight` |

**图标不等于 `useSpriteForPickup`**：`item.Icon`（`Sprite`）是**背包卡片**上的图，任何物品都能设；`useSpriteForPickup` 决定**世界里**用不用那张 2D 图，我们换模型时会把它设成假。所以「换了模型但卡片还是游戏原本的图标」缺的是 `Item.Icon`。

**重量要反射写**：`Item.weight` 只有**私有字段**（`SelfWeight` / `UnitSelfWeight` 是只读属性）。`ItemFactory.ApplyItemNumbers` 会反射写它，并额外清一次 `_cachedTotalWeight` 作保险（`TotalWeight` 的 getter 自己也会比对失效）。不得自己再写一遍。

**Tag 决定分类、功能与掉落**：品类 tag 从原型继承（克隆是完整拷贝），config 里的 `tags` 是**追加不是替换**。功能随 tag 走（`Item.Repairable` / `Sticky` / `CanBeSold` 都是 `Tags.Contains("…")`；插槽准入是 `Slot.CanPlug(item)`，内部判 `Tags.Check(requireTags, excludeTags)`），所以品类 tag 选错不是分类难看，而是**功能会错**。掉落也按物品自己的 tag 从 `LootBoxLoader.tags.entries[]` 的池子里抽；要上展示台得有 `ShowCase`（12 个槽都要求它，且物品要自带 3D 图形）。`Collection` / `Luxury` / `Daily` 在公开签名与 strings 里查不到读取点（不等于一定没有，只是没证据）；`NotForSell` 在公开签名里查不到（代码读的是 `NotSellable`）。

**依据**：`libs/mod-kit/ItemFactory.cs:428-431,462-463`；`docs/api/ItemStatsSystem.md:137,314`。

## 13. 我要查 X 用哪条（`inspect_game_data` 速查）

**症状**：手上有个「我要查 XX」的问题，不知道用哪个 action、哪个参数。

**做**：下表每行都实跑过（2026-10-17 复核）。机制、路径语法、`match` 运算符、`--file` / `--limit` / `--offset`，以及「不得把查到的数值抄进代码」，只在 `docs/guides/00-overview.md`。

两条比表格更重要：
- **`export` 不给 `out` 只回 500 行，`dump` 不给 `out` 只回 2000 行**（都是预览）。要全量就落盘 `out="/tmp/<name>"`（落盘上限 100 万行）。落盘**只写 `/tmp`**：`out` 由工具直接写盘、**不校验路径**，写错位置会污染仓库。
- **`pathID` 是文件内编号，跨文件会撞号**（实测 `pathID 1013` 在 `level0` 与 `sharedassets34.assets` 里是两个不同对象）。查之前先确认 `file=`；下表里的 pathID 是**撰写时实测值，会变**，可用 `action=export, class=<C>, field=["pathID"]` 现查。

| 我要查 | 命令 |
|---|---|
| 游戏里有哪些类（含资产类）| `action=classes`（实测：`Material` 2870 · `Mesh` 3162 · `Texture2D` 4366 · `Sprite` 1371 · `Shader` 320）|
| 按名字找一个资产（美术、脚本、预制体都行）| `action=search, pattern=T_Showcase`，返回类名与 `pathID` |
| 某一类资产的名字清单 | `action=export, class=Texture2D, field=["m_Name"], out="/tmp/textures.tsv"` |
| 物品的 typeID 与名字键 | `action=export, class=Item, file=resources.assets, field=["typeID","displayName"], out="/tmp/items.tsv"` |
| 按 tag 筛物品 | 上面那条加 `match=["tags.list[].#name=Food"]` |
| 某个 tag 挂在多少件物品上 | `action=refs, class=Tag, name=ShowCase, by=Item` |
| 谁引用了它（反向查）| `action=refs, class=Tag, pathid=76698, by=Item`（76698 = `ShowCase`，撰写时实测；pathID 按文件编，传错**不报错**，会静默查另一个资产）|
| 一件物品的全部字段 | `action=dump, class=Item, pathid=108943, depth=7`（108943 = 撰写时实测）|
| 某对象的父链与坐标 | `action=transform, pathid=<Transform 的 pathID>, file=levelN`（场景与 prefab 在 `levelN` 里，`file=` 不得省：不给 `file` 只会扫 `*.assets`，可能命中另一个文件的同号对象）|
| 商店档位（`merchantID`）| `action=export, class=StockShop, field=["merchantID","DisplayNameKey","sellFactor"]` |
| 商店档案（保留嵌套，看每个店有哪些货）| `action=dump, class=StockShopDatabase, pathid=76730, depth=8, out="/tmp/shopdb.txt"`（`depth=6` 看不到货，全量 5075 行；同一条也记在 `docs/guides/world.md`）|
| 掉落池（哪些 tag 会掉）| `action=export, class=LootBoxLoader, file=resources.assets, field=["tags.entries[].value.#name","tags.entries[].weight","activeChance"]` |
| 插槽要求哪些 tag | `action=export, class=SlotCollection, field=["list[].key","list[].requireTags"]`（`requireTags` 是引用，默认只回 `pathID N`；先 `dump` 一件确认形状，再加 `.#name`）|
| 全部 tag 资产 | `action=export, class=Tag, file=resources.assets, field=["m_Name"]` |
| **中文名 / 文案** | **不在这里**：读游戏自带的 `StreamingAssets/Localization/ChineseSimplified.csv`（见第 14 节）|

**依据**：`docs/guides/00-overview.md`（机制与坑的唯一副本）、`tools/data-probe/README.md`（出口码与静默边界）。失败时看退出码：`0` 成功（含合法空结果，会带 `# WARNING`）、`1` 目标不存在、`2` 调用方式错。

## 14. 中文名查不到 / 以为游戏里没有

**症状**：查不到某件物品的中文名，或者「表里没有」被当成「游戏里没有」。

**做**：**唯一权威**是游戏自带的本地化文件：

```
macOS   <游戏>/Duckov.app/Contents/Resources/Data/StreamingAssets/Localization/<语言>.csv
Windows <游戏>/Duckov_Data/StreamingAssets/Localization/<语言>.csv
```

共 10 个语言：`ChineseSimplified`（简体中文）、`ChineseTraditional`、`English`、`French`、`German`、`Japanese`、`Korean`、`Portuguese`、`Russian`、`Spanish`。表头是 `key,value,version,sheet`：

```
Item_Candy,棒棒糖,#20250911092425,Items
Character_Ming,小明,#20250807035821,Characters
```

- **按 `key` 建索引**：同一个 key 在**不同语言包**里可能属不同 sheet（实测 10 个 CSV 共 58394 行，**没有重复 key**；`Item_ButcherKnife` 在英文包里属 `Default`，在中文包里属 `Items`）。
- 固定命令：`grep -F "Character_Ming," "<那个目录>/ChineseSimplified.csv"`。
- key 从哪来：数据里的 `displayName` / `nameKey` / `displayNameKey` / `xxxKey` 字段就是它。

**两条不得**：
1. **不得说「某个表里没有 = 游戏里没有」**。这个仓库**不再保存**本地化快照（旧那份 `docs/data/*.csv` 的 Items 只有 892 条本地化行，游戏自带的有 2942 条，已经害过几次「以为没有，其实有」）。要文案就读游戏自带那份，或先 `inspect_game_data` 现查。
2. **不得把文案抄进代码**（会随版本变）。

**依据**：游戏目录实读（10 个 csv、表头与两行示例逐字核对过）。

## 15. 音效放不出来 / FMOD 事件找不到

**症状**：mod 里加的音效没响，或者不知道怎么触发游戏自己的 FMOD 事件。

**做**：游戏用 **FMOD**。**先考虑 `AudioSource` + `AudioClip`**（Unity 原生，不用碰 FMOD API）：

```csharp
var source = gameObject.AddComponent<AudioSource>();
source.clip = myClip;   // 自己加载到的 AudioClip
source.Play();
```

要走 FMOD 本身时：

```csharp
var ev = FMODUnity.RuntimeManager.CreateInstance("event:/MyEvent");
ev.start();
```

- `LoadBank` 是**两参**：`LoadBank(String bankName, Boolean loadSamples)`（签名见 `docs/api/unity-resources.md`）。
- 游戏自己的 bank 在 `StreamingAssets/` 下：撰写时（`game.version` = `2.3.30`）有 `Master.bank`、`Master.strings.bank` 与 `Music/`。事件名要用 FMOD 工具解析，本文不保证。
- 音频资源的查找命令见第 13 节。

**依据**：`docs/api/unity-resources.md` 的 FMOD 段；游戏目录实读。

## 16. 游戏侧 API 怎么拼（物品 / 角色 / 对话 / 日志）

**症状**：知道要调哪个类，但签名、参数个数或返回值对不上。

**做**：**签名一律以 `docs/api/` 为准**，不够时用 `inspect_game_api`。这里只记「几行拼起来就能用」的套路与易错点（静态方法、返回值是 `UniTask`）。

```csharp
using ItemStatsSystem;

// 生成物品（typeID 现查：见 docs/guides/items.md）
Item item = await ItemAssetsCollection.InstantiateAsync(254);   // UniTask<Item>，必须 await
Item itemSync = ItemAssetsCollection.InstantiateSync(254);      // 同步版

// 发物品给玩家（三参是正解）
ItemUtilities.SendToPlayer(item, dontMerge: false, sendToStorage: true);
```

- `IsInPlayerCharacter` / `IsInPlayerStorage` / `TryPlug` 都是 `ItemUtilities` 的**静态方法**，第一个参数是物品；`TryPlug` 是 **5 参**，最后一个是 `preferredFirstIndex`。
- `Item.Icon` 可直接赋值（`Sprite`）。

```csharp
// 队伍是枚举，没有 TeamA（权威见 docs/api/TeamSoda.Duckov.Core.md 的 ## Teams）
character.SetTeam(Teams.player);
// Teams：player=0 、scav=1 、usec=3 、bear=4 、middle=5 、lab=6 、all=7 、wolf=8

using Dialogues;
using Duckov.UI.DialogueBubbles;

// 底部字幕：收的是 SubtitlesRequestInfo，不是字符串
await DialogueUI.instance.DoSubtitle(info);

// 对话气泡：七参（text, target, yOffset, needInteraction, skippable, speed, duration）
DialogueBubblesManager.Show("Hello world!", target, 0f, false, true, 30f, 3f);
```

- 日志：macOS `~/Library/Logs/TeamSoda/Duckov/Player.log`；Windows `%USERPROFILE%\AppData\LocalLow\TeamSoda\Duckov\Player.log`。

**依据**：`docs/api/TeamSoda.Duckov.Core.md:5333,5337,3629,6239-6246`、`docs/api/ItemStatsSystem.md:137,254`。
