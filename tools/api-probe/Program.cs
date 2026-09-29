using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

static class ApiProbe
{
    static int Main(string[] args)
    {
        var opt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i].Substring(2);
            opt[key] = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
        }
        var managed = Get(opt, "managed");
        var action = Get(opt, "action");
        var dllArg = Get(opt, "dll") ?? "TeamSoda.Duckov.Core";
        var target = Get(opt, "target");
        var member = Get(opt, "member");
        int limit = int.TryParse(Get(opt, "limit"), out var ln) ? ln : 2000;
        if (managed == null || action == null)
        {
            Console.Error.WriteLine("usage: api-probe --managed <dir> --action <search|members|decompile|il|strings> [--dll <name[,name]|*>] [--target <t>] [--member <m>] [--limit N]");
            return 2;
        }
        var dlls = ResolveDlls(managed, dllArg);
        if (dlls.Count == 0) { Console.Error.WriteLine($"api-probe: no DLL matched '{dllArg}' under {managed}"); return 1; }
        var sb = new List<string>();
        foreach (var dll in dlls)
        {
            try { RunOne(dll, managed, action, target, member, sb); }
            catch (Exception ex) { sb.Add($"// [{Path.GetFileName(dll)}] ERROR: {ex.Message}"); }
        }
        var text = string.Join("\n", sb);
        var lines = text.Split('\n');
        if (lines.Length > limit) text = string.Join("\n", lines.Take(limit)) + $"\n... ({lines.Length - limit} more lines truncated)";
        Console.Write(text);
        if (!text.EndsWith("\n")) Console.WriteLine();
        return 0;
    }

    static string Get(Dictionary<string, string> o, string k) => o.TryGetValue(k, out var v) ? v : null;

    static List<string> ResolveDlls(string managed, string dllArg)
    {
        if (dllArg == "*") return Directory.GetFiles(managed, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var list = new List<string>();
        foreach (var raw in dllArg.Split(','))
        {
            var n = raw.Trim();
            if (n.Length == 0) continue;
            var p = Path.Combine(managed, n.EndsWith(".dll") ? n : n + ".dll");
            if (File.Exists(p)) list.Add(p);
        }
        return list;
    }

    static void RunOne(string dll, string managed, string action, string target, string member, List<string> sb)
    {
        var pe = new PEFile(dll);
        var resolver = new UniversalAssemblyResolver(dll, false, pe.Metadata.DetectTargetFrameworkId());
        resolver.AddSearchDirectory(managed);
        var dc = new CSharpDecompiler(pe, resolver, new DecompilerSettings());
        switch (action)
        {
            case "search": Search(dll, dc, target, sb); break;
            case "members": Members(dll, dc, target, sb); break;
            case "decompile": Decompile(dll, dc, target, member, sb); break;
            case "il": Il(dll, pe, dc, target, member, sb); break;
            case "strings": Strings(dll, target, sb); break;
            default: sb.Add($"// unknown action '{action}'"); break;
        }
    }

    static ITypeDefinition FindType(CSharpDecompiler dc, string name)
    {
        if (name == null) return null;
        var types = dc.TypeSystem.MainModule.TypeDefinitions.ToList();
        return types.FirstOrDefault(t => t.FullName == name)
            ?? types.FirstOrDefault(t => t.Name == name)
            ?? types.FirstOrDefault(t => t.FullName.EndsWith("." + name));
    }

    static string KindName(SymbolKind k) => k switch
    {
        SymbolKind.Field => "field",
        SymbolKind.Method => "method",
        SymbolKind.Property => "property",
        SymbolKind.Event => "event",
        SymbolKind.Constructor => "ctor",
        _ => k.ToString().ToLowerInvariant()
    };

    static string Sig(IMember m)
    {
        if (m is IMethod meth)
        {
            var ps = string.Join(", ", meth.Parameters.Select(p => p.Type.FullName + " " + p.Name));
            return $"{KindName(m.SymbolKind)} {meth.ReturnType.FullName} {meth.Name}({ps})";
        }
        return $"{KindName(m.SymbolKind)} {m.ReturnType.FullName} {m.Name}";
    }

    static void Search(string dll, CSharpDecompiler dc, string target, List<string> sb)
    {
        sb.Add($"## {Path.GetFileName(dll)} - search '{target}'");
        var t = target ?? "";
        int hits = 0;
        foreach (var type in dc.TypeSystem.MainModule.TypeDefinitions)
        {
            if (type.FullName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) { sb.Add($"TYPE  {type.FullName}"); hits++; }
            foreach (var m in type.Members)
                if (m.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sb.Add($"  {KindName(m.SymbolKind)} {type.FullName}.{m.Name}"); hits++;
                    if (hits > 500) { sb.Add("... (more matches truncated)"); return; }
                }
        }
        if (hits == 0) sb.Add("  (no match)");
    }

    static void Members(string dll, CSharpDecompiler dc, string target, List<string> sb)
    {
        var type = FindType(dc, target);
        sb.Add($"## {Path.GetFileName(dll)} - members of {type?.FullName ?? target}");
        if (type == null) { sb.Add("  (type not found)"); return; }
        var bases = type.DirectBaseTypes.Where(b => b.Kind != TypeKind.Unknown).Select(b => b.FullName);
        sb.Add($"  base: {string.Join(", ", bases)}");
        foreach (var m in type.Members) sb.Add($"  {Sig(m)}");
    }

    static void Decompile(string dll, CSharpDecompiler dc, string target, string member, List<string> sb)
    {
        var type = FindType(dc, target);
        sb.Add($"## {Path.GetFileName(dll)} - decompile {type?.FullName ?? target}{(member != null ? "." + member : "")}");
        if (type == null) { sb.Add("  (type not found)"); return; }
        if (member != null)
        {
            var ms = type.Members.Where(m => m.Name == member).ToList();
            if (ms.Count == 0) { sb.Add("  (member not found)"); return; }
            foreach (var m in ms) sb.Add(dc.DecompileAsString(m.MetadataToken));
        }
        else sb.Add(dc.DecompileTypeAsString(type.FullTypeName));
    }

    static void Il(string dll, PEFile pe, CSharpDecompiler dc, string target, string member, List<string> sb)
    {
        var type = FindType(dc, target);
        sb.Add($"## {Path.GetFileName(dll)} - IL {type?.FullName ?? target}{(member != null ? "." + member : "")}");
        if (type == null) { sb.Add("  (type not found)"); return; }
        var sw = new StringWriter();
        var dis = new ReflectionDisassembler(new PlainTextOutput(sw), CancellationToken.None);
        if (member != null)
        {
            var ms = type.Members.OfType<IMethod>().Where(m => m.Name == member).ToList();
            if (ms.Count == 0) { sb.Add("  (method not found)"); return; }
            foreach (var m in ms) dis.DisassembleMethod(pe, (MethodDefinitionHandle)m.MetadataToken);
        }
        else dis.DisassembleType(pe, (TypeDefinitionHandle)type.MetadataToken);
        sb.Add(sw.ToString());
    }

    static void Strings(string dll, string target, List<string> sb)
    {
        sb.Add($"## {Path.GetFileName(dll)} - strings '{target}'");
        var bytes = File.ReadAllBytes(dll);
        var cur = new StringBuilder();
        int hits = 0;
        var flush = new Action(() =>
        {
            if (cur.Length >= 4)
            {
                var s = cur.ToString();
                if (target == null || s.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) { sb.Add(s); hits++; }
            }
            cur.Clear();
        });
        foreach (var b in bytes)
        {
            if (b >= 32 && b < 127) cur.Append((char)b);
            else flush();
        }
        flush();
        if (hits == 0) sb.Add("  (no match)");
    }
}
