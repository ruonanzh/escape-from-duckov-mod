// WeaponModel（示例 mod）—— 把**一把或多把**武器的模型换成你给的 GLB。
//
// 这就是"替换武器模型"的**完整正确做法**：整个 mod 只有这一份配置 + 一句调用。
//
// config.json 支持两种写法（都行 ✓）：
//
//   ① 一套素材换一批武器（旧写法 ✓ 仍然有效）
//      { "typeIDs": [655], "model": "mp5.glb" }   // 模型名**按武器起** ✓（别固定叫 gun.glb ✗）
//
//   ② 每把武器各换各的（一个 mod 多条规则 ✓ 按数组顺序匹配，**先命中的生效**）
//      { "entries": [
//          { "targets": ["Item_SMG_MP5_Normal"], "model": "smg.glb", "icon": "smg_icon.png" },
//          { "typeIDs": [655], "model": "mp5.glb", "front": "-x" } ] }
//
//   字段（每条都能用 ✓）：
//     targets = 武器**对象名全等**（数组 ✓ 不区分大小写；例 ["Item_SMG_MP5_Normal"] ✓）
//     typeIDs = 或精确命中（数组 ✓ 例 [238, 655]）
//     model   = 放在本 mod 目录里的 GLB 文件（相对路径或绝对路径）
//     icon    = 图标文件名（默认 icon.png；没有就只换模型、不换图标）
//     front   = 仅"用户自带的模型"需要：auto/+z/-z/+x/-x 声明枪口朝向（Tripo 出的由提示词保证 ✓）
//
// 运行时会做（都在 libs/mod-kit 里，见 WeaponModel.cs / GltfLoader.cs / GameApi.cs）：
//   读 GLB → 建 Mesh+贴图（坐标系/绕序/UV 转换 + 握把归零）
//   → 找到你手里那把枪 → 关掉旧模型零件 → 挂上我们的模型 → 对齐 → 克隆游戏材质 + 换贴图

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModelKit;
using UnityEngine;

