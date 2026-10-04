// Gltf.cs —— 极简 GLB（glTF 2.0 二进制）读取：只取我们需要的
//   meshes[].primitives[] 的 POSITION / NORMAL / TEXCOORD_0 / indices
//
// 坐标系转换（重要）：
//   glTF 是**右手系**（+Y 上、-Z 前），Unity 是**左手系**（+Y 上、+Z 前）
//   → 位置/法线 **取反 X**，并把三角面**绕序反转**（否则整模型是背面，看不见 —— 这个坑我们在体素模型上踩过）
//   → UV：glTF 原点在左上，Unity 在左下 → **v = 1 - v**

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BundlePack
{
    public sealed class GltfMesh
    {
        public List<float[]> Positions = new List<float[]>();   // 3
        public List<float[]> Normals = new List<float[]>();     // 3
        public List<float[]> Uvs = new List<float[]>();         // 2
        public List<int> Indices = new List<int>();             // 三角面
        public int TriangleCount => Indices.Count / 3;
    }

    public static class Gltf
    {
        public static GltfMesh ReadGlb(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 20 || data[0] != 'g' || data[1] != 'l' || data[2] != 'T' || data[3] != 'F')
                throw new Exception("不是 GLB 文件（magic 不对）");

            int off = 12;
            string json = null; byte[] bin = null;
            while (off + 8 <= data.Length)
            {
                uint len = BitConverter.ToUInt32(data, off);
                uint type = BitConverter.ToUInt32(data, off + 4);
                int start = off + 8;
                if (type == 0x4E4F534A) json = Encoding.UTF8.GetString(data, start, (int)len);       // JSON
                else if (type == 0x004E4942) bin = Sub(data, start, (int)len);                        // BIN
                off = start + (int)len;
            }
            if (json == null || bin == null) throw new Exception("GLB 缺少 JSON 或 BIN chunk");

            var doc = MiniJson.Parse(json);
            var mesh = new GltfMesh();
            var meshes = doc.Arr("meshes");
            if (meshes.Count == 0) throw new Exception("glTF 没有 meshes");
            var accessors = doc.Arr("accessors");
            var views = doc.Arr("bufferViews");

            foreach (var m in meshes)
            {
                foreach (var prim in m.Arr("primitives"))
                {
                    int baseVertex = mesh.Positions.Count;
                    var attrs = prim.Obj("attributes");
                    var pos = ReadAccessor(accessors, views, bin, attrs.Int("POSITION"), 3);
                    var nrm = attrs.Has("NORMAL") ? ReadAccessor(accessors, views, bin, attrs.Int("NORMAL"), 3) : null;
                    var uv = attrs.Has("TEXCOORD_0") ? ReadAccessor(accessors, views, bin, attrs.Int("TEXCOORD_0"), 2) : null;
                    for (int i = 0; i < pos.Count; i++)
                    {
                        var p = pos[i];
                        // 右手 → 左手：X 取反
                        mesh.Positions.Add(new[] { -p[0], p[1], p[2] });
                        mesh.Normals.Add(nrm != null ? new[] { -nrm[i][0], nrm[i][1], nrm[i][2] } : new[] { 0f, 1f, 0f });
                        // UV：V 翻转
                        mesh.Uvs.Add(uv != null ? new[] { uv[i][0], 1f - uv[i][1] } : new[] { 0f, 0f });
                    }
                    var idx = ReadIndices(accessors, views, bin, prim.Has("indices") ? prim.Int("indices") : -1);
                    // 绕序反转（左手系）
                    for (int t = 0; t + 2 < idx.Count; t += 3)
                    {
                        mesh.Indices.Add(idx[t] + baseVertex);
                        mesh.Indices.Add(idx[t + 2] + baseVertex);
                        mesh.Indices.Add(idx[t + 1] + baseVertex);
                    }
                }
            }
            return mesh;
        }

        static byte[] Sub(byte[] src, int off, int len)
        {
            var d = new byte[len]; Buffer.BlockCopy(src, off, d, 0, len); return d;
        }

        static List<float[]> ReadAccessor(List<MiniJson.J> accessors, List<MiniJson.J> views, byte[] bin, int accessorIndex, int dim)
        {
            var outp = new List<float[]>();
            if (accessorIndex < 0) return outp;
            var acc = accessors[accessorIndex];
            var bv = views[acc.Int("bufferView")];
            int bvOff = bv.Has("byteOffset") ? bv.Int("byteOffset") : 0;
            int accOff = acc.Has("byteOffset") ? acc.Int("byteOffset") : 0;
            int compType = acc.Int("componentType");
            int count = acc.Int("count");
            string type = acc.Str("type");
            int comps = type == "VEC3" ? 3 : type == "VEC2" ? 2 : type == "VEC4" ? 4 : 1;
            int elemSize = compType == 5126 ? 4 : compType == 5123 ? 2 : compType == 5121 ? 1 : 4;
            int stride = bv.Has("byteStride") ? bv.Int("byteStride") : elemSize * comps;
            int baseOff = bvOff + accOff;
            for (int i = 0; i < count; i++)
            {
                int p = baseOff + i * stride;
                var v = new float[dim];
                for (int c = 0; c < Math.Min(dim, comps); c++)
                {
                    int q = p + c * elemSize;
                    switch (compType)
                    {
                        case 5126: v[c] = BitConverter.ToSingle(bin, q); break;
                        case 5123: v[c] = BitConverter.ToUInt16(bin, q); break;
                        case 5121: v[c] = bin[q]; break;
                        case 5125: v[c] = BitConverter.ToUInt32(bin, q); break;
                        default: v[c] = 0f; break;
                    }
                }
                outp.Add(v);
            }
            return outp;
        }

        static List<int> ReadIndices(List<MiniJson.J> accessors, List<MiniJson.J> views, byte[] bin, int accessorIndex)
        {
            var outp = new List<int>();
            if (accessorIndex < 0) return outp;
            var acc = accessors[accessorIndex];
            var bv = views[acc.Int("bufferView")];
            int bvOff = bv.Has("byteOffset") ? bv.Int("byteOffset") : 0;
            int accOff = acc.Has("byteOffset") ? acc.Int("byteOffset") : 0;
            int compType = acc.Int("componentType");
            int count = acc.Int("count");
            int elemSize = compType == 5125 ? 4 : compType == 5123 ? 2 : 1;
            int baseOff = bvOff + accOff;
            for (int i = 0; i < count; i++)
            {
                int q = baseOff + i * elemSize;
                outp.Add(compType == 5125 ? (int)BitConverter.ToUInt32(bin, q)
                       : compType == 5123 ? BitConverter.ToUInt16(bin, q)
                       : bin[q]);
            }
            return outp;
        }

        /// <summary>float → IEEE half（Unity 法线常用）</summary>
        public static ushort ToHalf(float f)
        {
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
            int sign = (bits >> 16) & 0x8000;
            int val = (bits & 0x7FFFFFFF);
            if (val >= 0x477FF000) return (ushort)(sign | 0x7C00);        // 溢出/无穷
            if (val < 0x33000000) return (ushort)sign;                     // 太小 → 0
            int exp = val >> 23;
            int man = val & 0x7FFFFF;
            int newExp = exp - 127 + 15;
            if (newExp <= 0) return (ushort)sign;
            if (newExp >= 31) return (ushort)(sign | 0x7C00);
            return (ushort)(sign | (newExp << 10) | (man >> 13));
        }
    }

    /// <summary>够用的 JSON 解析（对象/数组/字符串/数字/布尔/null）</summary>
    public static class MiniJson
    {
        public sealed class J
        {
            public object V;
            public bool IsObj => V is Dictionary<string, J>;
            public List<J> AsArr => V as List<J> ?? new List<J>();
            public bool Has(string k) => V is Dictionary<string, J> d && d.ContainsKey(k);
            public J Get(string k) => V is Dictionary<string, J> d && d.ContainsKey(k) ? d[k] : null;
            public List<J> Arr(string k) { var j = Get(k); return j != null ? j.AsArr : new List<J>(); }
            public J Obj(string k) => Get(k);
            public int Int(string k) { var j = Get(k); return j != null && j.V != null ? Convert.ToInt32(j.V) : -1; }
            public string Str(string k) { var j = Get(k); return j != null ? j.V as string : null; }
            public override string ToString() => V == null ? "null" : V.ToString();
        }

        public static J Parse(string s) { int i = 0; var j = Value(s, ref i); return j; }

        static void Skip(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static J Value(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) return new J();
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return new J { V = Str(s, ref i) };
            if (s.Length - i >= 4 && s.Substring(i, 4) == "true") { i += 4; return new J { V = true }; }
            if (s.Length - i >= 5 && s.Substring(i, 5) == "false") { i += 5; return new J { V = false }; }
            if (s.Length - i >= 4 && s.Substring(i, 4) == "null") { i += 4; return new J(); }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double d;
            double.TryParse(s.Substring(st, i - st), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
            return new J { V = d };
        }

        static J Obj(string s, ref int i)
        {
            var d = new Dictionary<string, J>();
            i++; Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return new J { V = d }; }
            while (i < s.Length)
            {
                Skip(s, ref i);
                string k = Str(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                d[k] = Value(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
            }
            return new J { V = d };
        }

        static J Arr(string s, ref int i)
        {
            var l = new List<J>();
            i++; Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return new J { V = l }; }
            while (i < s.Length)
            {
                l.Add(Value(s, ref i));
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
            }
            return new J { V = l };
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;                                   // 开引号
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    char e = s[i];
                    sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'u'
                        ? (char)Convert.ToInt32(s.Substring(i + 1, 4), 16) : e);
                    if (e == 'u') i += 4;
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;                                   // 闭引号
            return sb.ToString();
        }
    }
}
