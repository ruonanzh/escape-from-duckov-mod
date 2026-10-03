// model-check —— 离线校验模型文件（干跑：不启动游戏就知道产物长什么样）。
//
//   model-check --file <模型.json>            校验单个文件
//   model-check --md   <文档.md>              校验文档里所有 ```json 块（零件清单 / YSM）
//   model-check --file a.json --file b.json   多个
//   --side N                                  覆盖贴图边长（演示自动密度）
//   --png <路径|目录>                          按 fills 生成贴图
//   --template <路径|目录>                     导出 UV 模板（给手绘）
//
// 退出码：有 FAIL → 1，否则 0。规范见 docs/unity-3d/05-model-format.md。
// 解析/几何/贴图都走 reference/mod-kit/ 的共享源码（与运行时 mod 同一份）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                        Console.Error.WriteLine("usage: model-check [--file <json>]... [--md <markdown>]... [--side N] [--png <path|dir>] [--template <path|dir>]");
                        return 2;
                }
            }
            if (files.Count == 0 && markdown.Count == 0) { Console.Error.WriteLine("usage: model-check [--file <json>]... [--md <markdown>]..."); return 2; }

            int fails = 0;
            foreach (var f in files)
            {
                var r = CheckBlock(File.ReadAllText(f), Path.GetFileName(f), sideOverride, pngOut, templateOut);
                Console.Write(r.Report);
                fails += r.Fails;
            }
            foreach (var md in markdown)
            {
                int idx = 0;
                foreach (Match m in Regex.Matches(File.ReadAllText(md), @"```json\n(.*?)```", RegexOptions.Singleline))
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

        static Result CheckBlock(string json, string label, int sideOverride, string pngOut, string templateOut)
        {
            var res = new Result();
            JsonValue root;
            try { root = Json.Parse(json); }
            catch (Exception e)
            {
                res.Report.AppendLine($"FAIL  {label}    JSON 解析失败: {e.Message}");
                res.Fails++;
                return res;
            }
            if (root == null || !root.IsObject) { res.Report.AppendLine($"FAIL  {label}    顶层不是对象"); res.Fails++; return res; }

            if (root["minecraft:geometry"] != null) return CheckYsm(root, label);

            var errors = new List<string>();
            var spec = ModelJson.ToModel(root, errors);
            foreach (var e in errors) { res.Report.AppendLine($"FAIL  {label}    {e}"); res.Fails++; }
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
                var path = WriteTarget(pngOut, spec.Name + ".png");
                PngWriter.Save(bmp, path);
                res.Report.AppendLine($"INFO  {label}    贴图已写出 {path}（{bmp.W}×{bmp.H}，{new FileInfo(path).Length / 1024} KB）");
            }
            if (templateOut != null)
            {
                var bmp = TextureKit.Template(mesh);
                var path = WriteTarget(templateOut, spec.Name + "_uv.png");
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
            CheckSize(mesh, warns);
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

        static string WriteTarget(string pathOrDir, string filename)
            => Directory.Exists(pathOrDir) ? Path.Combine(pathOrDir, filename) : (pathOrDir.EndsWith("/") ? Path.Combine(pathOrDir, filename) : pathOrDir);

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
            if (Environment.GetEnvironmentVariable("MC_SUBMESHES") == "1")
            {
                foreach (var sm in mesh.SubMeshes)
                    warns.Add($"  submesh '{sm.Role}': {sm.Count / 3} 三角面");
            }
            foreach (var uv in mesh.Uvs)
                if (uv.X < -1e-4f || uv.X > 1 + 1e-4f || uv.Y < -1e-4f || uv.Y > 1 + 1e-4f)
                { fail = $"UV 越界：({uv.X:0.###},{uv.Y:0.###})"; return; }

            // 配件槽位：可选、每项可为 null（= 没有该挂点）；名字只认游戏那 5 个
            foreach (var kv in spec.Slots)
                if (System.Array.IndexOf(new[] { "Scope", "Tec", "Muzzle", "Stock", "Grip" }, kv.Key) < 0)
                    warns.Add($"slots 里的 '{kv.Key}' 不是游戏槽位名（只有 Scope / Tec / Muzzle / Stock / Grip）");

            if (spec.Texture.Source == "file")
                warns.Add($"贴图来自外部文件 {spec.Texture.File} —— 无法在此校验图片尺寸（要求与 size 一致）");
        }

        /// <summary>单一尺寸上限：任何一边超过 10 m 都视为离谱（游戏里最大的资产 SM_BLD_Showcase 是 6 m）——
        /// 这条主要用来抓“单位写错”（把 cm 当 m 写就会大 100 倍）。</summary>
        const float MaxDimensionMeters = 10f;

        static void CheckSize(MeshData mesh, List<string> warns)
        {
            var size = mesh.Max - mesh.Min;
            float longest = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (longest > MaxDimensionMeters)
                warns.Add($"最长边 {longest:0.##} m 超过 {MaxDimensionMeters:0} m —— 单位写错了？（游戏里最大的资产是 6 m）");
            if (longest < 0.005f)
                warns.Add($"整个模型只有 {longest * 1000:0.#} mm —— 单位写错了？（我们按米）");
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

        static Result CheckYsm(JsonValue root, string label)
        {
            var res = new Result();
            var errors = new List<string>();
            var ysm = ModelJson.ToYsm(root, errors);
            foreach (var e in errors) { res.Report.AppendLine($"FAIL  {label}    {e}"); res.Fails++; }
            if (ysm.Bones.Count == 0) return res;

            var warns = new List<string>();
            var names = new HashSet<string>();
            var parentOf = new Dictionary<string, string>();
            int cubes = 0;

            foreach (var b in ysm.Bones)
            {
                if (string.IsNullOrEmpty(b.Name)) { warns.Add("有骨骼没有 name"); continue; }
                if (!names.Add(b.Name)) { warns.Add($"骨骼名重复：{b.Name}"); continue; }
                if (!string.IsNullOrEmpty(b.Parent)) parentOf[b.Name] = b.Parent;
                foreach (var c in b.Cubes)
                {
                    cubes++;
                    if (c.Size[0] <= 0 || c.Size[1] <= 0 || c.Size[2] <= 0) warns.Add($"{b.Name}: cube size 有非正数");
                    int rw = 2 * (int)Math.Round((double)c.Size[2]) + 2 * (int)Math.Round((double)c.Size[0]);
                    int rh = (int)Math.Round((double)c.Size[2]) + (int)Math.Round((double)c.Size[1]);
                    if (c.Uv[0] < 0 || c.Uv[1] < 0 || c.Uv[0] + rw > ysm.TextureWidth || c.Uv[1] + rh > ysm.TextureHeight)
                        warns.Add($"{b.Name}: cube uv=({c.Uv[0]},{c.Uv[1]}) 的展开矩形 {rw}×{rh} 超出贴图 {ysm.TextureWidth}×{ysm.TextureHeight}");
                }
            }

            foreach (var kv in parentOf)
            {
                if (!names.Contains(kv.Value)) { res.Report.AppendLine($"FAIL  {label}    骨骼 '{kv.Key}' 的 parent '{kv.Value}' 不存在"); res.Fails++; continue; }
                var seen = new HashSet<string> { kv.Key };
                var cur = kv.Value;
                while (cur != null)
                {
                    if (!seen.Add(cur)) { res.Report.AppendLine($"FAIL  {label}    骨骼父子关系成环：{kv.Key} → … → {cur}"); res.Fails++; break; }
                    cur = parentOf.TryGetValue(cur, out var p) ? p : null;
                }
            }

            var known = new HashSet<string>(PlayerDuckBones.Concat(NpcDuckBones));
            var unknown = names.Where(x => !known.Contains(x) && !x.EndsWith("_end")).ToList();
            if (unknown.Count > 0)
                warns.Add($"不在已知骨架里的骨骼名（拼写？）：{string.Join(", ", unknown.Take(6))}{(unknown.Count > 6 ? " …" : "")}");

            foreach (var w in warns) res.Report.AppendLine($"WARN  {label}    {w}");
            var tag = res.Fails > 0 ? "FAIL" : (warns.Count > 0 ? "WARN" : "PASS");
            res.Report.AppendLine($"{tag}  {label,-22} YSM  bones={names.Count,3}  cubes={cubes,3}  texture={ysm.TextureWidth}×{ysm.TextureHeight}  id={ysm.Identifier}");
            return res;
        }

        static bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;
    }
}
