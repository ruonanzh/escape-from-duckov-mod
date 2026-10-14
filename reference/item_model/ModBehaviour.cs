// ItemModel（示例 mod）—— 把**一件（或一批）物品**的模型换成你给的 GLB。
//
// 这就是"替换物品模型"的**完整正确做法**：整个 mod 只有这一份配置 + 一句调用
// （运行时的全部机制在 libs/mod-kit/ItemModel.cs 那里有实测踩过的坑清单）
//
// config.json 支持两种写法（都行）：
//
// ① 一套素材换一件物品
// { "typeIDs": [36], "model": "backpack.glb" }
//
// ② 每件物品各换各的（一个 mod 多条规则 按数组顺序匹配，**先命中的生效**）
// { "entries": [
// { "typeIDs": [260], "model": "backpack.glb" },
// { "targets": ["Item_BackpackLV3"], "model": "backpack_lv3.glb" } ] }
//
// 字段（每条都能用）：
// targets = 物品**对象名全等**（数组 不区分大小写；例 ["Item_BackpackLV3"]）
// typeIDs = 或精确命中（数组 例 [260, 261] —— 更稳 名字是英文对象名，typeIDs 不受本地化影响）
// model = 放在本 mod 目录里的 GLB 文件（相对路径或绝对路径）
// front = 仅"用户自带的模型"需要：auto/+z/-z/+x/-x 声明朝向（Tripo 出的由提示词保证）
//
// 运行时对**每一件命中的物品**做三件事（见 libs/mod-kit/ItemModel.cs）：
// ① **就地**改游戏自己的图形 prefab（item.ItemGraphic / ItemAssetsCollection.GetPrefab）
// —— 不克隆模板 不 DontDestroyOnLoad：
// 克隆件放进 DDOL 后写回 item.ItemGraphic，会让之后每个实例都生在 DDOL 场景，于是 **全都看不见**
// （实测现象：整件背包连它原来的外观一起消失）
// ② 关掉它下面**所有**旧外观（不限渲染器类型 **精灵也要关** 以前只关网格）
// ③ 挂上我们的 mesh（没有网格挂点就挂根）+ 清实体缓存，于是 游戏下次重建就读到改过的 prefab
//
// 物品的"外观"和"图标"是两回事：这里只换**世界里的模型** （背包卡片上的图标不变）
// 图标要另做（见 replace-item-icon 那份 SKILL）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelKit;
using UnityEngine;

