// WeaponModel（示例 mod）—— 把**一把或多把**武器的模型换成你给的 GLB。
//
// 这就是"替换枪械模型"的**完整正确做法**：整个 mod 只有这一份配置 + 一句调用。
//
// config.json 支持两种写法（都行）：
//
// ① 一套素材换一批武器（旧写法 仍然有效）
// { "typeIDs": [655], "model": "mp5.glb" } // 模型名**按武器起** （别固定叫 gun.glb）
//
// ② 每把武器各换各的（一个 mod 多条规则 按数组顺序匹配，**先命中的生效**）
// { "entries": [
// { "targets": ["Item_SMG_MP5_Normal"], "model": "smg.glb", "icon": "smg_icon.png" },
// { "typeIDs": [655], "model": "mp5.glb", "front": "-x" } ] }
//
// 字段（每条都能用）：
// action = replace_weapon（缺省，换已有枪的外观）| new_weapon（**新增一把枪**）
// targets / typeIDs / model / icon / front / size / slots = 见下方各条
//
// **新增武器**（action = "new_weapon"）的字段：
// newTypeId = 新 typeID（不写就取游戏自己的下一个空闲号）
// cloneFromItemId = 拿哪把现有枪当原型（数值/插槽全继承它）
// displayName / description = 界面上的名字与简介
// merchantID = 上架到哪个商人（缺省 Merchant_Weapon）；maxStock / priceFactor = 备货与价格倍数
// stats = 覆盖数值（绝对值，键用游戏自己的名字）；quality / displayQuality = 覆盖品质
// crafting = { "money": 0, "materials": { "662": 5 }, "tags": ["..."] } 制作台配方
//
// 运行时会做（都在 libs/mod-kit 里，见 ItemFactory.cs / WeaponModel.cs / GltfLoader.cs / GameApi.cs）：
// 换外观：读 GLB 建 Mesh+贴图（坐标系/绕序/UV 转换 + 握把归零），于是 找到你手里那把枪，
//        于是 关掉旧模型零件，于是 挂上我们的模型，于是 对齐，于是 克隆游戏材质 + 换贴图
// 新增武器：克隆原型 → 换图形 → 覆盖数值与品质 → 上架到商人 → 加一条制作台配方

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelKit;
using UnityEngine;

