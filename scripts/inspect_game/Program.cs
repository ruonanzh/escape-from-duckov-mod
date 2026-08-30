using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

// Duckov API inspector — dump public API signatures of game-owned DLLs to docs/api/.
//
// Usage: dotnet run --project scripts/inspect_game -- <managedDir> <outDir>
//   managedDir: Duckov.app/Contents/Resources/Data/Managed
//   outDir:     docs/api
//
// Scope: only game-owned DLLs (TeamSoda.*, ItemStatsSystem, Assembly-CSharp,
//         SodaLocalization, TeamSoda.MiniLocalizor). Engine/third-party DLLs
//         (DOTween, NodeCanvas, Odin, Unity internals, ...) are excluded.
// Depth: API signatures only (DeclaredOnly members), never method bodies.
//        This protects the game's implementation and keeps the output readable.

var managedDir = args[0];
var outDir = args[1];
Directory.CreateDirectory(outDir);

var dlls = new[]
{
    "TeamSoda.Duckov.Core",
    "TeamSoda.Duckov.Utilities",
    "ItemStatsSystem",
    "Assembly-CSharp",
    "SodaLocalization",
    "TeamSoda.MiniLocalizor",
};

// Unity engine + FMOD native APIs for asset mods (replace/add textures, audio,
// meshes, materials). Curated — only types a mod author needs for asset work.
var unityResourceTypes = new (string dll, string[] typeNames)[]
{
    ("UnityEngine.CoreModule", new[] {
        "UnityEngine.Resources", "UnityEngine.Object", "UnityEngine.GameObject",
        "UnityEngine.Component", "UnityEngine.Transform",
        "UnityEngine.Texture2D", "UnityEngine.Sprite", "UnityEngine.SpriteRenderer",
        "UnityEngine.Material", "UnityEngine.Mesh", "UnityEngine.MeshFilter",
        "UnityEngine.MeshRenderer", "UnityEngine.Shader",
    }),
    ("UnityEngine.AudioModule", new[] {
        "UnityEngine.AudioSource", "UnityEngine.AudioClip", "UnityEngine.AudioListener",
    }),
    ("UnityEngine.AssetBundleModule", new[] {
        "UnityEngine.AssetBundle", "UnityEngine.AssetBundleRequest", "UnityEngine.AssetBundleCreateRequest",
    }),
    ("UnityEngine.ImageConversionModule", new[] {
        "UnityEngine.ImageConversion",
    }),
    ("FMODUnity", new[] {
        "FMODUnity.RuntimeManager", "FMODUnity.Bank", "FMODUnity.EventReference",
        "FMODUnity.EventInstance", "FMODUnity.StudioEventEmitter",
    }),
};

var allProblems = new StringBuilder();

foreach (var dllName in dlls)
{
    var dllPath = Path.Combine(managedDir, dllName + ".dll");
    if (!File.Exists(dllPath))
    {
        Console.WriteLine($"SKIP {dllName}.dll (not found)");
        continue;
    }

    Type[] types;
    try
    {
        var asm = Assembly.LoadFrom(dllPath);
        types = asm.GetTypes().Where(t => t.IsPublic || t.IsNestedPublic).ToArray();
    }
    catch (ReflectionTypeLoadException e)
    {
        types = e.Types.Where(t => t != null).Where(t => t.IsPublic || t.IsNestedPublic).ToArray()!;
        foreach (var ex in e.LoaderExceptions.Take(3))
            allProblems.AppendLine($"{dllName}: {ex?.Message}");
    }

    var sb = new StringBuilder();
    sb.AppendLine($"# {dllName}.dll");
    sb.AppendLine();
    sb.AppendLine($"{types.Length} public types");
    sb.AppendLine();

    foreach (var t in types.OrderBy(t => FriendlyFullName(t)))
    {
        DumpType(t, sb);
    }

    var outPath = Path.Combine(outDir, dllName + ".md");
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"{dllName}.dll -> {outPath} ({types.Length} types)");
}

if (allProblems.Length > 0)
{
    Console.WriteLine("\n=== load problems (non-fatal) ===");
    Console.WriteLine(allProblems.ToString());
}

