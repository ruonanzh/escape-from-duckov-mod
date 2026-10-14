# Cookbook（做法速查）

> 每条都注明依据：**反编译**（`tools/api-probe`）、**数据 dump**（`tools/data-probe`）
> 或**运行时实测**。改结论前先复看依据。依据没了（代码改了、也测不了），就把那条删掉，
> 不要留没人验过的说法。
>
> 写到游戏行为时，先问一句「这条有依据吗」。没有就先查，再写。

## 1. 克隆游戏里的 prefab，当「新物体自己的 prefab」

（出处：物品没有 `itemGraphic` 时，我们克隆它自己的物品 prefab 来当新的 prefab。）

- 克隆出来的那份 **要放在很远的地方**：`transform.position = (0, -5000, 0)`。
  它是**模板**，不该在世界里露出来。游戏**实例化副本**时会自己摆正
  （把副本 `SetParent` 到显示点，再把位置、旋转、缩放归零）。
  **前提是调用方传了 `parent`**：传 `null` 时那段代码整块不执行（反编译原文：归零在 `if (parent != null)` 里面）。
     于是副本会**保留模板的变换**，停在 `-5000`，看不见。
- 并且要 **`DontDestroyOnLoad`**。否则**换场景时被销毁**。
  届时游戏手上的 `itemGraphic` 变成「已销毁引用」，**退回 2D 图标**。
- 但是**不得把它挂成物品的子物体**。那样模型会**跟着那件物品渲染**
  （「一把枪跟着玩家跑」或「跟着物品掉地上」）。要独立放置（`SetParent(null)`）。

## 2. 一个「外观」是三层

| | 是什么 | 谁决定 |
|---|---|---|
| ① **世界模型** | 地上、展示、**穿戴**的样子 | config `world`（**默认开**）。纯外观，不改行为 |
| ② **拿在手里** | 手里、装备的样子 | config `handheld`（**默认关**）。会**改行为**（变成可拿，UI 可选中）|
| ③ **2D 图标** | 背包格子、「使用时」那张图 | **游戏自带**。我们**从不删**，但**可以替换** |

- 第 ③ 层那张图是游戏自带的（`Duckov.UI.UsageUtilitiesDisplay`）。我们**不删**。
- 物品**自己没有 3D 图形**时（糖果那类），我们**给它造一份图形**，也就是换掉第 ③ 层显示的东西。

**上表的 `world` 与 `handheld` 只有物品那条路读**（`reference/item_model/ModBehaviour.cs`，
源码里就是 `GetBool("world", true)` 与 `GetBool("handheld", false)`）。
**武器那条路不读它们**：武器替换走 `WeaponModel`，它只改手里那把活枪与场上的实例。
枪的**掉落与展示**图形保持游戏原样，这是**已知限制**（实测两次都会弄坏配件，
记录在 `reference/weapon_model/ModBehaviour.cs` 里）。

新建一把枪的手持实体另有一条规矩，见第 10 节。

## 3. 怎么找到「该换的那个 mesh」

1. **游戏自己声明的集合**：图形根上的 `CharacterSubVisuals.renderers`，也就是「这套外观用哪些渲染器」。
   但 `CharacterSubVisuals.SetRenderers()` 会**重扫所有子渲染器**（反编译）。
     所以运行时挂的**灯与特效也可能被收进这个集合**（实测：头盔被 `SodaPointLight` 骗走过）。
     因此**第 2 条的「排灯与特效」不得省**。
2. 只认 `MeshRenderer` 与 `SkinnedMeshRenderer`（Sprite、灯、粒子一律排除）。
3. **武器**再加一条：排除 `ShowIf_*` 与 `HideIf_*`，并优先名字**含** `WPN_`
   （**子串**，不是前缀。很多枪叫 `Pfb_WPN_TOZ66_S_1`）。
4. 还有多个时，取**包围盒最大**的。
5. 集合为空或没有那个组件时，才回退到「遍历、排灯与特效、比大小」。

