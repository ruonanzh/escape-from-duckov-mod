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
// Actions: classes | search | list | dump | refs
static class DataProbe
{
    static int Main(string[] args)
    {
        // Windows：.NET 默认按控制台代码页（GBK/CP437）写 stdout，而调用方按 UTF-8 解码 → 资产里的非 ASCII 字段会乱码。
        // 统一成 UTF-8（无 console 时 setter 可能抛，忽略）。
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        var o = ParseArgs(args);
        var managed = Opt(o, "managed");
        var data = Opt(o, "data");
        var action = Opt(o, "action");
        if (managed == null || data == null || action == null) { Usage(); return 2; }
        int limit = int.TryParse(Opt(o, "limit"), out var ln) ? ln : 2000;
        int depth = int.TryParse(Opt(o, "depth"), out var dp) ? dp : 3;
        int offset = int.TryParse(Opt(o, "offset"), out var ofs) ? Math.Max(0, ofs) : 0;
        int rows = int.TryParse(Opt(o, "rows"), out var rw) ? Math.Max(1, rw) : 500;

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

        var outLines = new List<string>();
        switch (action)
        {
            case "classes": Classes(am, insts, outLines); break;
            case "list": List(am, insts, Opt(o, "class"), offset, outLines); break;
            case "search": Search(am, insts, Opt(o, "pattern"), Opt(o, "class"), offset, outLines); break;
            case "dump": DumpAsset(am, insts, o, depth, outLines); break;
            case "refs": Refs(am, insts, o, outLines); break;
            case "export":
            {
                var outPath = Opt(o, "out");
                // 写文件时默认不再限制行数（不进模型上下文，不存在刷屏问题）
                var rowCap = Opt(o, "rows") != null || outPath == null ? rows : 1000000;
                Export(am, insts, Opt(o, "class"), AllOpts(args, "match"), AllOpts(args, "field"), rowCap, offset, outPath, outLines);
                break;
            }
            default: Console.Error.WriteLine($"data-probe: unknown action '{action}'"); return 2;
        }

        var text = string.Join("\n", outLines);
        var lines = text.Split('\n');
        if (lines.Length > limit) text = string.Join("\n", lines.Take(limit)) + $"\n... ({lines.Length - limit} more lines truncated)";
        Console.Write(text);
        if (!text.EndsWith("\n")) Console.WriteLine();
        return 0;
    }

