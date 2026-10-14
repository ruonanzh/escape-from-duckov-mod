# Cookbook（做法速查）

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
| **SodaLight** | — | 游戏自定义：**灯效** |
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