- **层一律不动**：游戏掉落物的图形就是 `0` 层。
  我们**试过**动层，那期间出现过两种坏现象（模型**发白并渲染在最前**，或**看不见**）。
  所以做法就一句：**不得动它**（上面这些是「试过什么」，不是因果结论）。

## 4. 层（layer）—— **一律不动**

| 层 | 号 | 谁在用 |
|---|---|---|
| **Default** | 0 | 游戏**世界里**的掉落与展示物品（实测：掉落物 prefab 就是 0 层，而且看得见）。**我们也保持 0** |
| **Character** | 9 | 游戏给「**穿在身上或拿在手里**」的视觉（`CharacterSubVisuals.showLayer`）|
| **SpecialCamera** | 31 | 游戏用来「**隐藏**」的层（`hiddenLayer`。只有特写相机看得见，常规视角下等于隐身）|
| **SodaLight** | 15 | 游戏自定义：**灯效** |
| **UI** | 5（Unity 内置）| 界面用（背包格子、弹窗）。它**不是**给世界模型用的（物品的 GameObject 实测就在 `0` 层）|

- 游戏用**层**来开关视觉（不是 `SetActive`）：显示用 `Character`，隐藏用 `SpecialCamera`。
- **两条路不一样**：**掉落与展示 = `0` 层**，**穿在身上与拿在手里 = `Character`**。
- 所以我们**自己不挑层**：就保持 **`0`**。走「就地改」时，我们的 mesh 会**继承锚点的层**（游戏已经设好的）。

## 5. 运行时挂点表

下表是运行时实测的玩家挂点。游戏数据不是权威来源。

| 挂点字段 | 运行时的路径 | 实测时已装备 |
|---|---|---|
| `rightHandSocket` | `.../Hand.R/Hand.Soket.R/RightHandSocket` | 手里的枪 |
| `helmatSocket` | `.../Spine.004/Head/HelmatSocket` | 帽子、耳机 |
| `faceSocket` | `.../Spine.004/Head/FaceMaskSocket` | 无 |
| `armorSocket` | `.../Spine.004/ArmorSocket` | 无 |
| `backpackSocket` | `.../Spine.004/BackpackSocket` | 背包 |
| `meleeWeaponSocket` | `.../Hand.R/.../MeleeWeaponSocketFixed` | 无 |
| `lefthandSocket` | `.../Hand.L/Hand.Soket.L/LeftHandSocket` | 无 |

耳机使用 `helmatSocket`。实测中，帽子 `IG_Helmat_Hat(Clone)` 和耳机 `IG_Headset_LV1V2(Clone)` 是 `HelmatSocket` 下的兄弟节点，世界坐标相同。

运行时的玩家模型是 `0_CharacterModel_Custom_Template(Clone)`。它的挂点偏移与鸭子 prefab 不同，因此 3D editor 的挂点标记必须按运行时数值校准。

## 6. 上架到商人（往货架加一件东西）

> 本章与第 7、9、10 节都属于「新增一件东西」。只换外观的活看第 1 到第 5 节。

**商品清单在 `StockShopDatabase` 里**，一个商人档案对应一个条目列表。
商人的 `Awake` 跑到 `InitializeEntries()` 时会把条目**抄**进商店自己的列表。于是：

**必须在进关卡之前往档案里追加条目**，商人的 `Awake` 才抄得到它，货架上才有货。

判据收在 `ItemFactory.PutOnSale`。自己写时的要点：

- 条目类型是 `StockShopDatabase.ItemEntry`，字段有 `typeID`、`maxStock`、`forceUnlock`、`priceFactor`、`possibility`、`lockInDemo`，有无参构造。
- **`possibility` 写 1**。`DoRefreshStock()` 只对 `0 < possibility < 1` 的条目掷骰子，掷不中就隐藏并清零；等于 1 时每次刷新补满货。
- **存档扛得住**：`GenerateSaveData` 只按 typeID 存存货数量；`SetupSaveData` 只给**已经在列表里**的条目配对存货。新 typeID 在旧存档里没有记录，那一段整体跳过。
- **`GetMetaData(typeID)` 会被调用**。于是新物品必须**先注册**，商人的 `Awake` 再跑。
- 商人是场景里的组件，`data-probe` 读不到实例。于是只能改数据库。

