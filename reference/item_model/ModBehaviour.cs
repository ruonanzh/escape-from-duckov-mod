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
// 运行时对**每一件命中的物品**做四件事（缺哪件都会"只换一半" ✗，见 ItemModel.cs）：
//   ① 克隆它自己的图形 prefab（保留挂点/groundPoint ✓）→ 只换几何 → 写回 item.ItemGraphic
//   ② 写到**物品模板**（ItemAssetsCollection.GetPrefab ✓）→ 以后每个实例开局就是对的了 ✓
//   ③ 清实体缓存（ClearAgentCache ✓）→ 游戏下次建实体时读我们的图形 ✓
//   ④ 就地换**已经拿在手里**的那个（只影响以后生成的实例，手里那个不会自己变 ✗）
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
            public string Front = "auto";
            public string ResolvedPath = "";

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

        /// <summary>模型缓存：路径 → （指纹, Mesh, 贴图）。指纹 = 路径 + 朝向 + 文件 mtime+size ✓</summary>
        sealed class CachedModel { public string Sig; public Mesh Mesh; public Texture2D Texture; }
        readonly Dictionary<string, CachedModel> _modelCache = new Dictionary<string, CachedModel>();

        /// <summary>已经处理过的物品（实例 ID → 我们的结果）✓ 用来避免每帧重复挂 ✓ + 还原 ✓</summary>
        readonly Dictionary<int, ItemModel.Result> _applied = new Dictionary<int, ItemModel.Result>();

        void Start()
        {
            _configPath = Path.Combine(ModLoaderDir(), "config.json");
            ReadConfig();
            _cfgStamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
            Debug.Log($"[ItemModel] 规则 {_entries.Count} 条：" + string.Join(" / ",
                _entries.Select(e => $"targets=[{string.Join(",", e.Targets)}] typeIDs=[{string.Join(",", e.TypeIds)}] model='{e.ModelFile}'")));
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

                Debug.Log("[ItemModel] config 已重载 → 重新挂 " + _entries.Count + " 条规则");
            }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] config 热重载失败：" + ex.Message); }
        }

        /// <summary>对"所有已加载的物品（含模板 + 场上实例 ✓）"逐条匹配并挂上模型 ✓</summary>
        void ApplyAll()
        {
            if (_entries.Count == 0) return;

            List<ItemStatsSystem.Item> all;
            try { all = GameApi.AllItems().ToList(); }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] 枚举物品失败：" + ex.Message); return; }

            foreach (var item in all)
            {
                int id;
                try { id = item.GetInstanceID(); } catch { continue; }
                if (_applied.ContainsKey(id)) continue;      // 处理过了 ✓（不重复挂 ✓）

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

                try
                {
                    var res = ItemModel.Apply(item, cm.Mesh, cm.Texture);
                    _applied[id] = res;
                    if (res.Applied)
                        Debug.Log($"[ItemModel] 已换：'{item.name}'(typeID={item.TypeID}) ← {Path.GetFileName(hit.ModelFile)}｜{res.Report}");
                    else
                        Debug.LogWarning($"[ItemModel] 跳过：'{item.name}' {res.Report}");
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
            string sig = e.ResolvedPath + "|" + e.Front + "|" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;

            CachedModel c;
            if (_modelCache.TryGetValue(e.ResolvedPath, out c) && c != null && c.Sig == sig) return c;

            var loaded = GltfLoader.LoadFile(e.ResolvedPath, e.Front);
            c = new CachedModel { Sig = sig, Mesh = loaded.Mesh, Texture = loaded.MainTexture };
            _modelCache[e.ResolvedPath] = c;
            Debug.Log($"[ItemModel] 模型已解析：{Path.GetFileName(e.ResolvedPath)}｜{loaded.Report}");
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
