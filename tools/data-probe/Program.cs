using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

// data-probe — read a Unity3D game's content data offline.
//
// What it reads: the serialized Unity objects in the game's data files — prefabs,
// ScriptableObjects, MonoBehaviours and scene files (levelN) — as typed fields +
// values (e.g. an Item's `value`, a Quest's `requiredItemCount`, a
// CharacterRandomPreset's `isBoss`, a scene object's Transform position).
// It is READ-ONLY: it never changes the game or the mod.
//
// Why it can read custom fields: the game strips type trees from its data files, so
// we generate them from the game's own managed assemblies (MonoCecilTempGenerator),
// plus a class database (classdata.tpk) for the built-in Unity types.
//
// Usage:
//   data-probe --managed <Managed dir> --data <Data dir> --action <a> [options]
// Actions: classes | search | list | dump | refs | export | transform
static partial class DataProbe
{
    static int Main(string[] args)
    {
        // Windows：.NET 默认按控制台代码页（GBK/CP437）写 stdout，而调用方按 UTF-8 解码 → 资产里的非 ASCII 字段会乱码。
        // 统一成 UTF-8（无 console 时 setter 可能抛，忽略）。
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        var o = ParseArgs(args, out var missing);
        var managed = Opt(o, "managed");
        var data = Opt(o, "data");
        var action = Opt(o, "action");
        if (managed == null || data == null || action == null) { Usage(); return 2; }
        int limit = int.TryParse(Opt(o, "limit"), out var ln) ? ln : 2000;
        int depth = int.TryParse(Opt(o, "depth"), out var dp) ? dp : 3;
        int offset = int.TryParse(Opt(o, "offset"), out var ofs) ? Math.Max(0, ofs) : 0;
        int rows = int.TryParse(Opt(o, "rows"), out var rw) ? Math.Max(1, rw) : 500;

        // ── 参数体检（S5/S6）：未知 flag 与「有 flag 没值」一律 exit 2；
        //    给错 action 的 flag（例如 export 上的 --depth）只告警，不改变语义。
        var known = KnownFor(action);
        var unknown = o.Keys.Where(k => !AllKnown.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var irrelevant = o.Keys.Where(k => AllKnown.Contains(k) && !known.Contains(k)).ToList();
        if (unknown.Count > 0 || missing.Count > 0)
        {
            foreach (var k in unknown)
                Console.Error.WriteLine($"data-probe: unknown option --{k} (known for '{action}': {string.Join(" ", known.OrderBy(x => x, StringComparer.Ordinal))})");
            foreach (var k in missing)
                Console.Error.WriteLine($"data-probe: option --{k} needs a value");
            return 2;
        }

        var classData = FindClassData();
        if (classData == null) { Console.Error.WriteLine("data-probe: classdata.tpk not found next to the tool (lib/classdata.tpk)."); return 1; }

        var am = new AssetsManager();
        am.MonoTempGenerator = new MonoCecilTempGenerator(managed);
        am.LoadClassPackage(classData);

        var files = ResolveFiles(data, Opt(o, "file"));
        if (files.Count == 0) { Console.Error.WriteLine($"data-probe: no .assets file found under {data}"); return 1; }
        var insts = new List<AssetsFileInstance>();
        foreach (var f in files)
        {
            try
            {
                if (LooksLikeBundle(f))
                {
                    // AssetBundle（mod 的包也走这里）：内存解包 → 逐个串行化文件；只读，不落临时文件
                    var bun = am.LoadBundleFile(f, true);
                    var dirs = bun.file.BlockAndDirInfo.DirectoryInfos;
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        var nm = dirs[i].Name;
                        if (nm.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) || nm.EndsWith(".resS", StringComparison.OrdinalIgnoreCase)) continue;
                        var inst0 = am.LoadAssetsFileFromBundle(bun, i, true);
                        am.LoadClassDatabaseFromPackage(inst0.file.Metadata.UnityVersion);
                        insts.Add(inst0);
                    }
                }
                else
                {
                    var i = am.LoadAssetsFile(f, true); am.LoadClassDatabaseFromPackage(i.file.Metadata.UnityVersion); insts.Add(i);
                }
            }
            catch (Exception e) { Console.Error.WriteLine($"data-probe: skip {Path.GetFileName(f)}: {e.Message}"); }
        }

