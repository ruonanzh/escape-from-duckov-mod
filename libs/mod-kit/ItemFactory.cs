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
        }

        // ───────────────────────── ① typeID：分配与占用 ─────────────────────────

        /// <summary>游戏自己的下一个空闲 typeID。
        /// 单机游戏，没有并发争号的问题。
        /// （编译器实证：`NextTypeID` 是**实例**属性，走 `Instance`。）
        ///
        /// **不要直接拿它当分配器** —— 见 <see cref="AllocateTypeID"/>。</summary>
        public static int NextFreeTypeID()
        {
            try { return ItemStatsSystem.ItemAssetsCollection.Instance.NextTypeID; }
            catch { return 0; }
        }

        /// <summary>我们分过的号。
        /// 用处：同一个 mod 里两条规则都不写 `newTypeId` 时，它们不能拿到同一个号；
        /// 卸载或热重载后清空，于是同一个号能被重新用。</summary>
        static readonly HashSet<int> _assigned = new HashSet<int>();

        /// <summary>分一个新 typeID。
        ///
        /// **不得直接返回 `NextTypeID`。** 反编译实证：
        /// `NextTypeID` 的 getter 是 `entries.Max(e => e.typeID)` 起的缓存值；
        /// 而 `AddDynamicEntry` 只写 `dynamicDic`，**不碰 `entries`**。
        /// 于是同一个会话里它会**一直返回同一个号**。
        /// 后果：一个 config 里两条 `new_weapon` 都不写 `newTypeId` 时，
        /// 第二条会拿到与第一条相同的号，于是被当成已创建而**静默跳过**。
        /// 所以这里从 `NextTypeID` 起往下找，跳过游戏占的、也跳过我们分过的。
        ///
        /// **为什么是循环**：上面的“恒定”就是它的理由。不跳就只能拿到重复的号，
        /// 而重复的号会被当成“已创建”，于是第二条规则没任何提示地消失。
        ///
        /// **为什么先占号再返回**：两个条目在同一次注册里依次拿号，
        /// 必须在**分出去的时候**就登记，否则第二个会拿到同一个。
        /// 于是若随后克隆失败，那个号在本会话内不再分给别人（只浪费，无害）。
        ///
        /// 上界 1000：号段耗尽或数据异常时宁可报错，也不在这个循环里卡住游戏主线程。
        /// 返回 0 = 分不到（调用方必须报错停下）。</summary>
        public static int AllocateTypeID()
        {
            int id = NextFreeTypeID();
            if (id <= 0) return 0;
            for (int tries = 0; tries < 1000; tries++, id++)
            {
                if (IsTypeIDTaken(id) || _assigned.Contains(id)) continue;
                _assigned.Add(id);
                return id;
            }
            return 0;
        }

        /// <summary>显式指定的号也要登记，否则后面自动分配时可能又挑到它。</summary>
        static void MarkAssigned(int typeID) { if (typeID > 0) _assigned.Add(typeID); }

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
                // 官方做法是 `Item.CreateInstance()`，它就是 `Instantiate` + `Initialize()`。
                // Unity 的 Instantiate 会让 Awake 跑，而 `Item.Awake` 里就会调 Initialize（反编译实证），
                // 所以本来也已经初始化。这里显式再调一次是**不依赖 Awake 的时机**。
                //
                // **幂等有代码级证据**（反编译 `Item.Initialize`）：整段都在一个守卫里 ——
                //     if (!initialized) { initialized = true; … }
                // 第二次调用直接返回，不会重复注册。
                item.Initialize();
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
            MarkAssigned(newTypeID);
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
        /// `handheld`：**枪不要传 true**（默认不预造）。理由（反编译实证）：
        /// 游戏自己会建手持实体 —— `ItemExtensions.CreateHandheldAgent` 在物品第一次被拿时跑，
        /// 而枪那分支是 `ItemAgent_Gun.BuildAgent(item.ItemGraphic.gameObject)`：
        /// 它**实例化我们的图形**，于是**带着图形里的 `Sockets`**，并补 `Muzzle`/`Tec`，
        /// 还设 `handheldSocket = normalHandheld` 与 `handAnimationType = gun`。
        /// 而 `ItemModel.ApplyHandheld` 是克隆**通用模板**再清空它的子物体，
        /// 于是会**丢掉挂点结构**（配件没地方挂）。所以枪这条交给游戏就好。
        /// 物品（本来没有图形的那类）才需要 true。</summary>
        public static ItemModel.Result AttachModel(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture,
                                                   float size, bool handheld = false, int sizeTypeID = 0)
        {
            if (item == null || mesh == null) return new ItemModel.Result { Report = "the item is null or the mesh is null" };

            var ghost = item.ItemGraphic;
            if (ghost == null)
            {
                // 这件物品**本来没有世界图形**（游戏会给它画一张 2D 图片兜底）。实测 1581 件物品里
                // **1173 件**是这样（消耗品、材料大多如此），所以这条不是边角情况。
                //
                // 不能克隆一份不存在的图形，但**可以给它造一份**：`ItemModel.Apply` 在
                // `ItemGraphic == null` 时走的就是那条路（`RegisterDynamicOverride`）。
                // 于是「原型没有图形 + 我们带模型」照样成立。
                //
                // 缩放：这里要**按原型的档位**算，不能按新 typeID —— `ItemModel.Apply` 内部用的是
                // `ModelSize.For(item.TypeID, size)`，而新号在档位表里没有记录。所以我们先把原型
                // 的目标尺寸（米）算出来传进去。config 写了 `size` 就以它为准。
                float meters = size > 0f ? size
                    : (sizeTypeID > 0 ? (ModelSize.For(sizeTypeID, null) ?? 0f) : 0f);
                var built = ItemModel.Apply(item, mesh, texture, handheld, meters);
                item.useSpriteForPickup = false;        // 有模型了，世界里不必再用 2D 图片兜底
                built.Report = "the prototype had no world graphic, so one was built"
                             + " (ModelKit_new_" + item.name + "): " + built.Report;
                return built;
            }

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
                // 只给「本来没有图形」的物品用。枪不要走这条：会丢掉图形里的 Sockets
                // （见方法注释）。
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

        // ───────────────── ④b 分类（Tag）、物品数字、图标 ─────────────────
        //
        // 为什么有这三个：新增「物品」比新增「武器」多几样东西要给。反编译依据都写在方法上。

        /// <summary>给新物品**加** tag（也支持移除）。
        ///
        /// **追加，不是替换**：品类 tag 从原型继承下来，这里加的是**额外标记**
        /// （`ShowCase` = 可摆上展示台；`Collection` / `Luxury` / `Daily` = 归类）。
        /// 所以 config 写 `tags` 不会把原型的分类抹掉。
        ///
        /// 反编译依据（为什么分类重要）：
        ///  · `Item.Repairable` / `Item.Sticky` / `Item.CanBeSold` 都是 `Tags.Contains("…")`
        ///    —— **功能就是从 tag 算出来的**；
        ///  · 插槽准入是 `Slot.CheckAbleToPlug` 里的 `item.Tags.Check(requireTags, excludeTags)`；
        ///    展示台（`Building_Showcase_01` 下的 `Slots`）那 12 个槽全部要求 `ShowCase`。
        ///
        /// `Item.Tags` 这个**属性**只读，但它返回的 `TagCollection` 有 `Add` / `Remove`
        /// （`Duckov.Utilities.TagCollection`），所以改得动。
        /// 名字→`Tag` 用 `GameplayDataSettings.Tags.AllTags` 逐个比 `tag.name`：
        /// 游戏自己的 `TagsData.Get(string)` 是 `internal`，只能拿公开的 `AllTags` 自己找。
        ///
        /// 找不到的名字**不静默跳过**：收集到 `missing` 里让调用方报出来（config 是玩家手写的）。</summary>
        public static int ApplyTags(ItemStatsSystem.Item item, IList<string> add, IList<string> remove,
                                    IList<string> missing = null)
        {
            if (item == null) return 0;
            Duckov.Utilities.TagCollection tags = null;
            try { tags = item.Tags; }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot read Item.Tags: " + ex.Message); }
            if (tags == null)
            {
                if (missing != null) missing.Add("<Item.Tags is null>");
                return 0;
            }

            int changed = 0;
            if (remove != null)
                foreach (var name in remove)
                {
                    var t = FindTag(name);
                    if (t == null) { if (missing != null) missing.Add(name); continue; }
                    try { if (tags.Contains(t)) { tags.Remove(t); changed++; } } catch { }
                }
            if (add != null)
                foreach (var name in add)
                {
                    var t = FindTag(name);
                    if (t == null) { if (missing != null) missing.Add(name); continue; }
                    try { if (!tags.Contains(t)) { tags.Add(t); changed++; } } catch { }
                }
            return changed;
        }

        /// <summary>按名字找一个 `Tag`。名字**大小写不敏感**（config 是玩家手写的）。
        /// 游戏自己的查法是遍历 `AllTags` 比 `name`（`TagsData.Get` 的反编译实现），这里一样。</summary>
        static Duckov.Utilities.Tag FindTag(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                var data = Duckov.Utilities.GameplayDataSettings.Tags;
                if (data == null) return null;
                var all = data.AllTags;
                if (all == null) return null;
                foreach (var t in all)
                    if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ItemFactory] cannot look up the tag '{name}': {ex.Message}");
            }
            return null;
        }

        /// <summary>覆盖物品专有的数字：堆叠上限、价值、重量。
        /// `maxStackCount` ≤ 0 不改；`value` &lt; 0 不改；`weight` &lt; 0 不改（都表示「沿用原型」）。
        ///
        /// **重量要反射**：`Item` 上 `weight` 只有**私有字段**（`api-probe members` 实测），
        /// 公开的 `SelfWeight` / `UnitSelfWeight` 都是只读属性。
        ///
        /// **写完为什么不用手动清缓存**：`TotalWeight` 的 getter 自己会比较
        /// `_cachedWeight != SelfWeight`，不等就重算（反编译实证）。所以写字段就够。
        /// 只为保险把 `_cachedTotalWeight` 置空（万一 `StackCount` 为 0 时比较不出差别）。</summary>
        public static int ApplyItemNumbers(ItemStatsSystem.Item item, int maxStackCount, int value, float weight)
        {
            if (item == null) return 0;
            int changed = 0;

            if (maxStackCount > 0)
            {
                try { item.MaxStackCount = maxStackCount; changed++; }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot set maxStackCount: " + ex.Message); }
            }
            if (value >= 0)
            {
                try { item.Value = value; changed++; }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot set value: " + ex.Message); }
            }
            if (weight >= 0f)
            {
                try
                {
                    var t = typeof(ItemStatsSystem.Item);
                    var f = t.GetField("weight", Priv);
                    if (f == null)
                    {
                        UnityEngine.Debug.LogWarning("[ItemFactory] the private field 'weight' was not found; the weight was not changed");
                    }
                    else
                    {
                        f.SetValue(item, weight);
                        changed++;
                        var cache = t.GetField("_cachedTotalWeight", Priv);
                        if (cache != null) cache.SetValue(item, null);      // 强制重算
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[ItemFactory] cannot set weight: " + ex.Message); }
            }
            return changed;
        }

        /// <summary>把 `icon.png` 设成物品的卡片图标（`item.Icon`）。
        ///
        /// 为什么包一层：真正的实现是 `WeaponIcon.Apply` —— 名字带 Weapon 是历史原因，
        /// 它做的就是「把 png 抠成透明并设成 `Item.Icon`」，**任何物品都能用**（不改名，
        /// 已有玩家 mod 在引它）。图标与 `useSpriteForPickup` 是两件事：
        /// `Icon` 给背包卡片，`useSpriteForPickup` 决定世界里用不用 2D 精灵代替模型。</summary>
        public static string AttachIcon(ItemStatsSystem.Item item, string pngPath) => WeaponIcon.Apply(item, pngPath);

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
            _assigned.Clear();      // 反注册之后这些号又空了，允许重新用（热重载常常用同一个号）
        }

        /// <summary>把一件（新的）物品上架到商人的货架。
        ///
        /// 条目字段：`typeID` / `maxStock` / `forceUnlock` / `priceFactor` / `possibility` / `lockInDemo`。
        /// `possibility` 固定写 **1** —— 反编译实证：`DoRefreshStock` 只对 `0 < possibility < 1` 的条目摸骰子，
        /// 摸不中就 `Show=false` 加清空库存；等于 1 则 `Show=true` 加 `CurrentStock=MaxStock`（每次刷新还补货）。</summary>
        public static StepResult PutOnSale(string merchantID, int typeID, int maxStock, float priceFactor,
                                           bool forceUnlock = true, bool requireExplicitMerchant = false)
        {
            var res = new StepResult();
            if (typeID <= 0) { res.Report = "typeID is missing or not positive"; return res; }
            if (string.IsNullOrEmpty(merchantID))
            {
                if (requireExplicitMerchant)
                {
                    // 物品那条路**不得默认商店**（2026-10-14 定）。游戏里有 **12 个商店档位**
                    // （Merchant_Normal 售货机 / Merchant_Weapon 老吴 / Merchant_Equipment 橘子 /
                    //  Merchant_Ming 小明 / Merchant_Mud 泥巴 / Merchant_Fo 佛哥 /
                    //  Merchant_Myst ×3 神秘商人 / Merchant_Island ×2）加一套独立**黑市**。
                    // 默认上到某一个店是猜的，猜错了玩家在正确的店里看不到东西。
                    // 所以要显式写 merchantID（在线校验也会拦）。
                    res.Report = "merchantID is missing: do not default to a shop, ask the player which one";
                    return res;
                }
                merchantID = DefaultMerchantID;   // 武器那条路的旧约定（老吴的武器店）
            }

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