    // ── arg parsing ────────────────────────────────────────────────────────────
    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var k = args[i].Substring(2);
            o[k] = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
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
        "usage: data-probe --managed <Managed dir> --data <Data dir> --action <classes|search|list|dump|refs>\n" +
        "  classes                         list asset class names + counts\n" +
        "  list    --class <C>             list assets of a class (first 500, with a hint to narrow)\n" +
        "  search  --pattern <p> [--class C]  find assets by name\n" +
        "  dump    --class <C> (--name <n>|--typeid <t>|--pathid <p>) [--depth d] [--follow]\n" +
        "  refs    --class <C> (--name <n>|--typeid <t>|--pathid <p>)   what it references\n" +
        "  export  --class <C> [--match <path><op><value>]... [--field <path>]... [--rows N] [--offset N] [--out <file>]\n" +
        "          one table row per matched asset; --out writes the full table to a file (stdout then gets a preview only)\n" +
        "          'a.b' field, 'a[]'/'a[i]' expand/index an array, '#class'/'#name' = resolved object's class/name\n" +
        "          match ops: = != ~ (substring) > >= < <=   -   columns are TAB-separated, arrays joined with ';'\n" +
        "  common: [--file <x.assets|levelN>] [--limit N] [--offset N]\n" +
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
        if (file != null) return new List<string> { Path.IsPathRooted(file) ? file : Path.Combine(data, file) };
        var assets = Directory.GetFiles(data, "*.assets").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
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
    static void Classes(AssetsManager am, List<AssetsFileInstance> insts, List<string> outp)
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
    }

    static void List(AssetsManager am, List<AssetsFileInstance> insts, string cls, int offset, List<string> outp)
    {
        if (cls == null) { outp.Add("# list: --class required"); return; }
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
        if (last < total)
            outp.Add($"... (showing {offset + 1}-{last} of {total}; narrow with --name/--pathid or search --pattern, or use --offset {last} for the next page)");
        if (lines.Count > 0 && named == 0)
            outp.Add($"... (the {cls} objects shown have no name; locate one with dump --class {cls} --pathid <pathID>)");
    }

    static void Search(AssetsManager am, List<AssetsFileInstance> insts, string pattern, string cls, int offset, List<string> outp)
    {
        if (pattern == null) { outp.Add("# search: --pattern required"); return; }
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
        if (last < total)
            outp.Add($"... (showing {offset + 1}-{last} of {total}; refine the pattern, or use --offset {last} for the next page)");
    }

    static void DumpAsset(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, int maxDepth, List<string> outp)
    {
        var cls = Opt(o, "class");
        var name = Opt(o, "name");
        var typeId = Opt(o, "typeid");
        var pathId = Opt(o, "pathid");
        var follow = o.ContainsKey("follow");
        if (cls == null && pathId == null) { outp.Add("# dump: need --class (with --name/--typeid) or --pathid"); return; }

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
            return;
        }
        outp.Add($"# no asset matched (class={cls} name={name} typeid={typeId} pathid={pathId})");
    }

    static void DumpField(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField f, int depth, int maxDepth, int followLeft, List<string> outp)
    {
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
            outp.Add($"{pad}{f.FieldName} -> {(pid == 0 ? "(null)" : $"pathID {pid}  [{(fid == 0 ? "本文件" : "外部文件 fileId=" + fid)}]")}");
            if (followLeft > 0 && pid != 0)
            {
                try
                {
                    var ext = am.GetExtAsset(inst, f);
                    if (ext.baseField != null)
                    {
                        var efile = ext.file ?? inst;
                        var ec = ext.info != null ? ClassNameOf(am, efile, ext.info) : ClassName(am, efile, ext.baseField);
                        outp.Add($"{pad}  [ref] {ec ?? "?"} {AssetName(ext.baseField)}" + (efile != inst ? $"  （在 {efile.name}）" : ""));
                        foreach (var c in ext.baseField.Children)
                            DumpField(am, efile, c, depth + 2, maxDepth + 3, followLeft - 1, outp);   // ⭐ 继续跟 ✓（跨文件也能走到底 ✓）
                    }
                }
                catch { }
            }
            return;
        }
        string val = "";
        try { if (f.Children.Count == 0) val = " = " + f.AsString; } catch { }
        outp.Add($"{pad}{f.FieldName}{val}");
        if (depth >= maxDepth) return;
        foreach (var c in f.Children) DumpField(am, inst, c, depth + 1, maxDepth, followLeft, outp);
    }

    // ── export（批量表：一类对象 × 过滤 × 字段路径）───────────────────────────
    static void Export(AssetsManager am, List<AssetsFileInstance> insts, string cls, List<string> matches,
        List<string> fields, int rows, int offset, string outPath, List<string> outp)
    {
        if (cls == null) { outp.Add("# export: --class required"); return; }
        var body = new List<string>();
        int total = 0, shown = 0;
        foreach (var (inst, info) in AllInfos(insts))
        {
            if (!string.Equals(ClassNameOf(am, inst, info), cls, StringComparison.Ordinal)) continue;
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            var ok = true;
            foreach (var m in matches) if (!MatchPath(am, inst, bf, m)) { ok = false; break; }
            if (!ok) continue;
            total++;
            if (total <= offset || shown >= rows) continue;
            var cells = new List<string> { AssetName(bf) ?? "", TypeId(bf).ToString(), info.PathId.ToString() };
            foreach (var f in fields) cells.Add(string.Join(";", EvalPath(am, inst, bf, f)));
            body.Add(string.Join("\t", cells));
            shown++;
        }
        var header = string.Join("\t", new List<string> { "name", "typeID", "pathID" }.Concat(fields));
        var next = $"... (showing {offset + 1}-{offset + shown} of {total}; narrow with --match, or use --offset {offset + shown} for the next page)";
        if (outPath != null)
        {
            try { File.WriteAllText(outPath, string.Join("\n", body) + "\n"); }
            catch (Exception e) { outp.Add($"# export: could not write {outPath}: {e.Message}"); return; }
            outp.Add($"# {cls}: {total} row(s), {fields.Count} field(s) -> wrote {shown} to {outPath}");
            outp.Add("# " + header);
            outp.AddRange(body.Take(3));
            if (shown > 3) outp.Add($"... (+{shown - 3} more rows in the file)");
            if (offset + shown < total) outp.Add(next);
            return;
        }
        outp.Add($"# {cls}: {total} row(s), {fields.Count} field(s)");
        outp.Add("# " + header);
        outp.AddRange(body);
        if (offset + shown < total) outp.Add(next);
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

    static string LeafString(AssetTypeValueField f)
    {
        try
        {
            if (f.Children.Count == 0) return f.AsString;
            if (IsPtr(f)) { var pid = f["m_PathID"].AsLong; return pid == 0 ? "(null)" : $"pathID {pid}"; }
            // 原生数组（byte[] / int[]）：单个 `Array` 子节点且它是叶子 → 直接给它的字节/数字串（如 "03 00 00 00"）
            if (f.Children.Count == 1 && f.Children[0].FieldName == "Array" && f.Children[0].Children.Count == 0)
                return f.Children[0].AsString;
        }
        catch { }
        return null;
    }

    static void Refs(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, List<string> outp)
    {
        var cls = Opt(o, "class");
        var name = Opt(o, "name");
        var typeId = Opt(o, "typeid");
        var pathId = Opt(o, "pathid");
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
            outp.Add($"=== {className} {AssetName(bf)} references ===");
            CollectPtrs(am, inst, bf, "", outp, 0);
            return;
        }
        outp.Add("# no asset matched");
    }

    static void CollectPtrs(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField f, string path, List<string> outp, int depth)
    {
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
                        target = $"  → {ec ?? "?"} {AssetName(ext.baseField)}" + (efile != inst ? $"  （在 {efile.name}）" : "");
                    }
                }
                catch { }
                outp.Add($"  {path}: pathID {pid}  [{(fid == 0 ? "本文件" : "外部 fileId=" + fid)}]{target}");
            }
            return;
        }
        foreach (var c in f.Children) CollectPtrs(am, inst, c, path.Length == 0 ? c.FieldName : path + "." + c.FieldName, outp, depth + 1);
    }
}