namespace WeaponModelSwap
{
    /// <summary>一条规则。**类型定义在 `libs/mod-kit/ModConfig.cs`**（两个模板共用一份）
    /// —— 以前两个模板各有一份几乎相同的读取代码，物品模板就因为少了一份而落队
    /// （它读不了 `entries`，写 `new_item` 会静默无效）。字段没变，行为没变。
    /// 默认 action 由 `ModConfig.Options.DefaultAction` 给（武器模板 = replace_weapon）。</summary>
    using Entry = ModelKit.ModConfig.Entry;

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {

        string _configPath;
        readonly List<Entry> _entries = new List<Entry>();

        CharacterMainControl _player;
        float _nextFindPlayer;
        float _nextCfgCheck;              // a：限频 —— 每 0.25 秒才查一次 config 的 mtime
        DateTime _cfgStamp = DateTime.MinValue;
        float _nextIconSweep;

        /// <summary>模型缓存（每个路径只留**最新一份**）：路径，于是 （指纹 , Mesh , 贴图）
        /// b：指纹 = 路径 + 朝向 + **文件 mtime+size**，于是 文件真变了才重解析 </summary>
        readonly Dictionary<string, CachedModel> _modelCache = new Dictionary<string, CachedModel>();
        readonly Dictionary<int, ModelKit.WeaponModel.Result> _applied = new Dictionary<int, ModelKit.WeaponModel.Result>();   // transformId，于是 换上的结果（含被禁用的旧零件 用于恢复）
        /// <summary> 枪图形 prefab 名，于是 条目 （给"场上克隆"补挂用）</summary>
        /// <summary> 场上克隆（掉落/展示）的补挂结果 —— 热重载一起恢复 （有借有还）</summary>
        readonly Dictionary<int, ModelKit.ItemModel.Result> _appliedClone = new Dictionary<int, ModelKit.ItemModel.Result>();
        float _nextCloneSweep;
        string _lastHeld;

        // 新增武器（action = "new_weapon"）造出来的东西：typeID → 结果
        readonly Dictionary<int, ItemFactory.Result> _created = new Dictionary<int, ItemFactory.Result>();
        readonly List<ModelKit.ItemModel.Result> _newAttached = new List<ModelKit.ItemModel.Result>();

        void Start()
        {
            _configPath = Path.Combine(ModelLoaderDir(), "config.json");
            ReadConfig();
            _cfgStamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
            LoadModels();
            ApplyNewEntries();      // 新增武器那条（必须在进关卡之前，见方法注释）
            // 启动就读好模型，于是 玩家掏出枪时能**立刻**换（不然会先看到原模型）
            Debug.Log($"[WeaponModel] rules ({_entries.Count}): " + string.Join(" / ",
                _entries.Select((e, i) => $"#{i + 1} targets=[{string.Join(",", e.Targets)}] typeIDs=[{string.Join(",", e.TypeIds)}] model='{e.ModelFile}'")));

            // 事件驱动：枪的图形一造出来（掉落/展示）就标记一下，下一次扫描用短间隔。
            // 挂钩点的反编译依据见 libs/mod-kit/ItemHooks.cs。
            try
            {
                ItemHooks.OnGraphicReady = (item, _graphic) => { if (FirstMatch(item) >= 0) { _nextCloneSweep = 0f; } };
                var ok = ItemHooks.Install();
                Debug.Log($"[WeaponModel] hooks: {(ok ? "on (event-driven; periodic sweep 5s)" : "off (periodic sweep only)")} | {ItemHooks.Report}");
            }
            catch (Exception ex) { Debug.LogWarning("[WeaponModel] hooks: install threw: " + ex.Message); }
        }

        void Update()
        {
            ReloadIfChanged();            SweepGraphicClones();          // 场上的枪图形克隆也补一遍 （掉落/展示）

            // 图标要**尽早**盖上：背包卡片可能在"掏出武器"之前就建好了
            //，于是 启动后几秒内反复按名字匹配所有（模板 + 实例）并设图标 （每条规则各自扫一遍）
            if (Time.unscaledTime < 20f && Time.unscaledTime >= _nextIconSweep)
            {
                _nextIconSweep = Time.unscaledTime + 2f;
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    if (e.IconPath == null) continue;
                    var n = ModelKit.WeaponIcon.ApplyToAllMatching(e.Targets, e.TypeIds, e.IconPath);
                    if (n > 0) Debug.Log($"[WeaponModel] icons preset: rule #{i + 1} matched {n} items (templates included)");
                }
            }
            if (_entries.Count == 0) return;

            // 找玩家较重（FindObjectsOfType），于是 只在没有/每 2 秒找一次；**每帧**只看"手里拿的是什么"（廉价）
            if (_player == null && Time.unscaledTime >= _nextFindPlayer)
            {
                _nextFindPlayer = Time.unscaledTime + 2f;
                _player = GameApi.FindMainCharacter();
            }
            if (_player == null) return;
            var agent = _player.CurrentHoldItemAgent;
            var item = agent != null ? agent.Item : null;
            if (item == null || agent == null || agent.gameObject == null) return;

            int hit = FirstMatch(item);   // 按数组顺序，先命中的生效
            if (item.name != _lastHeld)
            {
                _lastHeld = item.name;
                Debug.Log($"[WeaponModel] held item: '{item.name}' (typeID={item.TypeID}); " +
                          (hit >= 0 ? $"rule #{hit + 1} matched; swapping" : "no rule matched; not swapped"));
            }
            if (hit < 0) return;

            var entry = _entries[hit];
            if (entry.Mesh == null) return;

            var root = agent.gameObject.transform;
            int id = root.GetInstanceID();
            if (_applied.TryGetValue(id, out var prev) && prev != null && prev.Instance != null) return;      // 换过、还在

            var r = ModelKit.WeaponModel.Apply(root, entry.Mesh, entry.Texture, default, entry.Slots, entry.Scale);
            if (r.Applied)
            {
                _applied[id] = r;
                Debug.Log("[WeaponModel] " + r.Report);
                // 掉落/展示图形由 `SweepGraphicClones()` 补挂（那里走 `ItemModel.ApplyToGraphicClone`）。
                // 2026-10-08 那次坏是因为当时的 `ItemModel.HideOld` **全关**，把挂在 `Sockets/<槽位>`
                // 下的配件（`AccessoryBase.Init` 把配件 SetParent 到挂点）也关了。
                // 现在两边共用 `HidePolicy`：保留 我们自己的 / 灯特效 / **槽位子树**，于是配件与枪口火焰都在。
                // 图标：卡片上那个 Sprite 也换成我们的
                if (entry.IconPath != null) Debug.Log("[WeaponModel] " + ModelKit.WeaponIcon.Apply(item, entry.IconPath));
            }
            else Debug.LogWarning("[WeaponModel] swap failed: " + r.Report);
        }

