// GltfLoader —— 运行时读取 GLB（glTF 2.0 二进制），造出 Unity 的 Mesh + 贴图。
//
// 为什么能在运行时做：游戏是 Mono + Assembly.LoadFrom 加载 mod（不是 IL2CPP），于是
// `new Mesh()` / `Texture2D.LoadImage()` 这些标准 API 都可用 （实测过同类做法）。
//
// 坐标系（实测过的规则，不得改错）：
// glTF 右手系（+Y 上、-Z 前），于是 Unity 左手系（+Y 上、+Z 前）
// 于是 位置/法线 **取反 X** 三角面 **绕序反转** UV **v = 1 - v**
// 于是 朝向还有 180° 的差别（glTF 物件的"前"是 -Z），于是 由模型规格/对齐处理，不在这里转

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ModelKit
{
    public static class GltfLoader
    {
        internal sealed class RigNode
        {
            /// <summary>glTF 节点名与父节点下标。-1 = 场景根。</summary>
            public string Name;
            public int Parent = -1;
            /// <summary>glTF 原始局部矩阵。坐标转换由骨架创建者统一处理。</summary>
            public Matrix4x4 LocalMatrix = Matrix4x4.identity;
        }

        public sealed class Loaded
        {
            public Mesh Mesh;
            public Texture2D MainTexture;      // baseColor 贴图（可能为 null）
            public Texture2D NormalTexture;    // 法线（Tripo 的 `normalTexture` 可能为 null）
            public int VertexCount;
            public int TriangleCount;
            /// <summary>这个 GLB 带蒙皮吗（`skins` 非空且权重读到了）。角色替换要它 ✓</summary>
            public bool HasSkin;
            /// <summary>关节名（**按 skin 的 joints 顺序**）。顶点权重里的下标指的就是这个数组。</summary>
            public string[] JointNames = new string[0];
            /// <summary>每根关节的**逆绑定矩阵**（mesh 空间，glTF 原样）。取逆就是它在绑定姿势下的世界矩阵。</summary>
            public Matrix4x4[] InverseBind = new Matrix4x4[0];
            /// <summary>第一个 skin 的关节节点与完整父子层级。手臂 IK 用源骨架，不按名字猜层级。</summary>
            internal int[] JointNodes = new int[0];
            internal RigNode[] Nodes = new RigNode[0];
            /// <summary>每个顶点的骨权重（最多 4 根，只读 `JOINTS_0`/`WEIGHTS_0`）。
            /// 下标是 `JointNames` 的下标，**不是** Unity 的骨骼下标。
            /// <para>**限制**：不读 `JOINTS_1`/`WEIGHTS_1`（多组关节）。Tripo 一模型一骨架一组合格。</para></summary>
            public BoneWeight[] Weights = new BoneWeight[0];
            /// <summary>加载期对**顶点**做过的变换（朝向旋转 + 握把归零位移）：`v_读入 → VertexTransform · v_读入`。
            /// <para>**带蒙皮的模型做重定向时必须带上它** —— 否则顶点在一个坐标系、骨架在另一个坐标系，
            /// 角色会歪掉或飘走（实测踩过）。恒等 = 没动过顶点。</para></summary>
            public Matrix4x4 VertexTransform = Matrix4x4.identity;
            public string Report = "";
        }

        /// <param name="zeroGrip"> 要不得做“**握把归零**” —— 默认 `true` （枪的行为不变）。
        /// <para> 近战（刀/铲…）要传 **`false`**：`GuessGrip` 的假设是**枪**（“枪口 +Z ⇒ 枪托在 −Z”，
        /// 且 x/z 取包围盒**中心**）—— 对弯刀/爪刀会把模型**横向挪偏**。
        /// 跳过后就用模型**自己的原点** （近战按“刃朝上 · 柄朝下”建模 再按包围盒中心对齐）。</para>
        public static Loaded LoadFile(string path, string front = "auto", bool zeroGrip = true) => Load(File.ReadAllBytes(path), front, zeroGrip);

        public static Loaded Load(byte[] data, string front = "auto", bool zeroGrip = true)
        {
            var r = new Loaded();
            if (data.Length < 20 || data[0] != 'g' || data[1] != 'l' || data[2] != 'T' || data[3] != 'F')
                throw new Exception("not a GLB file (wrong magic)");

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
            if (json == null || bin == null) throw new Exception("the GLB has no JSON or BIN chunk");

            var doc = Json.Parse(json);
            var accessors = doc["accessors"];
            var views = doc["bufferViews"];
            var meshes = doc["meshes"];
            if (meshes == null || meshes.Count == 0) throw new Exception("the glTF has no meshes");

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var weights = new List<BoneWeight>();

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
                        verts.Add(new Vector3(-pos[i][0], pos[i][1], pos[i][2]));           // X 取反（右手，于是 左手）
                        norms.Add(nrm != null ? new Vector3(-nrm[i][0], nrm[i][1], nrm[i][2]) : Vector3.up);
                        uvs.Add(uv != null ? new Vector2(uv[i][0], 1f - uv[i][1]) : Vector2.zero);   // V 翻转
                    }
                    // 蒙皮权重：glTF 每个顶点最多 4 根骨（只读第 0 组 ✓；JOINTS_1 这类多组不处理）
                    var jnt = attrs.Has("JOINTS_0") ? ReadAccessor(accessors, views, bin, attrs["JOINTS_0"].AsInt(-1), 4) : null;
                    var wgt = attrs.Has("WEIGHTS_0") ? ReadAccessor(accessors, views, bin, attrs["WEIGHTS_0"].AsInt(-1), 4) : null;
                    for (int i = 0; i < pos.Count; i++)
                    {
                        var bw = new BoneWeight();
                        if (jnt != null && wgt != null && i < jnt.Count && i < wgt.Count)
                        {
                            bw.boneIndex0 = (int)jnt[i][0]; bw.weight0 = wgt[i][0];
                            bw.boneIndex1 = (int)jnt[i][1]; bw.weight1 = wgt[i][1];
                            bw.boneIndex2 = (int)jnt[i][2]; bw.weight2 = wgt[i][2];
                            bw.boneIndex3 = (int)jnt[i][3]; bw.weight3 = wgt[i][3];
                        }
                        weights.Add(bw);
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

            // ① 朝向：**不在这里猜**
            // 实测（2026-10-04）：Tripo 的模型朝向由**提示词**决定 —— 提示词写
            // "the muzzle points to the left"，于是 枪口落在 **+Z**（= Unity 前向）；
            // 写 "to the right"，于是 落在 −Z。所以**朝向在生成阶段就定好**
            // 用户自己给的 GLB 没有这个保证，于是 用 mod 的 `config.json` 的 "front" 声明（下面会转）

            Quaternion vrot;
            ApplyFrontDeclaration(verts, norms, front, out vrot);

            // ② 握把归零（枪：枪口朝 +Z，于是 枪托在 −Z；枪托端起 8%~35% 区间的最低点 = 握把）
            // 近战传 zeroGrip:false 跳过 （理由见 LoadFile 的参数说明）
            var grip = zeroGrip ? GuessGrip(verts) : Vector3.zero;
            if (grip != Vector3.zero) for (int i = 0; i < verts.Count; i++) verts[i] -= grip;
            // 顶点净变换 = 先绕原点转 vrot，再平移 −grip ⇒ T(−grip)·R(vrot)。带蒙皮时骨架要用同一个 ✗ 否则对不上
            r.VertexTransform = Matrix4x4.TRS(-grip, vrot, Vector3.one);

            var mesh = new Mesh { name = "glb_model" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // **切线必须算** —— Unity 的 `_BumpMap`（法线贴图）**依赖 tangents**；
            // 缺了它 Unity 用退化值 ⇒ 表面出现**黑斑 / 破碎 / “裂缝”** （实测：刀在游戏里就是这个）
            // 而 three.js 查看器自己会算 ⇒ 同模型在那里是正常的 （交叉验证）
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            r.Mesh = mesh; r.VertexCount = verts.Count; r.TriangleCount = tris.Count / 3;

            // ①b 蒙皮：`skins[0]` 的 joints 名字 + 逆绑定矩阵（权重上面跟着顶点读好了 ✓）
            //     只处理第一个 skin ✓ —— Tripo 一个模型只有一个骨架 ✓
            try
            {
                var skins = doc["skins"];
                var nodes = doc["nodes"];
                if (skins != null && skins.Count > 0 && nodes != null)
                {
                    var skin0 = skins[0];
                    var joints = skin0["joints"];
                    int n = joints?.Count ?? 0;
                    if (n > 0)
                    {
                        var names = new string[n];
                        var jointNodes = new int[n];
                        for (int i = 0; i < n; i++)
                        {
                            int ni = joints[i].AsInt(-1);
                            jointNodes[i] = ni;
                            names[i] = (ni >= 0 && ni < nodes.Count) ? nodes[ni]["name"].AsString("joint" + ni) : "joint" + i;
                        }
                        var rigNodes = new RigNode[nodes.Count];
                        for (int i = 0; i < nodes.Count; i++) rigNodes[i] = ReadRigNode(nodes[i], i);
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            var children = nodes[i]["children"];
                            for (int c = 0; c < (children?.Count ?? 0); c++)
                            {
                                int child = children[c].AsInt(-1);
                                if (child >= 0 && child < rigNodes.Length) rigNodes[child].Parent = i;
                            }
                        }
                        var inv = new List<Matrix4x4>();
                        int ibm = skin0.Has("inverseBindMatrices") ? skin0["inverseBindMatrices"].AsInt(-1) : -1;
                        if (ibm >= 0)
                        {
                            foreach (var r16 in ReadAccessor(accessors, views, bin, ibm, 16))
                            {
                                // glTF 是**列主序**：前 4 个数是第 0 列 ⇒ 填到 m00 m10 m20 m30
                                var m = new Matrix4x4();
                                m.m00 = r16[0]; m.m10 = r16[1]; m.m20 = r16[2]; m.m30 = r16[3];
                                m.m01 = r16[4]; m.m11 = r16[5]; m.m21 = r16[6]; m.m31 = r16[7];
                                m.m02 = r16[8]; m.m12 = r16[9]; m.m22 = r16[10]; m.m32 = r16[11];
                                m.m03 = r16[12]; m.m13 = r16[13]; m.m23 = r16[14]; m.m33 = r16[15];
                                inv.Add(m);
                            }
                        }
                        r.JointNames = names;
                        r.JointNodes = jointNodes;
                        r.Nodes = rigNodes;
                        r.InverseBind = inv.ToArray();
                        r.Weights = weights.ToArray();
                        r.HasSkin = inv.Count == n && weights.Count == verts.Count
                            && jointNodes.Length == n && rigNodes.Length == nodes.Count;
                    }
                }
            }
            catch { r.HasSkin = false; }

            // ② 贴图：优先材质上的 baseColorTexture，否则第一张
            r.MainTexture = LoadBaseColor(doc, views, bin);
            // 法线 —— 只读这一张 （**不读 ORM**：金属/光滑一律不碰）
            r.NormalTexture = LoadNormal(doc, views, bin);
            // 逆绑定矩阵的平移量：**非零**才说明 MAT4 真读进来了（全零 = 解析又坏了 ✗）
            string ibHint = "";
            if (r.HasSkin && r.InverseBind.Length > 0)
            {
                var t0 = new Vector3(r.InverseBind[0].m03, r.InverseBind[0].m13, r.InverseBind[0].m23);
                ibHint = $"inverseBind[0].t=({t0.x:0.###},{t0.y:0.###},{t0.z:0.###})";
            }
            string vtHint = "";
            if (r.VertexTransform != Matrix4x4.identity)
            {
                var vt = r.VertexTransform;
                vtHint = $" [vertex transform: t=({vt.m03:0.###},{vt.m13:0.###},{vt.m23:0.###}) - a rigged model must be loaded with front=auto, zeroGrip:false]";
            }
            r.Report = (r.HasSkin ? $"skin {r.JointNames.Length} joints {ibHint}, " : "") + $"GLB: vertices {r.VertexCount}, triangles {r.TriangleCount}, texture {(r.MainTexture != null ? r.MainTexture.name + $" {r.MainTexture.width}x{r.MainTexture.height}" : "none")}, normal map {(r.NormalTexture != null ? "yes" : "no")}, bounds {mesh.bounds.size}, grip zeroed {grip}{vtHint}";
            return r;
        }

        static RigNode ReadRigNode(JsonValue node, int index)
        {
            var r = new RigNode { Name = node.GetStr("name", "node" + index) };
            var matrix = node["matrix"];
            if (matrix != null && matrix.Count == 16)
            {
                var m = new Matrix4x4();
                for (int col = 0; col < 4; col++)
                    for (int row = 0; row < 4; row++) m[row, col] = matrix[col * 4 + row].AsFloat(0f);
                r.LocalMatrix = m;
                return r;
            }
            var t = node["translation"];
            var q = node["rotation"];
            var s = node["scale"];
            var position = t != null && t.Count == 3
                ? new Vector3(t[0].AsFloat(0f), t[1].AsFloat(0f), t[2].AsFloat(0f)) : Vector3.zero;
            var rotation = q != null && q.Count == 4
                ? new Quaternion(q[0].AsFloat(0f), q[1].AsFloat(0f), q[2].AsFloat(0f), q[3].AsFloat(1f)) : Quaternion.identity;
            var scale = s != null && s.Count == 3
                ? new Vector3(s[0].AsFloat(1f), s[1].AsFloat(1f), s[2].AsFloat(1f)) : Vector3.one;
            r.LocalMatrix = Matrix4x4.TRS(position, rotation, scale);
            return r;
        }

        /// <summary>按声明旋转（用户自带的 GLB 用）：front = "auto"（不转）/"-z"/"+x"/"-x"，于是 把枪口转到 +Z </summary>
        public static void ApplyFrontDeclaration(List<Vector3> verts, List<Vector3> norms, string front)
        {
            Quaternion ignored;
            ApplyFrontDeclaration(verts, norms, front, out ignored);
        }

        /// <summary>同上，另交出**实际用过的旋转**（重定向要拿它对骨架做同一个变换）。</summary>
        public static void ApplyFrontDeclaration(List<Vector3> verts, List<Vector3> norms, string front, out Quaternion rot)
        {
            rot = Quaternion.identity;
            if (string.IsNullOrEmpty(front) || front == "auto" || front == "+z") return;
            // 近战专用 "up"：把**最长轴**转到 **+Y**（立起来）—— 刀 = 刃朝上 · 柄朝下
            // 其余 ±x/±z 都是**绕 Y**转，对“躺着的刀”不管用 （实测：Tripo 出的菜刀长轴在 X）
            if (front == "up")
            {
                int ax = LongAxis(verts);
                UnityEngine.Quaternion q =
                      ax == 1 ? UnityEngine.Quaternion.identity                       // 已经是 Y
                    : ax == 0 ? UnityEngine.Quaternion.Euler(0f, 0f, 90f)             // X，于是 +Y （-90 会到 -Y 倒过来）
                              : UnityEngine.Quaternion.Euler(-90f, 0f, 0f);           // Z，于是 Y
                for (int i = 0; i < verts.Count; i++) { verts[i] = q * verts[i]; norms[i] = q * norms[i]; }
                rot = q;
                // **不得**再绕 Y 转 —— 试过（把厚度轴变 Z ⇒ 刀面**正对镜头**）但那是给“出图”的要求；
                // 真实握刀是**刀面与视线平行** ⇒ 厚度轴保持 X （实测预测 [0.10, 1.00, 0.57]）。
                return;
            }
            foreach (var v0 in new[] { verts })
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var v = verts[i]; var n = norms[i]; Vector3 nv; Vector3 nn;
                    // 这三支都是**绕 Y 的纯旋转**（行列式 +1）⇒ 用四元数记下来给骨架用
                    switch (front)
                    {
                        case "-z": nv = new Vector3(-v.x, v.y, -v.z); nn = new Vector3(-n.x, n.y, -n.z); rot = Quaternion.Euler(0f, 180f, 0f); break;
                        case "+x": nv = new Vector3(-v.z, v.y, v.x); nn = new Vector3(-n.z, n.y, n.x); rot = Quaternion.Euler(0f, -90f, 0f); break;
                        case "-x": nv = new Vector3(v.z, v.y, -v.x); nn = new Vector3(n.z, n.y, -n.x); rot = Quaternion.Euler(0f, 90f, 0f); break;
                        default: nv = v; nn = n; break;
                    }
                    verts[i] = nv; norms[i] = nn;
                }
                break;
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
            // 朝向由提示词保证（枪口 +Z），于是 枪托在 −Z 不需要再判定
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
                    if (tex.LoadImage(bytes))                     // 支持 PNG/JPEG
                    {
                        tex.name = "glb_" + (im["name"].AsString("tex" + ii));
                        tex.wrapMode = TextureWrapMode.Clamp;
                        return tex;
                    }
                }
            }
            return null;
        }

        /// <summary> 法线图（glTF 的 `normalTexture`）—— **按线性读** （它是**数据**不是颜色，按 sRGB 读会偏）。
        /// <para>只取这一张；**不读 ORM** （金属/光滑数值一律不碰 —— 用户口径）。</para></summary>
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
            if (imgIndex < 0) return null;                       // **不回退到第 0 张** （那是 baseColor）

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
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);    // linear: true
            if (tex.LoadImage(bytes))                                           // 支持 PNG/JPEG
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
            // `normalized: true` 的整数分量要除满量程（ubyte 权重 0..255 ⇒ 0..1）。
            // 漏了它：权重变成 0..255 ⇒ 顶点被算飞（实测踩过）。
            bool norm = acc.Has("normalized") && acc["normalized"].AsInt(0) != 0;
            string type = acc["type"].AsString("VEC3");
            // ⚠️ MAT4 是 **16 个分量**（逆绑定矩阵就是它）。漏了它只读 1 个 float ⇒ 矩阵全是垃圾 ✗
            // 实测踩过：角色替换时顶点被算飞、模型在空中只剩几片碎片。
            int comps = type == "MAT4" ? 16 : type == "VEC4" ? 4 : type == "VEC3" ? 3 : type == "VEC2" ? 2 : 1;
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
                        case 5123: { ushort u = BitConverter.ToUInt16(bin, q); v[c] = norm ? u / 65535f : u; break; }
                        case 5121: v[c] = norm ? bin[q] / 255f : bin[q]; break;
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
