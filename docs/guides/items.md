# 物品（Items）与子类

游戏里所有物品都是 `Item`（`ItemStatsSystem`）—— **一次 export 就能拿全**（总数 **1581**）。
「武器 / 装备 / 药品 / 食物…」不是不同的类，而是 **`Item.tags` 里的 Tag**。

```
action=export, class=Item, file=resources.assets            # 全部 1581 件
```

## `Item` 的常用字段（实测）

| 字段 | 含义 |
|---|---|
| `typeID` | 物品 ID（`ItemAssetsCollection.InstantiateAsync(typeID)` 用它生成实例）|
| `displayName` | **本地化 key**（不是中文！见 `00-overview.md` 的 join 段）|
| `value` | 单价（**单位价值**；见下方⚠️）|
| `quality` | 品质（0–5）|
| `weight` | 重量 |
| `maxStackCount` | 堆叠上限 |
| `variables` | → `entries[]`（`key` / `dataType` / `data` / `display`）；**`data` 是原始字节** |
| `tags` | → `TagCollection`；**分类看这里** |
| `stats` | → `StatCollection`；**数值在这里**（见下）|
| `slots` / `modifiers` / `variables` / `constants` / `effects` | 插槽 / 修改器 / 变量 / 常量 / 效果 |

查看一件物品（`dump` 适合看单个）：

```
action=dump, class=Item, typeid=260, depth=3          # 或 name="Item_S_UAK45_Lv_2"
```

### ⚠️ `value` 是**单位**价值；「每包价值」要乘 `variables.Count`

有些物品在游戏/wiki 里显示的价是**一整包**的（实测：绷带 `Count=3`、子弹 `Count=30`）：

```
action=export, class=Item, match=["typeID=10"],
field=["displayName","value","maxStackCount","variables.entries[].key","variables.entries[].data"]
# → Item_Bandage  80  3  Count  "03 00 00 00"     （字节 = 小端 int32 = 3）
#   单价 80 × Count 3 = 240（wiki 上显示的就是 240）
```

- `variables.entries[]` 的 **`data` 是原始字节**（如 `03 00 00 00` = 小端 int32 → 3）；`dataType=2` 表示 int。
- 常见的 key：`Count`（一件等于几件）、`Durability`（耐久）。

> 本页的数量与分类**已与官方 wiki 逐条比对**（1564 件共有物品）：`tags` **100% 一致**，`quality`/`weight`/`displayNameKey` **零差异**；唯一差异就是 `value`（wiki 显示每包总量 = `value × Count`）。

## 分类 = Tag（官方分类法）

枚举全部 **134** 个 Tag：

```
action=export, class=Tag, file=resources.assets
```

用 Tag 过滤物品（**通用写法**，换成任意 tag 名即可）：

```
action=export, class=Item, match=["tags.list[].#name=Weapon"], field=["displayName","typeID"]
```

### 常用分类对照（括号内是**实测**数量）

| 概念 | Tag / 写法 | 数量 |
|---|---|---|
| **武器（要“全部武器”看这行）** | `Weapon` **∪** `MeleeWeapon`（无单条命令，见下）| **172** |
| └ 主标签 | `tags.list[].#name=Weapon` | 158 |
| └ 枪械 | `tags.list[].#name=Gun` | 124 |
| └ 近战 | `tags.list[].#name=MeleeWeapon` | 48 |
| **装备（总）** | `tags.list[].#name=Equipment` | 130 |
| └ 护甲 | `=Armor` | 49 |
| └ 头盔 | `=Helmat` | 56 |
| └ 面具 / 耳机 / 背包 | `=FaceMask` / `=Headset` / `=Backpack` | 13 / 2 / 12 |
| **配件（枪械部件）** | `tags.list[].#name=Accessory` | 263 |
| └ 枪口/弹匣/枪托/瞄具/握把 | `=Muzzle` / `=Magazine` / `=Stock` / `=Scope` / `=Grip` | 67 / 53 / 51 / 31 / 23 |
| **图腾** | `tags.list[].#name=Totem` | 69 |
| **钥匙** | `tags.list[].#name=Key`（另有 `=SpecialKey`）| 68 |
| **药品（总）** | `tags.list[].#name=Medic` | 36 |
| └ 治疗 / 注射 | `=Healing` / `=Injector` | 9 / 19 |
| **食物 / 饮料** | `tags.list[].#name=Food` / `=Drink` | 32 / 8 |
| **弹药** | `tags.list[].#name=Bullet` | 145（另有 `Cartridge` 6）|
| **蓝图 / 配方** | `tags.list[].#name=Formula`（`Formula_Blueprint` 178 / `Formula_Printer` 29 …）| 215 |
| **材料 / 种子 / 作物 / 工具 / 宝石** | `=Material` / `=Seed` / `=Crop` / `=Tool` / `=Gem` | 8 / 32 / 30 / 47 / 4 |
| **任务物品** | `tags.list[].#name=Quest` | 71 |