## 7. 加一条制作台配方

配方清单在 `CraftingFormulaCollection.Instance.list`，元素类型是 `CraftingFormula`。

一条配方有：`id`、`result`（`{id, amount}`）、`tags`、`cost`（`{money, items[]}`）、`unlockByDefault`、`lockInDemo`、`requirePerk`、`hideInIndex`。

**`unlockByDefault` 必须写 false，然后调 `CraftingManager.UnlockFormula(id)`。**

原因（反编译实证）：

- `IsFormulaUnlocked` 只看 `unlockedFormulaIDs`，**不看** `unlockByDefault`。
- 那个列表只在 `CraftingManager.Awake()` 到 `Load()` 这段里靠 `unlockByDefault` 填，而我们追加配方**晚于**它。
- `UnlockFormula` 碰到 `unlockByDefault == true` 会**直接报错返回**，不加 id。

于是按直觉写 `true` 的后果是**配方一直是锁的**。

`unlockedFormulaIDs` 由 `CraftingManager.Save()` 存盘。于是**存档后重进仍是解锁的**。

## 8. 哪些东西是「每件一份」，哪些是「共享的」

要改一件物品身上的东西之前，先问一句：**它是不是这件物品自己 GameObject 上的组件。**
是，克隆各有一份，改了只影响这一件。不是，它就是共享资产，改了会**连累所有人**。

| 东西 | 是什么 | 改它会不会影响别人 |
|---|---|---|
| 数值（`StatCollection`）| 物品自己那个 GameObject 上的组件（数据 dump 实证：它的 `m_GameObject` 就是物品那个）| **不会**。克隆各有一份 |
| 插槽 `slots`、`modifiers` | 同上 | **不会** |
| 手持实体表 `agents` | `ItemAgentUtilities` 每件一个（`private ItemAgentUtilities agentUtilities = new ItemAgentUtilities();`），`agents` 是 `[SerializeField]` | **不会** |
| **世界图形 `itemGraphic`** | 指向**一个共享资产**（枪的 prefab 里就是 `itemGraphic -> pathID …`）| **会。** 同型号那把枪共用同一个 |

于是规矩只有一条：**要改图形，先把图形 `Instantiate` 一份，改克隆，再把克隆写回物品。**
不克隆就直接改 = 把原型枪（以及所有同型号的枪）一起改掉。

**标记键不要自己设。** 像 `IsGun`、`IsBullet` 这种，是物品上那个 `ItemSetting_*` 组件
在**自己的 `Awake`** 里打的（`ItemSettingBase.Awake()` 调 `SetMarkerParam(Item)`，
枪那份是 `SetBool("IsGun", true)`）。Unity 语义：`Instantiate` 出来的对象是激活的，
于是 `Awake` **当场就跑**，标记也就打好了。自己再设一遍是多余。

## 9. 动态条目（新增一个 typeID）到底做了什么

`AddDynamicEntry(prefab)` 只做一件事：`dynamicDic[typeID] = { typeID, prefab }`。
**它不碰 `entries`（主游戏那张表），也不碰 `nextTypeID`。** 三条结论都由这一句推出来：

- **`NextTypeID` 不会推进。** 它的 getter 是 `entries.Max(e => e.typeID)` 起的缓存值，
  而 `entries` 里只有主游戏的物品。于是**同一个会话里它会一直返回同一个号**。
  一个 config 里两条都写「你自己给号」的规则，第二条必然撞号。
  要自动分配，就得**自己在本地去重**（撞游戏、也撞自己分过的号，都往后挪）。