namespace ItemModelSwap
{
    /// <summary>一条规则。**类型定义在 `libs/mod-kit/ModConfig.cs`**（两个模板共用一份）。
    /// 字段是超集：换外观那套与新增那套都在里面。默认 action = replace_item。</summary>
    using Entry = ModelKit.ModConfig.Entry;

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {

        string _configPath;
        readonly List<Entry> _entries = new List<Entry>();

        /// <summary> 我们目标物品/图形的名字 （`Set` 里是 `名字 + "(Clone)"`）
        /// 用途：装备显示那条路**不给** `ItemRefrence` （实测），于是 名字兜底 </summary>
        readonly HashSet<string> _ourCloneNames = new HashSet<string>();

        float _nextCfgCheck;                       // 限频：每 0.25 秒才查一次 config
        DateTime _cfgStamp = DateTime.MinValue;
        float _nextHeldSweep;                      // 手持/装备那条要**反复复查**（物品是后来才被拿起来的）
        float _nextApply;                                           // 扫描节流：`AllItems()` 是**整场景对象扫描** 不得每帧做

        /// <summary>模型缓存：路径，于是 （指纹, Mesh, 贴图）。指纹 = 路径 + 朝向 + 文件 mtime+size </summary>
        readonly Dictionary<string, CachedModel> _modelCache = new Dictionary<string, CachedModel>();

        /// <summary>action = "new_item" 造出来的东西：typeID → 结果。
        /// 记住它的用处：热重载时不要重复造同一个号（`IsTypeIDTaken` 会命中我们自己的注册）。</summary>
        readonly Dictionary<int, ItemFactory.Result> _created = new Dictionary<int, ItemFactory.Result>();
        /// <summary>给新物品挂上的模型结果 —— 热重载一起还原（有借有还）。</summary>
        readonly List<ItemModel.Result> _newAttached = new List<ItemModel.Result>();

        /// <summary>已经处理过的物品（实例 ID，于是 我们的结果） 用来避免每帧重复挂 + 还原 </summary>
        readonly Dictionary<int, ItemModel.Result> _applied = new Dictionary<int, ItemModel.Result>();
        /// <summary> 场上克隆的补挂结果 （热重载一起恢复 有借有还）</summary>
        readonly Dictionary<int, ItemModel.Result> _appliedClone = new Dictionary<int, ItemModel.Result>();
        float _nextCloneSweep;

        void Start()
        {
            _configPath = Path.Combine(ModLoaderDir(), "config.json");
            ReadConfig();
            ItemModel.DebugOn = ReadDebugFlag();     // config 顶层 "debug": true （只读诊断 默认关）
            _cfgStamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
            Debug.Log($"[ItemModel] rules ({_entries.Count}): " + string.Join(" / ",
                _entries.Select(e => $"targets=[{string.Join(",", e.Targets)}] typeIDs=[{string.Join(",", e.TypeIds)}]"
                    + $" world={(e.World ? "on" : "off")} handheld={(e.Handheld ? "on" : "off")} model='{e.ModelFile}'")));

            // 新增物品那条（必须在进关卡之前，见方法注释）
            ApplyNewEntries();

            // 事件驱动。两个挂钩点的反编译依据见 libs/mod-kit/ItemHooks.cs。
            // 过滤一下：只有命中规则的物品才触发扫描，无关的图形不动。
            try
            {
                ItemHooks.OnGraphicReady = (item, _graphic) => { if (MatchesAny(item)) { _nextApply = 0f; _nextCloneSweep = 0f; } };
                ItemHooks.OnAgentReady = (item, _agent) => { if (MatchesAny(item)) { _nextApply = 0f; _nextCloneSweep = 0f; } };
                var ok = ItemHooks.Install();
                Debug.Log($"[ItemModel] hooks: {(ok ? "on (event-driven; periodic sweep 5s)" : "off (periodic sweep only)")} | {ItemHooks.Report}");
            }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] hooks: install threw: " + ex.Message); }
        }