namespace WeaponModelSwap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        /// <summary>一条规则："给哪些武器，换成哪套素材"。一个 mod 可以有多条 ✓</summary>
        class Entry
        {
            public List<string> Targets = new List<string>();
            public readonly HashSet<int> TypeIds = new HashSet<int>();
            public string ModelFile = "";
            public string Front = "auto";
            public string IconFile = "icon.png";
            /// <summary>config 的 `size`（**米** ✓、可选 ✓）—— 目标最长边 ✓。
            /// ≤0 = 没填 ✓ → 用 <see cref="ModelSize.For"/> 的自动档位 ✓
            /// ⚠️ **武器一般不用填** ✓（已按游戏里那一档自动对齐 ✓）；只想特意做大/做小时才填 ✓</summary>
            public float Size;
            /// <summary>算出来的目标最长边（米 ✓）；null = 不缩放（scale = 1 ✓）</summary>
            public float? SizeTarget;
            /// <summary>⭐ 最终乘在 `localScale` 上的适配系数 ✓（抵消挂点缩放那部分另算 ✓）</summary>
            public float Scale = 1f;
            public string IconPath;          // 解析成绝对路径（相对 mod 目录 ✓）
            /// <summary>config 里的 `slots`（**比例** ✓ [L,H,D]，L 沿 Z、H 沿 Y、D 沿 X ✓）——
            /// 目前用来：① 换算成我们模型的局部坐标并打日志 ✓ ② 有 pivot 时用它当对齐基准 ✓
            /// （step 2 会把保留的配件按这些点重新挂上 ✓）</summary>
            public readonly Dictionary<string, Vector3> Slots = new Dictionary<string, Vector3>();
            public Mesh Mesh;                // 每个条目各自的模型缓存 ✓
            public Texture2D Texture;
            public string Signature = "";    // model+front 指纹：用来判断热重载后要不要重读 ✓

            public bool Matches(ItemStatsSystem.Item item)
            {
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
                IconPath = string.IsNullOrEmpty(IconFile)
                    ? null
                    : (Path.IsPathRooted(IconFile) ? IconFile : Path.Combine(dir, IconFile));
                Signature = ModelFile + "\n" + Front;   // ⭐ 不含 Size ✓（size 只影响挂载时的 scale ✓ 不影响 mesh ✓ 热重载不必重读 ✗）
                // ⭐ 指纹再带上**文件本身**的 mtime+size ✓（旧版只看文件名 ✗ → 同名换内容不重读 ✗、
                //    而重建 Entry 后又永不重读 ✗ —— 两种毛病都靠这个指纹治 ✓）
                var mp = ModelPath(dir);
                try
                {
                    if (!string.IsNullOrEmpty(mp) && File.Exists(mp))
                    {
                        var fi = new FileInfo(mp);
                        Signature += "\n" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;
                    }
                }
                catch { /* 拿不到指纹就退化成 路径+朝向 ✓（= 旧行为 ✓）*/ }
            }

            public string ModelPath(string dir) =>
                string.IsNullOrEmpty(ModelFile) ? null
                : (Path.IsPathRooted(ModelFile) ? ModelFile : Path.Combine(dir, ModelFile));
        }

        string _configPath;
        readonly List<Entry> _entries = new List<Entry>();

        CharacterMainControl _player;
        float _nextFindPlayer;
        float _nextCfgCheck;              // ⭐ a：限频 —— 每 0.25 秒才查一次 config 的 mtime ✓
        DateTime _cfgStamp = DateTime.MinValue;
        float _nextIconSweep;

        /// <summary>模型缓存（每个路径只留**最新一份** ✓）：路径 → （指纹 ✓, Mesh ✓, 贴图 ✓）
        /// ⭐ b：指纹 = 路径 + 朝向 + **文件 mtime+size** ✓ → 文件真变了才重解析 ✓✓</summary>
        sealed class CachedModel { public string Sig; public Mesh Mesh; public Texture2D Texture; }
        readonly Dictionary<string, CachedModel> _modelCache = new Dictionary<string, CachedModel>();
        readonly Dictionary<int, ModelKit.WeaponModel.Result> _applied = new Dictionary<int, ModelKit.WeaponModel.Result>();   // transformId → 换上的结果（含被禁用的旧零件 ✓ 用于恢复）
        /// <summary>⭐ 枪图形 prefab 名 → 条目 ✓（给"场上克隆"补挂用 ✓）</summary>
        readonly Dictionary<string, Entry> _graphicSrc = new Dictionary<string, Entry>();
        /// <summary>⭐ 场上克隆（掉落/展示 ✓）的补挂结果 ✓ —— 热重载一起恢复 ✓（有借有还 ✓）</summary>
        readonly Dictionary<int, ModelKit.ItemModel.Result> _appliedClone = new Dictionary<int, ModelKit.ItemModel.Result>();
        float _nextCloneSweep;
        string _lastHeld;

        void Start()
        {
            _configPath = Path.Combine(ModelLoaderDir(), "config.json");
            ReadConfig();
            _cfgStamp = File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
            LoadModels();
            // 启动就读好模型 → 玩家掏出枪时能**立刻**换（不然会先看到原模型 ✗）
            Debug.Log($"[WeaponModel] 规则 {_entries.Count} 条：" + string.Join(" / ",
                _entries.Select((e, i) => $"#{i + 1} targets=[{string.Join(",", e.Targets)}] typeIDs=[{string.Join(",", e.TypeIds)}] model='{e.ModelFile}'")));
        }

        void Update()
        {
            ReloadIfChanged();            SweepGraphicClones();          // ⭐ 场上的枪图形克隆也补一遍 ✓（掉落/展示 ✗）

            // 图标要**尽早**盖上：背包卡片可能在"掏出武器"之前就建好了 ✗
            // → 启动后几秒内反复按名字匹配所有（模板 + 实例）并设图标 ✓（每条规则各自扫一遍 ✓）
            if (Time.unscaledTime < 20f && Time.unscaledTime >= _nextIconSweep)
            {
                _nextIconSweep = Time.unscaledTime + 2f;
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    if (e.IconPath == null) continue;
                    var n = ModelKit.WeaponIcon.ApplyToAllMatching(e.Targets, e.TypeIds, e.IconPath);
                    if (n > 0) Debug.Log($"[WeaponModel] 图标预置：规则#{i + 1} 命中 {n} 个 Item（含模板）");
                }
            }
            if (_entries.Count == 0) return;

            // 找玩家较重（FindObjectsOfType）→ 只在没有/每 2 秒找一次；**每帧**只看"手里拿的是什么"（廉价 ✓）
            if (_player == null && Time.unscaledTime >= _nextFindPlayer)
            {
                _nextFindPlayer = Time.unscaledTime + 2f;
                _player = GameApi.FindMainCharacter();
            }
            if (_player == null) return;
            var agent = _player.CurrentHoldItemAgent;
            var item = agent != null ? agent.Item : null;
            if (item == null || agent == null || agent.gameObject == null) return;

            int hit = FirstMatch(item);   // 按数组顺序，先命中的生效 ✓
            if (item.name != _lastHeld)
            {
                _lastHeld = item.name;
                Debug.Log($"[WeaponModel] 手里的物品：'{item.name}'（typeID={item.TypeID}）→ " +
                          (hit >= 0 ? $"命中规则#{hit + 1} ✓ 会换" : "没有任何规则命中 —— 不换"));
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
                // ⭐ 记下枪图形 prefab 名 → 条目 ✓（场上克隆扫描直接查表 ✓）
                try { if (item.ItemGraphic != null) _graphicSrc[item.ItemGraphic.name] = entry; } catch { }
                Debug.Log("[WeaponModel] " + r.Report);
                // ⛔ **不要**用物品那条路来挂枪的掉落/展示图形 ✗（2026-10-08 两次实测都坏 ✓）
                //   `ItemModel.Apply` 会关掉枪身一批零件 ✓ **而配件（挂在 Sockets 下的子物体 ✓）会跟着一起看不见** ✗
                //   （游戏源码：`AccessoryBase.Init` → `transform.SetParent(socket)` ✓ → 配件是挂点的子物体 ✓）
                //   ⇒ 无论结果是否纳入恢复清单 ✓，**视觉上都会坏** ✗ → 一律不用 ✗
                //   ⇒ 枪的**掉落/展示**图形保持游戏原样 ✓ = **已知限制** ✓（要做得单独设计 ✓ 只换 mesh ✗ 不碰零件 ✓）
                // 图标：卡片上那个 Sprite 也换成我们的
                if (entry.IconPath != null) Debug.Log("[WeaponModel] " + ModelKit.WeaponIcon.Apply(item, entry.IconPath));
            }
            else Debug.LogWarning("[WeaponModel] 没换成：" + r.Report);
        }

        /// <summary>⭐ 给"**场上的图形克隆**"补挂 ✓ —— 枪**掉在地上**那条 ✗

        /// <para>为什么必须这样 ✓：`ItemGraphicInfo.CreateAGraphic` = `Instantiate(item.ItemGraphic)` ✓

        /// → 掉落物 = **游戏当场克隆的 prefab 副本** ✓。

        /// 直接改共享 prefab 会把**手里那把**的零件一起关掉 ✗（配件是挂点的子物体 ✓ 实测两次都坏 ✓）。

        /// ⇒ 只动"名字带 `(Clone)`"的那份 ✓ —— 每个副本独立 ✓ 动它**绝不影响**手里那把 ✓</para></summary>

        void SweepGraphicClones()

        {

            if (_graphicSrc.Count == 0) return;

            if (Time.unscaledTime < _nextCloneSweep) return;      // 0.5 秒一次 ✓（掉落后半秒内换好 ✓）

            _nextCloneSweep = Time.unscaledTime + 0.5f;

            List<ItemGraphicInfo> all;

            try { all = UnityEngine.Resources.FindObjectsOfTypeAll<ItemGraphicInfo>().ToList(); }

            catch { return; }

            foreach (var gi in all)

            {

                if (gi == null || gi.gameObject == null) continue;

                if (!gi.name.EndsWith("(Clone)")) continue;                       // ⭐ 只认克隆 ✓

                // ⭐⭐ 只碰"**掉落/展示**"那条 ✗ —— 判据：祖先里有游戏自己的显示挂点 `GraphicRoot` ✓
        //   ⚠️ 手里那把的图形**也是** `(Clone)` ✗（游戏给 agent 克隆的 ✓）→ 少了这一条就会误伤 ✓
        //      实测：配件看不见 ✓（配件是挂点的子物体 ✓ 挂点长在那些零件里 ✓）
        if (!HasAncestorNamed(gi.transform, "GraphicRoot")) continue;
        if (gi.GetComponent<ModelKit.ItemModel.Marked>() != null) continue; // 改过的 ✓

                string src = gi.name.Substring(0, gi.name.Length - "(Clone)".Length);

                if (!_graphicSrc.TryGetValue(src, out var e) || e == null) continue;

                if (e.Mesh == null) continue;

                try

                {

                    var rc = ModelKit.ItemModel.ApplyToGraphicClone(gi, src, 0, e.Mesh, e.Texture, e.Scale);

                    if (rc.Applied)

                    {

                        _appliedClone[gi.GetInstanceID()] = rc;

                        Debug.Log($"[WeaponModel] 已换（场上副本 ✓ 掉落/展示）：{gi.name}｜{rc.Report}");

                    }

                }

                catch (Exception ex) { Debug.LogWarning($"[WeaponModel] 场上副本补挂抛错 ✗：{ex.Message}"); }

            }

        }


        /// <summary>祖先里有叫 `name` 的吗 ✓（用来区分"掉落/展示"和"拿在手里" ✗）</summary>


        static bool HasAncestorNamed(Transform t, string name)


        {


            for (int i = 0; t != null && i < 12; i++, t = t.parent)


                if (t.name == name) return true;


            return false;


        }



        /// <summary>config.json 一改存盘就重新生效（不用重启游戏）：重读配置、丢旧实例、必要时重读模型 ✓</summary>
        void ReloadIfChanged()
        {
            // ⭐ a：**限频** —— 每 0.25 秒才查一次（不是每帧 ✗）：文件 stat 从 ~120 次/秒 降到 4 次/秒 ✓
            //    人感觉不出 0.25s ✓（“存盘即生效”的手感不变 ✓）
            if (Time.unscaledTime < _nextCfgCheck) return;
            _nextCfgCheck = Time.unscaledTime + 0.25f;
            try
            {
                if (!File.Exists(_configPath)) return;
                var st = File.GetLastWriteTimeUtc(_configPath);
                if (st == _cfgStamp) return;
                _cfgStamp = st;   // 先记时间戳 ✓ 避免下面抛错时反复重试 ✗

                ReadConfig();     // ⭐ 这一步不能少 ✗（我差点改丢 ✓）—— 重读 config → _entries

                // ⭐ 先销毁我们的实例 **并恢复被禁用的旧零件** ✓（否则一旦重挂失败（如模型文件缺失/解析出错 ✗），
                //    旧零件还关着 → **枪会整个看不见** ✗✓ —— 2026-10-06 实测踩到过 ✓）
                foreach (var kv in _applied)
                {
                    if (kv.Value == null) continue;
                    kv.Value.Restore();   // 旧零件显示 ✓ + 搬过的挂点位置 ✓ 全部还原 ✓
                    if (kv.Value.Instance != null) UnityEngine.Object.Destroy(kv.Value.Instance);
                }
                _applied.Clear();
                foreach (var kv in _appliedClone) if (kv.Value != null) kv.Value.Restore();
                _appliedClone.Clear();
                _lastHeld = null;
                // ⭐ **每次都重读模型** ✗：热重载会把 Entry 重建（Mesh 变 null ✗），
                //    而旧代码只在 model/front 指纹变了时才重读 ✗ → 只改 slots/target/icon 时会
                //    “永远挂不上” ✗✓（同样导致枪不见 ✓）。重读一个 GLB 只要几十毫秒 ✓ 换来正确性 ✓。
                LoadModels();
                Debug.Log($"[WeaponModel] 配置已热重载：{_entries.Count} 条规则（旧实例已丢弃，稍后按新配置重挂）");
            }
            catch (Exception e) { Debug.LogWarning($"[WeaponModel] 热重载失败：{e.Message}"); }
        }

        /// <summary>按数组顺序找第一条命中的规则 ✓（找不到返回 -1）</summary>
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
                var path = e.ModelPath(dir);
                if (string.IsNullOrEmpty(path)) { Debug.LogWarning($"[WeaponModel] 规则 model='{e.ModelFile}' 是空的 —— 跳过"); continue; }
                if (!File.Exists(path)) { Debug.LogWarning($"[WeaponModel] 找不到模型文件：{path}"); continue; }
                // ⭐ b：指纹没变就直接用上次解析好的 ✓✓（改 slots/target/icon 时热重载≈瞬发 ✓）
                if (_modelCache.TryGetValue(path, out var c) && c != null && c.Sig == e.Signature && c.Mesh != null)
                {
                        e.Mesh = c.Mesh; e.Texture = c.Texture;
                        // ⭐ 缓存命中时也要**重算尺寸系数** ✗ —— 以前这里直接 `continue` ✓ → 改 `size` 无效 ✓
                        //    （实测报过 ✓：热重载后 `e.Scale` 停在默认 1 ✗ 而模型根本不重读 ✓ → 看着像 size 坏了 ✓）
                        e.SizeTarget = ModelSize.For(e.TypeIds.Count > 0 ? e.TypeIds.First() : 0, e.Size > 0f ? (float?)e.Size : null);
                        e.Scale = ModelSize.Factor(c.Mesh, e.SizeTarget);
                        continue;
                    }
                try
                {
                    var g = GltfLoader.LoadFile(path, e.Front);
                    e.Mesh = g.Mesh; e.Texture = g.MainTexture;
                    // ⭐ 尺寸：**只算一个系数** ✓（不改 mesh 顶点 ✗）；由挂载时的 `localScale` 承担 ✓
                    //    优先 config `size`（玩家/agent 覆盖 ✓）→ 否则按游戏 `GunType_*` tag ✓ → 否则 1 ✓
                    e.SizeTarget = ModelSize.For(e.TypeIds.Count > 0 ? e.TypeIds.First() : 0, e.Size > 0f ? (float?)e.Size : null);
                    e.Scale = ModelSize.Factor(g.Mesh, e.SizeTarget);
                    float sizeBefore = ModelSize.Longest(g.Mesh);
                    _modelCache[path] = new CachedModel { Sig = e.Signature, Mesh = g.Mesh, Texture = g.MainTexture };
                    Debug.Log($"[WeaponModel] 读到模型 {Path.GetFileName(path)}：" + g.Report
                              + $"｜尺寸：{(e.SizeTarget.HasValue ? $"最长边 {sizeBefore:0.###} → {e.SizeTarget.Value:0.###} m（scale={e.Scale:0.###} ✓）" : "不缩放（scale=1 ✓）")}");
                }
                catch (Exception ex) { Debug.LogError($"[WeaponModel] 读模型失败（{Path.GetFileName(path)}）：{ex.Message}"); }
            }
        }

        void ReadConfig()
        {
            try
            {
                var dir = ModelLoaderDir();
                _entries.Clear();
                if (!File.Exists(_configPath)) { Debug.LogWarning($"[WeaponModel] 没有 config.json：{_configPath}"); return; }
                var cfg = Json.Parse(File.ReadAllText(_configPath));

                // ② 多条目写法（优先 ✓）
                var arr = cfg["entries"];
                if (arr != null && arr.IsArray && arr.Count > 0)
                {
                    for (int i = 0; i < arr.Count; i++)
                    {
                        var j = arr[i];
                        if (j == null || !j.IsObject) { Debug.LogWarning($"[WeaponModel] entries[{i}] 不是对象 —— 跳过"); continue; }
                        _entries.Add(EntryFrom(j));
                    }
                }
                // ① 旧写法：扁平字段 = 一条规则 ✓（向后兼容 ✓ 已装的 mod 不用改 ✓）
                else if (cfg.Has("targets") || cfg.Has("typeIDs") || cfg.Has("model"))
                {
                    _entries.Add(EntryFrom(cfg));
                }
                else Debug.LogWarning("[WeaponModel] config.json 里既没有 entries 也没有 targets/typeIDs/model");

                foreach (var e in _entries) e.Resolve(dir);
            }
            catch (Exception e) { Debug.LogError($"[WeaponModel] 读 config 失败：{e.Message}"); }
        }

        static Entry EntryFrom(JsonValue j)
        {
            var e = new Entry
            {
                Targets = Json.Strings(j, "targets"),
                ModelFile = j.GetStr("model", ""),
                Front = j.GetStr("front", "auto"),
                IconFile = j.GetStr("icon", "icon.png"),
                Size = j.GetFloat("size", 0f),
            };
            var ids = j["typeIDs"];
            if (ids != null && ids.IsArray) for (int i = 0; i < ids.Count; i++) e.TypeIds.Add(ids[i].AsInt(0));
            foreach (var kv in ParseSlots(j)) e.Slots[kv.Key] = kv.Value;
            return e;
        }

        /// <summary>读 config 的 `slots`：{ "Muzzle": [L,H,D], … } —— 每个值必须是≥3 个数的数组 ✓（0~1 比例 ✓）</summary>
        static Dictionary<string, Vector3> ParseSlots(JsonValue j)
        {
            var dict = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var s = j["slots"];
            if (s == null || !s.IsObject) return dict;
            foreach (var kv in s.Object)
            {
                var a = kv.Value;
                if (a == null || !a.IsArray || a.Count < 3)
                { Debug.LogWarning($"[WeaponModel] slots.{kv.Key} 应是 [L,H,D] 三个 0~1 的数 —— 已跳过"); continue; }
                dict[kv.Key] = new Vector3(a[0].AsFloat(0f), a[1].AsFloat(0f), a[2].AsFloat(0f));
            }
            return dict;
        }

        static string ModelLoaderDir()
        {
            try { return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location); }
            catch { return "."; }
        }
    }
}