- **元数据是从 prefab 现算的。** `DynamicEntry.MetaData` 是懒算：没值就 `new ItemMetaData(prefab)`。
  于是注册之后 `GetMetaData(号)` 立刻可用（商店刷新与载入存档都会读它，不用怕它空）。
- **存档按 typeID 走。** `ItemTreeData.DataEntry` 的字段是 `instanceID`、`typeID`、`variables`、
  `slotContents`、`inventory`。于是**每次进游戏要用同一个号重新注册**，存档里那把枪才认得回来。

**时机：注册要在进关卡之前**（mod 被激活的菜单阶段就做完）。两个原因都在这份 cookbook 里：
商品表是商人在它自己的 `Awake` 里抄走的（第 6 节）；图形与标记键是在 `Instantiate` 那一刻定下的（第 8 节）。

## 10. 手持实体：**交给游戏建**，不要自己造

症状：新枪拿在手里**没有挂点**（配件挂不上去），或者手里显示的干脆是别的东西。

游戏的做法（反编译）。`ItemExtensions.CreateHandheldAgent(item)` 里，枪走
`ItemAgent_Gun.BuildAgent(item.ItemGraphic.gameObject)`，它做四件事：

1. `Instantiate(图形)`。用的是**我们那份图形**，于是带着图形里的 `Sockets`。
2. 没有 `Sockets` 就补一个，并保证 `Muzzle` 与 `Tec` 在，然后 `AddSocket`。
3. 设 `handheldSocket = normalHandheld`。
4. 设 `handAnimationType = gun`。

所以**枪不要预造手持实体**。交给游戏在第一次被拿时建，它用的是我们那份已经换过网格的图形，
于是手里就是我们的模型，而且挂点结构齐全。

反过来，如果自己走「克隆通用模板，清空它的子物体，再挂上我们的网格」那条路，
手里就**只剩一个网格**，`Sockets` 没了，配件没地方挂。

- 物品 prefab 的数据里 `agents` 是**空的**，这是正常的。`HasHandHeldAgent` 在没拿出来之前是假，不是错。
- 只有「本来没有图形」的物品才需要预造（那条才走通用模板）。

## 11. 别用 `FindObjectsOfType*` 找东西：先看有没有现成入口

`FindObjectsOfType` 和 `Resources.FindObjectsOfTypeAll` 都是**整场景枚举**。
它们不得放进每帧或高频循环。

我们踩过：物品模板的 `ApplyAll` 原本每帧调一次。症状是「越玩越卡」。
记录在 `reference/item_model/ModBehaviour.cs` 的注释里，节流是 `58f3bef` 加的。

三个高频需求都有现成接口。先查这里，再考虑扫描。

**① 找玩家**

| 做法 | 接口 |
|---|---|
| **首选** | `LevelManager.Instance.MainCharacter`。反编译实证：getter 就是 `=> mainCharacter`，读一个缓存字段 |
| 同类 | `ControllingCharacter`（当前操作的角色）、`PetCharacter`（宠物） |
| 事件 | `LevelManager.OnControllingCharacterChanged`。它是静态 `Action<CharacterMainControl>` |
| 等关卡 | `LevelManager.LevelInited` 与 `AfterInit`。两个都是静态 |
| 兜底 | 菜单阶段还没有关卡，`MainCharacter` 是空。这时才退回扫描 |

**② 等时机，不要轮询**

需要「进关卡之后做一次」时，先找这种静态标志，不要每帧问一次。
`LevelManager.LevelInited`、`AfterInit`、`LevelInitializing` 都是静态的。

**③ 枚举物品实例**

游戏**没有**「全部物品实例」的注册表。只能按来源枚举。

| 来源 | 接口 |
|---|---|
| 某个背包 | `Inventory.Content`（`List<Item>`）或 `Inventory.GetEnumerator()` |
| 战利品箱 | `LevelManager.LootBoxInventories`（静态字典） |

