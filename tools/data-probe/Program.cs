using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

// data-probe — read a Unity3D game's content data offline.
//
// What it reads: the serialized Unity objects in the game's data files — prefabs,
// ScriptableObjects and MonoBehaviours — as typed fields + values (e.g. an Item's
// `value`, a Quest's `requiredItemCount`, a CharacterRandomPreset's `isBoss`).
// It does NOT read art (textures/meshes/audio) and it does NOT modify anything.
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
        var o = ParseArgs(args);
        var managed = Opt(o, "managed");
        var data = Opt(o, "data");
        var action = Opt(o, "action");
        if (managed == null || data == null || action == null) { Usage(); return 2; }
        int limit = int.TryParse(Opt(o, "limit"), out var ln) ? ln : 2000;
        int depth = int.TryParse(Opt(o, "depth"), out var dp) ? dp : 3;

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
            try { var i = am.LoadAssetsFile(f, true); am.LoadClassDatabaseFromPackage(i.file.Metadata.UnityVersion); insts.Add(i); }
            catch (Exception e) { Console.Error.WriteLine($"data-probe: skip {Path.GetFileName(f)}: {e.Message}"); }
        }

        var outLines = new List<string>();
        switch (action)
        {
            case "classes": Classes(am, insts, outLines); break;
            case "list": List(am, insts, Opt(o, "class"), outLines); break;
            case "search": Search(am, insts, Opt(o, "pattern"), Opt(o, "class"), outLines); break;
            case "dump": DumpAsset(am, insts, o, depth, outLines); break;
            case "refs": Refs(am, insts, o, outLines); break;
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

    static void Usage() => Console.Error.WriteLine(
        "usage: data-probe --managed <Managed dir> --data <Data dir> --action <classes|search|list|dump|refs>\n" +
        "  classes                         list asset class names + counts\n" +
        "  list    --class <C>             list assets of a class\n" +
        "  search  --pattern <p> [--class C]  find assets by name\n" +
        "  dump    --class <C> (--name <n>|--typeid <t>|--pathid <p>) [--depth d] [--follow]\n" +
        "  refs    --class <C> (--name <n>|--typeid <t>|--pathid <p>)   what it references\n" +
        "  common: [--file <x.assets>] [--limit N]");

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
        return Directory.GetFiles(data, "*.assets").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
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

    /// Enumerate (inst, info, baseField, className) for every MonoBehaviour; className may be null.
    static IEnumerable<(AssetsFileInstance inst, AssetFileInfo info, AssetTypeValueField bf, string cls)>
        AllMonoBehaviours(AssetsManager am, List<AssetsFileInstance> insts)
    {
        foreach (var inst in insts)
        {
            foreach (var info in inst.file.GetAssetsOfType(0x72))
            {
                AssetTypeValueField bf;
                try { bf = am.GetBaseField(inst, info); } catch { continue; }
                if (bf == null) continue;
                yield return (inst, info, bf, ClassName(am, inst, bf));
            }
        }
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
        foreach (var x in AllMonoBehaviours(am, insts))
        {
            var c = x.cls ?? "(unknown)";
            counts[c] = counts.TryGetValue(c, out var v) ? v + 1 : 1;
        }
        outp.Add($"{counts.Count} asset classes (MonoBehaviour/ScriptableObject), {counts.Values.Sum()} objects");
        foreach (var kv in counts.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            outp.Add($"  {kv.Value,6}  {kv.Key}");
    }

    static void List(AssetsManager am, List<AssetsFileInstance> insts, string cls, List<string> outp)
    {
        if (cls == null) { outp.Add("# list: --class required"); return; }
        int n = 0;
        foreach (var x in AllMonoBehaviours(am, insts))
        {
            if (!string.Equals(x.cls, cls, StringComparison.Ordinal)) continue;
            var tid = TypeId(x.bf);
            outp.Add($"  {AssetName(x.bf) ?? "(no name)"}{(tid >= 0 ? $"  typeID={tid}" : "")}  pathID={x.info.PathId}");
            n++;
        }
        outp.Insert(0, $"{cls}: {n} asset(s)");
    }

    static void Search(AssetsManager am, List<AssetsFileInstance> insts, string pattern, string cls, List<string> outp)
    {
        if (pattern == null) { outp.Add("# search: --pattern required"); return; }
        int n = 0;
        foreach (var x in AllMonoBehaviours(am, insts))
        {
            if (cls != null && !string.Equals(x.cls, cls, StringComparison.Ordinal)) continue;
            var tid = TypeId(x.bf);
            var hay = (AssetName(x.bf) ?? "") + " " + (x.cls ?? "") + " " + tid;
            if (hay.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) < 0) continue;
            outp.Add($"  {x.cls}  {AssetName(x.bf) ?? "(no name)"}{(tid >= 0 ? $"  typeID={tid}" : "")}  pathID={x.info.PathId}");
            n++;
            if (n >= 500) { outp.Add("... (more matches truncated)"); break; }
        }
        outp.Insert(0, $"search '{pattern}': {n} match(es)");
    }

    static void DumpAsset(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, int maxDepth, List<string> outp)
    {
        var cls = Opt(o, "class");
        var name = Opt(o, "name");
        var typeId = Opt(o, "typeid");
        var pathId = Opt(o, "pathid");
        var follow = o.ContainsKey("follow");
        if (cls == null && pathId == null) { outp.Add("# dump: need --class (with --name/--typeid) or --pathid"); return; }

        foreach (var inst in insts)
        {
            foreach (var info in inst.file.GetAssetsOfType(0x72))
            {
                if (pathId != null && info.PathId.ToString() != pathId) continue;
                AssetTypeValueField bf;
                try { bf = am.GetBaseField(inst, info); } catch { continue; }
                if (bf == null) continue;
                var className = ClassName(am, inst, bf);
                if (cls != null && !string.Equals(className, cls, StringComparison.Ordinal)) continue;
                if (typeId != null && TypeId(bf).ToString() != typeId) continue;
                if (name != null && !Matches(bf, className, name)) continue;
                outp.Add($"=== {className} {AssetName(bf)} (typeID {TypeId(bf)}, pathID {info.PathId}) ===");
                DumpField(am, inst, bf, 0, maxDepth, follow, outp);
                return;
            }
        }
        outp.Add($"# no asset matched (class={cls} name={name} typeid={typeId} pathid={pathId})");
    }

    static void DumpField(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField f, int depth, int maxDepth, bool follow, List<string> outp)
    {
        var pad = new string(' ', depth * 2);
        bool isPtr = f.Children.Count == 2
            && f.Children[0].FieldName == "m_FileID"
            && f.Children[1].FieldName == "m_PathID";
        if (isPtr)
        {
            long pid = 0;
            try { pid = f["m_PathID"].AsLong; } catch { }
            outp.Add($"{pad}{f.FieldName} -> {(pid == 0 ? "(null)" : "pathID " + pid)}");
            if (follow && pid != 0)
            {
                try
                {
                    var ext = am.GetExtAsset(inst, f);
                    if (ext.baseField != null)
                    {
                        var efile = ext.file ?? inst;
                        var ec = ClassName(am, efile, ext.baseField);
                        outp.Add($"{pad}  [ref] {ec ?? "?"} {AssetName(ext.baseField)}");
                        foreach (var c in ext.baseField.Children)
                            DumpField(am, efile, c, depth + 2, maxDepth + 3, false, outp);
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
        foreach (var c in f.Children) DumpField(am, inst, c, depth + 1, maxDepth, follow, outp);
    }

    static void Refs(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, List<string> outp)
    {
        var cls = Opt(o, "class");
        var name = Opt(o, "name");
        var typeId = Opt(o, "typeid");
        foreach (var inst in insts)
        {
            foreach (var info in inst.file.GetAssetsOfType(0x72))
            {
                AssetTypeValueField bf;
                try { bf = am.GetBaseField(inst, info); } catch { continue; }
                if (bf == null) continue;
                var className = ClassName(am, inst, bf);
                if (cls != null && !string.Equals(className, cls, StringComparison.Ordinal)) continue;
                if (typeId != null && TypeId(bf).ToString() != typeId) continue;
                if (name != null && !Matches(bf, className, name)) continue;
                outp.Add($"=== {className} {AssetName(bf)} references ===");
                CollectPtrs(bf, "", outp, 0);
                return;
            }
        }
        outp.Add("# no asset matched");
    }

    static void CollectPtrs(AssetTypeValueField f, string path, List<string> outp, int depth)
    {
        if (depth > 6) return;
        if (f.Children.Count == 2 && f.Children[0].FieldName == "m_FileID" && f.Children[1].FieldName == "m_PathID")
        {
            long pid = 0;
            try { pid = f["m_PathID"].AsLong; } catch { }
            if (pid != 0) outp.Add($"  {path}: pathID {pid}");
            return;
        }
        foreach (var c in f.Children) CollectPtrs(c, path.Length == 0 ? c.FieldName : path + "." + c.FieldName, outp, depth + 1);
    }
}
