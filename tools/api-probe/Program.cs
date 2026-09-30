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
    // `--dll *` = 游戏自有 DLL（与维护者 inspect_game 的口径一致）；避免把几百个引擎/第三方库全扫一遍。
    static readonly string[] GameOwnedDlls =
    {
        "TeamSoda.Duckov.Core",
        "TeamSoda.Duckov.Utilities",
        "ItemStatsSystem",
        "Assembly-CSharp",
        "SodaLocalization",
        "TeamSoda.MiniLocalizor",
    };

    // 白名单全落空时的退路：排除引擎/.NET 框架（它们占 Managed 的大多数），而不是把几百个 DLL 全扫一遍。
    static readonly string[] FrameworkDllPrefixes =
    {
        "UnityEngine", "Unity.", "UnityEditor", "System", "mscorlib", "netstandard",
        "Microsoft.", "Mono.", "nunit", "JetBrains", "Bee.", "PlayerConnection",
        "Newtonsoft", "com.unity",
    };
    static bool IsFrameworkDll(string file) =>
        FrameworkDllPrefixes.Any(p => Path.GetFileName(file).StartsWith(p, StringComparison.OrdinalIgnoreCase));

    static int Main(string[] args)
    {
        // Windows：.NET 默认按控制台代码页（GBK/CP437）写 stdout，而调用方按 UTF-8 解码 → 非 ASCII 会乱码。
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
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
        int offset = int.TryParse(Get(opt, "offset"), out var ofs) ? Math.Max(0, ofs) : 0;
        if (managed == null || action == null)
        {
            Console.Error.WriteLine("usage: api-probe --managed <dir> --action <search|members|decompile|il|strings> [--dll <name[,name]|*>] [--target <t>] [--member <m>] [--limit N] [--offset N]");
            return 2;
        }
        var dlls = ResolveDlls(managed, dllArg);
        if (dlls.Count == 0) { Console.Error.WriteLine($"api-probe: no DLL matched '{dllArg}' under {managed}"); return 1; }

        var outLines = new List<string>();
        foreach (var dll in dlls)
        {
            try
            {
                var lines = RunOne(dll, managed, action, target, member, offset);
                outLines.AddRange(lines);
            }
            catch (Exception ex) { outLines.Add($"## {Path.GetFileName(dll)}\n// ERROR: {ex.Message}"); }
        }
        if (outLines.Count == 0)
            outLines.Add($"# {action}: no result for target='{target}' member='{member}' in {dlls.Count} DLL(s)");

        var text = string.Join("\n", outLines);
        var lines2 = text.Split('\n');
        if (lines2.Length > limit) text = string.Join("\n", lines2.Take(limit)) + $"\n... ({lines2.Length - limit} more lines truncated)";
        Console.Write(text);
        if (!text.EndsWith("\n")) Console.WriteLine();
        return 0;
    }

    static string Get(Dictionary<string, string> o, string k) => o.TryGetValue(k, out var v) ? v : null;

    static List<string> ResolveDlls(string managed, string dllArg)
    {
        if (dllArg == "*")
        {
            var owned = GameOwnedDlls
                .Select(n => Path.Combine(managed, n + ".dll"))
                .Where(File.Exists)
                .ToList();
            if (owned.Count > 0) return owned;
            return Directory.GetFiles(managed, "*.dll")
                .Where(p => !IsFrameworkDll(p))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
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

    static List<string> RunOne(string dll, string managed, string action, string target, string member, int offset)
    {
        var pe = new PEFile(dll);
        var resolver = new UniversalAssemblyResolver(dll, false, pe.Metadata.DetectTargetFrameworkId());
        resolver.AddSearchDirectory(managed);
        var dc = new CSharpDecompiler(pe, resolver, new DecompilerSettings());
        return action switch
        {
            "search" => Search(dll, dc, target, offset),
            "members" => Members(dll, dc, target),
            "decompile" => Decompile(dll, dc, target, member),
            "il" => Il(dll, pe, dc, target, member),
            "strings" => Strings(dll, target, offset),
            _ => new List<string> { $"// unknown action '{action}'" },
        };
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

    // 成员解析：先按名字直接找；找不到时把 get_X/set_X（属性）与 add_X/remove_X（事件）映射回去
    // （IProperty/IEvent 是成员，但访问器方法不在 ITypeDefinition.Methods 里，模型常直接写 get_X）。
    static List<IMember> ResolveMembers(ITypeDefinition type, string name)
    {
        var direct = type.Members.Where(m => m.Name == name).Cast<IMember>().ToList();
        if (direct.Count > 0) return direct;
        if (name != null && (name.StartsWith("get_") || name.StartsWith("set_")))
        {
            var prop = name.Substring(4);
            var props = type.Properties.Where(p => p.Name == prop).Cast<IMember>().ToList();
            if (props.Count > 0) return props;
        }
        if (name != null && (name.StartsWith("add_") || name.StartsWith("remove_")))
        {
            var ev = name.Substring(name.StartsWith("add_") ? 4 : 7);
            var evs = type.Events.Where(e => e.Name == ev).Cast<IMember>().ToList();
            if (evs.Count > 0) return evs;
        }
        return new List<IMember>();
    }

    // IL 需要方法句柄：属性/事件的访问器从 getter/setter 拿。
    static List<IMethod> ResolveMethods(ITypeDefinition type, string name)
    {
        var direct = type.Methods.Where(m => m.Name == name).ToList();
        if (direct.Count > 0) return direct;
        if (name != null && (name.StartsWith("get_") || name.StartsWith("set_")))
        {
            var prop = name.Substring(4);
            foreach (var p in type.Properties.Where(p => p.Name == prop))
            {
                var acc = name.StartsWith("get_") ? p.Getter : p.Setter;
                if (acc != null) direct.Add(acc);
            }
        }
        return direct;
    }

    static List<string> Search(string dll, CSharpDecompiler dc, string target, int offset)
    {
        var t = target ?? "";
        var hits = new List<string>();
        foreach (var type in dc.TypeSystem.MainModule.TypeDefinitions)
        {
            if (type.FullName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) hits.Add($"TYPE  {type.FullName}");
            foreach (var m in type.Members)
                if (m.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    hits.Add($"  {KindName(m.SymbolKind)} {type.FullName}.{m.Name}");
        }
        if (hits.Count == 0) return new List<string>();
        const int cap = 500;
        var page = hits.Skip(offset).Take(cap).ToList();
        var outp = new List<string> { $"## {Path.GetFileName(dll)} - search '{target}'  ({hits.Count} match(es))" };
        outp.AddRange(page);
        int last = offset + page.Count;
        if (last < hits.Count)
            outp.Add($"... (showing {offset + 1}-{last} of {hits.Count}; refine --target, or use --offset {last} for the next page)");
        return outp;
    }

    static List<string> Members(string dll, CSharpDecompiler dc, string target)
    {
        var type = FindType(dc, target);
        if (type == null) return new List<string>();
        var outp = new List<string> { $"## {Path.GetFileName(dll)} - members of {type.FullName}" };
        var bases = type.DirectBaseTypes.Where(b => b.Kind != TypeKind.Unknown).Select(b => b.FullName);
        outp.Add($"  base: {string.Join(", ", bases)}");
        outp.AddRange(type.Members.Select(Sig).Select(s => "  " + s));
        return outp;
    }

    static List<string> Decompile(string dll, CSharpDecompiler dc, string target, string member)
    {
        var type = FindType(dc, target);
        if (type == null) return new List<string>();
        var outp = new List<string> { $"## {Path.GetFileName(dll)} - decompile {type.FullName}{(member != null ? "." + member : "")}" };
        if (member != null)
        {
            var ms = ResolveMembers(type, member);
            if (ms.Count == 0) return new List<string>();
            outp.AddRange(ms.Select(m => dc.DecompileAsString(m.MetadataToken)));
        }
        else outp.Add(dc.DecompileTypeAsString(type.FullTypeName));
        return outp;
    }

    static List<string> Il(string dll, PEFile pe, CSharpDecompiler dc, string target, string member)
    {
        var type = FindType(dc, target);
        if (type == null) return new List<string>();
        var sw = new StringWriter();
        var dis = new ReflectionDisassembler(new PlainTextOutput(sw), CancellationToken.None);
        if (member != null)
        {
            var ms = ResolveMethods(type, member);
            if (ms.Count == 0) return new List<string>();
            foreach (var m in ms) dis.DisassembleMethod(pe, (MethodDefinitionHandle)m.MetadataToken);
        }
        else dis.DisassembleType(pe, (TypeDefinitionHandle)type.MetadataToken);
        var outp = new List<string> { $"## {Path.GetFileName(dll)} - IL {type.FullName}{(member != null ? "." + member : "")}" };
        outp.AddRange(sw.ToString().Split('\n'));
        return outp;
    }

    static List<string> Strings(string dll, string target, int offset)
    {
        var bytes = File.ReadAllBytes(dll);
        var hits = new List<string>();
        var cur = new StringBuilder();
        var flush = new Action(() =>
        {
            if (cur.Length >= 4)
            {
                var s = cur.ToString();
                if (target == null || s.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) hits.Add(s);
            }
            cur.Clear();
        });
        foreach (var b in bytes)
        {
            if (b >= 32 && b < 127) cur.Append((char)b);
            else flush();
        }
        flush();
        if (hits.Count == 0) return new List<string>();
        const int cap = 500;
        var page = hits.Skip(offset).Take(cap).ToList();
        var outp = new List<string> { $"## {Path.GetFileName(dll)} - strings '{target}'  ({hits.Count} string(s))" };
        outp.AddRange(page);
        int last = offset + page.Count;
        if (last < hits.Count)
            outp.Add($"... (showing {offset + 1}-{last} of {hits.Count}; refine --target, or use --offset {last} for the next page)");
        return outp;
    }
}
