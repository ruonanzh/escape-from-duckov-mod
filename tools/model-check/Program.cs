// model-check —— 离线校验模型文件（干跑：不启动游戏就能知道产物长什么样）。
//
//   model-check --file <模型.json>           校验单个文件
//   model-check --md   <文档.md>             校验文档里所有 ```json 块（零件清单 / YSM）
//   model-check --file a.json --file b.json  多个
//   --side N                                 覆盖贴图边长（只用于自动密度的演示）
//
// 退出码：有 FAIL → 1，否则 0（WARN 不失败）。规范见 docs/unity-3d/05-model-format.md。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelKit;

namespace ModelCheck
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            var files = new List<string>();
            var markdown = new List<string>();
            int sideOverride = 0;
            string pngOut = null, templateOut = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--file": files.Add(args[++i]); break;
                    case "--md": markdown.Add(args[++i]); break;
                    case "--side": sideOverride = int.Parse(args[++i]); break;
                    case "--png": pngOut = args[++i]; break;
                    case "--template": templateOut = args[++i]; break;
                    default:
                        Console.Error.WriteLine("usage: model-check [--file <json>]... [--md <markdown>]... [--side N]");
                        return 2;
                }
            }
            if (files.Count == 0 && markdown.Count == 0) { Console.Error.WriteLine("usage: model-check [--file <json>]... [--md <markdown>]..."); return 2; }

            int fails = 0;
            foreach (var f in files)
            {
                var text = File.ReadAllText(f);
                var r = CheckBlock(text, Path.GetFileName(f), sideOverride, pngOut, templateOut);
                Console.Write(r.Report);
                fails += r.Fails;
            }
            foreach (var md in markdown)
            {
                var text = File.ReadAllText(md);
                int idx = 0;
                foreach (Match m in Regex.Matches(text, @"```json\n(.*?)```", RegexOptions.Singleline))
                {
                    idx++;
                    var r = CheckBlock(m.Groups[1].Value, $"{Path.GetFileName(md)} #{idx}", sideOverride, pngOut, templateOut);
                    Console.Write(r.Report);
                    fails += r.Fails;
                }
            }

            Console.WriteLine(fails == 0 ? "\nALL OK" : $"\n{fails} FAIL(s)");
            return fails == 0 ? 0 : 1;
        }

        sealed class Result { public StringBuilder Report = new StringBuilder(); public int Fails; }

        static Result CheckBlock(string json, string label, int sideOverride, string pngOut = null, string templateOut = null)
        {
            var res = new Result();
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch (Exception e)
            {
                res.Report.AppendLine($"FAIL  {label}    JSON 解析失败: {e.Message}");
                res.Fails++;
                return res;
            }

            var root = doc.RootElement;
            if (root.TryGetProperty("minecraft:geometry", out _)) return CheckYsm(root, label);

            var spec = ParseModel(root, label, out var parseErrors);
            foreach (var e in parseErrors) { res.Report.AppendLine($"FAIL  {label}    {e}"); res.Fails++; }
            if (spec == null) return res;

            if (sideOverride > 0) spec.Texture.Size = sideOverride;

            var warns = new List<string>();
            MeshData mesh;
            try { mesh = MeshKit.Build(spec); }
            catch (ModelKitException e)
            {
                res.Report.AppendLine($"FAIL  {label}    {e.Message}");
                res.Fails++;
                return res;
            }

            if (pngOut != null)
            {
                var bmp = TextureKit.Paint(spec, mesh, faceEdges: true);
                var path = System.IO.Directory.Exists(pngOut) || pngOut.EndsWith("/")
                    ? System.IO.Path.Combine(pngOut, spec.Name + ".png") : pngOut;
                PngWriter.Save(bmp, path);
                res.Report.AppendLine($"INFO  {label}    贴图已写出 {path}（{bmp.W}×{bmp.H}，{new FileInfo(path).Length / 1024} KB）");
            }
            if (templateOut != null)
            {
                var bmp = TextureKit.Template(mesh);
                var path = System.IO.Directory.Exists(templateOut) || templateOut.EndsWith("/")
                    ? System.IO.Path.Combine(templateOut, spec.Name + "_uv.png") : templateOut;
                PngWriter.Save(bmp, path);
                res.Report.AppendLine($"INFO  {label}    UV 模板已写出 {path}（{bmp.W}×{bmp.H}）");
            }

            // ── 校验项 ──────────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(spec.Name)) warns.Add("缺 name");
            if (string.IsNullOrEmpty(spec.Category)) warns.Add("缺 category");
            if (!IsPow2(spec.Texture.Size)) warns.Add($"贴图 size={spec.Texture.Size} 不是 2 的幂");
            if (mesh.PixelsPerMeter < 32) warns.Add($"密度只有 {mesh.PixelsPerMeter} px/m（{1000f / mesh.PixelsPerMeter:0} mm/像素）—— 只够大色块");
            if (mesh.VertexCount > 5000) warns.Add($"{mesh.VertexCount} 顶点，远超游戏本体量级（中位 402）");
            foreach (var p in spec.Parts)
                if (!string.IsNullOrEmpty(p.Role) && !spec.Texture.Fills.ContainsKey(p.Role))
                    warns.Add($"零件 '{p.Role}' 没有配底色（fills）");

            CheckUv(mesh, spec, warns, out var uvFail);
            CheckSize(spec, mesh, warns);
            foreach (var w in warns) res.Report.AppendLine($"WARN  {label}    {w}");
            if (uvFail != null) { res.Report.AppendLine($"FAIL  {label}    {uvFail}"); res.Fails++; }

            var size = mesh.Max - mesh.Min;
            var center = (mesh.Max + mesh.Min) * 0.5f;
            var tag = res.Fails > 0 ? "FAIL" : (warns.Count > 0 ? "WARN" : "PASS");
            res.Report.AppendLine(
                $"{tag}  {label,-22} category={spec.Category,-18} parts={mesh.Emitted.Count,2}  verts={mesh.VertexCount,5}  tris={mesh.TriangleCount,5}  " +
                $"bbox={size.X:0.###}×{size.Y:0.###}×{size.Z:0.###}m  centerX={center.X:0.###}  atlas={mesh.AtlasSize}²  density={mesh.PixelsPerMeter} px/m ({1000f / mesh.PixelsPerMeter:0.##} mm/px)");
            return res;
        }

        // ── 零件清单 ────────────────────────────────────────────────────────────

        static ModelSpec ParseModel(JsonElement root, string label, out List<string> errors)
        {
            errors = new List<string>();
            var spec = new ModelSpec();
            if (root.TryGetProperty("name", out var n)) spec.Name = n.GetString() ?? "";
            if (root.TryGetProperty("category", out var c)) spec.Category = c.GetString() ?? "";
            if (root.TryGetProperty("summary", out var s)) spec.Summary = s.GetString() ?? "";

            if (!root.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            {
                errors.Add("缺 parts[]（或不是数组）");
                return null;
            }
            foreach (var pe in parts.EnumerateArray()) spec.Parts.Add(ParsePart(pe, errors));

            if (root.TryGetProperty("texture", out var tex) && tex.ValueKind == JsonValueKind.Object)
            {
                if (tex.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Array && sz.GetArrayLength() == 2)
                    spec.Texture.Size = sz[0].GetInt32();
                if (tex.TryGetProperty("pixels_per_meter", out var ppm) && ppm.ValueKind == JsonValueKind.Number)
                    spec.Texture.PixelsPerMeter = ppm.GetSingle();
                if (tex.TryGetProperty("source", out var src))
                {
                    if (src.ValueKind == JsonValueKind.String) spec.Texture.Source = src.GetString();
                    else if (src.ValueKind == JsonValueKind.Object && src.TryGetProperty("file", out var f))
                    { spec.Texture.Source = "file"; spec.Texture.File = f.GetString(); }
                }
                if (tex.TryGetProperty("fills", out var fills) && fills.ValueKind == JsonValueKind.Object)
                    foreach (var kv in fills.EnumerateObject()) spec.Texture.Fills[kv.Name] = kv.Value.GetString();
            }
            return spec;
        }

        static PartSpec ParsePart(JsonElement e, List<string> errors)
        {
            var p = new PartSpec();
            string Get(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            float[] Arr3(string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 3
                ? new[] { v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle() } : null;

            p.Role = Get("role") ?? "";
            p.Shape = Get("shape") ?? "";
            p.Size = Arr3("size");
            if (e.TryGetProperty("r", out var r)) p.R = r.GetSingle();
            if (e.TryGetProperty("r1", out var r1)) p.R1 = r1.GetSingle();
            if (e.TryGetProperty("r2", out var r2)) p.R2 = r2.GetSingle();
            if (e.TryGetProperty("h", out var h)) p.H = h.GetSingle();
            if (e.TryGetProperty("depth", out var dp)) p.Depth = dp.GetSingle();
            if (e.TryGetProperty("segments", out var sg)) p.Segments = sg.GetInt32();
            if (e.TryGetProperty("rings", out var rg)) p.Rings = rg.GetInt32();
            if (e.TryGetProperty("profile", out var pr)) p.Profile = ToPairs(pr);
            if (e.TryGetProperty("outline", out var ol)) p.Outline = ToPairs(ol);
            p.At = Arr3("at") ?? new float[] { 0, 0, 0 };
            p.Rot = Arr3("rot") ?? new float[] { 0, 0, 0 };
            p.Scale = Arr3("scale") ?? new float[] { 1, 1, 1 };
            p.Mirror = Get("mirror");

            // 形状与必填参数
            switch (p.Shape)
            {
                case "box":
                    if (p.Size == null || p.Size.Any(v => v <= 0)) errors.Add($"零件 '{p.Role}': box 需要 size[3] 且都 > 0");
                    break;
                case "cylinder":
                    if (p.R <= 0 || p.H <= 0) errors.Add($"零件 '{p.Role}': cylinder 需要 r > 0 且 h > 0");
                    break;
                case "cone":
                    if (p.H <= 0 || (p.R1 <= 0 && p.R2 <= 0)) errors.Add($"零件 '{p.Role}': cone 需要 h > 0 且 r1/r2 至少一个 > 0");
                    break;
                case "sphere":
                    if (p.R <= 0) errors.Add($"零件 '{p.Role}': sphere 需要 r > 0");
                    break;
                case "lathe":
                    if (p.Profile == null || p.Profile.Length < 2) errors.Add($"零件 '{p.Role}': lathe 需要 profile（≥2 个 [半径,高度]）");
                    break;
                case "extrude":
                    if (p.Outline == null || p.Outline.Length < 3) errors.Add($"零件 '{p.Role}': extrude 需要 outline（≥3 个 [x,y]）");
                    if (p.Depth <= 0) errors.Add($"零件 '{p.Role}': extrude 需要 depth > 0");
                    break;
                default:
                    errors.Add($"零件 '{p.Role}': 不支持的 shape '{p.Shape}'（box|cylinder|cone|sphere|lathe|extrude）");
                    break;
            }
            if (p.Mirror != null && p.Mirror != "x" && p.Mirror != "y" && p.Mirror != "z")
                errors.Add($"零件 '{p.Role}': mirror 只能是 x/y/z");
            return p;
        }

        static float[][] ToPairs(JsonElement arr)
        {
            if (arr.ValueKind != JsonValueKind.Array) return null;
            var list = new List<float[]>();
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Array) continue;
                var row = new List<float>();
                foreach (var v in e.EnumerateArray()) row.Add(v.GetSingle());
                list.Add(row.ToArray());
            }
            return list.ToArray();
        }

        // ── 校验细节 ────────────────────────────────────────────────────────────

        static void CheckUv(MeshData mesh, ModelSpec spec, List<string> warns, out string fail)
        {
            fail = null;
            var used = new List<AtlasRect>();
            foreach (var r in mesh.Atlas)
            {
                if (r.X < 0 || r.Y < 0 || r.X + r.W > mesh.AtlasSize || r.Y + r.H > mesh.AtlasSize)
                { fail = $"零件 '{r.Role}' 的展开矩形超出贴图（{r.X},{r.Y} {r.W}×{r.H} / {mesh.AtlasSize}²）"; return; }
                foreach (var o in used)
                    if (r.X < o.X + o.W && o.X < r.X + r.W && r.Y < o.Y + o.H && o.Y < r.Y + r.H)
                    { fail = $"零件 '{r.Role}' 的展开矩形与 '{o.Role}' 重叠"; return; }
                used.Add(r);
            }
            // UV 值域
            foreach (var uv in mesh.Uvs)
                if (uv.X < -1e-4f || uv.X > 1 + 1e-4f || uv.Y < -1e-4f || uv.Y > 1 + 1e-4f)
                { fail = $"UV 越界：({uv.X:0.###},{uv.Y:0.###})"; return; }

            if (spec.Texture.Source == "file")
                warns.Add($"贴图来自外部文件 {spec.Texture.File} —— 无法在此校验图片尺寸（要求与 size 一致）");
        }

        /// <summary>单一尺寸上限：任何一边超过 10 m 都视为离谱（游戏里最大的资产 SM_BLD_Showcase 是 6 m）——
        /// 这条主要用来抓"单位写错"（把 cm 当 m 写就会大 100 倍）。</summary>
        const float MaxDimensionMeters = 10f;

        static void CheckSize(ModelSpec spec, MeshData mesh, List<string> warns)
        {
            var size = mesh.Max - mesh.Min;
            float longest = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (longest > MaxDimensionMeters)
                warns.Add($"最长边 {longest:0.##} m 超过 {MaxDimensionMeters:0} m —— 单位写错了？（游戏里最大的资产是 6 m）");
            float smallest = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (smallest < 0.005f)
                warns.Add($"整个模型只有 {smallest * 1000:0.#} mm —— 单位写错了？（我们按米）");
        }

        // ── YSM（角色）──────────────────────────────────────────────────────────

        static readonly string[] PlayerDuckBones = {
            "Root","Hip","Spine.001","Spine.002","Spine.003","Head","HairTip",
            "Arm.Root.R","Arm.Upper.R","Arm.Fore.R","Hand.R","Finger.Middle.R.001","Finger.Middle.R.002","Finger.Pinky.R.001","Finger.Pinky.R.002","Finger.Index.R.001","Finger.Index.R.002",
            "Arm.Root.L","Arm.Upper.L","Arm.Fore.L","Hand.L","Finger.Middle.L.001","Finger.Middle.L.002","Finger.Pinky.L.001","Finger.Pinky.L.002","Finger.Index.L.001","Finger.Index.L.002",
            "Leg.Upper.R","Leg.Lower.R","Foot.R.001","Foot.R.002","Leg.Upper.L","Leg.Lower.L","Foot.L.001","Foot.L.002","Tail.001","Tail.002" };
        static readonly string[] NpcDuckBones = {
            "Root","Pelvis","Spine.001","Spine.002","Spine.003","Spine.004","Head","HeadTip",
            "UpperArm.L","Elbow.L","ForeArm.L","Hand.L","Hand.Soket.L","UpperArm.R","Elbow.R","ForeArm.R","Hand.R","Hand.Soket.R",
            "Thigh.R","Foot.R","Thigh.L","Foot.L","Tail","Tail.001" };

        static Result CheckYsm(JsonElement root, string label)
        {
            var res = new Result();
            var geo = root.GetProperty("minecraft:geometry");
            if (geo.ValueKind != JsonValueKind.Array || geo.GetArrayLength() == 0) { res.Report.AppendLine($"FAIL  {label}    minecraft:geometry 为空"); res.Fails++; return res; }
            var g = geo[0];
            if (!g.TryGetProperty("bones", out var bones) || bones.ValueKind != JsonValueKind.Array || bones.GetArrayLength() == 0)
            { res.Report.AppendLine($"FAIL  {label}    bones 为空"); res.Fails++; return res; }

            int tw = 64, th = 64; string identifier = "";
            if (g.TryGetProperty("description", out var desc))
            {
                if (desc.TryGetProperty("texture_width", out var a)) tw = a.GetInt32();
                if (desc.TryGetProperty("texture_height", out var b)) th = b.GetInt32();
                if (desc.TryGetProperty("identifier", out var i)) identifier = i.GetString() ?? "";
            }

            var names = new HashSet<string>();
            var parentOf = new Dictionary<string, string>();
            var warns = new List<string>();
            int cubes = 0, fails = 0;

            foreach (var b in bones.EnumerateArray())
            {
                var name = b.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(name)) { warns.Add("有骨骼没有 name"); continue; }
                if (!names.Add(name)) { warns.Add($"骨骼名重复：{name}"); continue; }
                if (b.TryGetProperty("parent", out var pa) && pa.ValueKind == JsonValueKind.String) parentOf[name] = pa.GetString();
                if (b.TryGetProperty("cubes", out var cs) && cs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cs.EnumerateArray())
                    {
                        cubes++;
                        if (!c.TryGetProperty("size", out var sz) || sz.ValueKind != JsonValueKind.Array || sz.GetArrayLength() != 3)
                        { warns.Add($"{name}: cube 缺 size[3]"); continue; }
                        float w = sz[0].GetSingle(), h = sz[1].GetSingle(), d = sz[2].GetSingle();
                        if (w <= 0 || h <= 0 || d <= 0) warns.Add($"{name}: cube size 有非正数");
                        if (c.TryGetProperty("uv", out var uv) && uv.ValueKind == JsonValueKind.Array && uv.GetArrayLength() == 2)
                        {
                            int u = uv[0].GetInt32(), v = uv[1].GetInt32();
                            int rw = 2 * (int)Math.Round(d) + 2 * (int)Math.Round(w), rh = (int)Math.Round(d) + (int)Math.Round(h);
                            if (u < 0 || v < 0 || u + rw > tw || v + rh > th)
                                warns.Add($"{name}: cube uv=({u},{v}) 的展开矩形 {rw}×{rh} 超出贴图 {tw}×{th}");
                        }
                        else warns.Add($"{name}: cube 缺 uv[2]");
                    }
                }
            }

            // 父骨骼必须存在 + 无环
            foreach (var kv in parentOf)
            {
                if (!names.Contains(kv.Value)) { res.Report.AppendLine($"FAIL  {label}    骨骼 '{kv.Key}' 的 parent '{kv.Value}' 不存在"); fails++; continue; }
                var seen = new HashSet<string> { kv.Key };
                var cur = kv.Value;
                while (cur != null)
                {
                    if (!seen.Add(cur)) { res.Report.AppendLine($"FAIL  {label}    骨骼父子关系成环：{kv.Key} → … → {cur}"); fails++; break; }
                    cur = parentOf.TryGetValue(cur, out var p) ? p : null;
                }
            }

            // 骨骼名是否在已知骨架里（防拼写错 → 会静默不挂载）
            var known = new HashSet<string>(PlayerDuckBones.Concat(NpcDuckBones));
            var unknown = names.Where(x => !known.Contains(x) && !x.EndsWith("_end")).ToList();
            if (unknown.Count > 0)
                warns.Add($"不在已知骨架里的骨骼名（拼写？）：{string.Join(", ", unknown.Take(6))}{(unknown.Count > 6 ? " …" : "")}");

            foreach (var w in warns) res.Report.AppendLine($"WARN  {label}    {w}");
            var tag = fails > 0 ? "FAIL" : (warns.Count > 0 ? "WARN" : "PASS");
            res.Report.AppendLine($"{tag}  {label,-22} YSM  bones={names.Count,3}  cubes={cubes,3}  texture={tw}×{th}  id={identifier}");
            res.Fails += fails;
            return res;
        }

        static bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;
    }
}
