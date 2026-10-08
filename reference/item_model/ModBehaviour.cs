// ItemModel（示例 mod）—— 把**一件（或一批）物品**的模型换成你给的 GLB。
//
// 这就是"替换物品模型"的**完整正确做法**：整个 mod 只有这一份配置 + 一句调用 ✓
// （运行时的全部机制在 libs/mod-kit/ItemModel.cs ✓ 那里有实测踩过的坑清单 ✓）
//
// config.json 支持两种写法（都行 ✓）：
//
//   ① 一套素材换一件物品
//      { "typeIDs": [36], "model": "backpack.glb" }
//
//   ② 每件物品各换各的（一个 mod 多条规则 ✓ 按数组顺序匹配，**先命中的生效**）
//      { "entries": [
//          { "typeIDs": [260], "model": "backpack.glb" },
//          { "targets": ["Item_BackpackLV3"], "model": "backpack_lv3.glb" } ] }
//
//   字段（每条都能用 ✓）：
//     targets = 物品**对象名全等**（数组 ✓ 不区分大小写；例 ["Item_BackpackLV3"] ✓）
//     typeIDs = 或精确命中（数组 ✓ 例 [260, 261] —— 更稳 ✓ 名字是英文对象名，[L,H,D] 不受本地化影响 ✓）
//     model   = 放在本 mod 目录里的 GLB 文件（相对路径或绝对路径）
//     front   = 仅"用户自带的模型"需要：auto/+z/-z/+x/-x 声明朝向（Tripo 出的由提示词保证 ✓）
//
// 运行时对**每一件命中的物品**做三件事（见 libs/mod-kit/ItemModel.cs ✓）：
//   ① **就地**改游戏自己的图形 prefab（item.ItemGraphic / ItemAssetsCollection.GetPrefab ✓）
//      —— ✗ 不克隆模板 ✗ 不 DontDestroyOnLoad：
//         克隆件放进 DDOL 后写回 item.ItemGraphic，会让之后每个实例都生在 DDOL 场景 → **全都看不见** ✗
//         （实测现象：整件背包连它原来的外观一起消失 ✗）
//   ② 关掉它下面**所有**旧外观（不限渲染器类型 ✓ **精灵也要关** ✗ 以前只关网格 ✗）
//   ③ 挂上我们的 mesh（没有网格挂点就挂根 ✓）+ 清实体缓存 → 游戏下次重建就读到改过的 prefab ✓
//
// ⚠️ 物品的"外观"和"图标"是两回事 ✓：这里只换**世界里的模型** ✓（背包卡片上的图标不变 ✗）
//    图标要另做（见 replace-item-icon 那份 SKILL ✓）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelKit;
using UnityEngine;

