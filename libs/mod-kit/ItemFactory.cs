// ItemFactory.cs —— 新增物品与武器（**数据层**）：克隆成新 typeID、换我们的模型、覆盖数值与品质、上架、加配方。
//
// 设计：设计仓 `desktop-gamer-agent-pi/plans/add-weapon-skill.md`（新增武器能力）。
//
// 为什么单独一个文件（不塞进 ItemModel）：
//   · `ItemModel` 管的是「把外观换成我们的」（替换）。这里管的是「造一件游戏里本来没有的东西」（新增）。
//   · 数据层**没有武器与物品之分**（武器与物品都走同一套：克隆、typeID、名字、注册、发放），所以放一处。
//   · 模型层仍然分开：武器另外还要 `WeaponModel`（手里那把活枪 + 搬挂点），物品只用 `ItemModel`。
//
// 反编译依据（都有出处，见每段注释）：
//   · `Item.DescriptionRaw` 的 getter 是 `private string description { get { return displayName + "_Desc"; } }`
//     ⇒ **描述键是自动派生的**：名字键 + `_Desc`。只要设名字键，描述就跟着走。
//   · `Item.displayName` 有 get/set（公开成 `DisplayNameRaw`），直接写。
//   · 库里的实测结论：`ItemAssetsCollection.GetPrefab` / `AddDynamicEntry` 是工坊 mod 走过的路
//     （见 `ItemModel.RegisterDynamicOverride` 的长注释）。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    public static class ItemFactory
    {
        public sealed class Result
        {
            public bool Created;
            /// <summary>已经做过、这次不用再做（幂等）</summary>
            public bool NoOp;
            public int TypeID;
            public int SourceTypeID;
            /// <summary>名字的本地化键（描述键是它加 `_Desc`）</summary>
            public string NameKey = "";
            public ItemStatsSystem.Item Item;
            public string Report = "";
            /// <summary>config 里写了、但游戏里不存在的数值键（**不得静默跳过**）</summary>
            public readonly List<string> MissingStats = new List<string>();
        }

        // ───────────────────────── ① typeID：分配与占用 ─────────────────────────

        /// <summary>游戏自己的下一个空闲 typeID。config 不写 `newTypeId` 时用它。
        /// 单机游戏，没有并发争号的问题。
        /// （编译器实证：`NextTypeID` 是**实例**属性，走 `Instance`。）</summary>
        public static int NextFreeTypeID()
        {
            try { return ItemStatsSystem.ItemAssetsCollection.Instance.NextTypeID; }
            catch { return 0; }
        }

        /// <summary>这个 typeID 被占用了吗。
        /// 判据用 `GetPrefab(id) != null`（比 `GetAllTypeIds(ItemFilter)` 少一个依赖，结论相同）。
        /// **必须在注册之前调** —— 我们自己注册过的号也会命中。</summary>
        public static bool IsTypeIDTaken(int typeID)
        {
            if (typeID <= 0) return false;
            try { return ItemStatsSystem.ItemAssetsCollection.GetPrefab(typeID) != null; }
            catch { return false; }
        }

        // ───────────────────────── ② 克隆成新物品 ─────────────────────────

        /// <summary>把一件现有物品克隆成**新的 typeID**。
        ///
        /// 官方指南只说了造「原版物品」的实例（`InstantiateSync`）。造一个**新 typeID** 的那一步
        /// （克隆 + `SetTypeID` + `AddDynamicEntry`）官方没写，只有社区 mod 在做（优香MPX、三角洲合集）。
        ///
        /// `Object.Instantiate` 是**完整拷贝**：数值、插槽、图标、变量、图形引用全部继承原型。
        /// 图形引用是**共享**的（指向原型那份）—— 要换模型时必须**先克隆图形**，见 `AttachModel`。
        ///
        /// 失败必须销毁克隆体（否则留下一个半成品挂在 DDOL 场景里）。</summary>
        public static Result CloneAsNewItem(int srcTypeID, int newTypeID, string displayName, string description)
        {
            var res = new Result { TypeID = newTypeID, SourceTypeID = srcTypeID };
            if (srcTypeID <= 0) { res.Report = "cloneFromItemId is missing or not positive"; return res; }
            if (newTypeID <= 0) { res.Report = "newTypeId is missing or not positive"; return res; }

            ItemStatsSystem.Item src = null;
            try { src = ItemStatsSystem.ItemAssetsCollection.GetPrefab(srcTypeID); }
            catch (Exception ex) { res.Report = "cannot get the source item: " + ex.Message; return res; }
            if (src == null) { res.Report = $"no source item for typeID={srcTypeID}"; return res; }

            ItemStatsSystem.Item item = null;
            try
            {
                item = UnityEngine.Object.Instantiate(src);
                if (item == null) { res.Report = "Instantiate returned null"; return res; }
                item.gameObject.name = "ModelKit_new_" + src.name;
                // 运行时造出来的东西必须活过换场景（图形、物品模板、手持实体都是）
                UnityEngine.Object.DontDestroyOnLoad(item.gameObject);

                SetTypeID(item, newTypeID);
                res.NameKey = SetNameAndDescription(item, newTypeID, displayName, description);

                if (!ItemStatsSystem.ItemAssetsCollection.AddDynamicEntry(item))
                {
                    res.Report = "AddDynamicEntry returned false";
                    UnityEngine.Object.Destroy(item.gameObject);
                    return res;
                }
            }
            catch (Exception ex)
            {
                if (item != null) { try { UnityEngine.Object.Destroy(item.gameObject); } catch { } }
                res.Report = "cannot clone the item: " + ex.Message;
                return res;
            }

            res.Item = item;
            res.Created = true;
            _createdItems.Add(item);            // 卸载/热重载时用它反注册（RemoveAdded）
            res.Report = $"cloned typeID={srcTypeID} -> new typeID={newTypeID} (name key '{res.NameKey}')";
            return res;
        }

        /// <summary>改 typeID。**只能用 `SetTypeID`** —— 编译器实证：`Item.TypeID` 是只读属性
        /// （`docs/api/` 那份 dump 写的是 `[get/set]`，与实际程序集不符；以编译器为准）。</summary>
        static void SetTypeID(ItemStatsSystem.Item item, int typeID)
        {
            item.SetTypeID(typeID);
        }

        /// <summary>写名字与简介。
        ///
        /// **不得**把字面量直接塞进 `DisplayNameRaw` —— 游戏会把它当**本地化键**，界面上显示成 `*字面量*`。
        /// 正解：造一个键，`SetOverrideText(键, 文本)`，再把键写给 `DisplayNameRaw`。
        ///
        /// 描述**不用单独设**：`Item.description` 的 getter 就是 `displayName + "_Desc"`
        /// （反编译实证），所以描述键 = 名字键 + `_Desc`，写同一个本地化表即可。</summary>
        static string SetNameAndDescription(ItemStatsSystem.Item item, int typeID, string displayName, string description)
        {
            string modName = "mod";
            try
            {
                var dir = ModelCache.ModDir();
                if (!string.IsNullOrEmpty(dir)) modName = System.IO.Path.GetFileName(dir.TrimEnd('/', '\\'));
            }
            catch { }
            string key = $"{modName}.{typeID}.name";

            try
            {
                var lm = FindType("SodaCraft.Localizations.LocalizationManager");
                var set = lm?.GetMethod("SetOverrideText",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (set != null)
                {
                    if (!string.IsNullOrEmpty(displayName)) set.Invoke(null, new object[] { key, displayName });
                    // 描述键由游戏自己派生（见上面的反编译实证）
                    if (!string.IsNullOrEmpty(description)) set.Invoke(null, new object[] { key + "_Desc", description });
                }
                else UnityEngine.Debug.LogWarning("[ItemFactory] LocalizationManager.SetOverrideText was not found; the name will show as the raw key");
                if (!string.IsNullOrEmpty(displayName)) item.DisplayNameRaw = key;
            }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot set the name: " + ex.Message); }
            return key;
        }

        /// <summary>按完整名找类型。
        /// `SodaCraft.Localizations.LocalizationManager` 在 **`SodaLocalization.dll`** 里，
        /// 而 mod 的 csproj 只引用 `TeamSoda.*` / `ItemStatsSystem.dll` / `Unity*` ——
        /// 所以只能运行时找，不能编译期写类型名。</summary>
        static Type FindType(string fullName)
        {
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = a.GetType(fullName, throwOnError: false); } catch { }
                    if (t != null) return t;
                }
            }
            catch { }
            return null;
        }

        // ───────────────────────── ③ 图形：换成我们的模型 ─────────────────────────

        /// <summary>把我们 GLB 的网格装到这件**新物品**上。
        ///
        /// **必须克隆图形**，不得改原型的图形：`Instantiate` 出来的物品，它的 `ItemGraphic`
        /// 与原型**指向同一个对象**。就地改它 = 把原型的枪也改了（验收里「原枪没变」那一条）。
        ///
        /// 复用 `ItemModel.ApplyToGraphicClone`（挂网格）与 `ItemModel.ApplyHandheld`（拿在手里那条），
        /// 所以新物品与替换物品走的是同一套挂载逻辑。</summary>
        public static ItemModel.Result AttachModel(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture,
                                                   float size, bool handheld = true, int sizeTypeID = 0)
        {
            if (item == null || mesh == null) return new ItemModel.Result { Report = "the item is null or the mesh is null" };

            var ghost = item.ItemGraphic;
            if (ghost == null)
                return new ItemModel.Result { Report = "the prototype has no ItemGraphic; nothing to clone (use the replace path for this item)" };

            ItemGraphicInfo clone;
            try { clone = UnityEngine.Object.Instantiate(ghost); }
            catch (Exception ex) { return new ItemModel.Result { Report = "cannot clone the graphic: " + ex.Message }; }

            // 游戏自己的图形是**独立资源**，从不挂在物品下面（挂在下面 ⇒ 物品到哪它跟到哪，实测踩过）
            clone.gameObject.name = "ModelKit_graphic_" + item.name;
            clone.transform.SetParent(null);
            clone.transform.position = new Vector3(0f, -5000f, 0f);
            UnityEngine.Object.DontDestroyOnLoad(clone.gameObject);

            // 缩放参照哪件物品：**原型枪**（新造出来的 typeID 不一定进得了 `AllItems()`）
            float scale = ModelSize.FactorFor(sizeTypeID > 0 ? sizeTypeID : item.TypeID, size, mesh);
            var res = ItemModel.ApplyToGraphicClone(clone, item.name, item.TypeID, mesh, texture, scale);

            if (!ItemModel.WriteGraphic(item, clone))
            {
                res.Report = "cannot write back the cloned itemGraphic | " + res.Report;
                return res;
            }
            item.useSpriteForPickup = false;                    // 用模型，不用 2D 精灵

            if (handheld)
            {
                var rh = ItemModel.ApplyHandheld(item, mesh, texture, scale);
                if (rh != null) res.Nested.Add(rh);             // 有借有还：还原时要一起撤
            }
            return res;
        }

        // ───────────────────────── ④ 数值与品质覆盖 ─────────────────────────

        /// <summary>按名字覆盖数值，用**绝对值**（工坊 mod 也是绝对值）。
        ///
        /// 键必须与游戏自己的 `Stat.Key` 完全一致。写错的键**不得静默跳过**，
        /// 收集到 `missing` 里让调用方报出来（config 是玩家手写的，静默无效最难查）。</summary>
        public static int ApplyStats(ItemStatsSystem.Item item, IDictionary<string, float> stats, List<string> missing)
        {
            if (item == null || stats == null) return 0;
            int changed = 0;
            foreach (var kv in stats)
            {
                ItemStatsSystem.Stat stat = null;
                try { stat = item.GetStat(kv.Key); }
                catch { stat = null; }
                if (stat == null)
                {
                    if (missing != null) missing.Add(kv.Key);
                    continue;
                }
                try { stat.BaseValue = kv.Value; changed++; }
                catch { if (missing != null) missing.Add(kv.Key); }
            }
            return changed;
        }

        /// <summary>覆盖品质。`quality` 是整数（-1 表示不改）；`displayQuality` 是枚举名（空表示不改）。
        /// 枚举取值：None White Green Blue Purple Orange Red Q7 Q8（docs/api/ItemStatsSystem.md:37）。</summary>
        public static bool ApplyQuality(ItemStatsSystem.Item item, int quality, string displayQuality)
        {
            if (item == null) return false;
            bool did = false;
            if (quality >= 0) { try { item.Quality = quality; did = true; } catch { } }
            if (!string.IsNullOrEmpty(displayQuality))
            {
                try
                {
                    var v = (ItemStatsSystem.DisplayQuality)Enum.Parse(
                        typeof(ItemStatsSystem.DisplayQuality), displayQuality, ignoreCase: true);
                    item.DisplayQuality = v;
                    did = true;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[ItemFactory] displayQuality '{displayQuality}' is not a value of DisplayQuality: {ex.Message}");
                }
            }
            return did;
        }

        // ───────────────────────── ⑤ 上架到商人，与 ⑥ 加配方 ─────────────────────────
        //
        // 两条机制同构：**ScriptableObject 里的一个 List，注册期追加**（都是反编译实证）。
        //   · 商店：`StockShopDatabase.Instance.merchantProfiles[].entries`
        //   · 配方：`CraftingFormulaCollection.Instance.list`
        //
        // 时机：必须在商人的 `Awake` 之前（`InitializeEntries` 会把条目抄进商店自己的列表）。
        // 也就是**进关卡之前**，与新物品注册同一时机。

        public sealed class StepResult
        {
            public bool Ok;
            public bool NoOp;
            public string Report = "";
        }

        /// <summary>默认上架到哪个商人。`Merchant_Weapon` = 卖枪的那个
        /// （库里有 3 个商人：`Merchant_Normal` 82 条 / `Merchant_Ming` 43 条 / `Merchant_Weapon` 158 条）。</summary>
        public const string DefaultMerchantID = "Merchant_Weapon";

        const System.Reflection.BindingFlags Priv =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        /// <summary>取私有 List 字段（`merchantProfiles[].entries` 与 `CraftingFormulaCollection.list` 都是私有字段）。</summary>
        static System.Collections.IList ListField(object target, string name)
        {
            if (target == null) return null;
            try { return target.GetType().GetField(name, Priv)?.GetValue(target) as System.Collections.IList; }
            catch { return null; }
        }

        // 我们追加过的东西 —— 卸载/热重载时要对称撤销（步骤 9）
        static readonly List<SaleRecord> _sales = new List<SaleRecord>();
        static readonly List<RecipeRecord> _recipes = new List<RecipeRecord>();
        static readonly List<ItemStatsSystem.Item> _createdItems = new List<ItemStatsSystem.Item>();

        sealed class SaleRecord { public string MerchantID; public int TypeID; public object Entry; }
        sealed class RecipeRecord { public string Id; public object Formula; }

        /// <summary>把我们加过的东西全部撤掉：商品条目、配方、动态条目。
        ///
        /// 用途：卸载与**热重载**。不撤的话，配置一改就会**多出一条**（同一件东西在货架上出现两次）。
        /// 幂等：撤过之后再调无事可做。
        ///
        /// **不销毁**那件克隆体：它可能已经进了玩家的背包或存档，销毁它会砸存档。
        /// 只反注册（于是游戏不再用它）—— 热重载时留下的那一件不会有人引用到。
        ///
        /// 注意：**不碰** `unlockedFormulaIDs`（那是存档里的列表，留一条旧 id 无影响）。</summary>
        public static void RemoveAdded()
        {
            // 商品条目：按我们记下的那个对象引用删（引用相等）
            foreach (var s in _sales)
            {
                try
                {
                    var db = StockShopDatabase.Instance;
                    var profile = db != null ? db.GetMerchantProfile(s.MerchantID) : null;
                    var list = ListField(profile, "entries");
                    list?.Remove(s.Entry);
                }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot remove the sale entry: " + ex.Message); }
            }
            _sales.Clear();

            // 配方：按 id 找到下标再删（`CraftingFormula` 是值类型，`Remove(对象)` 不可靠）
            foreach (var r in _recipes)
            {
                try
                {
                    var list = ListField(CraftingFormulaCollection.Instance, "list");
                    if (list == null) continue;
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var f = (CraftingFormula)list[i];
                        if (f.id == r.Id) list.RemoveAt(i);
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot remove the recipe: " + ex.Message); }
            }
            _recipes.Clear();

            // 动态条目：反注册（否则游戏还会把我们那件当成"注册过的"）
            foreach (var it in _createdItems)
            {
                try { if (it != null) ItemStatsSystem.ItemAssetsCollection.RemoveDynamicEntry(it); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot unregister the dynamic entry: " + ex.Message); }
            }
            _createdItems.Clear();
        }

        /// <summary>把一件（新的）物品上架到商人的货架。
        ///
        /// 条目字段：`typeID` / `maxStock` / `forceUnlock` / `priceFactor` / `possibility` / `lockInDemo`。
        /// `possibility` 固定写 **1** —— 反编译实证：`DoRefreshStock` 只对 `0 < possibility < 1` 的条目摸骰子，
        /// 摸不中就 `Show=false` 加清空库存；等于 1 则 `Show=true` 加 `CurrentStock=MaxStock`（每次刷新还补货）。</summary>
        public static StepResult PutOnSale(string merchantID, int typeID, int maxStock, float priceFactor,
                                           bool forceUnlock = true)
        {
            var res = new StepResult();
            if (typeID <= 0) { res.Report = "typeID is missing or not positive"; return res; }
            if (string.IsNullOrEmpty(merchantID)) merchantID = DefaultMerchantID;

            try
            {
                var db = StockShopDatabase.Instance;
                if (db == null) { res.Report = "StockShopDatabase.Instance is null"; return res; }
                var profile = db.GetMerchantProfile(merchantID);
                if (profile == null)
                {
                    res.Report = $"no merchant profile named '{merchantID}' in StockShopDatabase";
                    return res;
                }
                var list = ListField(profile, "entries");
                if (list == null) { res.Report = "cannot read MerchantProfile.entries"; return res; }

                foreach (var e in list)
                {
                    var ie = e as StockShopDatabase.ItemEntry;
                    if (ie != null && ie.typeID == typeID)
                    {
                        res.Ok = true; res.NoOp = true;
                        res.Report = $"typeID={typeID} is already on '{merchantID}'";
                        return res;
                    }
                }

                var entry = new StockShopDatabase.ItemEntry
                {
                    typeID = typeID,
                    maxStock = maxStock > 0 ? maxStock : 1,
                    forceUnlock = forceUnlock,
                    priceFactor = priceFactor > 0f ? priceFactor : 1f,
                    possibility = 1f,
                    lockInDemo = false,
                };
                list.Add(entry);
                _sales.Add(new SaleRecord { MerchantID = merchantID, TypeID = typeID, Entry = entry });

                res.Ok = true;
                res.Report = $"put typeID={typeID} on '{merchantID}' (maxStock={entry.maxStock}, priceFactor={entry.priceFactor}, possibility=1)";
                return res;
            }
            catch (Exception ex) { res.Report = "cannot put the item on sale: " + ex.Message; return res; }
        }

        /// <summary>加一条制作台配方。
        ///
        /// **`unlockByDefault` 必须写 false，并自己调 `UnlockFormula`** —— 反编译实证：
        /// · `CraftingManager.IsFormulaUnlocked(id)` 只看 `unlockedFormulaIDs`，**不看 `unlockByDefault`**。
        /// · 那个列表只在 `CraftingManager.Awake()` → `Load()` 里靠 `unlockByDefault` 填；我们追加配方**晚于**它。
        /// · 而 `UnlockFormula` 碰到 `unlockByDefault == true` 会直接报错返回（"unlocked by default"），**不加 id**。
        /// ⇒ 如果按直觉写 `unlockByDefault = true`，配方会**一直是锁着的**。
        ///
        /// `unlockedFormulaIDs` 会被 `CraftingManager.Save()` 存盘，于是**存档后重进仍是解锁的**。</summary>
        public static StepResult AddRecipe(string id, int typeID, int amount, long money,
                                           IDictionary<int, int> materials, string[] tags)
        {
            var res = new StepResult();
            if (string.IsNullOrEmpty(id)) { res.Report = "the recipe id is empty"; return res; }
            if (typeID <= 0) { res.Report = "typeID is missing or not positive"; return res; }

            try
            {
                var coll = CraftingFormulaCollection.Instance;
                if (coll == null) { res.Report = "CraftingFormulaCollection.Instance is null"; return res; }

                CraftingFormula existing;
                if (CraftingFormulaCollection.TryGetFormula(id, out existing) && !string.IsNullOrEmpty(existing.id))
                {
                    res.Ok = true; res.NoOp = true;
                    res.Report = $"the recipe '{id}' is already there";
                    return res;
                }

                var list = ListField(coll, "list");
                if (list == null) { res.Report = "cannot read CraftingFormulaCollection.list"; return res; }

                var costItems = new List<Duckov.Economy.Cost.ItemEntry>();
                if (materials != null)
                    foreach (var kv in materials)
                        costItems.Add(new Duckov.Economy.Cost.ItemEntry { id = kv.Key, amount = kv.Value });
                var cost = new Duckov.Economy.Cost { money = money, items = costItems.ToArray() };

                var formula = new CraftingFormula
                {
                    id = id,
                    result = new CraftingFormula.ItemEntry { id = typeID, amount = amount > 0 ? amount : 1 },
                    tags = tags ?? new string[0],
                    cost = cost,
                    unlockByDefault = false,        // 见上面的反编译实证：必须 false，否则 UnlockFormula 会拒绝
                    lockInDemo = false,
                    requirePerk = "",
                    hideInIndex = false,
                };
                list.Add(formula);
                _recipes.Add(new RecipeRecord { Id = id, Formula = formula });

                CraftingManager.UnlockFormula(id);      // 公开 static（反编译确认）

                res.Ok = true;
                res.Report = $"added recipe '{id}' -> typeID={typeID} x{formula.result.amount}"
                           + $" (money={money}, materials={costItems.Count}, unlocked)";
                return res;
            }
            catch (Exception ex) { res.Report = "cannot add the recipe: " + ex.Message; return res; }
        }
    }
}