> 枪械还有细分子类 tag：`GunType_AR` / `GunType_SMG` / `GunType_SHT` / `GunType_SNP` / `GunType_Rifle` / `GunType_BR` / `GunType_PST` …（`match="tags.list[].#name=GunType_AR"`）。

> ⚠️ **“全部武器”不是 158（实测踩过）**：`Weapon`(158) 与 `MeleeWeapon`(48) 有 **34** 重叠；另有 **14 个只挂 `MeleeWeapon`、没有 `Weapon`** —— `Item_HammerL`(大锤) / `Item_Wrench`(扳手) / `Item_Shovel`(铁铲) / `Item_GolfClub` / `Item_GoldDumbbell` / `Item_SaltedFish` / `Item_GiantSwordFish` …（仓库里算工具/日用，但能当近战用）。**并集 = 172**。
> `match` **只有 AND**（多条 = 同时满足），所以并集要**两次调用再按 typeID 合并去重**：
> ```
> action=export, class=Item, match=["tags.list[].#name=Weapon"],     field=["typeID","displayName",...], out="/tmp/w1.tsv"
> action=export, class=Item, match=["tags.list[].#name=MeleeWeapon"], field=["typeID","displayName",...], out="/tmp/w2.tsv"
> # 然后按第 2 列 typeID 去重合并（bash/python）→ 172
> ```

## 数值（stats）—— 平行数组

`Item.stats` 是 PPtr，指向 `StatCollection`；其 `list[]` 是**内联**的 `{ key, baseValue, ... }`。
`export` 里 `stats.list[].key` 与 `stats.list[].baseValue` 输出的是**两个平行数组**（按 `;` 切开后一一对应）。

**一次调用拿全 124 把枪的全部数值**（不猜、不用逐条 dump）：

```
action=export,
class=Item,
match=["m_GameObject.m_Component[].component.#class=ItemSetting_Gun"],
field=["displayName","value","quality","stats.list[].key","stats.list[].baseValue"],
out="/tmp/guns.tsv", file="resources.assets"
```

> `match` 里的 `m_GameObject.m_Component[].component.#class=ItemSetting_Gun` 是「这个物品的 GameObject 上挂着 `ItemSetting_Gun` 组件」——**组件的类名**也能判子类型。但它与 tag **口径略有差异**（实测：组件 `ItemSetting_MeleeWeapon`=45 vs tag `MeleeWeapon`=48；有的设置在子物体上），**能不用 tag 就不用组件**，用组件时心里有数。
> 其他组件：`ItemSetting_Accessory`(263) / `ItemSetting_Bullet`(145) / `ItemSetting_Formula`(215) / `ItemSetting_Skill`(19) / `ItemSetting_NightVision`(3)。

把平行数组转成 `属性=值`（bash + python 片段）：

```python
keys = c[6].split(';'); vals = c[7].split(';')      # 列号按导出顺序
for k, v in zip(keys, vals):
    print(f"{k} = {v}")
```

## 附录：全部 Tag（134 个）—— **别当成 134 个品类**

`Tag` 资产一**134 个**，但只有 **121 个真的挂在物品上**，且含义混三类：**品类**（这是什么）、**子类型**（品类细分）、**行为标记**（可修理/展示/锁定…）。
选物品时**从 A / B 里挑**；C 类当过滤条件可以，当“品类”会奇怪。数字是实测（多少件物品挂了这个 tag）。

> ⚠️ **本表只用于“选对 tag 名”；数字会随版本变，精确值一律现查**：`action=export, class=Tag`（列出全部 tag）+ 计数（`match=["tags.list[].#name=X"]` 的返回行数）。表内数字与正文「常用分类对照」不一致时，**以现查为准**。

### A. 主要品类（挑“要哪类物品”用这些）

