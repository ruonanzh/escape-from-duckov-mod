// MeshKit —— 几何核心：把"零件清单"变成网格数据（顶点 / 索引 / UV / 法线 / submesh + 图集排布）。
//
// 约束：**不依赖 UnityEngine**（只用 System / System.Collections.Generic）。
// 同一份源码既编进离线工具（校验、以后可做线框预览），也编进 mod 的运行时库 ——
// 校验算出来的东西必须与游戏里生成的完全一致，所以只能有一份实现。
//
// 规范见 docs/unity-3d/05-model-format.md：
//   · box-UV：每个零件的展开矩形 = (2·d + 2·w) × (d + h) 像素
//   · 排布：按 parts 顺序、从左到右、满行换行（行高 = 该行最高矩形），矩形间留 Margin 像素
//   · 面落点：top/bottom 在上一行，right/front/left/back 在下一行（标准摆法）
//   · 作者不写 UV；贴图生成器用同一份排布表往矩形里画

using System;
using System.Collections.Generic;

namespace ModelKit
{
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public float Length() => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vec3 Normalized() { var l = Length(); return l <= 1e-9f ? new Vec3(0, 0, 0) : new Vec3(X / l, Y / l, Z / l); }
        public override string ToString() => $"({X:0.####},{Y:0.####},{Z:0.####})";
    }

    public struct Vec2
    {
        public float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }
    }

    /// <summary>图像空间的一块矩形（像素，左上角为原点，y 向下）。</summary>
    public struct Rect
    {
        public int X, Y, W, H;
        public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
    }

    public sealed class PartSpec
    {
        public string Role = "";
        public string Shape = "";
        public float[] Size;
        public float R, R1, R2, H, Depth;
        public int Segments = 12;
        public int Rings = 0;
        public float[][] Profile;
        public float[][] Outline;
        public float[] At = { 0, 0, 0 };
        public float[] Rot = { 0, 0, 0 };
        public float[] Scale = { 1, 1, 1 };
        public string Mirror;

        /// <summary>零件外接尺寸（米）—— 决定它在贴图里的展开矩形。</summary>
        public Vec3 Dims()
        {
            switch (Shape)
            {
                case "box": return new Vec3(Size[0], Size[1], Size[2]);
                case "cylinder": return new Vec3(2 * R, H, 2 * R);
                case "cone": { float r = Math.Max(R1, R2); return new Vec3(2 * r, H, 2 * r); }
                case "sphere": return new Vec3(2 * R, 2 * R, 2 * R);
                case "lathe":
                {
                    float rmax = 0, ymin = float.MaxValue, ymax = float.MinValue;
                    foreach (var p in Profile) { rmax = Math.Max(rmax, p[0]); ymin = Math.Min(ymin, p[1]); ymax = Math.Max(ymax, p[1]); }
                    return new Vec3(2 * rmax, ymax - ymin, 2 * rmax);
                }
                case "extrude":
                {
                    float xmin = float.MaxValue, xmax = float.MinValue, ymin = float.MaxValue, ymax = float.MinValue;
                    foreach (var p in Outline) { xmin = Math.Min(xmin, p[0]); xmax = Math.Max(xmax, p[0]); ymin = Math.Min(ymin, p[1]); ymax = Math.Max(ymax, p[1]); }
                    return new Vec3(xmax - xmin, ymax - ymin, Depth);
                }
                default: throw new ModelKitException($"unsupported shape '{Shape}' (role={Role})");
            }
        }
    }

    public sealed class TextureSpec
    {
        public int Size = 512;
        public float PixelsPerMeter = 0;      // 0 = 自动
        public string Source = "generated";   // "generated" | "file"
        public string File;
        public Dictionary<string, string> Fills = new Dictionary<string, string>();
        public string DefaultFill = "#6A7076";
    }

    public sealed class ModelSpec
    {
        public string Name = "";
        public string Category = "";
        public string Summary = "";
        /// <summary>模型原点相对几何中心的偏移（米）：声明"游戏放置点"在哪（武器 = 握把）。
        /// 由模型文件声明、构建时一次性平移，**运行时不缩放**。</summary>
        public float[] PivotOffset = { 0, 0, 0 };

        /// <summary>配件槽位的**挂点位置**（米，模型自身坐标系）：槽位名 → [x,y,z]（或 [x,y,z,rx,ry,rz]）。
        /// 槽位名是游戏定的：`Scope` / `Tec` / `Muzzle` / `Stock` / `Grip`。
        /// 换枪模型后，游戏靠原模型里 `ShowIf_&lt;槽位&gt;` 零件的 Transform 摆放配件 —— 声明这里就能把它们挪到我们枪的对应部位。</summary>
        public Dictionary<string, float[]> Slots = new Dictionary<string, float[]>();
        public List<PartSpec> Parts = new List<PartSpec>();
        public TextureSpec Texture = new TextureSpec();
    }

    /// <summary>零件在贴图里的展开矩形（图像空间像素）。</summary>
    public sealed class AtlasRect
    {
        public string Role;
        public int X, Y, W, H;      // 展开矩形
        public int PxW, PxH, PxD;   // 零件尺寸 → 像素
    }

    public sealed class SubMesh
    {
        public string Role;
        public int Start, Count;    // Indices 里的区间
    }

    /// <summary>模型坐标系里的轴对齐包围盒。</summary>
    public struct Box3
    {
        public Vec3 Min, Max;
        public Box3(Vec3 min, Vec3 max) { Min = min; Max = max; }
        public Vec3 Size => new Vec3(Max.X - Min.X, Max.Y - Min.Y, Max.Z - Min.Z);
        public Vec3 Center => new Vec3((Min.X + Max.X) * 0.5f, (Min.Y + Max.Y) * 0.5f, (Min.Z + Max.Z) * 0.5f);
    }

    public sealed class MeshData
    {
        public List<Vec3> Positions = new List<Vec3>();
        public List<Vec3> Normals = new List<Vec3>();
        public List<Vec2> Uvs = new List<Vec2>();
        public List<int> Indices = new List<int>();
        public List<SubMesh> SubMeshes = new List<SubMesh>();
        public List<AtlasRect> Atlas = new List<AtlasRect>();
        public List<PartSpec> Emitted;      // 与 Atlas 同序（已展开 mirror）
        public List<Box3> EmittedBounds = new List<Box3>();   // 与 Emitted 同序：每个零件在**模型坐标系**里的包围盒

        /// <summary>按 role（前缀匹配）取这些零件合并后的包围盒；没有该 role 时返回 false。
        /// 用来按**语义零件**（barrel / receiver / stock…）算挂点，而不是拿整模型的 AABB 当边缘。</summary>
        public bool TryBoundsOfRole(string rolePrefix, out Box3 bounds)
        {
            var mn = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;
            for (int i = 0; i < Emitted.Count && i < EmittedBounds.Count; i++)
            {
                if (Emitted[i].Role == null || !Emitted[i].Role.StartsWith(rolePrefix)) continue;
                var b = EmittedBounds[i];
                mn = new Vec3(Math.Min(mn.X, b.Min.X), Math.Min(mn.Y, b.Min.Y), Math.Min(mn.Z, b.Min.Z));
                mx = new Vec3(Math.Max(mx.X, b.Max.X), Math.Max(mx.Y, b.Max.Y), Math.Max(mx.Z, b.Max.Z));
                any = true;
            }
            bounds = any ? new Box3(mn, mx) : new Box3(new Vec3(0, 0, 0), new Vec3(0, 0, 0));
            return any;
        }

        /// <summary>按**语义零件**给某槽位一个默认位置（模型坐标系）：枪口取 `barrel` 前端、顶部取 `receiver` 顶面…
        /// 而不是拿整模型的 AABB 当边缘（那会把"后照门最高点"当导轨面）。找不到对应零件时返回 false。</summary>
        public bool TryGuessSlot(string slot, out Vec3 p)
        {
            p = new Vec3(0, 0, 0);
            if (slot == "Muzzle" && TryBoundsOfRole("barrel", out var b))
            { p = new Vec3(b.Center.X, b.Center.Y, b.Max.Z); return true; }
            if ((slot == "Scope" || slot == "Tec" || slot == "Grip") && TryBoundsOfRole("receiver", out var r))
            {
                p = slot == "Scope" ? new Vec3(r.Center.X, r.Max.Y, r.Center.Z + 0.02f)
                  : slot == "Tec" ? new Vec3(r.Center.X, r.Max.Y, r.Min.Z + 0.03f)
                  : new Vec3(r.Center.X, r.Min.Y, r.Max.Z - 0.04f);
                return true;
            }
            if (slot == "Stock" && TryBoundsOfRole("stock", out var st))
            { p = new Vec3(st.Center.X, st.Center.Y, st.Min.Z); return true; }
            return false;
        }

        public int AtlasSize;
        public float PixelsPerMeter;
        public Vec3 Min, Max;
        public int VertexCount => Positions.Count;
        public int TriangleCount => Indices.Count / 3;
    }

    public sealed class ModelKitException : Exception
    {
        public ModelKitException(string msg) : base(msg) { }
    }

    public static class MeshKit
    {
        public const int Margin = 1;

        // ── 入口 ────────────────────────────────────────────────────────────────

        public static MeshData Build(ModelSpec model)
        {
            if (model.Parts.Count == 0) throw new ModelKitException("parts is empty");

            var parts = ExpandMirrors(model.Parts);
            int atlas = model.Texture.Size;
            float ppm = model.Texture.PixelsPerMeter > 0
                ? model.Texture.PixelsPerMeter
                : AutoDensity(parts, atlas);
            if (ppm <= 0) throw new ModelKitException($"parts do not fit in a {atlas}x{atlas} texture (auto density reached 0)");

            var rects = Pack(parts, atlas, ppm);

            var data = new MeshData { AtlasSize = atlas, PixelsPerMeter = ppm, Atlas = rects, Emitted = parts };
            for (int i = 0; i < parts.Count; i++) data.EmittedBounds.Add(new Box3(new Vec3(0, 0, 0), new Vec3(0, 0, 0)));

            // 按 role 分组发射（同 role 连续 → 一个 submesh）
            var roles = new List<string>();
            foreach (var p in parts) if (!roles.Contains(p.Role)) roles.Add(p.Role);

            foreach (var role in roles)
            {
                int start = data.Indices.Count;
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i].Role == role)
                    {
                        int vstart = data.Positions.Count;
                        EmitPart(data, parts[i], rects[i], atlas);
                        data.EmittedBounds[i] = AabbOf(data.Positions, vstart);   // 该零件在**作者坐标系**里的盒
                    }
                data.SubMeshes.Add(new SubMesh { Role = role, Start = start, Count = data.Indices.Count - start });
            }

            // 原点平移：把声明的 pivotOffset 从所有顶点里减掉（于是"握把"落在原点）
            if (model.PivotOffset != null && (model.PivotOffset[0] != 0 || model.PivotOffset[1] != 0 || model.PivotOffset[2] != 0))
            {
                var pv = new Vec3(model.PivotOffset[0], model.PivotOffset[1], model.PivotOffset[2]);
                for (int i = 0; i < data.Positions.Count; i++) data.Positions[i] = data.Positions[i] - pv;
                for (int i = 0; i < data.EmittedBounds.Count; i++)               // 零件盒跟着移到模型坐标系
                {
                    var b = data.EmittedBounds[i];
                    data.EmittedBounds[i] = new Box3(b.Min - pv, b.Max - pv);
                }
            }

            var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in data.Positions)
            {
                min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
            data.Min = min; data.Max = max;
            return data;
        }

        /// <summary>只生成一个零件（矩形由调用方指定）。给 YSM 用：cube 的 uv 已经写在模型里，
        /// 不需要（也不能）走自动排布。</summary>
        /// <summary>positions[start..] 的包围盒（给"每个零件的盒"用）。</summary>
        static Box3 AabbOf(List<Vec3> positions, int start)
        {
            var mn = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var mx = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = start; i < positions.Count; i++)
            {
                var p = positions[i];
                mn = new Vec3(Math.Min(mn.X, p.X), Math.Min(mn.Y, p.Y), Math.Min(mn.Z, p.Z));
                mx = new Vec3(Math.Max(mx.X, p.X), Math.Max(mx.Y, p.Y), Math.Max(mx.Z, p.Z));
            }
            return new Box3(mn, mx);
        }

        public static MeshData BuildPart(PartSpec part, AtlasRect rect, int atlasSize)
        {
            var data = new MeshData { AtlasSize = atlasSize, PixelsPerMeter = 1f, Atlas = new List<AtlasRect> { rect },
                                      Emitted = new List<PartSpec> { part } };
            EmitPart(data, part, rect, atlasSize);
            data.EmittedBounds.Add(AabbOf(data.Positions, 0));
            data.SubMeshes.Add(new SubMesh { Role = part.Role, Start = 0, Count = data.Indices.Count });

            var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var q in data.Positions)
            {
                min = new Vec3(Math.Min(min.X, q.X), Math.Min(min.Y, q.Y), Math.Min(min.Z, q.Z));
                max = new Vec3(Math.Max(max.X, q.X), Math.Max(max.Y, q.Y), Math.Max(max.Z, q.Z));
            }
            data.Min = min; data.Max = max;
            return data;
        }

        /// <summary>展开 `mirror`：复制一份并把指定轴取反（镜像会翻转绕序，AddQuad/AddTri 会按期望法线纠正）。</summary>
        public static List<PartSpec> ExpandMirrors(List<PartSpec> parts)
        {
            var outList = new List<PartSpec>();
            foreach (var p in parts)
            {
                outList.Add(p);
                if (!string.IsNullOrEmpty(p.Mirror))
                {
                    int axis = "xyz".IndexOf(p.Mirror[0]);
                    if (axis < 0) throw new ModelKitException($"mirror must be x/y/z (role={p.Role})");
                    var c = Clone(p);
                    c.At[axis] = -c.At[axis];
                    outList.Add(c);
                }
            }
            return outList;
        }

        static PartSpec Clone(PartSpec p) => new PartSpec
        {
            Role = p.Role, Shape = p.Shape, Size = p.Size, R = p.R, R1 = p.R1, R2 = p.R2, H = p.H,
            Depth = p.Depth, Segments = p.Segments, Rings = p.Rings, Profile = p.Profile, Outline = p.Outline,
            At = (float[])p.At.Clone(), Rot = (float[])p.Rot.Clone(), Scale = (float[])p.Scale.Clone(), Mirror = null
        };

        // ── UV 排布 ─────────────────────────────────────────────────────────────

        /// <summary>取"能塞进 atlas×atlas 的最大整数像素/米"（从 startPpm 往下试）。</summary>
        public static float AutoDensity(List<PartSpec> parts, int atlas, int startPpm = 1024)
        {
            for (int ppm = startPpm; ppm >= 1; ppm--)
            {
                try { Pack(parts, atlas, ppm); return ppm; }
                catch (ModelKitException) { }
            }
            return 0;
        }

        public static List<AtlasRect> Pack(List<PartSpec> parts, int atlas, float ppm)
        {
            int x = 0, y = 0, rowH = 0;
            var list = new List<AtlasRect>();
            foreach (var p in parts)
            {
                var d = p.Dims();
                int w = Math.Max(1, (int)Math.Round(d.X * ppm));
                int h = Math.Max(1, (int)Math.Round(d.Y * ppm));
                int dep = Math.Max(1, (int)Math.Round(d.Z * ppm));
                int rw = 2 * dep + 2 * w, rh = dep + h;
                if (rw > atlas || rh > atlas) throw new ModelKitException($"part '{p.Role}' unwrap {rw}x{rh} px does not fit {atlas}x{atlas}");
                if (x + rw > atlas) { y += rowH + Margin; x = 0; rowH = 0; }
                if (y + rh > atlas) throw new ModelKitException($"parts do not fit in {atlas}x{atlas} at {ppm} px/m");
                list.Add(new AtlasRect { Role = p.Role, X = x, Y = y, W = rw, H = rh, PxW = w, PxH = h, PxD = dep });
                x += rw + Margin; rowH = Math.Max(rowH, rh);
            }
            return list;
        }

        /// <summary>标准 box-UV 落点：返回该面的子矩形（图像空间像素）。</summary>
        public static Rect FaceRect(AtlasRect r, string face)
        {
            switch (face)
            {
                case "top": return new Rect(r.X + r.PxD, r.Y, r.PxW, r.PxD);
                case "bottom": return new Rect(r.X + r.PxD + r.PxW, r.Y, r.PxW, r.PxD);
                case "right": return new Rect(r.X, r.Y + r.PxD, r.PxD, r.PxH);
                case "front": return new Rect(r.X + r.PxD, r.Y + r.PxD, r.PxW, r.PxH);
                case "left": return new Rect(r.X + r.PxD + r.PxW, r.Y + r.PxD, r.PxD, r.PxH);
                case "back": return new Rect(r.X + 2 * r.PxD + r.PxW, r.Y + r.PxD, r.PxW, r.PxH);
                default: throw new ModelKitException("face? " + face);
            }
        }

        // ── 几何 ────────────────────────────────────────────────────────────────

        static void EmitPart(MeshData data, PartSpec p, AtlasRect rect, int atlas)
        {
            switch (p.Shape)
            {
                case "box": Box(data, p, rect, atlas); break;
                case "cylinder": Revolve(data, p, rect, atlas, p.R, p.R); break;
                case "cone": Revolve(data, p, rect, atlas, p.R1, p.R2); break;
                case "sphere": Sphere(data, p, rect, atlas); break;
                case "lathe": Lathe(data, p, rect, atlas); break;
                case "extrude": Extrude(data, p, rect, atlas); break;
                default: throw new ModelKitException($"unsupported shape '{p.Shape}' (role={p.Role})");
            }
        }

        /// <summary>法线变换：只做旋转 + 按缩放的倒数（法线的正确变换是逆转置；我们只支持轴对齐缩放）。
        /// ⚠️ 忘了这一步 → 旋转过的零件法线全错（表现：不受光/看着是黑的；绕序检查也会误判）。</summary>
        static Vec3 XfNormal(PartSpec p, Vec3 n)
        {
            var s = new Vec3(
                Math.Abs(p.Scale[0]) > 1e-6f ? n.X / p.Scale[0] : n.X,
                Math.Abs(p.Scale[1]) > 1e-6f ? n.Y / p.Scale[1] : n.Y,
                Math.Abs(p.Scale[2]) > 1e-6f ? n.Z / p.Scale[2] : n.Z);
            return Rotate(s, p.Rot).Normalized();
        }

        static Vec3 Xf(PartSpec p, Vec3 v)
        {
            var s = new Vec3(v.X * p.Scale[0], v.Y * p.Scale[1], v.Z * p.Scale[2]);
            return Rotate(s, p.Rot) + new Vec3(p.At[0], p.At[1], p.At[2]);
        }

        /// <summary>与 Unity 的 Quaternion.Euler 一致：先 Z、再 X、再 Y。</summary>
        static Vec3 Rotate(Vec3 v, float[] deg)
        {
            float rx = deg[0] * (float)Math.PI / 180f, ry = deg[1] * (float)Math.PI / 180f, rz = deg[2] * (float)Math.PI / 180f;
            float cx = (float)Math.Cos(rx), sx = (float)Math.Sin(rx);
            float cy = (float)Math.Cos(ry), sy = (float)Math.Sin(ry);
            float cz = (float)Math.Cos(rz), sz = (float)Math.Sin(rz);
            float m00 = cy * cz + sy * sx * sz, m01 = -cy * sz + sy * sx * cz, m02 = sy * cx;
            float m10 = cx * sz, m11 = cx * cz, m12 = -sx;
            float m20 = -sy * cz + cy * sx * sz, m21 = sy * sz + cy * sx * cz, m22 = cy * cx;
            return new Vec3(
                m00 * v.X + m01 * v.Y + m02 * v.Z,
                m10 * v.X + m11 * v.Y + m12 * v.Z,
                m20 * v.X + m21 * v.Y + m22 * v.Z);
        }

        static Vec2 UvOf(Rect r, int atlas, float s, float t)
        {
            float px = r.X + s * r.W;
            float py = r.Y + (1f - t) * r.H;      // t=0 → 该面底边
            return new Vec2(px / atlas, 1f - py / atlas);
        }

        /// <summary>加一个四边形；绕序与期望法线相反时自动翻转（顶点与 UV 的对应不变）。</summary>
        static void Quad(MeshData d, PartSpec p, Rect r, int atlas, Vec3 n,
                         (Vec3 pos, float s, float t) a, (Vec3 pos, float s, float t) b,
                         (Vec3 pos, float s, float t) c, (Vec3 pos, float s, float t) e)
        {
            var q = new[] { a, b, c, e };
            // ⚠️ Unity 是**左手系**：正面 = 从外侧看**顺时针** → 右手叉积应指向法线**反方向**（点积 < 0）。
            // 反过来写 = 每个面都是背面 → 整模型被剔除（实测踩过：模型完全看不见）
            if (Vec3.Dot(Vec3.Cross(q[1].pos - q[0].pos, q[2].pos - q[0].pos), n) > 0)
            { var t = q[1]; q[1] = q[3]; q[3] = t; }
            int b0 = d.Positions.Count;
            var xn = XfNormal(p, n);
            foreach (var v in q)
            {
                d.Positions.Add(Xf(p, v.pos));
                d.Normals.Add(xn);
                d.Uvs.Add(UvOf(r, atlas, v.s, v.t));
            }
            d.Indices.Add(b0); d.Indices.Add(b0 + 1); d.Indices.Add(b0 + 2);
            d.Indices.Add(b0); d.Indices.Add(b0 + 2); d.Indices.Add(b0 + 3);
        }

        static void Tri(MeshData d, PartSpec p, Rect r, int atlas, Vec3 n,
                        (Vec3 pos, float s, float t) a, (Vec3 pos, float s, float t) b, (Vec3 pos, float s, float t) c)
        {
            var q = new[] { a, b, c };
            if (Vec3.Dot(Vec3.Cross(q[1].pos - q[0].pos, q[2].pos - q[0].pos), n) > 0) { var t = q[1]; q[1] = q[2]; q[2] = t; }   // 同 Quad：Unity 左手系
            int b0 = d.Positions.Count;
            var xn = XfNormal(p, n);
            foreach (var v in q)
            {
                d.Positions.Add(Xf(p, v.pos));
                d.Normals.Add(xn);
                d.Uvs.Add(UvOf(r, atlas, v.s, v.t));
            }
            d.Indices.Add(b0); d.Indices.Add(b0 + 1); d.Indices.Add(b0 + 2);
        }

        static void Box(MeshData d, PartSpec p, AtlasRect r, int atlas)
        {
            float w = p.Size[0] / 2, h = p.Size[1] / 2, dp = p.Size[2] / 2;
            // 每个面：4 个角（带面内 s,t）+ 期望法线
            var faces = new (string name, Vec3 n, (float x, float y, float z, float s, float t)[] c)[]
            {
                ("front",  new Vec3(0,0,1),  new (float,float,float,float,float)[]{ (-1,-1, 1, 0,0), ( 1,-1, 1, 1,0), ( 1, 1, 1, 1,1), (-1, 1, 1, 0,1) }),
                ("back",   new Vec3(0,0,-1), new (float,float,float,float,float)[]{ ( 1,-1,-1, 0,0), (-1,-1,-1, 1,0), (-1, 1,-1, 1,1), ( 1, 1,-1, 0,1) }),
                ("right",  new Vec3(1,0,0),  new (float,float,float,float,float)[]{ ( 1,-1,-1, 0,0), ( 1,-1, 1, 1,0), ( 1, 1, 1, 1,1), ( 1, 1,-1, 0,1) }),
                ("left",   new Vec3(-1,0,0), new (float,float,float,float,float)[]{ (-1,-1, 1, 0,0), (-1,-1,-1, 1,0), (-1, 1,-1, 1,1), (-1, 1, 1, 0,1) }),
                ("top",    new Vec3(0,1,0),  new (float,float,float,float,float)[]{ (-1, 1,-1, 0,0), ( 1, 1,-1, 1,0), ( 1, 1, 1, 1,1), (-1, 1, 1, 0,1) }),
                ("bottom", new Vec3(0,-1,0), new (float,float,float,float,float)[]{ (-1,-1, 1, 0,0), ( 1,-1, 1, 1,0), ( 1,-1,-1, 1,1), (-1,-1,-1, 0,1) }),
            };
            foreach (var f in faces)
            {
                var fr = FaceRect(r, f.name);
                var c = f.c;
                Quad(d, p, fr, atlas, f.n,
                    (new Vec3(c[0].x * w, c[0].y * h, c[0].z * dp), c[0].s, c[0].t),
                    (new Vec3(c[1].x * w, c[1].y * h, c[1].z * dp), c[1].s, c[1].t),
                    (new Vec3(c[2].x * w, c[2].y * h, c[2].z * dp), c[2].s, c[2].t),
                    (new Vec3(c[3].x * w, c[3].y * h, c[3].z * dp), c[3].s, c[3].t));
            }
        }

        /// <summary>圆柱 / 圆台 / 圆锥（R2 = 0 时收成尖）。侧面用 front 子矩形，两端盖用 top/bottom。</summary>
        static void Revolve(MeshData d, PartSpec p, AtlasRect r, int atlas, float rBot, float rTop)
        {
            int n = Math.Max(3, p.Segments);
            if (rTop <= 1e-6f) { Apex(d, p, r, atlas, rBot, -p.H / 2, p.H / 2, true); Cap(d, p, FaceRect(r, "bottom"), atlas, rBot, -p.H / 2, new Vec3(0, -1, 0), n); return; }
            if (rBot <= 1e-6f) { Apex(d, p, r, atlas, rTop, p.H / 2, -p.H / 2, false); Cap(d, p, FaceRect(r, "top"), atlas, rTop, p.H / 2, new Vec3(0, 1, 0), n); return; }
            var side = FaceRect(r, "front");
            float y0 = -p.H / 2, y1 = p.H / 2;
            for (int i = 0; i < n; i++)
            {
                float a0 = 2 * (float)Math.PI * i / n, a1 = 2 * (float)Math.PI * (i + 1) / n;
                var v00 = new Vec3((float)Math.Cos(a0) * rBot, y0, (float)Math.Sin(a0) * rBot);
                var v10 = new Vec3((float)Math.Cos(a1) * rBot, y0, (float)Math.Sin(a1) * rBot);
                var v11 = new Vec3((float)Math.Cos(a1) * rTop, y1, (float)Math.Sin(a1) * rTop);
                var v01 = new Vec3((float)Math.Cos(a0) * rTop, y1, (float)Math.Sin(a0) * rTop);
                float s0 = (float)i / n, s1 = (float)(i + 1) / n;
                var nm = new Vec3((float)Math.Cos((a0 + a1) / 2), 0, (float)Math.Sin((a0 + a1) / 2));
                Quad(d, p, side, atlas, nm, (v00, s0, 0), (v10, s1, 0), (v11, s1, 1), (v01, s0, 1));
            }
            Cap(d, p, FaceRect(r, "top"), atlas, rTop, y1, new Vec3(0, 1, 0), n);
            Cap(d, p, FaceRect(r, "bottom"), atlas, rBot, y0, new Vec3(0, -1, 0), n);
        }

        /// <summary>圆锥（一段收成尖）：侧面用三角形，避免退化四边形。</summary>
        static void Apex(MeshData d, PartSpec p, AtlasRect r, int atlas, float rBase, float yBase, float yApex, bool apexUp)
        {
            int n = Math.Max(3, p.Segments);
            var side = FaceRect(r, "front");
            var apex = new Vec3(0, yApex, 0);
            for (int i = 0; i < n; i++)
            {
                float a0 = 2 * (float)Math.PI * i / n, a1 = 2 * (float)Math.PI * (i + 1) / n;
                var b0 = new Vec3((float)Math.Cos(a0) * rBase, yBase, (float)Math.Sin(a0) * rBase);
                var b1 = new Vec3((float)Math.Cos(a1) * rBase, yBase, (float)Math.Sin(a1) * rBase);
                float s0 = (float)i / n, s1 = (float)(i + 1) / n;
                var nm = new Vec3((float)Math.Cos((a0 + a1) / 2), apexUp ? 0.6f : -0.6f, (float)Math.Sin((a0 + a1) / 2));
                var tc = (pos: apex, s: (s0 + s1) / 2, t: apexUp ? 1f : 0f);
                var t0 = (pos: b0, s: s0, t: apexUp ? 0f : 1f);
                var t1 = (pos: b1, s: s1, t: apexUp ? 0f : 1f);
                Tri(d, p, side, atlas, nm, tc, t0, t1);
            }
        }

        static void Cap(MeshData d, PartSpec p, Rect fr, int atlas, float rad, float y, Vec3 n, int nSeg)
        {
            if (rad <= 1e-6f) return;
            var c = new Vec3(0, y, 0);
            for (int i = 0; i < nSeg; i++)
            {
                float a0 = 2 * (float)Math.PI * i / nSeg, a1 = 2 * (float)Math.PI * (i + 1) / nSeg;
                var q0 = new Vec3((float)Math.Cos(a0) * rad, y, (float)Math.Sin(a0) * rad);
                var q1 = new Vec3((float)Math.Cos(a1) * rad, y, (float)Math.Sin(a1) * rad);
                Tri(d, p, fr, atlas, n,
                    (c, 0.5f, 0.5f),
                    (q0, 0.5f + 0.5f * (float)Math.Cos(a0), 0.5f + 0.5f * (float)Math.Sin(a0)),
                    (q1, 0.5f + 0.5f * (float)Math.Cos(a1), 0.5f + 0.5f * (float)Math.Sin(a1)));
            }
        }

        static void Sphere(MeshData d, PartSpec p, AtlasRect r, int atlas)
        {
            int lon = Math.Max(3, p.Segments);
            int lat = p.Rings > 0 ? p.Rings : Math.Max(3, lon / 2);
            var fr = FaceRect(r, "front");
            for (int j = 0; j < lat; j++)
            {
                float p0 = (float)Math.PI * j / lat, p1 = (float)Math.PI * (j + 1) / lat;
                for (int i = 0; i < lon; i++)
                {
                    float a0 = 2 * (float)Math.PI * i / lon, a1 = 2 * (float)Math.PI * (i + 1) / lon;
                    Vec3 On(float ph, float al) => new Vec3(
                        (float)(Math.Sin(ph) * Math.Cos(al)) * p.R, (float)Math.Cos(ph) * p.R, (float)(Math.Sin(ph) * Math.Sin(al)) * p.R);
                    var v00 = On(p0, a0); var v10 = On(p0, a1); var v11 = On(p1, a1); var v01 = On(p1, a0);
                    float s0 = (float)i / lon, s1 = (float)(i + 1) / lon, t0 = 1f - (float)j / lat, t1 = 1f - (float)(j + 1) / lat;
                    Quad(d, p, fr, atlas, v00, (v00, s0, t0), (v10, s1, t0), (v11, s1, t1), (v01, s0, t1));
                }
            }
        }

        static void Lathe(MeshData d, PartSpec p, AtlasRect r, int atlas)
        {
            int n = Math.Max(3, p.Segments);
            int k = p.Profile.Length;
            var fr = FaceRect(r, "front");
            for (int i = 0; i < n; i++)
            {
                float a0 = 2 * (float)Math.PI * i / n, a1 = 2 * (float)Math.PI * (i + 1) / n;
                for (int j = 0; j < k - 1; j++)
                {
                    float r0 = p.Profile[j][0], y0 = p.Profile[j][1];
                    float r1 = p.Profile[j + 1][0], y1 = p.Profile[j + 1][1];
                    var v00 = new Vec3((float)Math.Cos(a0) * r0, y0, (float)Math.Sin(a0) * r0);
                    var v10 = new Vec3((float)Math.Cos(a1) * r0, y0, (float)Math.Sin(a1) * r0);
                    var v11 = new Vec3((float)Math.Cos(a1) * r1, y1, (float)Math.Sin(a1) * r1);
                    var v01 = new Vec3((float)Math.Cos(a0) * r1, y1, (float)Math.Sin(a0) * r1);
                    float s0 = (float)i / n, s1 = (float)(i + 1) / n, t0 = (float)j / (k - 1), t1 = (float)(j + 1) / (k - 1);
                    Quad(d, p, fr, atlas, v00, (v00, s0, t0), (v10, s1, t0), (v11, s1, t1), (v01, s0, t1));
                }
            }
        }

        /// <summary>拉伸：轮廓（凸多边形）+ 厚度。正/背面用 front/back 子矩形，侧壁用 right。</summary>
        static void Extrude(MeshData d, PartSpec p, AtlasRect r, int atlas)
        {
            var o = p.Outline;
            int m = o.Length;
            float minx = float.MaxValue, maxx = float.MinValue, miny = float.MaxValue, maxy = float.MinValue;
            foreach (var q in o) { minx = Math.Min(minx, q[0]); maxx = Math.Max(maxx, q[0]); miny = Math.Min(miny, q[1]); maxy = Math.Max(maxy, q[1]); }
            float cxm = (minx + maxx) / 2, cym = (miny + maxy) / 2;
            float sx = Math.Max(1e-6f, maxx - minx), sy = Math.Max(1e-6f, maxy - miny);
            var front = FaceRect(r, "front"); var back = FaceRect(r, "back"); var side = FaceRect(r, "right");

            for (int i = 0; i < m; i++)
            {
                var q0 = o[i]; var q1 = o[(i + 1) % m];
                float u0 = (q0[0] - minx) / sx, v0 = (q0[1] - miny) / sy;
                float u1 = (q1[0] - minx) / sx, v1 = (q1[1] - miny) / sy;
                var c = (u: 0.5f, v: 0.5f);

                // 正面 (+Z)
                Tri(d, p, front, atlas, new Vec3(0, 0, 1),
                    (new Vec3(cxm, cym, p.Depth / 2), c.u, c.v),
                    (new Vec3(q0[0], q0[1], p.Depth / 2), u0, v0),
                    (new Vec3(q1[0], q1[1], p.Depth / 2), u1, v1));
                // 背面 (-Z)：注意左右镜像，UV 用 1-u
                Tri(d, p, back, atlas, new Vec3(0, 0, -1),
                    (new Vec3(cxm, cym, -p.Depth / 2), c.u, c.v),
                    (new Vec3(q0[0], q0[1], -p.Depth / 2), 1 - u0, v0),
                    (new Vec3(q1[0], q1[1], -p.Depth / 2), 1 - u1, v1));

                // 侧壁：法线 = 边方向 × Z，取朝向多边形外侧的那个
                var edge = new Vec3(q1[0] - q0[0], q1[1] - q0[1], 0);
                var nrm = Vec3.Cross(edge, new Vec3(0, 0, 1));
                var mid = new Vec3((q0[0] + q1[0]) / 2 - cxm, (q0[1] + q1[1]) / 2 - cym, 0);
                if (Vec3.Dot(nrm, mid) < 0) nrm = nrm * -1;
                var a0 = new Vec3(q0[0], q0[1], -p.Depth / 2);
                var a1 = new Vec3(q1[0], q1[1], -p.Depth / 2);
                var a2 = new Vec3(q1[0], q1[1], p.Depth / 2);
                var a3 = new Vec3(q0[0], q0[1], p.Depth / 2);
                Quad(d, p, side, atlas, nrm, (a0, u0, 0), (a1, u1, 0), (a2, u1, 1), (a3, u0, 1));
            }
        }
    }
}