按来源枚举会漏。漏掉的物品保持旧外观。所以它只有在拿不到事件时才是好选择。

**④ 图形创建：挂钩子，不要轮询**

游戏每次造世界图形都会走 `ItemGraphicInfo.CreateAGraphic`：

```csharp
public static ItemGraphicInfo CreateAGraphic(Item item, Transform parent, bool snapGround, bool useSpriteIfNoGraphic)
```

它是**公开静态**方法。要「图形一造出来就换成我们的」，就在它上面挂 Harmony 的 postfix。
这样一次扫描都不用。轮询只在拿不到这个依赖时才用。

`ItemGraphicInfo.Setup(item)` 会写反向索引 `ItemRefrence`。
那个索引让你从图形找到它属于哪件物品，但它替代不了枚举。

**代价提醒**：用 Harmony 需要 mod 在 csproj 里引用 `libs/0Harmony.dll`。
仓库的两个模板（`reference/weapon_model`、`reference/item_model`）都没有引。`reference/example_mod` 引了。
玩家自己的 mod 可能已经引了（例如 `weapon_editor` 的产物里就带 `0Harmony.dll`）。
加这个依赖意味着没引的 mod 都要重编。

## 12. 新增物品与新增武器的差别

**数据层是同一套**（`ItemFactory`）：克隆、typeID、名字、注册、上架、配方都不分武器与物品。
差别在**调用方多给几样东西**。做一件新物品的完整流程见 `.pi/skills/add-item/SKILL.md`。

| | 新增武器 | 新增物品 |
|---|---|---|
| 上架到哪 | 不写就默认 `Merchant_Weapon`（老约定）| **必须显式写 `merchantID`**（校验会拦）|
| 模型层 | 手里那把活枪 + 场内实例 | 世界图形 + 卡片图标 |
| 图标 | `icon.png` → `item.Icon` | 同一套 |
| 额外几样 | 插槽偏移 `slots` | `tags`、`maxStackCount`、`value`、`weight` |

### 图标与 `useSpriteForPickup` 不是一回事

- `item.Icon`（一个 `Sprite`）是**背包卡片**上的图。`WeaponIcon.Apply` 就是把 png 抠成透明再设它，
  任何物品都能用（名字带 Weapon 是历史原因）。
- `useSpriteForPickup` 决定**世界里**用不用那张 2D 图代替模型。我们把模型换上去时会把它设成假。
- 所以「换了模型但卡片还是原图」时，缺的是 `Item.Icon`，不是 `useSpriteForPickup`。

### 重量要反射写

`Item` 上 `weight` 只有**私有字段**，公开的 `SelfWeight` / `UnitSelfWeight` 都是只读属性。
`ItemFactory.ApplyItemNumbers` 内部反射写它。**写完不用手动清缓存**：
`TotalWeight` 的 getter 自己会比较 `_cachedWeight != SelfWeight`，不等就重算（反编译实证）。

### Tag 决定分类、功能与掉落

- **品类 tag 从原型继承**（克隆是完整拷贝）。config 里的 `tags` 是**额外标记，追加不是替换**。
- **功能随 tag 走**：`Item.Repairable` / `Sticky` / `CanBeSold` 都是 `Tags.Contains("…")`；
  插槽准入是 `Slot.CheckAbleToPlug` 判 `item.Tags.Check(requireTags, excludeTags)`。
  ⇒ 品类 tag 选错不是分类难看，而是**功能会错**。
- **掉落也随 tag 走**：掉落箱的 `LootBoxLoader.tags.entries[]` 是 tag 池，按物品自己的 tag 抽。
  想上展示台就要 `ShowCase`（展示台的 12 个槽全都要求它）。
- `Collection` / `Luxury` / `Daily` 截至编写时**没查到任何读取点**，写它们不会改变行为。
- 反例提醒：代码读的是 `NotSellable`（`Item.CanBeSold`），而 `NotForSell` **没有代码读**。