| Tag | n | 说明（看名字/实例）|
|---|---|---|
| `Accessory` | 263 | 枪械配件（总）|
| `Formula` | 215 | 配方 / 蓝图（总）|
| `Weapon` | 158 | 武器（**注意：不包含只有 `MeleeWeapon` 的 14 件工具/日用，见上文警告**）|
| `Bullet` | 145 | 弹药 |
| `Equipment` | 130 | 装备（总）|
| `Gun` | 124 | 枪械 |
| `Quest` | 71 | 任务物品 |
| `Totem` | 69 | 图腾 |
| `Key` | 68 | 钥匙 |
| `Muzzle` | 67 | 枪口 |
| `Helmat` | 56 | 头盔（原文拼写如此）|
| `Magazine` | 53 | 弹匣 |
| `Stock` | 51 | 枪托 |
| `Armor` | 49 | 护甲 |
| `MeleeWeapon` | 48 | 近战武器 |
| `Tool` | 47 | 工具 |
| `Element` | 47 | 元素相关（子弹：毒/燃烧/电）|
| `Medic` | 36 | 药品（总）|
| `Food` | 32 | 食物 |
| `Seed` | 32 | 种子 |
| `Scope` | 31 | 瞄具 |
| `Crop` | 30 | 作物 |
| `Grip` | 23 | 握把 |
| `Injector` | 19 | 注射剂 |
| `FaceMask` | 13 | 面具 |
| `Backpack` | 12 | 背包 |
| `Explosive` | 12 | 爆炸物 |
| `Bait` | 12 | 鱼饵 |
| `Healing` | 9 | 治疗品 |
| `Drink` | 8 | 饮料 |
| `Material` | 8 | 材料 |
| `Continer` | 7 | 容器（原文拼写如此）|
| `Cartridge` | 6 | 卡带 / 弹壳 |
| `Gem` | 4 | 宝石 |
| `Pelt` | 3 | 兽皮 |
| `Headset` | 2 | 耳机 |
| `Cash` | 1 | 现金 |

### B. 子类型（在品类上再细分）

| 组 | Tag（n）|
|---|---|
| 枪枝细分 | `GunType_AR`(67) `GunType_SMG`(54) `GunType_SHT`(50) `GunType_SNP`(47) `GunType_PST`(26) `GunType_BR`(12) `GunType_ARR`(5) `GunType_MAG`(3) `GunType_Rocket`(1) `GunType_PWS`(1)（未用：`GunType_Rifle/Shot/Sniper`）|
| 配方细分 | `Formula_Blueprint`(178) `Formula_Printer`(29) `Formula_Medic`(7) `Formula_Normal`(1)（未用：`Formula_Cook`）|
| 鱼 / 钓鱼 | `Fish`(33) `Fish_Special`(14) `Fish_OnlyNight`(10) `Fish_OnlyDay`(8) `Fish_OnlyRainDay`(6) `Fish_OnlySunDay`(5) `Fish_OnlyStorm`(5)（未用：`Fish_Other`）|
| 配件专属 | `Acc_*`（每个 1–2 件：`Acc_Aug` / `Acc_M14` / `Acc_UZI` …）|
| 被动 / 特殊件 | `PassiveProp`(51) `SpecialAcc`(22) `SpecialKey`(20) `TecEquip`(15 科技件) `Western`(55 西式枪械) `SnowLand`(12 雪地版) |
| 其它细分 | `Matryoshka_1`(5) `Gem_Armor_Igny`(1) `Gem_Armor_Fleeze`(1) |

### C. 行为 / 标记（**不是品类**；当过滤条件用）

`Special`(269 含义宽——家具/植物/枪都有) · `Repairable`(221 可修理) · `ShowCase`(109 展示) ·
`DontDropOnDeadInSlot`(107) · `DestroyInBase`(69) · `DestroyOnLootBox`(50) · `LockInDemo`(44 试玩锁定) ·
`Luxury`(40 贵重) · `Daily`(34 日用) · `DecorateEquipment`(27) · `MiniGame`(14) · `Collection`(14 收藏) ·
`Computer`(13) · `Information`(7) · `ColorCard`(6) · `Base_WallPaper`(6) · `NotNested`(5) ·
`AdvancedDebuffMode`(4) · `JLab`(4) · `Shit`(4) · `ComputerParts_GPU`(3) · `Base_Deco`(3) ·
`SnowBall`(2) · `Monitor`(2) · `Sticky`(1) · `NotForSell`(1) · `Character`(1) · `FcController`(1) · `GamingConsole`(1) ·
`SoulCube`(1) · `WaterBall`(1) · `Earthworm`(1)

### D. 物品上**没用到**的 13 个

`DogTag` `Fish_Other` `Formula_Cook` `GunType_Rifle` `GunType_Shot` `GunType_Sniper` `Matryoshka_2` `Matryoshka_3` `Matryoshka_4` `Matryoshka_5` `Misc` `NotSellable` `Weapon_LV1`

> 这 13 个可能在**其它类**（非 `Item`）或未使用的旧 tag 上。要确认全量就现查：`action=export, class=Tag`。

## 物品相关 API（改物品时看这些）

`ItemStatsSystem`：`ItemAssetsCollection.InstantiateAsync(typeID)` 生成实例；`Item.GetStat(key)` / `GetStatValue(key)` 读写数值；`ItemUtilities.SendToPlayer(...)` 发给玩家。
更细的 API/实现用 `inspect_game_api`（decompile）。**改数值要在 mod 的 C# 运行时做，不要改游戏数据文件**。
