// GltfLoader —— 运行时读取 GLB（glTF 2.0 二进制），造出 Unity 的 Mesh + 贴图。
//
// 为什么能在运行时做：游戏是 Mono + Assembly.LoadFrom 加载 mod（不是 IL2CPP）→
// `new Mesh()` / `Texture2D.LoadImage()` 这些标准 API 都可用 ✓（实测过同类做法）。
//
// 坐标系（实测过的规则，别改错）：
//   glTF 右手系（+Y 上、-Z 前）→ Unity 左手系（+Y 上、+Z 前）
//   → 位置/法线 **取反 X** ✓ 三角面 **绕序反转** ✓ UV **v = 1 - v** ✓
//   → 朝向还有 180° 的差别（glTF 物件的"前"是 -Z）→ 由模型规格/对齐处理，不在这里转 ✓

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ModelKit
{
    public static class GltfLoader
    {
        public sealed class Loaded
        {
            public Mesh Mesh;
            public Texture2D MainTexture;      // baseColor 贴图（可能为 null）
            public Texture2D NormalTexture;    // ⭐ 法线（Tripo 的 `normalTexture` ✓ 可能为 null ✓）
            public int VertexCount;
            public int TriangleCount;
            public string Report = "";
        }

        /// <param name="zeroGrip">⭐ 要不要做“**握把归零**” ✓ —— 默认 `true` ✓（枪的行为不变 ✓）。
        /// <para>⚠️ 近战（刀/铲…）要传 **`false`** ✗：`GuessGrip` 的假设是**枪**（“枪口 +Z ⇒ 枪托在 −Z”✓，
        /// 且 x/z 取包围盒**中心** ✗）—— 对弯刀/爪刀会把模型**横向挪偏** ✓。
        /// 跳过后就用模型**自己的原点** ✓（近战按“刃朝上 · 柄朝下”建模 ✓ 再按包围盒中心对齐 ✓）。</para>
        public static Loaded LoadFile(string path, string front = "auto", bool zeroGrip = true) => Load(File.ReadAllBytes(path), front, zeroGrip);

        public static Loaded Load(byte[] data, string front = "auto", bool zeroGrip = true)
        {
            var r = new Loaded();
            if (data.Length < 20 || data[0] != 'g' || data[1] != 'l' || data[2] != 'T' || data[3] != 'F')
                throw new Exception("不是 GLB 文件（magic 不对）");

            // ① 拆 chunk：JSON + BIN
            int off = 12; string json = null; byte[] bin = null;
            while (off + 8 <= data.Length)
            {
                uint len = BitConverter.ToUInt32(data, off);
                uint type = BitConverter.ToUInt32(data, off + 4);
                int start = off + 8;
                if (type == 0x4E4F534A && start + (int)len <= data.Length) json = Encoding.UTF8.GetString(data, start, (int)len);
                else if (type == 0x004E4942 && start + (int)len <= data.Length) { bin = new byte[len]; Buffer.BlockCopy(data, start, bin, 0, (int)len); }
                off = start + (int)len;
            }
            if (json == null || bin == null) throw new Exception("GLB 缺少 JSON 或 BIN chunk");

            var doc = Json.Parse(json);
            var accessors = doc["accessors"];
            var views = doc["bufferViews"];
            var meshes = doc["meshes"];
            if (meshes == null || meshes.Count == 0) throw new Exception("glTF 里没有 meshes");

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int mi = 0; mi < meshes.Count; mi++)
            {
                var prims = meshes[mi]["primitives"];
                for (int pi = 0; pi < (prims?.Count ?? 0); pi++)
                {
                    var prim = prims[pi];
                    var attrs = prim["attributes"];
                    int baseV = verts.Count;
                    var pos = ReadAccessor(accessors, views, bin, attrs["POSITION"].AsInt(-1), 3);
                    var nrm = attrs.Has("NORMAL") ? ReadAccessor(accessors, views, bin, attrs["NORMAL"].AsInt(-1), 3) : null;
                    var uv = attrs.Has("TEXCOORD_0") ? ReadAccessor(accessors, views, bin, attrs["TEXCOORD_0"].AsInt(-1), 2) : null;
                    for (int i = 0; i < pos.Count; i++)
                    {
                        verts.Add(new Vector3(-pos[i][0], pos[i][1], pos[i][2]));           // X 取反（右手→左手）
                        norms.Add(nrm != null ? new Vector3(-nrm[i][0], nrm[i][1], nrm[i][2]) : Vector3.up);
                        uvs.Add(uv != null ? new Vector2(uv[i][0], 1f - uv[i][1]) : Vector2.zero);   // V 翻转
                    }
                    var idx = ReadIndices(accessors, views, bin, prim.Has("indices") ? prim["indices"].AsInt(-1) : -1);
                    for (int t = 0; t + 2 < idx.Count; t += 3)                                    // 绕序反转（左手系）
                    {
                        tris.Add(idx[t] + baseV);
                        tris.Add(idx[t + 2] + baseV);
                        tris.Add(idx[t + 1] + baseV);
                    }
                }
            }

            // ① 朝向：**不在这里猜** ✗
            //    实测（2026-10-04）：Tripo 的模型朝向由**提示词**决定 —— 提示词写
            //    "the muzzle points to the left" → 枪口落在 **+Z**（= Unity 前向 ✓）；
            //    写 "to the right" → 落在 −Z。所以**朝向在生成阶段就定好** ✓
            //    用户自己给的 GLB 没有这个保证 → 用 mod 的 `config.json` 的 "front" 声明（下面会转）

            ApplyFrontDeclaration(verts, norms, front);

            // ② 握把归零（枪：枪口朝 +Z → 枪托在 −Z；枪托端起 8%~35% 区间的最低点 = 握把）
            //   ⚠️ 近战传 zeroGrip:false 跳过 ✗（理由见 LoadFile 的参数说明 ✓）
            var grip = zeroGrip ? GuessGrip(verts) : Vector3.zero;
            if (grip != Vector3.zero) for (int i = 0; i < verts.Count; i++) verts[i] -= grip;

            var mesh = new Mesh { name = "glb_model" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // ⭐⭐ **切线必须算** ✗ —— Unity 的 `_BumpMap`（法线贴图）**依赖 tangents** ✓；
            //   缺了它 Unity 用退化值 ⇒ 表面出现**黑斑 / 破碎 / “裂缝”** ✓（实测：刀在游戏里就是这个 ✓）
            //   而 three.js 查看器自己会算 ✓ ⇒ 同模型在那里是正常的 ✓（交叉验证 ✓）
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            r.Mesh = mesh; r.VertexCount = verts.Count; r.TriangleCount = tris.Count / 3;

            // ② 贴图：优先材质上的 baseColorTexture，否则第一张
            r.MainTexture = LoadBaseColor(doc, views, bin);
            // ⭐ 法线 ✓ —— 只读这一张 ✗（**不读 ORM** ✗：金属/光滑一律不碰 ✓）
            r.NormalTexture = LoadNormal(doc, views, bin);
            r.Report = $"GLB：顶点 {r.VertexCount}，三角面 {r.TriangleCount}，贴图 {(r.MainTexture != null ? r.MainTexture.name + $" {r.MainTexture.width}x{r.MainTexture.height}" : "无")}，法线 {(r.NormalTexture != null ? "有" : "无")}；包围盒 {mesh.bounds.size}；握把归零 {grip}";
            return r;
        }

        /// <summary>按声明旋转（用户自带的 GLB 用）：front = "auto"（不转）/"-z"/"+x"/"-x" → 把枪口转到 +Z ✓</summary>
        public static void ApplyFrontDeclaration(List<Vector3> verts, List<Vector3> norms, string front)
        {
            if (string.IsNullOrEmpty(front) || front == "auto" || front == "+z") return;
            // ⭐ 近战专用 "up" ✓：把**最长轴**转到 **+Y**（立起来 ✓）—— 刀 = 刃朝上 · 柄朝下 ✓
            //   ⚠️ 其余 ±x/±z 都是**绕 Y**转 ✗，对“躺着的刀”不管用 ✓（实测：Tripo 出的菜刀长轴在 X ✗）
            if (front == "up")
            {
                int ax = LongAxis(verts);
                UnityEngine.Quaternion q =
                      ax == 1 ? UnityEngine.Quaternion.identity                       // 已经是 Y ✓
                    : ax == 0 ? UnityEngine.Quaternion.Euler(0f, 0f, 90f)             // X → +Y ✓（-90 会到 -Y ✗ 倒过来 ✓）
                              : UnityEngine.Quaternion.Euler(-90f, 0f, 0f);           // Z → Y ✓
                for (int i = 0; i < verts.Count; i++) { verts[i] = q * verts[i]; norms[i] = q * norms[i]; }
                // ⚠️ **不要**再绕 Y 转 ✗ —— 试过（把厚度轴变 Z ⇒ 刀面**正对镜头**✓）但那是给“出图”的要求 ✗；
                //   真实握刀是**刀面与视线平行** ✓ ⇒ 厚度轴保持 X ✓（实测预测 [0.10, 1.00, 0.57] ✓）。
                return;
            }
            foreach (var v0 in new[] { verts })
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var v = verts[i]; var n = norms[i]; Vector3 nv; Vector3 nn;
                    switch (front)
                    {
                        case "-z": nv = new Vector3(-v.x, v.y, -v.z); nn = new Vector3(-n.x, n.y, -n.z); break;
                        case "+x": nv = new Vector3(-v.z, v.y, v.x); nn = new Vector3(-n.z, n.y, n.x); break;
                        case "-x": nv = new Vector3(v.z, v.y, -v.x); nn = new Vector3(n.z, n.y, -n.x); break;
                        default: nv = v; nn = n; break;
                    }
                    verts[i] = nv; norms[i] = nn;
                }
                break;
            }
        }

        /// <summary>统一朝向：把最长轴转到 Z，并让**枪口朝 +Z**（游戏武器坐标系期望的方向）</summary>
        public static void OrientToUnity(List<Vector3> verts, List<Vector3> norms)
        {
            int ax = LongAxis(verts);
            if (ax != 2)                                   // X 或 Y → 绕轴转到 Z
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var v = verts[i]; var n = norms[i];
                    if (ax == 0) { verts[i] = new Vector3(v.z, v.y, -v.x); norms[i] = new Vector3(n.z, n.y, -n.x); }
                    else { verts[i] = new Vector3(v.x, v.z, -v.y); norms[i] = new Vector3(n.x, n.z, -n.y); }
                }
            }
            if (!MuzzleAtPositiveZ(verts))                 // 枪口在 −Z → 绕 Y 转 180°
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var v = verts[i]; var n = norms[i];
                    verts[i] = new Vector3(-v.x, v.y, -v.z);
                    norms[i] = new Vector3(-n.x, n.y, -n.z);
                }
            }
        }

        static int LongAxis(List<Vector3> pts)
        {
            var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            var s = mx - mn;
            return s.x > s.y ? (s.x > s.z ? 0 : 2) : (s.y > s.z ? 1 : 2);
        }

        /// <summary>枪口在 +Z 吗？（判据：**两个方向都细**的那端是枪管 ✓ —— 枪托只是"一个方向薄但另一个方向宽" ✗）</summary>
        public static bool MuzzleAtPositiveZ(List<Vector3> pts)
        {
            var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            float sp = mx.z - mn.z;
            if (sp <= 1e-6f) return true;
            // 两端各看 6% 与 15% 两片，取较大者（枪管"细得久" ✓）
            float tMin = Math.Max(Thickness(pts, mn.z, mn.z + sp * 0.06f), Math.Max(Thickness(pts, mn.z, mn.z + sp * 0.15f), 0f));
            float tMax = Math.Max(Thickness(pts, mx.z - sp * 0.06f, mx.z), Math.Max(Thickness(pts, mx.z - sp * 0.15f, mx.z), 0f));
            return tMax < tMin;                            // 细的那端是枪口
        }

        static float Thickness(List<Vector3> pts, float lo, float hi)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            bool any = false;
            foreach (var p in pts)
            {
                if (p.z < lo || p.z > hi) continue;
                any = true;
                if (p.x < x0) x0 = p.x; if (p.x > x1) x1 = p.x;
                if (p.y < y0) y0 = p.y; if (p.y > y1) y1 = p.y;
            }
            if (!any) return float.MaxValue;
            return Math.Max(x1 - x0, y1 - y0);
        }

        /// <summary>猜握把（与打包器同一套规则）：最长轴=枪管轴，两端薄片细端=枪口，枪托端起 8%~35% 的最低点</summary>
        public static Vector3 GuessGrip(List<Vector3> pts)
        {
            if (pts.Count == 0) return Vector3.zero;
            var mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            var span = mx - mn;
            int ax = span.x > span.y ? (span.x > span.z ? 0 : 2) : (span.y > span.z ? 1 : 2);
            var o1 = ax == 0 ? 1 : 0; var o2 = ax == 2 ? 1 : 2;
            float Lo() => ax == 0 ? mn.x : ax == 1 ? mn.y : mn.z;
            float Hi() => ax == 0 ? mx.x : ax == 1 ? mx.y : mx.z;
            float C(Vector3 v, int a) => a == 0 ? v.x : a == 1 ? v.y : v.z;
            float sp = Hi() - Lo();
            // 朝向由提示词保证（枪口 +Z）→ 枪托在 −Z ✓ 不需要再判定
            float stock = Lo();
            float dir = 1f;
            float l2 = stock + dir * sp * 0.08f, h2 = stock + dir * sp * 0.35f;
            if (l2 > h2) { var t = l2; l2 = h2; h2 = t; }
            Vector3 low = Vector3.zero; bool found = false;
            foreach (var p in pts)
            {
                float c = C(p, ax);
                if (c < l2 || c > h2) continue;
                if (!found || p.y < low.y) { low = p; found = true; }
            }
            var g = new Vector3((mn.x + mx.x) * 0.5f, low.y + 0.02f, (mn.z + mx.z) * 0.5f);
            if (ax == 0) g.x = C(low, 0);
            else if (ax == 1) g.y = low.y + 0.02f;
            else g.z = C(low, 2);
            return g;
        }

        static Texture2D LoadBaseColor(JsonValue doc, JsonValue views, byte[] bin)
        {
            int imgIndex = -1;
            try
            {
                var mats = doc["materials"];
                var texs = doc["textures"];
                var imgs = doc["images"];
                if (mats != null && mats.Count > 0 && texs != null && imgs != null)
                {
                    var pbr = mats[0]["pbrMetallicRoughness"];
                    if (pbr != null && pbr.Has("baseColorTexture"))
                    {
                        int ti = pbr["baseColorTexture"]["index"].AsInt(-1);
                        if (ti >= 0 && ti < texs.Count) imgIndex = texs[ti]["source"].AsInt(-1);
                    }
                }
            }
            catch { }

            var images = doc["images"];
            if (images != null && images.Count > 0)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int ii = attempt == 0 ? imgIndex : 0;
                    if (ii < 0 || ii >= images.Count) continue;
                    var im = images[ii];
                    if (!im.Has("bufferView")) continue;
                    int bv = im["bufferView"].AsInt(-1);
                    if (bv < 0 || bv >= views.Count) continue;
                    var v = views[bv];
                    int vo = v.Has("byteOffset") ? v["byteOffset"].AsInt(0) : 0;
                    int vl = v["byteLength"].AsInt(0);
                    if (vl <= 0 || vo + vl > bin.Length) continue;
                    var bytes = new byte[vl];
                    Buffer.BlockCopy(bin, vo, bytes, 0, vl);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))                     // 支持 PNG/JPEG ✓
                    {
                        tex.name = "glb_" + (im["name"].AsString("tex" + ii));
                        tex.wrapMode = TextureWrapMode.Clamp;
                        return tex;
                    }
                }
            }
            return null;
        }

        /// <summary>⭐ 法线图（glTF 的 `normalTexture` ✓）—— **按线性读** ✗（它是**数据**不是颜色 ✓，按 sRGB 读会偏 ✓）。
        /// <para>只取这一张 ✓；**不读 ORM** ✗（金属/光滑数值一律不碰 ✓—— 用户口径 ✓）。</para></summary>
        static Texture2D LoadNormal(JsonValue doc, JsonValue views, byte[] bin)
        {
            int imgIndex = -1;
            try
            {
                var mats = doc["materials"];
                var texs = doc["textures"];
                var imgs = doc["images"];
                if (mats != null && mats.Count > 0 && texs != null && imgs != null)
                {
                    var node = mats[0]["normalTexture"];
                    if (node != null && node.Has("index"))
                    {
                        int ti = node["index"].AsInt(-1);
                        if (ti >= 0 && ti < texs.Count) imgIndex = texs[ti]["source"].AsInt(-1);
                    }
                }
            }
            catch { }
            if (imgIndex < 0) return null;                       // ⚠️ **不回退到第 0 张** ✗（那是 baseColor ✓）

            var images = doc["images"];
            if (images == null || imgIndex >= images.Count) return null;
            var im = images[imgIndex];
            if (!im.Has("bufferView")) return null;
            int bv = im["bufferView"].AsInt(-1);
            if (bv < 0 || bv >= views.Count) return null;
            var v = views[bv];
            int vo = v.Has("byteOffset") ? v["byteOffset"].AsInt(0) : 0;
            int vl = v["byteLength"].AsInt(0);
            if (vl <= 0 || vo + vl > bin.Length) return null;
            var bytes = new byte[vl];
            Buffer.BlockCopy(bin, vo, bytes, 0, vl);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);    // ⭐ linear: true ✓
            if (tex.LoadImage(bytes))                                           // 支持 PNG/JPEG ✓
            {
                tex.name = "glb_" + (im["name"].AsString("normal"));
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            return null;
        }

        static List<float[]> ReadAccessor(JsonValue accessors, JsonValue views, byte[] bin, int ai, int dim)
        {
            var outp = new List<float[]>();
            if (ai < 0 || accessors == null || ai >= accessors.Count) return outp;
            var acc = accessors[ai];
            var bv = views[acc["bufferView"].AsInt(-1)];
            int bvOff = bv.Has("byteOffset") ? bv["byteOffset"].AsInt(0) : 0;
            int accOff = acc.Has("byteOffset") ? acc["byteOffset"].AsInt(0) : 0;
            int compType = acc["componentType"].AsInt(0);
            int count = acc["count"].AsInt(0);
            string type = acc["type"].AsString("VEC3");
            int comps = type == "VEC3" ? 3 : type == "VEC2" ? 2 : type == "VEC4" ? 4 : 1;
            int elem = compType == 5126 ? 4 : compType == 5123 ? 2 : compType == 5121 ? 1 : 4;
            int stride = bv.Has("byteStride") ? bv["byteStride"].AsInt(0) : elem * comps;
            int baseOff = bvOff + accOff;
            for (int i = 0; i < count; i++)
            {
                int p = baseOff + i * stride;
                var v = new float[dim];
                for (int c = 0; c < Math.Min(dim, comps); c++)
                {
                    int q = p + c * elem;
                    if (q + elem > bin.Length) break;
                    switch (compType)
                    {
                        case 5126: v[c] = BitConverter.ToSingle(bin, q); break;
                        case 5123: v[c] = BitConverter.ToUInt16(bin, q); break;
                        case 5121: v[c] = bin[q]; break;
                        case 5125: v[c] = BitConverter.ToUInt32(bin, q); break;
                    }
                }
                outp.Add(v);
            }
            return outp;
        }

        static List<int> ReadIndices(JsonValue accessors, JsonValue views, byte[] bin, int ai)
        {
            var outp = new List<int>();
            if (ai < 0 || accessors == null || ai >= accessors.Count) return outp;
            var acc = accessors[ai];
            var bv = views[acc["bufferView"].AsInt(-1)];
            int bvOff = bv.Has("byteOffset") ? bv["byteOffset"].AsInt(0) : 0;
            int accOff = acc.Has("byteOffset") ? acc["byteOffset"].AsInt(0) : 0;
            int compType = acc["componentType"].AsInt(0);
            int count = acc["count"].AsInt(0);
            int elem = compType == 5125 ? 4 : compType == 5123 ? 2 : 1;
            int baseOff = bvOff + accOff;
            for (int i = 0; i < count; i++)
            {
                int q = baseOff + i * elem;
                if (q + elem > bin.Length) break;
                outp.Add(compType == 5125 ? (int)BitConverter.ToUInt32(bin, q)
                        : compType == 5123 ? BitConverter.ToUInt16(bin, q) : bin[q]);
            }
            return outp;
        }
    }
}
