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
| `value` | 售价 |
| `quality` | 品质（0–5）|
| `weight` | 重量 |
| `maxStackCount` | 堆叠上限 |
| `tags` | → `TagCollection`；**分类看这里** |
| `stats` | → `StatCollection`；**数值在这里**（见下）|
| `slots` / `modifiers` / `variables` / `constants` / `effects` | 插槽 / 修改器 / 变量 / 常量 / 效果 |

查看一件物品（`dump` 适合看单个）：

```
action=dump, class=Item, typeid=260, depth=3          # 或 name="Item_S_UAK45_Lv_2"
```

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
| **武器** | `tags.list[].#name=Weapon` | 158 |
| └ 枪械 | `tags.list[].#name=Gun` | 124 |
| └ 近战 | `tags.list[].#name=MeleeWeapon` | 48 |
| **装备（总）** | `tags.list[].#name=Equipment` | 130 |
| └ 护甲 | `=Armor` | 49 |
| └ 头盔 | `=Helmat` | 56 |
| └ 面具 / 耳机 / 背包 | `=FaceMask` / `=Headset` / `=Backpack` | 13 / 2 / 12 |
| **配件（枪械部件）** | `tags.list[].#name=Accessory` | 263 |
| └ 枪口/弹匣/枪托/瞄具/握把 | `=Muzzle` / `=Magazine` / `=Stock` / `=Scope` / `=Grip` | 66 / 53 / 51 / 31 / 23 |
| **图腾** | `tags.list[].#name=Totem` | 69 |
| **钥匙** | `tags.list[].#name=Key`（另有 `=SpecialKey`）| 68 |
| **药品（总）** | `tags.list[].#name=Medic` | 36 |
| └ 治疗 / 注射 | `=Healing` / `=Injector` | 9 / 19 |
| **食物 / 饮料** | `tags.list[].#name=Food` / `=Drink` | 32 / 8 |
| **弹药** | `tags.list[].#name=Bullet` | 141（含 `Cartridge`）|
| **蓝图 / 配方** | `tags.list[].#name=Formula`（`Formula_Blueprint` / `Formula_Cook` …）| 215（`ItemSetting_Formula`）|
| **材料 / 种子 / 作物 / 工具 / 宝石** | `=Material` / `=Seed` / `=Crop` / `=Tool` / `=Gem` | 8 / 32 / 30 / 47 / 4 |
| **任务物品** | `tags.list[].#name=Quest` | 71 |

> 枪械还有细分子类 tag：`GunType_AR` / `GunType_SMG` / `GunType_SHT` / `GunType_SNP` / `GunType_Rifle` / `GunType_BR` / `GunType_PST` …（`match="tags.list[].#name=GunType_AR"`）。

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

## 物品相关 API（改物品时看这些）

`ItemStatsSystem`：`ItemAssetsCollection.InstantiateAsync(typeID)` 生成实例；`Item.GetStat(key)` / `GetStatValue(key)` 读写数值；`ItemUtilities.SendToPlayer(...)` 发给玩家。
更细的 API/实现用 `inspect_game_api`（decompile）。**改数值要在 mod 的 C# 运行时做，不要改游戏数据文件**。