        /// <summary>这件物品命中任何一条规则吗（钩子用它过滤）</summary>
        bool MatchesAny(ItemStatsSystem.Item item)
        {
            if (item == null || item.name == null || item.name.StartsWith("ModelKit_item_")) return false;
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Matches(item)) return true;
            return false;
        }

        /// <summary>本 mod 的目录（DLL 所在处）</summary>
        static string ModLoaderDir() => ModelCache.ModDir();   // 搬到 kit（ModelCache.ModDir）

        void Update()
        {
            ReloadIfChanged();
            ApplyAll();
            SweepGraphicClones();        // 场上的图形克隆也补一遍 （掉落/展示那条）
            ItemModel.DumpNewObjects();              // 只读诊断 （DebugOn 为 false 时直接返回）
        }

        /// <summary>config 顶层的 `debug`（只读诊断开关 默认关）</summary>
        static bool ReadDebugFlag()
        {
            try
            {
                var p = Path.Combine(ModLoaderDir(), "config.json");
                if (!File.Exists(p)) return false;
                var j = Json.Parse(File.ReadAllText(p));
                return j != null && j.GetBool("debug", false);
            }
            catch { return false; }
        }

        /// <summary>config.json 存盘即生效（热重载 不用重启）</summary>
        // ───────────────── 新增物品（action = "new_item"）─────────────────

        /// <summary>`action = "new_item"` 的规则：造一件游戏里本来没有的物品。
        ///
        /// **数据层不分武器与物品**（`ItemFactory` 的文件头就这么写）。所以顺序与武器模板的
        /// `ApplyNewEntries` 一样，只是最后多了物品专有的三样：Tag、物品数字、图标。
        ///
        /// 顺序：分配 typeID → 克隆 → 挂模型 → 数值 → 品质 → **Tag** → **堆叠/价值/重量** →
        /// **图标** → 上架 → 配方。
        ///
        /// **时机在 `Start()`**，也就是进关卡之前。两个原因（都是反编译实证）：
        ///   · 商人的 `Awake` → `InitializeEntries()` 会把商品条目抄进商店自己的列表；要抢在它前面。
        ///   · 掉落池在箱子 `Setup()`（进关卡之后）才现算候选，插槽在插入时才判 `requireTags`
        ///     —— 两处都晚于这里，所以在这里写下的 tag 会生效。
        ///
        /// 已造过的 typeID 不重复造（热重载会再跑一遍这个方法）：只把上架与配方重提一次。
        /// 上架与配方自己也是幂等的（同一条目/同一个 id 不重复加）。</summary>
        void ApplyNewEntries()
        {
            foreach (var e in _entries)
            {
                if (!e.IsNew) continue;
                if (e.CloneFromItemId <= 0)
                { Debug.LogError($"[NewItem] '{e.DisplayName}' has no cloneFromItemId; skipped"); continue; }

                int typeId = e.NewTypeId;
                if (typeId > 0)
                {
                    // 显式写的号：撞号就报错停下，不静默换号（玩家写的就是他要的）
                    if (ItemFactory.IsTypeIDTaken(typeId) && !_created.ContainsKey(typeId))
                    { Debug.LogError($"[NewItem] typeID={typeId} is already taken by the game or another mod; nothing was created"); continue; }
                }
                else
                {
                    // 不写号：自己分一个。
                    // **不得直接用 `NextTypeID`** —— 它在一个会话里会一直返回同一个号
                    // （反编译实证：它是 `entries.Max(...)` 起的缓存值，而 `AddDynamicEntry`
                    // 只写 `dynamicDic`，不碰 `entries`）。同一个 config 里两条不写号的规则
                    // 就会拿到同一个号，第二条被当成已创建而静默跳过。
                    int free = ItemFactory.AllocateTypeID();
                    if (free <= 0) { Debug.LogError("[NewItem] cannot get a free typeID; skipped"); continue; }
                    if (free != ItemFactory.NextFreeTypeID())
                        Debug.LogWarning($"[NewItem] the game's NextTypeID ({ItemFactory.NextFreeTypeID()}) was already used by us or by the game, so this entry took {free}");
                    typeId = free;
                    e.NewTypeId = typeId;      // 记回条目，于是 上架与配方的 id 跟着一致
                }

                if (!_created.ContainsKey(typeId))
                {
                    var r = ItemFactory.CloneAsNewItem(e.CloneFromItemId, typeId, e.DisplayName, e.Description);
                    if (!r.Created) { Debug.LogError("[NewItem] " + r.Report); continue; }
                    _created[typeId] = r;

                    // 模型（不写 model 就沿用原型外形）
                    var cm = GetModel(e);
                    if (cm != null && cm.Mesh != null)
                    {
                        if (!e.World)
                            Debug.LogWarning("[NewItem] 'world': false has no effect on a new item"
                                           + " (the model defines its look); attaching it anyway");
                        // handheld 按 config（默认关）；本来没有图形的物品才需要它去预造手持实体
                        var ra = ItemFactory.AttachModel(r.Item, cm.Mesh, cm.Texture, e.Size,
                                                         e.Handheld, sizeTypeID: e.CloneFromItemId);
                        if (ra != null) { _newAttached.Add(ra); Debug.Log("[NewItem] " + ra.Report); }
                    }

                    // 原型没有 3D 图形，而 config 也没给 model ⇒ 这件物品只有游戏画的 2D 图片兜底。
                    // 后果要说清（实测：1581 件物品里 1173 件没有图形，摆上展示台什么都看不见）。
                    if (r.Item.ItemGraphic == null)
                    {
                        bool wantsShowCase = false;
                        for (int t = 0; t < e.Tags.Count; t++)
                            if (string.Equals(e.Tags[t], "ShowCase", StringComparison.OrdinalIgnoreCase)) wantsShowCase = true;
                        Debug.LogWarning($"[NewItem] typeID={typeId}: the prototype {e.CloneFromItemId} has no 3D graphic,"
                            + " and no model was given, so this item has a 2D sprite only."
                            + (wantsShowCase
                                ? " It carries ShowCase, but a display stand will show NOTHING: the stand builds a"
                                  + " graphic from the item, and there is none. Give it a model, or pick a prototype"
                                  + " that has a 3D graphic (for example 1259)."
                                : " It still shows in the world as the 2D sprite."));
                    }

                    // 数值、品质、Tag、物品数字（只改 config 里写了的）
                    var missing = new List<string>();
                    int nStats = ItemFactory.ApplyStats(r.Item, e.Stats, missing);
                    ItemFactory.ApplyQuality(r.Item, e.Quality, e.DisplayQuality);
                    // Tag 是**追加**：品类从原型继承，这里加的是额外标记（ShowCase 之类）
                    int nTags = ItemFactory.ApplyTags(r.Item, e.Tags, null, missing);
                    int nNums = ItemFactory.ApplyItemNumbers(r.Item, e.MaxStackCount, e.Value, e.Weight);
                    if (missing.Count > 0)
                        Debug.LogError("[NewItem] these names do not exist in the game, so they were NOT applied: "
                                       + string.Join(", ", missing) + " (copy the names from inspect_game_data)");

                    Debug.Log($"[NewItem] created typeID={typeId} (name key '{r.NameKey}') from prototype {e.CloneFromItemId}"
                              + $" | name='{e.DisplayName}'"
                              + $" | model={(cm != null && cm.Mesh != null ? "ours" : "none, keeps the prototype's look")}"
                              + $" | stats={nStats} tags={nTags} numbers={nNums}"
                              + $" | quality={(e.Quality >= 0 || !string.IsNullOrEmpty(e.DisplayQuality) ? "overridden" : "inherited")}");

                    // 图标（可选；与 useSpriteForPickup 是两件事：那是世界里的，这是背包卡片上的）
                    if (e.IconPath != null) Debug.Log("[NewItem] " + ItemFactory.AttachIcon(r.Item, e.IconPath));
                }

                // 上架到商人（幂等：同一个 typeID 不加第二次）
                if (e.OnSale)
                {
                    // requireExplicitMerchant = true：**物品这条路不得默认商店**。
                    // 游戏里有 12 个商店档位（售货机 / 老吴 / 橘子 / 小明 / 泥巴 / 佛哥 / 神秘商人 ×3 / 岛 ×2）
                    // 加一套独立黑市，默认上到某一个店是猜的。缺 merchantID 时这里会报错停下。
                    var rs = ItemFactory.PutOnSale(e.MerchantID, typeId, e.MaxStock, e.PriceFactor,
                                                   requireExplicitMerchant: true);
                    Debug.Log((rs.Ok ? "[NewItem] " : "[NewItem] FAILED ") + rs.Report);
                }

                // 制作台配方（幂等：同一个 id 不加第二次）
                if (e.CraftRecipe)
                {
                    var rr = ItemFactory.AddRecipe(e.RecipeId(), typeId, 1, e.CraftMoney, e.CraftMaterials, e.CraftTags.ToArray());
                    Debug.Log((rr.Ok ? "[NewItem] " : "[NewItem] FAILED ") + rr.Report);
                }
            }
        }

        void ReloadIfChanged()
        {
            if (Time.unscaledTime < _nextCfgCheck) return;
            _nextCfgCheck = Time.unscaledTime + 0.25f;
            try
            {
                if (!File.Exists(_configPath)) return;
                var st = File.GetLastWriteTimeUtc(_configPath);
                if (st == _cfgStamp) return;
                _cfgStamp = st;

                ReadConfig();

                // 先**还原**旧结果（旧几何重新显示 我们的实例销毁），于是 再重挂
                // 少了这一步：重挂失败时旧零件还关着，于是 物品会整个看不见 （武器那边实测踩过）
                foreach (var kv in _applied)
                    if (kv.Value != null) kv.Value.Restore();
                _applied.Clear();
                foreach (var kv in _appliedClone) if (kv.Value != null) kv.Value.Restore();
                _appliedClone.Clear();
                // 标记不在这里"全局摘" —— 由 `Result.Restore()` **精确摘掉自己那一层** （上面那个循环里）
                // 全局摘会把**游戏对象上的标记**也摘掉，于是 下一轮 Apply 会去重处理原物品模板，于是 走成另一条分支
                // **热重载后立刻重挂**：把三个节流计时器清零
                // 否则要等下一个 0.5 秒才挂回来，于是 视觉上会"先退回原样、等一会儿才变" （实测报过）
                // 清零后同一帧就 `ApplyAll` + `SweepGraphicClones`，于是 空档从 ≤0.5 秒降到 ~1 帧
                _nextApply = 0f; _nextCloneSweep = 0f; _nextHeldSweep = 0f;

                // 新增物品：挂上的模型要还（有借有还），商品条目/配方/动态条目要撤
                // （不撤的话每改一次配置，货架上就多一条、配方也多一条）
                foreach (var r in _newAttached) if (r != null) r.Restore();
                _newAttached.Clear();
                ItemFactory.RemoveAdded();
                _created.Clear();
                ApplyNewEntries();     // 新配置里的新增物品重新造一遍（已撤掉了旧的）

                Debug.Log("[ItemModel] config reloaded; re-applying " + _entries.Count + " rules");
            }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] config hot reload failed: " + ex.Message); }
        }

        /// <summary> 给"**场上的图形克隆**"补挂 （掉落/展示 / 换场景遗留）

        /// <para>原理：`ItemGraphicInfo.CreateAGraphic` = `Instantiate(item.ItemGraphic)`

        /// —— 已经在场上的副本**不会**跟着 prefab 变，于是 逐个补。</para>

        /// <para> 只动名字带 `(Clone)` 的 **绝不碰共享 prefab**

        /// （碰了就会像上次那样：手里那把枪的配件被一起关掉）</para></summary>

                /// <summary> 给"场上**已经存在**的图形实例"补挂 （掉落 手里 穿戴 背包 一个扫描全覆盖）
        /// <para>判据 = 游戏自带的**反向索引**：`ItemGraphicInfo.ItemRefrence`（`Setup(item)` 里写的）
        /// —— 源码实锤：`CreateAGraphic` 里会调 `itemGraphicInfo.Setup(item)`
        /// ⇒ **每个实例都知道自己属于哪件物品**，于是 不再需要猜名字 不再需要判祖先 </para>
        /// <para> 共享 prefab 自己**没有** owner （没被 Setup 过），于是 自动跳过 （它由 `Apply` 就地改）</para></summary>
        void SweepGraphicClones()
        {
            if (_entries.Count == 0) return;
            if (Time.unscaledTime < _nextCloneSweep) return;      // 钩子来时会在下一帧扫一次（回调把 _next* 归零）；没钩子时每 5 秒兜底一次
            _nextCloneSweep = Time.unscaledTime + 5f;

            List<ItemGraphicInfo> all;
            try { all = UnityEngine.Resources.FindObjectsOfTypeAll<ItemGraphicInfo>().ToList(); }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] instance scan failed: " + ex.Message); return; }

            foreach (var gi in all)
            {
                if (gi == null || gi.gameObject == null) continue;
                var owner = gi.ItemRefrence;                        // 反向索引
                // owner 为空时用**名字兜底**：装备显示那条路不给 `ItemRefrence` （实测）
                // （判据仍是"是我们目标的图形副本 "；登记表会挡住重复挂）
                bool byName = _ourCloneNames.Contains(gi.name);
                if (owner == null && !byName) continue;          // prefab 本身名字不带 (Clone)，于是 仍然跳过
                Entry hit = null;
                if (owner != null)
                    for (int i = 0; i < _entries.Count; i++) if (_entries[i].Matches(owner)) { hit = _entries[i]; break; }
                if (hit == null && byName)
                    foreach (var e2 in _entries)
                    {
                        var m2 = GetModel(e2);
                        if (m2 != null && (_ourCloneNames.Contains(gi.name))) { hit = e2; break; }
                    }
                if (hit == null) continue;
                if (PatchRegistry.IsPatched(gi.gameObject)) continue;
                if (PatchRegistry.HasOurMeshUnder(gi.transform)) continue;   // 已经带我们 mesh 的（我们自己造的图形），于是 不得再补 // 唯一判据（登记表）
                var cm = GetModel(hit);
                if (cm == null || cm.Mesh == null) continue;
                ItemModel.CurrentNormalMap = cm.NormalTexture;        // 只给物品设 （武器不设）
                try
                {
                    string who = owner != null ? owner.name : gi.name;
                    int tid = owner != null ? owner.TypeID : (hit.TypeIds.Count > 0 ? hit.TypeIds.First() : 0);
                    var rc = ItemModel.ApplyToGraphicClone(gi, who, tid, cm.Mesh, cm.Texture,
                                                           ModelSize.FactorFor(tid, hit.Size, cm.Mesh));
                    if (rc.Applied)
                    {
                        _appliedClone[gi.gameObject.GetInstanceID()] = rc;
                        Debug.Log($"[ItemModel] swapped (live instance): {gi.name} (owner '{who}') | {rc.Report}");
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[ItemModel] live-instance attach threw: {ex.Message}"); }
            }
        }


        /// <summary>对"所有已加载的物品（含模板 + 场上实例）"逐条匹配并挂上模型 </summary>
        void ApplyAll()
        {
            if (_entries.Count == 0) return;
            // 钩子来时下一帧就扫（回调把 _nextApply 归零）；没钩子时每 5 秒兜底一次。
            // 5 秒这条必要：`useSpriteForPickup` 为真的路径、以及已经造好的图形，都不会触发钩子。
            // （旧写法：固定 0.5 秒。`AllItems()` 内部是整场景对象扫描，于是 3 个这类 mod 就是每秒 6 次。）
            if (Time.unscaledTime < _nextApply) return;
            _nextApply = Time.unscaledTime + 5f;

            List<ItemStatsSystem.Item> all;
            try { all = GameApi.AllItems().ToList(); }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] item enumeration failed: " + ex.Message); return; }

            foreach (var item in all)
            {
                // **跳过我们自己造的克隆物品** （否则会"克隆，于是 又被扫到，于是 再克隆"自增 实测卡死）
                if (item.name != null && item.name.StartsWith("ModelKit_item_")) continue;

                int id;
                try { id = item.GetInstanceID(); } catch { continue; }
                Entry hit = null;
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Matches(item)) { hit = _entries[i]; break; }   // 先命中的生效
                if (hit == null) continue;
                try
                {
                    if (item.name != null) _ourCloneNames.Add(item.name + "(Clone)");
                    if (item.ItemGraphic != null) _ourCloneNames.Add(item.ItemGraphic.name + "(Clone)");
                }
                catch { }

                var cm = GetModel(hit);
                if (cm == null || cm.Mesh == null)
                {
                    _applied[id] = null;                     // 记一下，避免每帧重复报错
                    Debug.LogWarning($"[ItemModel] rule '{string.Join(",", hit.Targets)}': the model is not usable ({hit.ModelFile})");
                    continue;
                }

                // 已经处理过的：**只补做"手持/装备"那条**
                // 物品是后来才被拿起来的 那时代替它的 `ActiveAgent` 才出现 —— 图形那条早就改好了
                // （拿起/放下才需要重做 1 秒一次足够 幂等 已换过的会直接返回）
                if (_applied.ContainsKey(id))
                {
                    // 游戏会**重建物品预制体**，于是 我们写的 ItemGraphic 会被冲掉
                    // （实测：糖果预制体的 ItemGraphic 变回 null，于是 掉地上那件就从预制体实例化出 2D 图）
                    //，于是 每秒复查：**一旦发现被重置，就把它写回去** （复用同一个图形对象 不漏）
                    if (hit.World && item.ItemGraphic == null)
                    {
                        try
                        {
                            var rw = ItemModel.Apply(item, cm.Mesh, cm.Texture, hit.Handheld, hit.Size);
                            if (rw.Applied)
                                Debug.Log($"[ItemModel] swapped (re-written after a reset): '{item.name}' (typeID={item.TypeID}) | {rw.Report}");
                        }
                        catch { /* 补写失败不影响别的 */ }
                    }
                    if (hit.Handheld && Time.unscaledTime >= _nextHeldSweep)
                    {
                        _nextHeldSweep = Time.unscaledTime + 1f;
                        try
                        {
                            var r = ItemModel.ApplyHandheld(item, cm.Mesh, cm.Texture,
                                                ModelSize.FactorFor(item.TypeID, hit.Size, cm.Mesh));
                            if (r.Applied)
                                Debug.Log($"[ItemModel] swapped: '{item.name}' (typeID={item.TypeID}) from {Path.GetFileName(hit.ModelFile)} | {r.Report}");
                        }
                        catch (Exception ex)
                        { Debug.LogWarning($"[ItemModel] handheld threw: {ex.Message}"); }
                    }
                    continue;
                }

                try
                {
                    // 按开关做（三层里的前两层；第三层 = 游戏自带的 2D 图片 我们永远不动）
                    bool didWorld = false;
                    if (hit.World)
                    {
                        var res = ItemModel.Apply(item, cm.Mesh, cm.Texture, hit.Handheld, hit.Size);
                        _applied[id] = res;
                        didWorld = res.Applied;
                        if (res.Applied)
                            Debug.Log($"[ItemModel] swapped: '{item.name}' (typeID={item.TypeID}) from {Path.GetFileName(hit.ModelFile)} | {res.Report}");
                        else if (!hit.Handheld && !res.NoOp)
                            Debug.Log($"[ItemModel] skipped: '{item.name}' {res.Report}");
                    }
                    else _applied[id] = null;                        // 世界那条不得 （只记一下）

                    if (hit.Handheld)                                // 手持那条（会改行为 默认关）
                    {
                        var rh = ItemModel.ApplyHandheld(item, cm.Mesh, cm.Texture,
                                                  ModelSize.FactorFor(item.TypeID, hit.Size, cm.Mesh));
                        // **入册** —— 世界那条有自己的 `Result`，于是手持这条塞进它的 `Nested`；
                        // 世界那条没做（`_applied[id]` 是 null）时，就直接存手持这条。
                        // 不入册的后果（实测）：`Restore()` 不管它，热重载后旧的 mesh 还在。
                        // 只入册**真换过**的：物品没有 `ItemGraphic` 时，`Apply` 内部已经调过一次，
                        // 这里再调得到的是 NoOp 结果，入册只会让 `Restore()` 多跑一次空操作。
                        var world = _applied.TryGetValue(id, out var wr) ? wr : null;
                        if (rh.Applied)
                        {
                            if (world != null) world.Nested.Add(rh);
                            else _applied[id] = rh;
                        }
                        if (rh.Applied)
                            Debug.Log($"[ItemModel] swapped: '{item.name}' (typeID={item.TypeID}) from {Path.GetFileName(hit.ModelFile)} | {rh.Report}");
                        else if (!rh.NoOp)
                            Debug.Log($"[ItemModel] skipped: '{item.name}' {rh.Report}");
                    }
                    if (!didWorld && !hit.Handheld && hit.World) { /* 世界那条做不了 保持 2D 图片兜底 上面已打过日志 */ }
                }
                catch (Exception ex)
                {
                    _applied[id] = null;
                    Debug.LogWarning($"[ItemModel] '{item.name}': swap threw: {ex.Message}");
                }
            }
        }

        /// <summary>取（并缓存）某个规则要用的 Mesh+贴图 （同一个文件只解析一次）</summary>
        CachedModel GetModel(Entry e)
        {
            if (string.IsNullOrEmpty(e.ResolvedPath) || !File.Exists(e.ResolvedPath)) return null;
            var fi = new FileInfo(e.ResolvedPath);
            // 指纹**不含 Size** （size 只影响挂载时的 scale 不改 mesh，于是 改 size 热重载不必重读模型）
            string sig = e.ResolvedPath + "|" + e.Front + "|" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;

            CachedModel c;
            if (_modelCache.TryGetValue(e.ResolvedPath, out c) && c != null && c.Sig == sig) return c;

            // **不做"握把归零"** （`zeroGrip: false`）—— 那是**枪**的猜测规则（`GuessGrip`：“枪口 +Z ⇒
            // 枪托在 −Z”），物品/装备/近战都不适用。而且这条路上 `Attach()` 本来就按**包围盒中心**
            // 对齐（`ItemModel.cs`）“先减原点”会被它抵消 ⇒ 做了≈没做。摆在哪儿由对齐与 `pivot` 决定。
            var loaded = GltfLoader.LoadFile(e.ResolvedPath, e.Front, zeroGrip: false);
            c = new CachedModel { Sig = sig, Mesh = loaded.Mesh, Texture = loaded.MainTexture,
                                 NormalTexture = loaded.NormalTexture };
            _modelCache[e.ResolvedPath] = c;
            // 尺寸：**只算系数** （不改 mesh 顶点）；由挂载时的 `localScale` 承担
            // 只有武器会 ≠1 （按 `GunType_*` tag），于是 装备/物品 = 1；config `size` 可覆盖
            float scaleFactor = ModelSize.FactorFor(e.TypeIds.Count > 0 ? e.TypeIds.First() : 0, e.Size, loaded.Mesh);
            Debug.Log($"[ItemModel] model resolved: {Path.GetFileName(e.ResolvedPath)} | {loaded.Report}"
                      + $" | size: {(scaleFactor != 1f ? $"scale={scaleFactor:0.###}" : "scale=1 (no scaling)")}");
            return c;
        }

        // ───────────────────────── config ─────────────────────────

        /// <summary>读 config.json。**读取逻辑在 `libs/mod-kit/ModConfig.cs`**（两个模板共用一份）。
        /// 物品模板的选项：没有目标的 **replace** 条目丢掉；认顶层 `size` 当默认；写法不对时静默跳过。
        /// （`new_item` 条目本来就没有目标，不会被丢掉 —— 见第 5 步。）</summary>
        void ReadConfig()
        {
            _entries.Clear();
            var entries = ModConfig.Read(_configPath, new ModConfig.Options
            {
                LogTag = "[ItemModel]",
                DefaultAction = ModConfig.ReplaceItem,
                SkipReplaceEntriesWithoutTargets = true,    // 老行为：没目标的替换条目直接丢
                ApplyTopLevelSize = true,                   // 老行为：顶层 size 当默认值
                WarnOnNonObjectEntry = false,
                WarnWhenNothing = false,
            });
            _entries.AddRange(entries);
        }
    }
}