DumpUnityResources(managedDir, outDir, unityResourceTypes, allProblems);

static void DumpUnityResources(string managedDir, string outDir, (string dll, string[] typeNames)[] spec, StringBuilder problems)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Unity Resource APIs");
    sb.AppendLine();
    sb.AppendLine("Unity engine + FMOD native APIs for asset mods — loading, replacing, and adding textures / audio / meshes / materials. These live in Unity engine DLLs (not game-owned DLLs).");
    sb.AppendLine();

    var count = 0;
    foreach (var (dll, typeNames) in spec)
    {
        var dllPath = Path.Combine(managedDir, dll + ".dll");
        if (!File.Exists(dllPath))
        {
            Console.WriteLine($"SKIP {dll}.dll (not found)");
            continue;
        }
        Assembly asm;
        try { asm = Assembly.LoadFrom(dllPath); }
        catch (Exception e) { problems.AppendLine($"{dll}: {e.Message}"); continue; }

        foreach (var typeName in typeNames)
        {
            var t = asm.GetType(typeName);
            if (t == null) { problems.AppendLine($"{dll}: {typeName} not found"); continue; }
            DumpType(t, sb);
            count++;
        }
    }

    var outPath = Path.Combine(outDir, "unity-resources.md");
    File.WriteAllText(outPath, sb.ToString());
    Console.WriteLine($"unity-resources -> {outPath} ({count} types)");
}

static void DumpType(Type t, StringBuilder sb)
{
    var kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class";
    var baseName = t.BaseType != null && t.BaseType != typeof(object) && t.BaseType != typeof(ValueType) && t.BaseType != typeof(Enum)
        ? FriendlyFullName(t.BaseType)
        : null;

    if (t.IsEnum)
    {
        sb.AppendLine($"## {FriendlyFullName(t)} [enum]");
        sb.AppendLine();
        foreach (var name in Enum.GetNames(t))
        {
            var value = Convert.ToInt64(Enum.Parse(t, name));
            sb.AppendLine($"- {name} = {value}");
        }
        sb.AppendLine();
        return;
    }

    const BindingFlags F = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    var members = new StringBuilder();
    foreach (var p in t.GetProperties(F).OrderBy(p => p.Name))
    {
        var access = (p.CanRead ? "get" : "") + (p.CanWrite ? "/set" : "");
        var stat = p.GetAccessors(true).Any(a => a.IsStatic) ? " (static)" : "";
        members.AppendLine($"- prop [{access}] {FriendlyTypeName(p.PropertyType)} {p.Name}{stat}");
    }
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.Name))
    {
        members.AppendLine($"- field {FriendlyTypeName(f.FieldType)} {f.Name}" + (f.IsStatic ? " (static)" : ""));
    }
    foreach (var m in t.GetMethods(F).Where(m => !m.IsSpecialName).OrderBy(m => m.Name).ThenBy(m => m.GetParameters().Length))
    {
        var pars = string.Join(", ", m.GetParameters().Select(p => $"{FriendlyTypeName(p.ParameterType)} {p.Name}"));
        members.AppendLine($"- method {FriendlyTypeName(m.ReturnType)} {m.Name}({pars})" + (m.IsStatic ? " (static)" : ""));
    }

    if (members.Length == 0) return; // skip types with no public API

    sb.AppendLine($"## {FriendlyFullName(t)} [{kind}]" + (baseName != null ? $" : {baseName}" : ""));
    sb.AppendLine();
    sb.Append(members);
    sb.AppendLine();
}

static string FriendlyTypeName(Type t)
{
    if (t.IsGenericType)
    {
        var name = t.Name.Split('`')[0];
        var args = string.Join(", ", t.GetGenericArguments().Select(FriendlyTypeName));
        return $"{name}<{args}>";
    }
    return t.Name;
}

static string FriendlyFullName(Type t)
{
    if (t.IsNested)
        return $"{FriendlyFullName(t.DeclaringType)}.{FriendlyTypeName(t)}";
    var ns = t.Namespace;
    var name = FriendlyTypeName(t);
    return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
}
