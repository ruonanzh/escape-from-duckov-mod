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
            public int VertexCount;
            public int TriangleCount;
            public string Report = "";
        }

        public static Loaded LoadFile(string path) => Load(File.ReadAllBytes(path));

        public static Loaded Load(byte[] data)
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

            // 握把归零（同打包器那套规则：最长轴=枪管轴；细端=枪口；枪托端起 8%~35% 区间最低点=握把）
            var grip = GuessGrip(verts);
            for (int i = 0; i < verts.Count; i++) verts[i] -= grip;

            var mesh = new Mesh { name = "glb_model" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            r.Mesh = mesh; r.VertexCount = verts.Count; r.TriangleCount = tris.Count / 3;

            // ② 贴图：优先材质上的 baseColorTexture，否则第一张
            r.MainTexture = LoadBaseColor(doc, views, bin);
            r.Report = $"GLB：顶点 {r.VertexCount}，三角面 {r.TriangleCount}，贴图 {(r.MainTexture != null ? r.MainTexture.name + $" {r.MainTexture.width}x{r.MainTexture.height}" : "无")}；包围盒 {mesh.bounds.size}；握把归零 {grip}";
            return r;
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
            float Lo1 = ax == 0 ? mn.x : ax == 1 ? mn.y : mn.z, Hi1 = ax == 0 ? mx.x : ax == 1 ? mx.y : mx.z;
            Func<float, float, double> area = (lo, hi) =>
            {
                double a1 = 0, a2 = 0; bool any = false;
                float min1 = float.MaxValue, max1 = float.MinValue, min2 = float.MaxValue, max2 = float.MinValue;
                foreach (var p in pts)
                {
                    float c = C(p, ax);
                    if (c < lo || c > hi) continue;
                    any = true;
                    float v1 = C(p, o1), v2 = C(p, o2);
                    if (v1 < min1) min1 = v1; if (v1 > max1) max1 = v1;
                    if (v2 < min2) min2 = v2; if (v2 > max2) max2 = v2;
                }
                if (!any) return double.MaxValue;
                a1 = max1 - min1; a2 = max2 - min2; return a1 * a2;
            };
            float sp = Hi1 - Lo1;
            double aLo = area(Lo1, Lo1 + sp * 0.06f), aHi = area(Hi1 - sp * 0.06f, Hi1);
            bool muzzleAtMin = aLo < aHi;
            float stock = muzzleAtMin ? Hi1 : Lo1;
            float dir = muzzleAtMin ? -1f : 1f;
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