        /// <summary> 给"**场上的图形克隆**"补挂 —— 枪**掉在地上**那条

        /// <para>为什么必须这样：`ItemGraphicInfo.CreateAGraphic` = `Instantiate(item.ItemGraphic)`

        ///，于是 掉落物 = **游戏当场克隆的 prefab 副本**。

        /// 直接改共享 prefab 会把**手里那把**的零件一起关掉 （配件是挂点的子物体 实测两次都坏）。

        /// ⇒ 只动"名字带 `(Clone)`"的那份 —— 每个副本独立 动它**绝不影响**手里那把 </para></summary>

                /// <summary>祖先里有叫 `name` 的吗 （用来区分"掉落/展示"和"拿在手里"）</summary>


                /// <summary> 给"场上**已经存在**的枪图形实例"补挂 （掉落 手里 一个扫描全覆盖）
        /// <para>判据 = 游戏自带的**反向索引**：`ItemGraphicInfo.ItemRefrence`（`Setup(item)` 里写的）
        /// —— 源码实锤：`CreateAGraphic` 会调 `itemGraphicInfo.Setup(item)`
        /// ⇒ **每个实例都知道自己属于哪件物品**，于是 不再猜名字 不再判祖先 </para>
        /// <para> 手里那把的 agent **已由 `WeaponModel.Apply` 挂过并登记**，于是 登记表会挡掉 不会重复挂 </para></summary>
        void SweepGraphicClones()
        {
            if (_entries.Count == 0) return;
            if (Time.unscaledTime < _nextCloneSweep) return;      // 钩子来时会在下一帧扫一次（回调把 _next* 归零）；没钩子时每 5 秒兜底一次
            _nextCloneSweep = Time.unscaledTime + 5f;

            List<ItemGraphicInfo> all;
            try { all = UnityEngine.Resources.FindObjectsOfTypeAll<ItemGraphicInfo>().ToList(); }
            catch { return; }

            foreach (var gi in all)
            {
                if (gi == null || gi.gameObject == null) continue;
                var owner = gi.ItemRefrence;                       // 反向索引
                if (owner == null) continue;                       // 共享 prefab 没 owner，于是 跳过

                // **跳过"手里那把 agent"**：它也是 `ItemGraphicInfo` 实例 也带 `ItemRefrence`
                //，于是 会被这个扫描抢在 `WeaponModel.Apply` **登记之前**补挂 （0.5 秒竞态 实测踩到）
                //，于是 结果：**两个模型** + 又关一批枪身零件（配件没了）
                // 判据：带 `ItemAgent_Gun` 组件的就是"手里那把" （掉落/展示那份没有）
                if (gi.GetComponent("ItemAgent_Gun") != null) continue;
                Entry e = null;
                for (int i = 0; i < _entries.Count; i++) if (_entries[i].Matches(owner)) { e = _entries[i]; break; }
                if (e == null || e.Mesh == null) continue;
                if (ModelKit.PatchRegistry.IsPatched(gi.gameObject)) continue;
                if (ModelKit.PatchRegistry.HasOurMeshUnder(gi.transform)) continue;   // 已经带我们 mesh 的（我们自己造的图形），于是 不得再补 // 唯一判据（登记表）
                try
                {
                    var rc = ModelKit.ItemModel.ApplyToGraphicClone(gi, owner.name, owner.TypeID, e.Mesh, e.Texture, e.Scale);
                    if (rc.Applied)
                    {
                        _appliedClone[gi.gameObject.GetInstanceID()] = rc;
                        Debug.Log($"[WeaponModel] swapped (live instance): {gi.name} (owner '{owner.name}') | {rc.Report}");
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[WeaponModel] live-instance attach threw: {ex.Message}"); }
            }
        }

        /// <summary>`action = "new_weapon"` 的规则：造一件游戏里本来没有的枪。
        ///
        /// **时机在 `Start()`**，也就是进关卡之前。两个原因（都是反编译实证）：
        ///   · 商人的 `Awake` → `InitializeEntries()` 会把商品条目抄进商店自己的列表；我们要抢在它前面。
        ///   · 注册只在进关卡前做；而发物品需要玩家（那条另算）。
        ///
        /// 这个时机靠什么保证（查过的证据）：
        ///   · mod 由 `Duckov.Modding.ModManager.ScanAndActivateMods()` 激活（菜单阶段），
        ///     激活时 `ActivateMod` 把 mod 的 GameObject 挂到 ModManager 底下。
        ///   · 现装的替换 mod 在关卡里能生效，说明这个 GameObject 活过了换场景，
        ///     也就是它的 `Start()` 在关卡加载之前就跑过了。
        ///   · 于是关卡里那些 `StockShop` 的 `Awake` 在我们之后。
        /// **例外**：如果 mod 是在**已经进了关卡**之后才激活（手动激活、装载后重载），
        /// 那个关卡里的商店已经拄过条目了。补救就是重新进一次关卡（SKILL 里写了）。
        ///
        /// 已造过的 typeID 不重复造（热重载会再跑一遍这个方法）：只把上架与配方重提一次。
        /// 上架与配方自己也是幂等的（同一条目/同一个 id 不重复加）。</summary>
        void ApplyNewEntries()
        {
            foreach (var e in _entries)
            {
                if (!e.IsNew) continue;
                if (e.CloneFromItemId <= 0)
                { Debug.LogError($"[NewWeapon] '{e.DisplayName}' has no cloneFromItemId; skipped"); continue; }

                int typeId = e.NewTypeId;
                if (typeId > 0)
                {
                    // 显式写的号：撞号就报错停下，不静默换号（玩家写的就是他要的）
                    if (ItemFactory.IsTypeIDTaken(typeId) && !_created.ContainsKey(typeId))
                    { Debug.LogError($"[NewWeapon] typeID={typeId} is already taken by the game or another mod; nothing was created"); continue; }
                }
                else
                {
                    // 不写号：自己分一个。
                    // **不得直接用 `NextTypeID`** —— 它在一个会话里会一直返回同一个号
                    // （反编译实证：它是 `entries.Max(...)` 起的缓存值，而 `AddDynamicEntry`
                    // 只写 `dynamicDic`，不碰 `entries`）。同一个 config 里两条不写号的规则
                    // 就会拿到同一个号，第二条被当成已创建而静默跳过。
                    int free = ItemFactory.AllocateTypeID();
                    if (free <= 0) { Debug.LogError("[NewWeapon] cannot get a free typeID; skipped"); continue; }
                    if (free != ItemFactory.NextFreeTypeID())
                        Debug.LogWarning($"[NewWeapon] the game's NextTypeID ({ItemFactory.NextFreeTypeID()}) was already used by us or by the game, so this entry took {free}");
                    typeId = free;
                    e.NewTypeId = typeId;      // 记回条目，于是 上架与配方的 id 跟着一致
                }

                if (!_created.ContainsKey(typeId))
                {
                    var r = ItemFactory.CloneAsNewItem(e.CloneFromItemId, typeId, e.DisplayName, e.Description);
                    if (!r.Created) { Debug.LogError("[NewWeapon] " + r.Report); continue; }
                    _created[typeId] = r;

                    // 模型（不写 model 就沿用原型外形）
                    if (e.Mesh != null)
                    {
                        // handheld 不传：枪的手持实体交给游戏建（它从我们的图形造，
                        // 于是带着图形里的 Sockets；参见 ItemFactory.AttachModel 的注释）
                        var ra = ItemFactory.AttachModel(r.Item, e.Mesh, e.Texture, e.Size,
                                                         sizeTypeID: e.CloneFromItemId);
                        if (ra != null) { _newAttached.Add(ra); Debug.Log("[NewWeapon] " + ra.Report); }
                    }

                    // 数值与品质（只改 config 里写了的）
                    var missing = new List<string>();
                    int changed = ItemFactory.ApplyStats(r.Item, e.Stats, missing);
                    ItemFactory.ApplyQuality(r.Item, e.Quality, e.DisplayQuality);
                    if (missing.Count > 0)
                        Debug.LogError("[NewWeapon] these stats do not exist in the game, so they were NOT applied: "
                                       + string.Join(", ", missing) + " (copy the keys from inspect_game_data)");

                    Debug.Log($"[NewWeapon] created typeID={typeId} (name key '{r.NameKey}') from prototype {e.CloneFromItemId}"
                              + $" | name='{e.DisplayName}' | model={(e.Mesh != null ? "ours" : "none, keeps the prototype's look")}"
                              + $" | stats written={changed} | quality={(e.Quality >= 0 || !string.IsNullOrEmpty(e.DisplayQuality) ? "overridden" : "inherited")}");

                    // 图标（可选）
                    if (e.IconPath != null) Debug.Log("[NewWeapon] " + ModelKit.WeaponIcon.Apply(r.Item, e.IconPath));
                }

                // 上架到商人（幂等：同一个 typeID 不加第二次）
                if (e.OnSale)
                {
                    var rs = ItemFactory.PutOnSale(e.MerchantID, typeId, e.MaxStock, e.PriceFactor);
                    Debug.Log((rs.Ok ? "[NewWeapon] " : "[NewWeapon] FAILED ") + rs.Report);
                }

                // 制作台配方（幂等：同一个 id 不加第二次）
                if (e.CraftRecipe)
                {
                    var rr = ItemFactory.AddRecipe(e.RecipeId(), typeId, 1, e.CraftMoney, e.CraftMaterials, e.CraftTags.ToArray());
                    Debug.Log((rr.Ok ? "[NewWeapon] " : "[NewWeapon] FAILED ") + rr.Report);
                }
            }
        }

        /// <summary>config.json 一改存盘就重新生效（不用重启游戏）：重读配置、丢旧实例、必要时重读模型 </summary>
        void ReloadIfChanged()
        {
            // a：**限频** —— 每 0.25 秒才查一次（不是每帧）：文件 stat 从 ~120 次/秒 降到 4 次/秒
            // 人感觉不出 0.25s （“存盘即生效”的手感不变）
            if (Time.unscaledTime < _nextCfgCheck) return;
            _nextCfgCheck = Time.unscaledTime + 0.25f;
            try
            {
                if (!File.Exists(_configPath)) return;
                var st = File.GetLastWriteTimeUtc(_configPath);
                if (st == _cfgStamp) return;
                _cfgStamp = st;   // 先记时间戳 避免下面抛错时反复重试

                ReadConfig();     // 这一步不得少 （我差点改丢）—— 重读 config，于是 _entries

                // 先销毁我们的实例 **并恢复被禁用的旧零件** （否则一旦重挂失败（如模型文件缺失/解析出错），
                // 旧零件还关着，于是 **枪会整个看不见** —— 2026-10-06 实测踩到过）
                foreach (var kv in _applied)
                {
                    if (kv.Value == null) continue;
                    kv.Value.Restore();   // 旧零件显示 + 搬过的挂点位置 + 销毁我们挂的实例（全在 Restore 里）
                }
                _applied.Clear();
                foreach (var kv in _appliedClone) if (kv.Value != null) kv.Value.Restore();
                _appliedClone.Clear();
                // 新增武器：挂上的模型要还（有借有还），商品条目/配方/动态条目要撤
                // （不撤的话每改一次配置，货架上就多一条、配方也多一条）
                foreach (var r in _newAttached) if (r != null) r.Restore();
                _newAttached.Clear();
                ItemFactory.RemoveAdded();
                _created.Clear();
                _lastHeld = null;
                // **每次都重读模型**：热重载会把 Entry 重建（Mesh 变 null），
                // 而旧代码只在 model/front 指纹变了时才重读，于是 只改 slots/target/icon 时会
                // “永远挂不上” （同样导致枪不见）。重读一个 GLB 只要几十毫秒 换来正确性。
                LoadModels();
                ApplyNewEntries();      // 新配置里的新增武器重新造一遍（已撤掉了旧的）
                Debug.Log($"[WeaponModel] config hot reloaded: {_entries.Count} rules (old instances dropped; the new config re-applies later)");
            }
            catch (Exception e) { Debug.LogWarning($"[WeaponModel] hot reload failed: {e.Message}"); }
        }

        /// <summary>按数组顺序找第一条命中的规则 （找不到返回 -1）</summary>
        int FirstMatch(ItemStatsSystem.Item item)
        {
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Matches(item)) return i;
            return -1;
        }

        void LoadModels()
        {
            var dir = ModelLoaderDir();
            foreach (var e in _entries)
            {
                // 新增武器可以**不带模型**（沿用原型枪的外形）—— 不算错，不必报警
                if (e.IsNew && string.IsNullOrEmpty(e.ModelFile)) continue;
                var path = e.ModelPath(dir);
                if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"[WeaponModel] rule model='{e.ModelFile}' is empty; skipped"); continue; }
                if (!File.Exists(path)) { Debug.LogWarning($"[WeaponModel] no model file: {path}"); continue; }
                // b：指纹没变就直接用上次解析好的 （改 slots/target/icon 时热重载≈瞬发）
                if (_modelCache.TryGetValue(path, out var c) && c != null && c.Sig == e.Signature && c.Mesh != null)
                {
                        e.Mesh = c.Mesh; e.Texture = c.Texture;
                        // 缓存命中时也要**重算尺寸系数** —— 以前这里直接 `continue`，于是 改 `size` 无效
                        // （实测报过：热重载后 `e.Scale` 停在默认 1 而模型根本不重读，于是 看着像 size 坏了）
                        e.SizeTarget = ModelSize.For(e.TypeIds.Count > 0 ? e.TypeIds.First() : 0, e.Size > 0f ? (float?)e.Size : null);
                        e.Scale = ModelSize.Factor(c.Mesh, e.SizeTarget);
                        continue;
                    }
                try
                {
                    var g = GltfLoader.LoadFile(path, e.Front);
                    e.Mesh = g.Mesh; e.Texture = g.MainTexture;
                    // 尺寸：**只算一个系数** （不改 mesh 顶点）；由挂载时的 `localScale` 承担
                    // 优先 config `size`（玩家/agent 覆盖），于是 否则按游戏 `GunType_*` tag，于是 否则 1
                    e.SizeTarget = ModelSize.For(e.TypeIds.Count > 0 ? e.TypeIds.First() : 0, e.Size > 0f ? (float?)e.Size : null);
                    e.Scale = ModelSize.Factor(g.Mesh, e.SizeTarget);
                    float sizeBefore = ModelSize.Longest(g.Mesh);
                    _modelCache[path] = new CachedModel { Sig = e.Signature, Mesh = g.Mesh, Texture = g.MainTexture };
                    Debug.Log($"[WeaponModel] model read {Path.GetFileName(path)}: " + g.Report
                              + $" | size: {(e.SizeTarget.HasValue ? $"longest side {sizeBefore:0.###} -> {e.SizeTarget.Value:0.###} m (scale={e.Scale:0.###})" : "no scaling (scale=1)")}");
                }
                catch (Exception ex) { Debug.LogError($"[WeaponModel] model read failed ({Path.GetFileName(path)}): {ex.Message}"); }
            }
        }

        /// <summary>读 config.json。**读取逻辑在 `libs/mod-kit/ModConfig.cs`**（两个模板共用一份）。
        /// 武器模板的选项：条目没有目标也留着（不丢）；不认顶层 `size`；写法不对时打日志。</summary>
        void ReadConfig()
        {
            _entries.Clear();
            var entries = ModConfig.Read(_configPath, new ModConfig.Options
            {
                LogTag = "[WeaponModel]",
                DefaultAction = ModConfig.ReplaceWeapon,
                SkipReplaceEntriesWithoutTargets = false,   // 老行为：留着，让日志里看得见
                ApplyTopLevelSize = false,                  // 老行为：武器不认顶层 size
                WarnOnNonObjectEntry = true,
                WarnWhenNothing = true,
            });
            _entries.AddRange(entries);
        }

        static string ModelLoaderDir() => ModelCache.ModDir();   // 搬到 kit（ModelCache.ModDir）
    }
}