namespace ItemModelSwap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        /// <summary>一条规则："给哪些物品，换成哪个模型" ✓</summary>
        class Entry
        {
            public List<string> Targets = new List<string>();
            public readonly List<int> TypeIds = new List<int>();
            public string ModelFile = "";

            // ⭐ 三层里我们要"往上加"的两层 ✓（第三层 = 游戏自带的 2D 图片 ✓ 永远不动 ✗）
            //   world    = 世界/展示那条（地上 ✓）     默认 **开** ✓（纯外观 ✓ 不改行为 ✓）
            //   handheld = 拿在手里那条（会改行为 ✓：可拿/UI 可选中 ✓）→ 默认 **关** ✓ 必须显式开 ✓
            public bool World = true;
            public bool Handheld = false;
            public string Front = "auto";
            public string ResolvedPath = "";
            /// <summary>config 的 `size`（**米** ✓、可选 ✓）—— 目标最长边 ✓。
            /// ≤0 = 没填 ✓ → 用 <see cref="ModelSize.For"/> 的自动档位 ✓
            /// ⚠️ **武器/装备一般不用填** ✓（已按游戏里那一档自动对齐 ✓）；只想特意做大/做小时才填 ✓</summary>
            public float Size;

            public bool Matches(ItemStatsSystem.Item item)
            {
                if (item == null) return false;
                if (Targets.Count == 0 && TypeIds.Count == 0) return false;
                if (TypeIds.Count > 0 && TypeIds.Contains(item.TypeID)) return true;
                // ⭐ 名字是**全等**匹配（不区分大小写 ✓）：子串会误伤 ✗
                //    （实测 target="MP5" 连 Item_BP_MP5 一起换 ✗、"Backpack" 命中 12 件 ✗）
                //    → 要一批，就写多个 typeIDs 或 targets ✓
                if (item.name != null)
                    for (int i = 0; i < Targets.Count; i++)
                        if (item.name.Equals(Targets[i], StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            public void Resolve(string dir)
            {
                ResolvedPath = string.IsNullOrEmpty(ModelFile) ? ""
                    : (Path.IsPathRooted(ModelFile) ? ModelFile : Path.Combine(dir, ModelFile));
            }
        }

        string _configPath;
        readonly List<Entry> _entries = new List<Entry>();

        float _nextCfgCheck;                       // 限频：每 0.25 秒才查一次 config ✓
        DateTime _cfgStamp = DateTime.MinValue;
        float _nextHeldSweep;                      // ⭐ 手持/装备那条要**反复复查**（物品是后来才被拿起来的 ✓）
        float _nextApply;                                           // ⭐ 扫描节流：`AllItems()` 是**整场景对象扫描** ✗ 不能每帧做 ✓

        /// <summary>模型缓存：路径 → （指纹, Mesh, 贴图）。指纹 = 路径 + 朝向 + 文件 mtime+size ✓</summary>
        sealed class CachedModel { public string Sig; public Mesh Mesh; public Texture2D Texture; }
        readonly Dictionary<string, CachedModel> _modelCache = new Dictionary<string, CachedModel>();

        /// <summary>已经处理过的物品（实例 ID → 我们的结果）✓ 用来避免每帧重复挂 ✓ + 还原 ✓</summary>
        readonly Dictionary<int, ItemModel.Result> _applied = new Dictionary<int, ItemModel.Result>();
        /// <summary>⭐ 场上克隆的补挂结果 ✓（热重载一起恢复 ✓ 有借有还 ✓）</summary>
        readonly Dictionary<int, ItemModel.Result> _appliedClone = new Dictionary<int, ItemModel.Result>();
        float _nextCloneSweep;

        void Start()
        {
            _configPath = Path.Combine(ModLoaderDir(), "config.json");
            ReadConfig();
            ItemModel.DebugOn = ReadDebugFlag();     // config 顶层 "debug": true ✓（只读诊断 ✓ 默认关 ✗）
            _cfgStamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
            Debug.Log($"[ItemModel] 规则 {_entries.Count} 条：" + string.Join(" / ",
                _entries.Select(e => $"targets=[{string.Join(",", e.Targets)}] typeIDs=[{string.Join(",", e.TypeIds)}]"
                    + $" world={(e.World ? "开" : "关")} handheld={(e.Handheld ? "开" : "关")} model='{e.ModelFile}'")));
        }

        /// <summary>本 mod 的目录（DLL 所在处）✓</summary>
        static string ModLoaderDir()
        {
            try { return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "."; }
            catch { return "."; }
        }

        void Update()
        {
            ReloadIfChanged();
            ApplyAll();
            SweepGraphicClones();        // ⭐ 场上的图形克隆也补一遍 ✓（掉落/展示那条 ✗）
            ItemModel.DumpNewObjects();              // 只读诊断 ✓（DebugOn 为 false 时直接返回 ✓）
        }

        /// <summary>config 顶层的 `debug`（只读诊断开关 ✓ 默认关 ✓）</summary>
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

        /// <summary>config.json 存盘即生效（热重载 ✓ 不用重启 ✓）</summary>
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

                // 先**还原**旧结果（旧几何重新显示 ✓ 我们的实例销毁 ✓）→ 再重挂 ✓
                // ⚠️ 少了这一步：重挂失败时旧零件还关着 → 物品会整个看不见 ✗（武器那边实测踩过 ✓）
                foreach (var kv in _applied)
                    if (kv.Value != null) kv.Value.Restore();
                _applied.Clear();
                foreach (var kv in _appliedClone) if (kv.Value != null) kv.Value.Restore();
                _appliedClone.Clear();
                // ⭐ 标记不在这里"全局摘" ✗ —— 由 `Result.Restore()` **精确摘掉自己那一层** ✓（上面那个循环里 ✓）
                //   全局摘会把**游戏对象上的标记**也摘掉 ✗ → 下一轮 Apply 会去重处理原物品模板 ✗ → 走成另一条分支 ✓
                Debug.Log("[ItemModel] config 已重载 → 重新挂 " + _entries.Count + " 条规则");
            }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] config 热重载失败：" + ex.Message); }
        }

        /// <summary>⭐ 给"**场上的图形克隆**"补挂 ✓（掉落/展示 / 换场景遗留 ✓）

        /// <para>原理 ✓：`ItemGraphicInfo.CreateAGraphic` = `Instantiate(item.ItemGraphic)` ✓

        /// —— 已经在场上的副本**不会**跟着 prefab 变 ✗ → 逐个补 ✓。</para>

        /// <para>⚠️ 只动名字带 `(Clone)` 的 ✗ **绝不碰共享 prefab** ✗

        /// （碰了就会像上次那样：手里那把枪的配件被一起关掉 ✗）</para></summary>

                /// <summary>⭐ 给"场上**已经存在**的图形实例"补挂 ✓（掉落 ✓ 手里 ✓ 穿戴 ✓ 背包 ✓ 一个扫描全覆盖 ✓）
        /// <para>判据 = 游戏自带的**反向索引** ✓：`ItemGraphicInfo.ItemRefrence`（`Setup(item)` 里写的 ✓）
        /// —— 源码实锤 ✓：`CreateAGraphic` 里会调 `itemGraphicInfo.Setup(item)` ✓
        /// ⇒ **每个实例都知道自己属于哪件物品** ✓ → 不再需要猜名字 ✗ 不再需要判祖先 ✗</para>
        /// <para>⚠️ 共享 prefab 自己**没有** owner ✓（没被 Setup 过 ✓）→ 自动跳过 ✓（它由 `Apply` 就地改 ✓）</para></summary>
        void SweepGraphicClones()
        {
            if (_entries.Count == 0) return;
            if (Time.unscaledTime < _nextCloneSweep) return;      // 0.5 秒一次 ✓（全场景扫 ✗ 要节流 ✓）
            _nextCloneSweep = Time.unscaledTime + 0.5f;

            List<ItemGraphicInfo> all;
            try { all = UnityEngine.Resources.FindObjectsOfTypeAll<ItemGraphicInfo>().ToList(); }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] 实例扫描失败 ✗：" + ex.Message); return; }

            foreach (var gi in all)
            {
                if (gi == null || gi.gameObject == null) continue;
                var owner = gi.ItemRefrence;                        // ⭐ 反向索引 ✓
                if (owner == null) continue;                        // prefab / 未绑定 → 跳过 ✓
                Entry hit = null;
                for (int i = 0; i < _entries.Count; i++) if (_entries[i].Matches(owner)) { hit = _entries[i]; break; }
                if (hit == null) continue;
                if (ItemModel.IsPatched(gi.gameObject)) continue;    // ⭐ 唯一判据（登记表）✓
                var cm = GetModel(hit);
                if (cm == null || cm.Mesh == null) continue;
                try
                {
                    var rc = ItemModel.ApplyToGraphicClone(gi, owner.name, owner.TypeID, cm.Mesh, cm.Texture,
                                                           ModelSize.FactorFor(owner.TypeID, hit.Size, cm.Mesh));
                    if (rc.Applied)
                    {
                        _appliedClone[gi.gameObject.GetInstanceID()] = rc;
                        Debug.Log($"[ItemModel] 已换（场上实例 ✓）：{gi.name}（属于 '{owner.name}'）｜{rc.Report}");
                    }
                }
                catch (Exception ex) { Debug.LogWarning($"[ItemModel] 场上实例补挂抛错 ✗：{ex.Message}"); }
            }
        }


        /// <summary>对"所有已加载的物品（含模板 + 场上实例 ✓）"逐条匹配并挂上模型 ✓</summary>
        void ApplyAll()
        {
            if (_entries.Count == 0) return;
            // ⭐ 节流 0.5 秒 ✓：`GameApi.AllItems()` 内部是 `FindObjectsOfTypeAll<Item>` ✗
            //   （**整场景对象扫描** ✓）→ 每帧做一次会明显拖慢游戏 ✗（实测：越玩越卡 ✓）
            if (Time.unscaledTime < _nextApply) return;
            _nextApply = Time.unscaledTime + 0.5f;

            List<ItemStatsSystem.Item> all;
            try { all = GameApi.AllItems().ToList(); }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] 枚举物品失败：" + ex.Message); return; }

            foreach (var item in all)
            {
                // ⭐ **跳过我们自己造的克隆物品** ✗（否则会"克隆 → 又被扫到 → 再克隆"自增 ✗ 实测卡死 ✓）
                if (item.name != null && item.name.StartsWith("ModelKit_item_")) continue;

                int id;
                try { id = item.GetInstanceID(); } catch { continue; }
                Entry hit = null;
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Matches(item)) { hit = _entries[i]; break; }   // 先命中的生效 ✓
                if (hit == null) continue;

                var cm = GetModel(hit);
                if (cm == null || cm.Mesh == null)
                {
                    _applied[id] = null;                     // 记一下，避免每帧重复报错 ✓
                    Debug.LogWarning($"[ItemModel] 规则 '{string.Join(",", hit.Targets)}' 的模型不可用 ✗（{hit.ModelFile}）");
                    continue;
                }

                // ⭐ 已经处理过的：**只补做"手持/装备"那条** ✓
                //   物品是后来才被拿起来的 ✓ 那时代替它的 `ActiveAgent` 才出现 ✓ —— 图形那条早就改好了 ✓
                //   （拿起/放下才需要重做 ✓ 1 秒一次足够 ✓ 幂等 ✓ 已换过的会直接返回 ✓）
                if (_applied.ContainsKey(id))
                {
                    // ⭐ 游戏会**重建物品预制体** ✗ → 我们写的 ItemGraphic 会被冲掉 ✓
                    //   （实测：糖果预制体的 ItemGraphic 变回 null ✗ → 掉地上那件就从预制体实例化出 2D 图 ✗）
                    //   → 每秒复查：**一旦发现被重置，就把它写回去** ✓（复用同一个图形对象 ✓ 不漏 ✓）
                    if (hit.World && item.ItemGraphic == null)
                    {
                        try
                        {
                            var rw = ItemModel.Apply(item, cm.Mesh, cm.Texture, hit.Handheld, hit.Size);
                            if (rw.Applied)
                                Debug.Log($"[ItemModel] 已换（被重置后补写）：'{item.name}'(typeID={item.TypeID})｜{rw.Report}");
                        }
                        catch { /* 补写失败不影响别的 ✓ */ }
                    }
                    if (hit.Handheld && Time.unscaledTime >= _nextHeldSweep)
                    {
                        _nextHeldSweep = Time.unscaledTime + 1f;
                        try
                        {
                            var r = ItemModel.ApplyHandheld(item, cm.Mesh, cm.Texture,
                                                ModelSize.FactorFor(item.TypeID, hit.Size, cm.Mesh));
                            if (r.Applied)
                                Debug.Log($"[ItemModel] 已换：'{item.name}'(typeID={item.TypeID}) ← {Path.GetFileName(hit.ModelFile)}｜{r.Report}");
                        }
                        catch (Exception ex)
                        { Debug.LogWarning($"[ItemModel] 手持抛错 ✗：{ex.Message}"); }
                    }
                    continue;
                }

                try
                {
                    // ⭐ 按开关做（三层里的前两层 ✓；第三层 = 游戏自带的 2D 图片 ✓ 我们永远不动 ✗）
                    bool didWorld = false;
                    if (hit.World)
                    {
                        var res = ItemModel.Apply(item, cm.Mesh, cm.Texture, hit.Handheld, hit.Size);
                        _applied[id] = res;
                        didWorld = res.Applied;
                        if (res.Applied)
                            Debug.Log($"[ItemModel] 已换：'{item.name}'(typeID={item.TypeID}) ← {Path.GetFileName(hit.ModelFile)}｜{res.Report}");
                        else if (!hit.Handheld && !res.NoOp)
                            Debug.Log($"[ItemModel] 跳过：'{item.name}' {res.Report}");
                    }
                    else _applied[id] = null;                        // 世界那条不要 ✓（只记一下 ✓）

                    if (hit.Handheld)                                // 手持那条（会改行为 ✓ 默认关 ✓）
                    {
                        var rh = ItemModel.ApplyHandheld(item, cm.Mesh, cm.Texture,
                                                  ModelSize.FactorFor(item.TypeID, hit.Size, cm.Mesh));
                        if (rh.Applied)
                            Debug.Log($"[ItemModel] 已换：'{item.name}'(typeID={item.TypeID}) ← {Path.GetFileName(hit.ModelFile)}｜{rh.Report}");
                        else if (!rh.NoOp)
                            Debug.Log($"[ItemModel] 跳过：'{item.name}' {rh.Report}");
                    }
                    if (!didWorld && !hit.Handheld && hit.World) { /* 世界那条做不了 ✓ 保持 2D 图片兜底 ✓ 上面已打过日志 ✓ */ }
                }
                catch (Exception ex)
                {
                    _applied[id] = null;
                    Debug.LogWarning($"[ItemModel] '{item.name}' 换模型抛错 ✗：{ex.Message}");
                }
            }
        }

        /// <summary>取（并缓存）某个规则要用的 Mesh+贴图 ✓（同一个文件只解析一次 ✓）</summary>
        CachedModel GetModel(Entry e)
        {
            if (string.IsNullOrEmpty(e.ResolvedPath) || !File.Exists(e.ResolvedPath)) return null;
            var fi = new FileInfo(e.ResolvedPath);
            // ⭐ 指纹**不含 Size** ✓（size 只影响挂载时的 scale ✓ 不改 mesh ✓ → 改 size 热重载不必重读模型 ✓）
            string sig = e.ResolvedPath + "|" + e.Front + "|" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;

            CachedModel c;
            if (_modelCache.TryGetValue(e.ResolvedPath, out c) && c != null && c.Sig == sig) return c;

            var loaded = GltfLoader.LoadFile(e.ResolvedPath, e.Front);
            c = new CachedModel { Sig = sig, Mesh = loaded.Mesh, Texture = loaded.MainTexture };
            _modelCache[e.ResolvedPath] = c;
            // ⭐ 尺寸：**只算系数** ✓（不改 mesh 顶点 ✗）；由挂载时的 `localScale` 承担 ✓
            //    只有武器会 ≠1 ✓（按 `GunType_*` tag ✓）→ 装备/物品 = 1 ✓；config `size` 可覆盖 ✓
            float scaleFactor = ModelSize.FactorFor(e.TypeIds.Count > 0 ? e.TypeIds.First() : 0, e.Size, loaded.Mesh);
            Debug.Log($"[ItemModel] 模型已解析：{Path.GetFileName(e.ResolvedPath)}｜{loaded.Report}"
                      + $"｜尺寸：{(scaleFactor != 1f ? $"scale={scaleFactor:0.###} ✓" : "scale=1（不缩放 ✓）")}");
            return c;
        }

        // ───────────────────────── config ─────────────────────────

        void ReadConfig()
        {
            _entries.Clear();
            if (!File.Exists(_configPath)) return;
            JsonValue root;
            try { root = Json.Parse(File.ReadAllText(_configPath)); }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] config.json 解析失败：" + ex.Message); return; }

            var arr = root["entries"];
            if (root.Has("entries") && arr != null && arr.IsArray)
            {
                foreach (var it in arr.Array)
                {
                    var e = ReadEntry(it);
                    if (e != null) _entries.Add(e);
                }
            }
            else
            {
                var e = ReadEntry(root);
                if (e != null) _entries.Add(e);
            }
            // ⭐ 顶层 `"size": <米>` = 所有条目的默认值 ✓（条目自己写了就听条目 ✓）
            //    用户实测报过 ✓：写在顶层却只认条目 → 以为"size 无效" ✗
            float topSize = root.GetFloat("size", 0f);
            if (topSize > 0f)
                foreach (var e in _entries) if (e.Size <= 0f) e.Size = topSize;
            foreach (var e in _entries) e.Resolve(ModLoaderDir());
        }

        Entry ReadEntry(JsonValue j)
        {
            if (j == null || j.Kind != JsonKind.Object) return null;
            var e = new Entry
            {
                Targets = Json.Strings(j, "targets"),
                ModelFile = j.GetStr("model", ""),
                Front = j.GetStr("front", "auto"),
                World = j.GetBool("world", true),        // 世界那条默认开 ✓
                Handheld = j.GetBool("handheld", false), // 手持那条默认关 ✓（它会改行为 ✓）
                Size = j.GetFloat("size", 0f),
            };
            var tids = j["typeIDs"];
            if (tids != null && tids.IsArray)
                foreach (var t in tids.Array)
                {
                    var v = t.AsInt(0);
                    if (v > 0) e.TypeIds.Add(v);
                }
            if (e.TypeIds.Count == 0 && e.Targets.Count == 0) return null;   // 没给匹配条件 ✗
            return e;
        }
    }
}