        var outPath = Opt(o, "out");
        if (!string.IsNullOrEmpty(outPath))
        {
            var outDir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (outDir != null && !Directory.Exists(outDir))
            { Console.Error.WriteLine($"data-probe: --out directory does not exist: {outDir}"); return 2; }
        }
        // P2：数字型 flag 写错不再默默回默认值
        foreach (var k in new[] { "limit", "offset", "depth", "rows" })
        {
            var v = Opt(o, k);
            if (v != null && !int.TryParse(v, out _))
            { Console.Error.WriteLine($"data-probe: --{k} needs an integer, got '{v}'"); return 2; }
        }
        using var outLines = new Out(outPath, limit);
        foreach (var k in irrelevant)
            outLines.Note($"# WARNING: --{k} has no effect on '{action}' (use it with: {string.Join(", ", ActionsUsing(k))})");
        var rc = 0;
        switch (action)
        {
            case "classes": rc = Classes(am, insts, outLines); break;
            case "list": rc = List(am, insts, Opt(o, "class"), offset, outLines); break;
            case "search": rc = Search(am, insts, Opt(o, "pattern"), Opt(o, "class"), offset, outLines); break;
            case "dump": rc = DumpAsset(am, insts, o, depth, outLines); break;
            case "refs": rc = Refs(am, insts, o, outLines); break;
            case "transform": rc = Transform(am, insts, o, depth, outLines); break;
            case "export":
            {
                // 写文件时默认不再限制行数（落盘不进模型上下文，不存在刷屏问题 ✓）
                // S8：export 的限行参数是 --rows；给了 --limit 也当 alias 用（不静默忽略）。
                // 行数上限：--rows 是唯一权威。--limit 只在「没有 --out」时当预览行数 alias ——
                // 有 --out 时绝不能用它：扩展 inspect_game_data 每次都传 --limit 2000，照用会把落盘截在 2000 行。
                var rowsArg = Opt(o, "rows");
                var limitArg = Opt(o, "limit");
                int rowCap;
                if (rowsArg != null)
                {
                    if (!int.TryParse(rowsArg, out var rv) || rv <= 0)
                    { Console.Error.WriteLine($"data-probe: --rows needs a positive integer, got '{rowsArg}'"); return 2; }
                    rowCap = rv;
                }
                else if (outPath != null)
                {
                    rowCap = 1000000;
                    if (limitArg != null)
                        outLines.Note("# note: --limit is ignored for export when --out is set (the file gets the full table, cap 1,000,000); use --rows N to cap it");
                }
                else
                {
                    if (limitArg != null && (!int.TryParse(limitArg, out var lv2) || lv2 <= 0))
                    { Console.Error.WriteLine($"data-probe: --limit needs a positive integer, got '{limitArg}'"); return 2; }
                    rowCap = limitArg != null ? int.Parse(limitArg) : rows;
                }
                rc = Export(am, insts, Opt(o, "class"), AllOpts(args, "match"), AllOpts(args, "field"), rowCap, offset, outLines);
                break;
            }
            default: Console.Error.WriteLine($"data-probe: unknown action '{action}'"); return 2;
        }

