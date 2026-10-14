// ModConfig.cs —— `config.json` 的条目读取（**两个模板共用一份**）。
//
// 为什么单独一个文件：`reference/weapon_model` 与 `reference/item_model` 各有一份**几乎相同**的
// 「读 entries 数组、按 action 分派、读字段」的代码。物品模板就因为少了一份而落过一次队
// （它不能读 `entries`，写 `new_item` 会静默无效）。搬到这里以后，两份模板不会再有这个差。
//
// **本文件只做「读」**：把 JSON 变成一组 `Entry`。**不碰游戏对象**，不改任何运行时状态。
// 因此它对两个模板都是纯输入层，谁也不会因为共用它而改变行为。
//
// 与设计仓 `desktop-gamer-agent-pi/plans/new-item-skill.md` 第 5.1 节对应。
//
// 两个模板的**既有差异**用 `Options` 保留（不合并成一个行为）：
//   · 武器模板：条目没有目标也留着；不认顶层 `size`；不认识的写法会打日志。
//   · 物品模板：条目没有目标就丢掉；认顶层 `size` 当默认；写法不完整时静默跳过。
// 这些差异是历史形成的，不是设计。**本步（抽共用）不改行为**，回归过了以后才可以在别处改。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ModelKit
{
    public static class ModConfig
    {
        // ── 四个 action 的名字（与 `.pi/lib/mod-config-data.ts` 的 `KNOWN_ACTIONS` 必须一致）──
        public const string ReplaceWeapon = "replace_weapon";
        public const string ReplaceItem = "replace_item";
        public const string NewWeapon = "new_weapon";
        public const string NewItem = "new_item";

        /// <summary>一条规则。**把所有模板用到的字段都放这里**（超集），
        /// 于是模板不需要再各自维护一份，也不会读到不存在的字段。</summary>
        public sealed class Entry
        {
            // ── 动作 ──
            /// <summary>由 `EntryFrom` 填，取值 `Options.DefaultAction` 或 JSON 里的 `action`
            /// （武器模板缺省 `replace_weapon`；物品模板缺省 `replace_item`）。
            /// 这里的初始值只是个占位：**每次解析都会被覆盖**，不要读它当默认值。</summary>
            public string Action = ReplaceItem;

            public bool IsNew => Is(NewWeapon) || Is(NewItem);
            public bool IsNewWeapon => Is(NewWeapon);
            public bool IsNewItem => Is(NewItem);

            /// <summary>这个条目是不是某个 action（大小写不敏感）。</summary>
            public bool Is(string action) => string.Equals(Action, action, StringComparison.OrdinalIgnoreCase);

            // ── 换外观（replace_*）──
            public List<string> Targets = new List<string>();
            public readonly HashSet<int> TypeIds = new HashSet<int>();
            public string ModelFile = "";
            public string Front = "auto";
            public string IconFile = "icon.png";

            /// <summary>物品那条路才读：世界/展示层（默认开）与手持层（默认关，它会改行为）。</summary>
            public bool World = true;
            public bool Handheld = false;

            /// <summary>config 的 `size`（**米**、可选）—— 目标最长边。
            /// ≤0 = 没填，于是用 `ModelSize.For` 的自动档位。
            /// **武器与装备一般不用填**（已按游戏里那一档自动对齐）；只想特意做大/做小时才填。</summary>
            public float Size;

            /// <summary>算出来的目标最长边（米）；null = 不缩放（scale = 1）。</summary>
            public float? SizeTarget;

            /// <summary>最终乘在 `localScale` 上的适配系数（抵消挂点缩放那部分另算）。</summary>
            public float Scale = 1f;

            /// <summary>`config.slots`（**米**，在我们模型的局部空间里）。</summary>
            public readonly Dictionary<string, Vector3> Slots = new Dictionary<string, Vector3>();

            // ── 解析出来的路径 ──
            /// <summary>图标的绝对路径（相对 mod 目录解析）。</summary>
            public string IconPath;
            /// <summary>模型的绝对路径（相对 mod 目录解析）。</summary>
            public string ResolvedPath = "";
            /// <summary>模型 + 朝向 + **文件 mtime/size** 的指纹：用来判断热重载后要不要重读。</summary>
            public string Signature = "";

            // ── 运行时缓存（每个条目各自一份）──
            public Mesh Mesh;
            public Texture2D Texture;

            // ── action = new_* 专用 ──
            public int NewTypeId;
            public int CloneFromItemId;
            public string DisplayName = "";
            public string Description = "";
            public string MerchantID = "";
            public int MaxStock = 1;
            public float PriceFactor = 1f;
            public bool OnSale = true;              // config 写 "onSale": false 就不上架
            public readonly Dictionary<string, float> Stats = new Dictionary<string, float>();
            public int Quality = -1;                // -1 = 不改
            public string DisplayQuality = "";      // 空 = 不改
            public bool CraftRecipe;
            public long CraftMoney;
            public readonly Dictionary<int, int> CraftMaterials = new Dictionary<int, int>();
            public List<string> CraftTags = new List<string>();

            // ── action = new_item 专用（物品模板第 5 步接上）──
            /// <summary>**额外标记**（Additional Tag）：在原型分类之上再加的 tag。
            /// 不写 = 完全沿用原型。见设计仓计划 §3.6.2。</summary>
            public List<string> Tags = new List<string>();
            /// <summary>堆叠上限；0 = 不改（沿用原型）。</summary>
            public int MaxStackCount;
            /// <summary>价值；负数 = 不改。</summary>
            public int Value = -1;
            /// <summary>重量（千克）；负数 = 不改。</summary>
            public float Weight = -1f;

            /// <summary>配方的 id：同一件物品固定一个，于是热重载不会重复加。</summary>
            public string RecipeId() => "ModelKit." + (NewTypeId > 0 ? NewTypeId.ToString() : "auto");

            /// <summary>这条规则管不管这件物品（只对 `replace_*` 有意义）。
            ///
            /// 名字是**全等**匹配（不区分大小写）：子串会误伤
            /// （实测 target="MP5" 连 Item_BP_MP5 一起换、"Backpack" 命中 12 件），
            /// 于是要一批，就写多个 typeIDs 或 targets。</summary>
            public bool Matches(ItemStatsSystem.Item item)
            {
                if (item == null) return false;
                if (IsNew) return false;                        // 新增那条不参与「换手里这把」的匹配
                if (Targets.Count == 0 && TypeIds.Count == 0) return false;
                if (TypeIds.Count > 0 && TypeIds.Contains(item.TypeID)) return true;
                if (item.name != null)
                    for (int i = 0; i < Targets.Count; i++)
                        if (item.name.Equals(Targets[i], StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            /// <summary>把相对路径解析成绝对路径，并算模型指纹。
            /// 指纹不含 `size`（size 只影响挂载时的 scale，不影响 mesh，热重载不必重读）。</summary>
            public void Resolve(string dir)
            {
                ResolvedPath = string.IsNullOrEmpty(ModelFile) ? ""
                    : (Path.IsPathRooted(ModelFile) ? ModelFile : Path.Combine(dir, ModelFile));
                IconPath = string.IsNullOrEmpty(IconFile)
                    ? null
                    : (Path.IsPathRooted(IconFile) ? IconFile : Path.Combine(dir, IconFile));

                Signature = ModelFile + "\n" + Front;
                // 指纹再带上**文件本身**的 mtime+size（旧版只看文件名，于是同名换内容不重读、
                // 而重建 Entry 后又永不重读 —— 两种毛病都靠这个指纹治）
                try
                {
                    if (!string.IsNullOrEmpty(ResolvedPath) && File.Exists(ResolvedPath))
                    {
                        var fi = new FileInfo(ResolvedPath);
                        Signature += "\n" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;
                    }
                }
                catch { /* 拿不到指纹就退化成「路径 + 朝向」（= 旧行为）*/ }
            }

            /// <summary>模型的绝对路径（相对 mod 目录解析）。</summary>
            public string ModelPath(string dir) =>
                string.IsNullOrEmpty(ModelFile) ? null
                : (Path.IsPathRooted(ModelFile) ? ModelFile : Path.Combine(dir, ModelFile));
        }

        /// <summary>两个模板的既有差异。**默认值 = 物品模板那份**（更严），武器模板显式改。</summary>
        public sealed class Options
        {
            /// <summary>日志前缀，例如 `[WeaponModel]`。</summary>
            public string LogTag = "[ModelKit]";

            /// <summary>条目没写 `action` 时用哪个。</summary>
            public string DefaultAction = ReplaceItem;

            /// <summary>true = 既没有 targets 也没有 typeIDs 的 **replace** 条目直接丢掉。
            /// 物品模板是 true（老行为）；武器模板是 false（留到最后并入日志）。
            /// 注意：`new_*` 条目本来就没有目标，任何取值下都**不丢**。</summary>
            public bool SkipReplaceEntriesWithoutTargets;

            /// <summary>true = 顶层 `size` 当所有条目的默认值（条目自己写了就听条目）。
            /// 物品模板是 true。用户实测报过：写在顶层却只认条目，于是以为「size 无效」。</summary>
            public bool ApplyTopLevelSize;

            /// <summary>true = 条目不是对象时打一条警告；false = 静默跳过。</summary>
            public bool WarnOnNonObjectEntry;

            /// <summary>true = config 里既没有 entries 也没有像样的扁平规则时打警告。</summary>
            public bool WarnWhenNothing;

            /// <summary>true = 读 `new_*` 的字段（`cloneFromItemId` / `stats` / `crafting` …）。</summary>
            public bool ReadNewFields = true;
        }

        /// <summary>读一个 config.json。
        /// 写法一：`{ "entries": [ … ] }`（多条目，优先）。
        /// 写法二：**扁平**——根对象本身就是一条规则（老写法，已装的 mod 不用改）。
        /// 两种都不像时返回空列表（按 `Options.WarnWhenNothing` 决定要不要打日志）。</summary>
        public static List<Entry> Read(string configPath, Options o)
        {
            var entries = new List<Entry>();
            if (o == null) o = new Options();
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                if (o.WarnWhenNothing) Debug.LogWarning($"{o.LogTag} no config.json: {configPath}");
                return entries;
            }

            JsonValue cfg;
            try { cfg = Json.Parse(StripJsonComments(File.ReadAllText(configPath))); }
            catch (Exception ex)
            {
                Debug.LogError($"{o.LogTag} config.json parse failed: {ex.Message}");
                return entries;
            }
            if (cfg == null || !cfg.IsObject) return entries;

            var arr = cfg["entries"];
            if (arr != null && arr.IsArray && arr.Count > 0)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    var j = arr[i];
                    if (j == null || !j.IsObject)
                    {
                        if (o.WarnOnNonObjectEntry) Debug.LogWarning($"{o.LogTag} entries[{i}] is not an object; skipped");
                        continue;
                    }
                    var e = EntryFrom(j, o);
                    if (e != null) entries.Add(e);
                }
            }
            else if (LooksLikeRule(cfg))
            {
                var e = EntryFrom(cfg, o);
                if (e != null) entries.Add(e);
            }
            else if (o.WarnWhenNothing)
            {
                Debug.LogWarning($"{o.LogTag} config.json has neither entries nor a flat rule"
                               + " (targets / typeIDs / model / newTypeId / cloneFromItemId)");
            }

            // 顶层 `"size": <米>` 当默认值（只有要求这个行为的模板才生效）
            if (o.ApplyTopLevelSize)
            {
                float topSize = cfg.GetFloat("size", 0f);
                if (topSize > 0f)
                    foreach (var e in entries) if (e.Size <= 0f) e.Size = topSize;
            }

            var dir = ModelCache.ModDir();
            foreach (var e in entries) e.Resolve(dir);
            return entries;
        }

        /// <summary>根对象像不像「一条扁平规则」。
        /// 比旧的 `targets || typeIDs || model` 多认三个：`action`、`newTypeId`、`cloneFromItemId`
        /// —— 否则一条只写新增字段的扁平 config 会被当成「什么都没有」，静默无效果。</summary>
        static bool LooksLikeRule(JsonValue cfg) =>
            cfg.Has("targets") || cfg.Has("typeIDs") || cfg.Has("model")
            || cfg.Has("action") || cfg.Has("newTypeId") || cfg.Has("cloneFromItemId");

        /// <summary>去掉 `//` 行注释（字符串里的不碰）—— **必须在 `Json.Parse` 之前**。
        ///
        /// 为什么需要：`Json.Parse` 不认注释，而
        ///   · 两个模板的 README 示例 config 里**就带 `//` 注释**（玩家会照抄），
        ///   · 离线校验器 `.pi/lib/mod-config-data.ts` 的 `stripJsonComments` **刻意容忍注释**。
        /// 两者不一致的后果很重：校验报 PASS，游戏里 `Json.Parse` 抛错、**整份 config 被丢掉**、
        /// 什么也不发生（静默无效果）。这里把两边统一。
        /// 规则与 TS 那份一致：只处理 `//`（不处理 `/* */`），字符串内部不动。</summary>
        public static string StripJsonComments(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("//", StringComparison.Ordinal) < 0) return text;
            var sb = new System.Text.StringBuilder(text.Length);
            bool inStr = false, esc = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    sb.Append('\n');
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>把一条 JSON 规则读成 Entry。返回 null = 这条该丢掉。</summary>
        public static Entry EntryFrom(JsonValue j, Options o)
        {
            if (j == null || !j.IsObject) return null;
            var e = new Entry
            {
                Action = j.GetStr("action", o.DefaultAction),
                Targets = Json.Strings(j, "targets"),
                ModelFile = j.GetStr("model", ""),
                Front = j.GetStr("front", "auto"),
                IconFile = j.GetStr("icon", "icon.png"),
                Size = j.GetFloat("size", 0f),
                World = j.GetBool("world", true),        // 世界那条默认开
                Handheld = j.GetBool("handheld", false), // 手持那条默认关（它会改行为）
            };

            var ids = j["typeIDs"];
            if (ids != null && ids.IsArray)
                for (int i = 0; i < ids.Count; i++)
                {
                    int v = ids[i].AsInt(0);
                    if (v > 0) e.TypeIds.Add(v);
                }

            // 没有目标的 **replace** 条目：按选项丢（`new_*` 本来就没有目标，不丢）
            if (o.SkipReplaceEntriesWithoutTargets && !e.IsNew && e.Targets.Count == 0 && e.TypeIds.Count == 0)
                return null;

            if (o.ReadNewFields)
            {
                e.NewTypeId = j.GetInt("newTypeId", 0);
                e.CloneFromItemId = j.GetInt("cloneFromItemId", 0);
                e.DisplayName = j.GetStr("displayName", "");
                e.Description = j.GetStr("description", "");
                e.MerchantID = j.GetStr("merchantID", "");
                e.MaxStock = j.GetInt("maxStock", 1);
                e.PriceFactor = j.GetFloat("priceFactor", 1f);
                e.OnSale = j.GetBool("onSale", true);
                e.Quality = j.GetInt("quality", -1);
                e.DisplayQuality = j.GetStr("displayQuality", "");

                // 物品专有
                e.Tags = Json.Strings(j, "tags");
                e.MaxStackCount = j.GetInt("maxStackCount", 0);
                e.Value = j.GetInt("value", -1);
                e.Weight = j.GetFloat("weight", -1f);

                foreach (var kv in ParseSlots(j)) e.Slots[kv.Key] = kv.Value;

                // stats = { "Damage": 13.0, … } —— 键用游戏自己的 `Stat.Key`
                var stats = j["stats"];
                if (stats != null && stats.IsObject)
                    foreach (var kv in stats.Object) e.Stats[kv.Key] = kv.Value.AsFloat(0f);

                // crafting = { "money": 0, "materials": { "662": 5 }, "tags": ["..."] }
                var craft = j["crafting"];
                if (craft != null && craft.IsObject)
                {
                    e.CraftRecipe = true;
                    e.CraftMoney = (long)craft.GetFloat("money", 0f);
                    e.CraftTags = Json.Strings(craft, "tags");
                    var mats = craft["materials"];
                    if (mats != null && mats.IsObject)
                        foreach (var kv in mats.Object)
                        {
                            int id;
                            if (int.TryParse(kv.Key, out id)) e.CraftMaterials[id] = kv.Value.AsInt(0);
                        }
                }
            }
            return e;
        }

        /// <summary>读 config 的 `slots`：`{ "muzzle": [x,y,z], … }`
        /// —— 键名**大小写不敏感**，我们统一写小写；每个值必须是 ≥3 个数的数组（**米**，可负）。</summary>
        public static Dictionary<string, Vector3> ParseSlots(JsonValue j)
        {
            var dict = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
            var s = j["slots"];
            if (s == null || !s.IsObject) return dict;
            foreach (var kv in s.Object)
            {
                var a = kv.Value;
                if (a == null || !a.IsArray || a.Count < 3)
                {
                    Debug.LogWarning($"{OptionsLogTag} slots.{kv.Key} must be three numbers [x,y,z] in metres; skipped");
                    continue;
                }
                dict[kv.Key] = new Vector3(a[0].AsFloat(0f), a[1].AsFloat(0f), a[2].AsFloat(0f));
            }
            return dict;
        }

        /// <summary>`ParseSlots` 的日志前缀（静态方法没有 Options 可传，用这个）。
        /// 由模板在 `Start` 里设一次。</summary>
        public static string OptionsLogTag = "[ModelKit]";
    }
}