        var text = outLines.Render();
        Console.Write(text);
        if (!text.EndsWith("\n")) Console.WriteLine();
        return rc;
    }

    // ── arg parsing ────────────────────────────────────────────────────────────
    /// 有值的 flag（其余视为开关：`--follow` / `--exact`）。对应 S6：末尾缺值不再当成 `"true"`。
    static readonly HashSet<string> BoolFlags = new(StringComparer.OrdinalIgnoreCase) { "follow", "exact" };

    /// 所有认识的选项（并集）。不在这个集合里 = 拼错 → exit 2（S5）。
    static readonly HashSet<string> AllKnown = new(StringComparer.OrdinalIgnoreCase)
    {
        "managed", "data", "action", "file", "limit", "offset", "out",
        "class", "pattern", "name", "typeid", "pathid", "depth", "mesh", "has", "by",
        "match", "field", "rows", "follow", "exact",
    };

    static HashSet<string> KnownFor(string action)
    {
        var common = new[] { "managed", "data", "action", "file", "limit", "offset", "out" };
        string[] extra = action switch
        {
            "list" => new[] { "class" },
            "search" => new[] { "pattern", "class" },
            "dump" => new[] { "class", "name", "typeid", "pathid", "depth", "follow" },
            "refs" => new[] { "class", "name", "typeid", "pathid", "by" },
            "transform" => new[] { "name", "pattern", "pathid", "mesh", "exact", "has", "depth" },
            "export" => new[] { "class", "match", "field", "rows" },
            _ => Array.Empty<string>(),
        };
        var set = new HashSet<string>(common, StringComparer.OrdinalIgnoreCase);
        foreach (var e in extra) set.Add(e);
        return set;
    }

    /// 某个 flag 属于哪些 action（给 S9 的告警用）
    static readonly string[] AllActions = { "classes", "list", "search", "dump", "refs", "export", "transform" };
    static List<string> ActionsUsing(string opt)
        => AllActions.Where(a => KnownFor(a).Contains(opt)).ToList();

    static Dictionary<string, string> ParseArgs(string[] args, out List<string> missing)
    {
        missing = new List<string>();
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var k = args[i].Substring(2);
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
            if (hasValue) { o[k] = args[++i]; continue; }
            if (BoolFlags.Contains(k)) { o[k] = "true"; continue; }
            o[k] = "true";
            missing.Add(k);                 // S6：值缺失（不再默默当 true）
        }
        return o;
    }
    static string Opt(Dictionary<string, string> o, string k) => o.TryGetValue(k, out var v) ? v : null;

    /// 同一个选项可重复出现（`--match` / `--field`）：把全部取值按出现顺序收回来。
    static List<string> AllOpts(string[] args, string k)
    {
        var list = new List<string>();
        for (int i = 0; i + 1 < args.Length; i++)
            if (string.Equals(args[i], "--" + k, StringComparison.OrdinalIgnoreCase) && !args[i + 1].StartsWith("--"))
                list.Add(args[i + 1]);
        return list;
    }

    static void Usage() => Console.Error.WriteLine(
        "usage: data-probe --managed <Managed dir> --data <Data dir> --action <classes|search|list|dump|refs|export|transform>\n" +
        "  classes                         list asset class names + counts\n" +
        "  list    --class <C>             list assets of a class (first 500, with a hint to narrow)\n" +
        "  search  --pattern <p> [--class C]  find assets by name\n" +
        "  dump    --class <C> (--name <n>|--typeid <t>|--pathid <p>) [--depth d] [--follow]\n" +
        "  refs    --class <C> (--name <n>|--typeid <t>|--pathid <p>)   what it references\n" +
        "          add --by <class>  **reverse lookup**: which objects of that class reference it\n" +
        "  transform (--name <n>|--pathid <p>|--mesh <mesh name>) [--exact] [--has <Component>] [--depth d]\n" +
        "          a Transform local TRS + parent chain + world scale;\n" +
        "          also prints the child tree (find Sockets/...) and, if a MeshFilter is attached,\n" +
        "          the mesh AABB x world scale = **real in-game size** (levelN scenes hold the prefabs)\n" +
        "  export  --class <C> [--match <path><op><value>]... [--field <path>]... [--rows N] [--offset N] [--out <file>]\n" +
        "          one table row per matched asset; --out writes the full table to a file (stdout then gets a preview only)\n" +
        "          'a.b' field, 'a[]'/'a[i]' expand/index an array, '#class'/'#name' = resolved object's class/name\n" +
        "          match ops: = != ~ (substring) > >= < <=   -   columns are TAB-separated, arrays joined with ';'\n" +
        "  common: [--file <x.assets|levelN>[,<more>]] [--limit N] [--offset N] [--out <file>]\n" +
        "  note:  an array can print \"<N item(s), not expanded>\" when the type database lacks its\n" +
        "         element type; \"[]\" means empty. This does not depend on --depth.\n" +
        "  note:  --file levelN reads a scene (level files are serialized like .assets)");

    static string FindClassData()
    {
        foreach (var p in new[] {
            Path.Combine(AppContext.BaseDirectory, "classdata.tpk"),
            Path.Combine(AppContext.BaseDirectory, "lib", "classdata.tpk"),
            Path.Combine(Directory.GetCurrentDirectory(), "lib", "classdata.tpk"),
        })
            if (File.Exists(p)) return p;
        return null;
    }

    static List<string> ResolveFiles(string data, string file)
    {
        // ⭐ `--file` 支持**多个**（逗号分隔 ✓）—— 跨文件反查必须同时加载两边 ✗
        //    例：`Mesh` 在 `sharedassets4.assets` ✓ 而引用它的 `MeshFilter` 在 `level4` ✗
        if (file != null)
        {
            var outList = new List<string>();
            foreach (var one in file.Split(','))
            {
                var t = one.Trim();
                if (t.Length == 0) continue;
                outList.Add(Path.IsPathRooted(t) ? t : Path.Combine(data, t));
            }
            if (outList.Count > 0) return outList;
        }
        List<string> assets;
        try { assets = Directory.GetFiles(data, "*.assets").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(); }
        catch (Exception e) { Console.Error.WriteLine($"data-probe: cannot list '*.assets' under '{data}': {e.Message}"); return new List<string>(); }
        if (assets.Count > 0) return assets;
        // 没有 .assets 时，把目录里看起来像 AssetBundle 的文件也接上（例如 mod 的包目录）
        return Directory.GetFiles(data).Where(LooksLikeBundle).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>按文件头判断是不是 Unity AssetBundle（UnityFS / UnityWeb / UnityRaw）。只读前 8 字节。</summary>
    static bool LooksLikeBundle(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[8];
            if (fs.Read(buf, 0, 8) < 8) return false;
            var magic = System.Text.Encoding.ASCII.GetString(buf);
            return magic.StartsWith("UnityFS") || magic.StartsWith("UnityWeb") || magic.StartsWith("UnityRaw");
        }
        catch { return false; }
    }

    // ── asset helpers ──────────────────────────────────────────────────────────
    static bool IsMonoBehaviour(AssetsFileInstance inst, AssetFileInfo info) => info.TypeId == 0x72;

    static string ClassName(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField bf)
    {
        try
        {
            var se = am.GetExtAsset(inst, bf["m_Script"]);
            if (se.baseField != null) return se.baseField["m_ClassName"].AsString;
        }
        catch { }
        return null;
    }

    static string AssetName(AssetTypeValueField bf)
    {
        foreach (var n in new[] { "m_Name", "displayName" })
        {
            try { var v = bf[n].AsString; if (!string.IsNullOrEmpty(v)) return v; } catch { }
        }
        return null;
    }

    static int TypeId(AssetTypeValueField bf)
    {
        try { return bf["typeID"].AsInt; } catch { return -1; }
    }

    /// Enumerate every asset in the loaded files (all Unity types, not only scripts).
    static IEnumerable<(AssetsFileInstance inst, AssetFileInfo info)> AllInfos(List<AssetsFileInstance> insts)
    {
        foreach (var inst in insts)
            foreach (var info in inst.file.AssetInfos)
                yield return (inst, info);
    }

    /// Resolve an asset's class name: for a MonoBehaviour via its m_Script, otherwise from the class database.
    static string ClassNameOf(AssetsManager am, AssetsFileInstance inst, AssetFileInfo info)
    {
        if (info.TypeId == 0x72)
        {
            try
            {
                var bf = am.GetBaseField(inst, info);
                if (bf != null) return ClassName(am, inst, bf);
            }
            catch { }
            return "(MonoBehaviour)";
        }
        try
        {
            var cdt = am.ClassDatabase?.FindAssetClassByID((int)info.TypeId);
            if (cdt != null)
            {
                var nm = am.ClassDatabase.GetString(cdt.Name);
                if (!string.IsNullOrEmpty(nm)) return nm;
            }
        }
        catch { }
        return $"typeid_{info.TypeId}";
    }

    static bool Matches(AssetTypeValueField bf, string className, string filter)
    {
        if (filter == null) return true;
        var name = AssetName(bf) ?? "";
        return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || (className ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
            || TypeId(bf).ToString() == filter;
    }

    // ── actions ────────────────────────────────────────────────────────────────
    // ── 静默边界（S1–S11）：类名写错 / 0 命中 / offset 越界 都要说出来 ────────────
    static List<string> _classPool;

    /// 当前已加载文件里真实存在的类名（只在“需要建近邻建议”时算一次）。
    static List<string> ClassPool(AssetsManager am, List<AssetsFileInstance> insts)
    {
        if (_classPool != null) return _classPool;
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (inst, info) in AllInfos(insts))
        {
            var n = ClassNameOf(am, inst, info);
            if (!string.IsNullOrEmpty(n)) set.Add(n);
        }
        _classPool = set.ToList();
        return _classPool;
    }

    /// 类名差一两个字符时给建议（实测最常见的错：大小写、少个 s、用了邻近的类）
    static List<string> Suggest(string want, IEnumerable<string> pool, int max = 3)
    {
        var w = (want ?? "").Trim();
        var res = new List<string>();
        if (w.Length == 0) return res;
        foreach (var cand in pool)
        {
            if (res.Count >= max) break;
            if (cand.Equals(w, StringComparison.Ordinal)) continue;
            if (cand.Equals(w, StringComparison.OrdinalIgnoreCase)
                || cand.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0
                || w.IndexOf(cand, StringComparison.OrdinalIgnoreCase) >= 0
                || Levenshtein(w.ToLowerInvariant(), cand.ToLowerInvariant()) <= 2)
                res.Add(cand);
        }
        return res;
    }

    static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[a.Length, b.Length];
    }

    /// 0 命中时的一句话（合法空结果：exit 0 + WARNING）
    static void WarnEmpty(Out outp, string what) => outp.Add($"# WARNING: {what} — this is a legal empty result; if you expected rows, check the name/path spelling");

    static int Classes(AssetsManager am, List<AssetsFileInstance> insts, Out outp)
    {
        var counts = new Dictionary<string, int>();
        int total = 0;
        foreach (var (inst, info) in AllInfos(insts))
        {
            var c = ClassNameOf(am, inst, info);
            counts[c] = counts.TryGetValue(c, out var v) ? v + 1 : 1;
            total++;
        }
        outp.Add($"{counts.Count} asset classes, {total} objects");
        foreach (var kv in counts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            outp.Add($"  {kv.Value,6}  {kv.Key}");
        return 0;
    }

    static int List(AssetsManager am, List<AssetsFileInstance> insts, string cls, int offset, Out outp)
    {
        if (cls == null) { Console.Error.WriteLine("data-probe: list needs --class <C>"); return 2; }
        const int cap = 500;
        var lines = new List<string>();
        int total = 0, named = 0, shown = 0;
        foreach (var (inst, info) in AllInfos(insts))
        {
            if (!string.Equals(ClassNameOf(am, inst, info), cls, StringComparison.Ordinal)) continue;
            total++;
            if (total <= offset) continue;
            if (shown >= cap) continue;
            AssetTypeValueField bf = null;
            try { bf = am.GetBaseField(inst, info); } catch { }
            var tid = bf != null ? TypeId(bf) : -1;
            var nm = bf != null ? AssetName(bf) : null;
            if (!string.IsNullOrEmpty(nm)) named++;
            lines.Add($"  {nm ?? "(no name)"}{(tid >= 0 ? $"  typeID={tid}" : "")}  pathID={info.PathId}");
            shown++;
        }
        outp.Add($"{cls}: {total} asset(s)");
        outp.AddRange(lines);
        int last = offset + shown;
        if (total == 0)
        {
            outp.Add($"# ERROR: no asset of class '{cls}' in the loaded file(s)");
            var sug = Suggest(cls, ClassPool(am, insts));
            outp.Add(sug.Count > 0
                ? $"#   did you mean: {string.Join(", ", sug)}?  (all class names: --action classes)"
                : "#   class names are case-sensitive; list them with --action classes");
            return 1;
        }
        if (offset >= total) outp.Add($"... (offset {offset} is past the {total} asset(s); use --offset 0)");
        else if (last < total)
            outp.Add($"... (showing {offset + 1}-{last} of {total}; narrow with --name/--pathid or search --pattern, or use --offset {last} for the next page)");
        if (lines.Count > 0 && named == 0)
            outp.Add($"... (the {cls} objects shown have no name; locate one with dump --class {cls} --pathid <pathID>)");
        return 0;
    }

    static int Search(AssetsManager am, List<AssetsFileInstance> insts, string pattern, string cls, int offset, Out outp)
    {
        if (pattern == null) { Console.Error.WriteLine("data-probe: search needs --pattern <p>"); return 2; }
        const int cap = 500;
        var lines = new List<string>();
        int total = 0, shown = 0;
        foreach (var (inst, info) in AllInfos(insts))
        {
            var className = ClassNameOf(am, inst, info);
            if (cls != null && !string.Equals(className, cls, StringComparison.Ordinal)) continue;
            AssetTypeValueField bf = null;
            try { bf = am.GetBaseField(inst, info); } catch { }
            var tid = bf != null ? TypeId(bf) : -1;
            var nm = bf != null ? AssetName(bf) : null;
            var hay = (nm ?? "") + " " + (className ?? "") + " " + tid;
            if (hay.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) < 0) continue;
            total++;
            if (total <= offset) continue;
            if (shown >= cap) continue;
            lines.Add($"  {className}  {nm ?? "(no name)"}{(tid >= 0 ? $"  typeID={tid}" : "")}  pathID={info.PathId}");
            shown++;
        }
        outp.Add($"search '{pattern}': {total} match(es)");
        outp.AddRange(lines);
        int last = offset + shown;
        if (total == 0) WarnEmpty(outp, $"search '{pattern}' matched nothing");
        else if (offset >= total) outp.Add($"... (offset {offset} is past the {total} match(es); use --offset 0)");
        else if (last < total)
            outp.Add($"... (showing {offset + 1}-{last} of {total}; refine the pattern, or use --offset {last} for the next page)");
        return 0;
    }

    static int DumpAsset(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, int maxDepth, Out outp)
    {
        var cls = Opt(o, "class");
        var name = Opt(o, "name");
        var typeId = Opt(o, "typeid");
        var pathId = Opt(o, "pathid");
        var follow = o.ContainsKey("follow");
        if (cls == null && pathId == null) { Console.Error.WriteLine("data-probe: dump needs --class (with --name/--typeid) or --pathid"); return 2; }

        foreach (var (inst, info) in AllInfos(insts))
        {
            if (pathId != null && info.PathId.ToString() != pathId) continue;
            var className = ClassNameOf(am, inst, info);
            if (cls != null && !string.Equals(className, cls, StringComparison.Ordinal)) continue;
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            if (typeId != null && TypeId(bf).ToString() != typeId) continue;
            if (name != null && !Matches(bf, className, name)) continue;
            var tid = TypeId(bf);
            outp.Add($"=== {className} {AssetName(bf)} ({(tid >= 0 ? $"typeID {tid}, " : "")}classID {info.TypeId}, pathID {info.PathId}) ===");
            DumpField(am, inst, bf, 0, maxDepth, follow ? Math.Max(1, maxDepth) : 0, outp);
            return 0;
        }
        outp.Add($"# ERROR: no asset matched (class={cls} name={name} typeid={typeId} pathid={pathId})");
        if (Opt(o, "file") == null)
            outp.Add("#   pathID is per-file: scene and prefab objects live in levelN — pass --file levelN (see docs/guides/00-overview.md)");
        if (cls != null && !ClassPool(am, insts).Contains(cls, StringComparer.Ordinal))
        {
            var sugCls = Suggest(cls, ClassPool(am, insts));
            if (sugCls.Count > 0) outp.Add($"#   did you mean class: {string.Join(", ", sugCls)}?");
        }
        return 1;
    }

    static void DumpField(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField f, int depth, int maxDepth, int followLeft, Out outp)
    {
        // ⭐ 到上限就**立刻停止展开** ✗ —— 只停"记录"没用 ✗：递归本身会把时间和栈吃光 ✓
        if (outp.Truncated) return;
        var pad = new string(' ', depth * 2);
        bool isPtr = f.Children.Count == 2
            && f.Children[0].FieldName == "m_FileID"
            && f.Children[1].FieldName == "m_PathID";
        if (isPtr)
        {
            long pid = 0;
            try { pid = f["m_PathID"].AsLong; } catch { }
            long fid = 0;
            try { fid = f["m_FileID"].AsLong; } catch { }
            // ⭐ 引用要带上**文件**信息 ✗ —— pathID 是**按文件**编的 ✓；只打 pathID 跨文件就定位不了 ✗
            outp.Add($"{pad}{f.FieldName} -> {(pid == 0 ? "(null)" : $"pathID {pid}  [{(fid == 0 ? "this file" : "external fileId=" + fid)}]")}");
            if (followLeft > 0 && pid != 0)
            {
                try
                {
                    var ext = am.GetExtAsset(inst, f);
                    if (ext.baseField != null)
                    {
                        var efile = ext.file ?? inst;
                        var ec = ext.info != null ? ClassNameOf(am, efile, ext.info) : ClassName(am, efile, ext.baseField);
                        outp.Add($"{pad}  [ref] {ec ?? "?"} {AssetName(ext.baseField)}" + (efile != inst ? $"  (in {efile.name})" : ""));
                        foreach (var c in ext.baseField.Children)
                            DumpField(am, efile, c, depth + 2, maxDepth + 3, followLeft - 1, outp);   // ⭐ 继续跟 ✓（跨文件也能走到底 ✓）
                    }
                }
                catch { }
            }
            return;
        }
        string val = "";
        try
        {
            if (f.Children.Count == 0)
            {
                var leaf = f.AsString;
                if (f.FieldName == "Array") leaf = ArrayStr(f, leaf);   // 把「空」和「不展开」说清楚 ✓
                val = " = " + leaf;
            }
        }
        catch { }
        outp.Add($"{pad}{f.FieldName}{val}");
        if (depth >= maxDepth) return;
        foreach (var c in f.Children) DumpField(am, inst, c, depth + 1, maxDepth, followLeft, outp);
    }

    // ── export（批量表：一类对象 × 过滤 × 字段路径）───────────────────────────
    /// <returns>0 = 正常；1 = 每个请求的 field 路径都没解析出任何值（表是空壳，不应当成数据用它）</returns>
    static int Export(AssetsManager am, List<AssetsFileInstance> insts, string cls, List<string> matches,
        List<string> fields, int rows, int offset, Out outp)
    {
        if (cls == null) { Console.Error.WriteLine("data-probe: export needs --class <C>"); return 2; }
        var body = new List<string>();
        var hits = new int[fields.Count];   // 每个 field 路径在多少行里真解出了值
        int total = 0, shown = 0, scanned = 0;
        foreach (var (inst, info) in AllInfos(insts))
        {
            if (!string.Equals(ClassNameOf(am, inst, info), cls, StringComparison.Ordinal)) continue;
            scanned++;
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            var ok = true;
            foreach (var m in matches) if (!MatchPath(am, inst, bf, m)) { ok = false; break; }
            if (!ok) continue;
            total++;
            if (total <= offset || shown >= rows) continue;
            var cells = new List<string> { AssetName(bf) ?? "", TypeId(bf).ToString(), info.PathId.ToString() };
            for (int i = 0; i < fields.Count; i++)
            {
                var v = string.Join(";", EvalPath(am, inst, bf, fields[i]));
                if (v.Trim().Length > 0) hits[i]++;
                cells.Add(v);
            }
            body.Add(string.Join("\t", cells));
            shown++;
        }
        var header = string.Join("\t", new List<string> { "name", "typeID", "pathID" }.Concat(fields));
        var next = offset < total
            ? $"... (showing {offset + 1}-{offset + shown} of {total}; narrow with --match, or use --offset {offset + shown} for the next page)"
            : $"... (offset {offset} is past the {total} row(s); use --offset 0)";
        // ⭐ 表头也**进数据**（落盘时它是文件第一行 ✓）—— 不然文件里是一堆没有列名的数字 ✗
        outp.Add("# " + header);
        outp.AddRange(body);
        outp.Note($"# {cls}: {total} row(s), {fields.Count} field(s), wrote {body.Count} line(s)");

        // ⭐ 类名写错 / --match 全滤掉：以前都只是「0 行」，读的人会以为数据里没有。现在分开说。
        if (scanned == 0)
        {
            outp.Note($"# ERROR: no asset of class '{cls}' in the loaded file(s)");
            var sug = Suggest(cls, ClassPool(am, insts));
            outp.Note(sug.Count > 0
                ? $"#   did you mean: {string.Join(", ", sug)}?  (all class names: --action classes)"
                : "#   class names are case-sensitive; list them with --action classes");
            return 1;
        }
        if (total == 0 && matches.Count > 0)
            outp.Note($"# WARNING: the --match filter matched no rows (scanned {scanned} '{cls}' object(s)) - check the match path/value; this is a legal empty result");
        else if (total == 0)
            outp.Note($"# WARNING: 0 row(s) — the class exists but nothing was loaded from these files");
        if (outp.ToFile && body.Count == 0)
            outp.Note("# WARNING: wrote only the header (0 rows) - the file has no data; check the class / --match");

        // ⭐ field 路径写错时，以前是**静默空列**（读表的人会以为「数据本来就是空的」）。现在说出来。
        //    只对「一行都没解出值」的路径报 —— 可选字段（例如 displayNameKey）很多行本来就空，不算错。
        // 只在「这一页盖住了全部行」时才判死字段 —— 否则 --rows/--offset 会把合法路径误判（实测 DisplayNameKey + --rows 1）。
        var dead = new List<string>();
        bool sawAll = offset == 0 && shown == total;
        for (int i = 0; i < fields.Count; i++)
            if (shown > 0 && hits[i] == 0) dead.Add(fields[i]);
        if (sawAll)
            foreach (var d in dead)
                outp.Note($"# WARNING: field '{d}' matched nothing in {shown} row(s) - check the path syntax ('a.b', 'a[]', 'a[0]', '#name', '#class'); see tools/data-probe/README.md");
        else if (dead.Count > 0)
            outp.Note($"# note: field coverage was checked on {shown} of {total} row(s) only; drop --offset/--rows to verify '{string.Join(", ", dead)}'");
        if (offset >= total || offset + shown < total) outp.Note(next);
        if (fields.Count > 0 && sawAll && dead.Count == fields.Count)
        {
            outp.Note("# ERROR: every requested field path matched nothing, so the table above has no data - fix the --field path(s) and rerun (do not report an empty column as 'no data')");
            return 1;
        }
        return 0;
    }

    /// --match <path><op><value>，op ∈ = != ~ > >= < <=（~ = 子串，忽略大小写）
    static bool MatchPath(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField bf, string expr)
    {
        foreach (var op in new[] { "!=", ">=", "<=", "~", "=", ">", "<" })
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0) continue;
            var path = expr.Substring(0, idx).Trim();
            var want = expr.Substring(idx + op.Length).Trim();
            var vals = EvalPath(am, inst, bf, path);
            if (op == "!=") return !vals.Any(v => Eq(v, want));
            return vals.Any(v => op switch
            {
                "~" => v.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0,
                ">=" => Cmp(v, want) >= 0,
                "<=" => Cmp(v, want) <= 0,
                ">" => Cmp(v, want) > 0,
                "<" => Cmp(v, want) < 0,
                _ => Eq(v, want),
            });
        }
        return false;
    }

    static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static int Cmp(string a, string b)
    {
        if (double.TryParse(a, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var x)
            && double.TryParse(b, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var y))
            return x.CompareTo(y);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    static List<string> EvalPath(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField root, string path)
    {
        var res = new List<string>();
        WalkPath(am, inst, root, path.Split('.'), 0, res, 0);
        return res;
    }

    /// 路径段：`a` 字段 · `a[]` 展开数组 · `a[i]` 取下标 · `#class`/`#name` 取“解析后对象”的类名/名字（PPtr 自动跟随）
    static void WalkPath(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField node, string[] segs, int i, List<string> outp, int depth)
    {
        if (node == null || depth > 16) return;
        if (i >= segs.Length) { var v = LeafString(node); if (v != null) outp.Add(v); return; }
        var seg = segs[i];
        if (seg == "#class" || seg == "#name")
        {
            if (!IsPtr(node)) return;
            try
            {
                var ext = am.GetExtAsset(inst, node);
                if (seg == "#name") { if (ext.baseField != null) outp.Add(AssetName(ext.baseField) ?? ""); return; }
                if (ext.info != null) outp.Add(ClassNameOf(am, ext.file ?? inst, ext.info));
                else if (ext.baseField != null) outp.Add(ClassName(am, ext.file ?? inst, ext.baseField) ?? "");
            }
            catch { }
            return;
        }
        SplitIndex(seg, out var key, out var idx);
        if (IsPtr(node))
        {
            try
            {
                var ext = am.GetExtAsset(inst, node);
                if (ext.baseField != null) WalkPath(am, ext.file ?? inst, ext.baseField, segs, i, outp, depth + 1);
            }
            catch { }
            return;
        }
        AssetTypeValueField child = null;
        try { child = key.Length == 0 ? node : node[key]; } catch { }
        if (child == null) return;
        var arr = ArrayNode(child);
        if (arr != null && arr.Children.Count > 0)
        {
            if (idx >= 0) { if (idx < arr.Children.Count) WalkPath(am, inst, arr.Children[idx], segs, i + 1, outp, depth + 1); return; }
            foreach (var el in arr.Children) WalkPath(am, inst, el, segs, i + 1, outp, depth + 1);
            return;
        }
        // arr != null 但 0 个子元素 = **原生数组**（如 byte[] 的 `data`）→ 当叶子处理（下面的 LeafString 会给出它的字节串）
        WalkPath(am, inst, child, segs, i + 1, outp, depth + 1);
    }

    static void SplitIndex(string seg, out string key, out int idx)
    {
        idx = -1; key = seg;
        if (key.EndsWith("[]")) { key = key.Substring(0, key.Length - 2); return; }
        if (!key.EndsWith("]")) return;
        var lb = key.LastIndexOf('[');
        if (lb < 0) return;
        if (int.TryParse(key.Substring(lb + 1, key.Length - lb - 2), out var v)) { idx = v; key = key.Substring(0, lb); }
    }

    static AssetTypeValueField ArrayNode(AssetTypeValueField f)
    {
        try { if (f.Children.Count == 1 && f.Children[0].FieldName == "Array") return f.Children[0]; } catch { }
        return null;
    }

    static bool IsPtr(AssetTypeValueField f)
        => f.Children.Count == 2 && f.Children[0].FieldName == "m_FileID" && f.Children[1].FieldName == "m_PathID";

    /// <summary>数组节点要印的字面值。
    /// 类型库没有元素类型时，数组**不展开**，库自带的 ToString 会给出
    /// `AssetsTools.NET.AssetTypeArrayInfo` —— 这个串**分不清「空数组」和「读不出」** ✗。
    /// 从 `AsArray.size` 能拿到真实个数 ✓（实测：空数组给 0 ✓，两项的给 2 ✓），
    /// 于是空数组印 `[]` ✓，不展开印 `<N item(s), not expanded>` ✓。
    /// 原生数组（如 byte[]）本身就有值 ✓ ⇒ 原样返回 ✓。</summary>
    const string OpaqueArray = "AssetsTools.NET.AssetTypeArrayInfo";

    static string ArrayStr(AssetTypeValueField arr, string fallback)
    {
        if (fallback != OpaqueArray) return fallback;
        try
        {
            var n = arr.AsArray.size;
            return n == 0 ? "[]" : $"<{n} item(s), not expanded>";
        }
        catch { return fallback; }
    }

    static string LeafString(AssetTypeValueField f)
    {
        try
        {
            if (f.Children.Count == 0) return f.AsString;
            if (IsPtr(f)) { var pid = f["m_PathID"].AsLong; return pid == 0 ? "(null)" : $"pathID {pid}"; }
            // 单个 `Array` 子节点且它是叶子：
            //   原生数组（byte[] / int[]）→ 它自带值（如 "03 00 00 00"）✓ 原样返回 ✓
            //   动态数组但元素类型缺失 → 库给占位串 ✗ ⇒ 换成 `[]` / `<N item(s), not expanded>` ✓
            if (f.Children.Count == 1 && f.Children[0].FieldName == "Array" && f.Children[0].Children.Count == 0)
            {
                var arr = f.Children[0];
                return ArrayStr(arr, arr.AsString);
            }
        }
        catch { }
        return null;
    }

    static int Refs(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, Out outp)
    {
        var by = Opt(o, "by");                 // ⭐ 反查模式：只扫这个类的对象 ✓
        var cls = Opt(o, "class");
        var name = Opt(o, "name");
        var typeId = Opt(o, "typeid");
        var pathId = Opt(o, "pathid");
        if (cls == null && pathId == null && name == null) { Console.Error.WriteLine("data-probe: refs needs --class / --name / --pathid"); return 2; }
        if (by != null && !ClassPool(am, insts).Contains(by, StringComparer.Ordinal))
        {
            outp.Add($"# ERROR: no asset class named '{by}' — --by must be a class that actually exists");
            var sug = Suggest(by, ClassPool(am, insts));
            outp.Add(sug.Count > 0
                ? $"#   did you mean: {string.Join(", ", sug)}?  (all class names: --action classes)"
                : "#   list them with --action classes");
            return 1;
        }
        foreach (var (inst, info) in AllInfos(insts))
        {
            if (pathId != null && info.PathId.ToString() != pathId) continue;
            var className = ClassNameOf(am, inst, info);
            if (cls != null && !string.Equals(className, cls, StringComparison.Ordinal)) continue;
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            if (typeId != null && TypeId(bf).ToString() != typeId) continue;
            if (name != null && !Matches(bf, className, name)) continue;
            if (by != null)
            {
                // ⭐ **反查**：哪些 `--by <类>` 的对象引用了它 ✓（现在只有"我引用了谁"✗ 没有反方向 ✗）
                //    用途：找"谁在用这个模型/挂点" ✓（如哪些 MeshFilter 引用了某 mesh ✓）
                outp.Add($"=== who references {className} {AssetName(bf)} (only class {by}) ===");
                long tgtPath = info.PathId;
                string tgtFile = inst.name;
                int n = 0;
                foreach (var (i2, nfo2) in AllInfos(insts))
                {
                    if (!string.Equals(ClassNameOf(am, i2, nfo2), by, StringComparison.Ordinal)) continue;
                    AssetTypeValueField bf2;
                    try { bf2 = am.GetBaseField(i2, nfo2); } catch { continue; }
                    if (bf2 == null) continue;
                    if (!ReferencesTarget(am, i2, bf2, tgtFile, tgtPath, out var path, 0)) continue;
                    outp.Add($"  {by} {AssetName(bf2)} (pathID {nfo2.PathId})  via {path}");
                    n++;
                }
                outp.Add($"  ({n} object(s) of class {by} reference it)");
                if (n == 0) WarnEmpty(outp, $"no object of class {by} references it");
                return 0;
            }
            outp.Add($"=== {className} {AssetName(bf)} references ===");
            CollectPtrs(am, inst, bf, "", outp, 0);
            return 0;
        }
        outp.Add($"# ERROR: no asset matched (class={cls} name={name} typeid={typeId} pathid={pathId})");
        if (Opt(o, "file") == null)
            outp.Add("#   pathID is per-file: scene and prefab objects live in levelN — pass --file levelN");
        return 1;
    }

    /// <summary>反查用：这个对象的字段里**有没有引用**（文件,name + pathID）指向的目标 ✓</summary>
    static bool ReferencesTarget(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField f,
        string tgtFile, long tgtPath, out string hitPath, int depth)
    {
        hitPath = null;
        if (depth > 6) return false;
        if (f.Children.Count == 2 && f.Children[0].FieldName == "m_FileID" && f.Children[1].FieldName == "m_PathID")
        {
            try
            {
                long pid = f["m_PathID"].AsLong, fid = f["m_FileID"].AsLong;
                if (pid != 0 && pid == tgtPath && fid == 0 &&
                    string.Equals(inst.name, tgtFile, StringComparison.Ordinal))
                {
                    hitPath = "(direct)";
                    return true;
                }
                // 跨文件：把引用解出来再比一次 ✓（同 pathID 撞号很常见 ✗ 不能只看数字 ✓）
                if (pid != 0)
                {
                    var ext = am.GetExtAsset(inst, f);
                    if (ext.baseField != null)
                    {
                        var efile = ext.file ?? inst;
                        long ep = ext.info != null ? ext.info.PathId : -1;
                        if (ep == tgtPath && string.Equals(efile.name, tgtFile, StringComparison.Ordinal))
                        {
                            hitPath = "(resolved)";
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
        foreach (var c in f.Children)
            if (ReferencesTarget(am, inst, c, tgtFile, tgtPath, out hitPath, depth + 1))
            {
                hitPath = (hitPath == "(direct)" || hitPath == "(resolved)") ? $"{c.FieldName} {hitPath}" : hitPath;
                return true;
            }
        return false;
    }

    static void CollectPtrs(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField f, string path, Out outp, int depth)
    {
        if (outp.Truncated) return;              // ⭐ 同上：到上限立刻停 ✓
        if (depth > 6) return;
        if (f.Children.Count == 2 && f.Children[0].FieldName == "m_FileID" && f.Children[1].FieldName == "m_PathID")
        {
            long pid = 0;
            try { pid = f["m_PathID"].AsLong; } catch { }
            long fid = 0;
            try { fid = f["m_FileID"].AsLong; } catch { }
            if (pid != 0)
            {
                // ⭐ 带上 fileId ✓ 并把**引用目标**也解出来 ✓（跨文件时标明在哪个文件 ✓）
                string target = "";
                try
                {
                    var ext = am.GetExtAsset(inst, f);
                    if (ext.baseField != null)
                    {
                        var efile = ext.file ?? inst;
                        var ec = ext.info != null ? ClassNameOf(am, efile, ext.info) : ClassName(am, efile, ext.baseField);
                        target = $"  -> {ec ?? "?"} {AssetName(ext.baseField)}" + (efile != inst ? $"  (in {efile.name})" : "");
                    }
                }
                catch { }
                outp.Add($"  {path}: pathID {pid}  [{(fid == 0 ? "this file" : "external fileId=" + fid)}]{target}");
            }
            return;
        }
        foreach (var c in f.Children) CollectPtrs(am, inst, c, path.Length == 0 ? c.FieldName : path + "." + c.FieldName, outp, depth + 1);
    }
}
