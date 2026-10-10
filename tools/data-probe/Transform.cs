// Transform.cs —— `--action transform`（`tools/data-probe` 的一部分）
//
// 为什么需要它：Unity 里 Transform 每一级都是 local（localPosition/localRotation/localScale），
//   「模型在游戏里到底多大 / 挂点到底在哪」必须沿父链累计才拿得到。
//   典型例子：原版步枪 `Rifle02` 的 mesh 本地长 1.525 m，但它挂在 `Rifle02` 这个 Transform 下、
//   累计缩放 0.6545 ⇒ 游戏里其实是 0.998 m（凭 mesh 尺寸猜就会错）。
//
// 输出的三件事：
//   ① 目标 Transform 的 local TRS + 父链（逐级 + 累计世界 TRS）
//   ② 子节点树（`m_Children` 按 --depth 递归）—— 用来找 `Sockets/Scope`、`Sockets/Muzzle` 这些挂点
//   ③ 若对象挂着 MeshFilter ⇒ 读 `Mesh.m_LocalAABB` × 累计缩放 = 真实尺寸（米）
//      ⚠️ mesh 挂在 `MeshFilter` 上（`MeshRenderer` 只有材质 —— 踩过这个坑）
//
// 只读 不改游戏、不改 mod。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

static partial class DataProbe
{
    public static void Transform(AssetsManager am, List<AssetsFileInstance> insts, Dictionary<string, string> o, int depth, Out outp)
    {
        var name = Opt(o, "name") ?? Opt(o, "pattern");
        var pathId = Opt(o, "pathid");
        var meshWant = Opt(o, "mesh");
        var has = Opt(o, "has");
        bool exact = o.ContainsKey("exact");
        if (name == null && pathId == null && meshWant == null)
        {
            outp.Add("# transform: need one of --name/--pattern, --pathid, --mesh <mesh name>");
            outp.Add("#   optional: --exact (strict name), --has <ComponentName> (only objects carrying it), --depth");
            return;
        }
        if (depth <= 0) depth = 2;

        // ① 定位候选 Transform（三种入口）
        //    · --pathid      直取
        //    · --mesh <名>   ⭐ 由 mesh 名反查挂它的 MeshFilter ⇒ 取它的 GameObject 的 Transform
        //                    （省掉"search 抄 pathID 再 dump"三步）
        //    · --name <名>   匹配 GameObject 名字（--exact ⇒ 严格相等 否则子串）
        var hits = new List<Node>();
        foreach (var (inst, info) in AllInfos(insts))
        {
            var cn = ClassNameOf(am, inst, info);
            if (meshWant != null)
            {
                // 只扫 MeshFilter（mesh 挂在它身上 不是 MeshRenderer）
                if (!string.Equals(cn, "MeshFilter", StringComparison.Ordinal)) continue;
                AssetTypeValueField mbf;
                try { mbf = am.GetBaseField(inst, info); } catch { continue; }
                if (mbf == null) continue;
                var mnode = Ext(am, inst, mbf, "m_Mesh");
                var mname = mnode != null ? AssetNameOrDash(mnode.bf) : "";
                if (!NameMatches(mname, meshWant, exact)) continue;
                // MeshFilter → 它的 GameObject → 它的 Transform
                var go = Ext(am, inst, mbf, "m_GameObject");
                if (go == null) continue;
                var tr = TransformOf(am, go);
                if (tr != null && HasComponent(am, tr, has)) hits.Add(tr);
                continue;
            }
            if (!string.Equals(cn, "Transform", StringComparison.Ordinal)) continue;
            if (pathId != null && info.PathId.ToString() != pathId) continue;
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            if (name != null && !NameMatches(GoName(am, inst, bf) ?? "", name, exact)) continue;
            if (!HasComponent(am, new Node(inst, info, bf), has)) continue;
            hits.Add(new Node(inst, info, bf));
            if (pathId != null || has == null) break;    // 精确/无 --has ⇒ 取第一个就够；--has 时继续找
        }
        if (hits.Count == 0)
        {
            outp.Add($"# transform: no Transform matched (name={name} pathid={pathId} mesh={meshWant} has={has})");
            return;
        }
        if (hits.Count > 1) outp.Add($"# matched {hits.Count} object(s) - expanding the first one (narrow with --exact / --has)");
        Node hit = hits[0];

        // ② 父链（子 → 根）
        var chain = new List<Node>();
        for (var cur = hit; cur != null; cur = Ext(am, cur.inst, cur.bf, "m_Father")) chain.Add(cur);
        var goName = GoName(am, hit.inst, hit.bf) ?? "(no name)";
        outp.Add($"=== Transform {goName} (pathID {hit.info.PathId}, {chain.Count} level(s) to root) ===");

        // ③ local TRS（从根 → 目标 读起来顺）
        outp.Add("## local TRS (root -> target)");
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var t = chain[i];
            outp.Add($"   {GoName(am, t.inst, t.bf),-30} pos={V3Str(t.bf, "m_LocalPosition"),-26} rot={QStr(t.bf, "m_LocalRotation")}");
            outp.Add($"   {new string(' ', 30)} scale={V3Str(t.bf, "m_LocalScale")}");
        }

        // ④ 累计世界（System.Numerics 是行向量约定 ⇒ 局部点乘矩阵：子在前、父在后）
        var world = Matrix4x4.Identity;
        foreach (var t in chain) world = world * Local(t.bf);          // chain 是 子→根
        var wp = world.Translation;
        var ws = new Vector3(
            new Vector3(world.M11, world.M12, world.M13).Length(),
            new Vector3(world.M21, world.M22, world.M23).Length(),
            new Vector3(world.M31, world.M32, world.M33).Length());
        outp.Add("## accumulated world");
        outp.Add($"   position = ({wp.X:0.####}, {wp.Y:0.####}, {wp.Z:0.####})");
        outp.Add($"   world scale = ({ws.X:0.#####}, {ws.Y:0.#####}, {ws.Z:0.#####})");

        // ⑤ mesh 真实尺寸（mesh 挂在 MeshFilter 上 不是 MeshRenderer）
        var mf = ComponentOf(am, hit, "MeshFilter");
        if (mf != null)
        {
            var mesh = Ext(am, mf.inst, mf.bf, "m_Mesh");
            if (meshWant != null)
            {
                var ext2 = ExtField(mesh.bf, "m_LocalAABB.m_Extent");
                var cen = ExtField(mesh.bf, "m_LocalAABB.m_Center");
                if (ext2 != null)
                {
                    var e = new Vector3(ext2["x"].AsFloat, ext2["y"].AsFloat, ext2["z"].AsFloat) * 2f;   // extent = 半长
                    var c = cen != null ? new Vector3(cen["x"].AsFloat, cen["y"].AsFloat, cen["z"].AsFloat) : Vector3.Zero;
                    var real = new Vector3(e.X * ws.X, e.Y * ws.Y, e.Z * ws.Z);
                    outp.Add("## real size (mesh local AABB x world scale)");
                    outp.Add($"   mesh = {AssetNameOrDash(mesh.bf)}   center = ({c.X:0.####}, {c.Y:0.####}, {c.Z:0.####})");
                    outp.Add($"   mesh local size = {e.X:0.####} x {e.Y:0.####} x {e.Z:0.####} m");
                    outp.Add($"   REAL in-game size = {real.X:0.####} x {real.Y:0.####} x {real.Z:0.####} m" +
                             $"   (longest edge {Math.Max(real.X, Math.Max(real.Y, real.Z)):0.####} m)");
                }
            }
        }

        // ⑥ 子节点树（找 Sockets/… 用）
        outp.Add($"## children (depth {depth})");
        Walk(am, hit, depth, 1, outp);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>名字匹配：`--exact` ⇒ 严格相等（`Rifle02` 就不会再匹配到 `Rifle02_Sight`）；否则子串</summary>
    static bool NameMatches(string value, string want, bool exact)
        => exact ? string.Equals(value, want, StringComparison.Ordinal)
                 : (value ?? "").IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>该 Transform 的 GameObject 是否挂着某组件（`--has`；没给 = 恒真）</summary>
    static bool HasComponent(AssetsManager am, Node t, string className)
        => className == null || ComponentOf(am, t, className) != null;

    /// <summary>由 GameObject 找它的 Transform（`Transform.m_GameObject` 反向查）</summary>
    static Node TransformOf(AssetsManager am, Node go)
    {
        AssetTypeValueField arr;
        try { arr = go.bf["m_Component"]; } catch { return null; }
        if (arr == null) return null;
        arr = ArrayNode(arr) ?? arr;
        foreach (var item in arr.Children)
        {
            AssetTypeValueField ptrField = null;
            try { ptrField = item["component"]; } catch { }
            if (ptrField == null || ptrField.Children.Count == 0) ptrField = item;
            try
            {
                if (ptrField["m_PathID"] == null) continue;
                var ext = am.GetExtAsset(go.inst, ptrField);
                if (ext.baseField == null) continue;
                var f = ext.file ?? go.inst;
                var cn = ext.info != null ? ClassNameOf(am, f, ext.info) : null;
                if (string.Equals(cn, "Transform", StringComparison.Ordinal)) return new Node(f, ext.info, ext.baseField);
            }
            catch { }
        }
        return null;
    }

    class Node
    {
        public readonly AssetsFileInstance inst;
        public readonly AssetFileInfo info;
        public readonly AssetTypeValueField bf;
        public Node(AssetsFileInstance i, AssetFileInfo n, AssetTypeValueField b) { inst = i; info = n; bf = b; }
    }

    static string AssetNameOrDash(AssetTypeValueField bf)
    {
        try { return bf["m_Name"].AsString; } catch { }
        return "(unnamed)";
    }

    /// <summary>读 PPtr 指向的对象（跨文件）；空/解析失败返回 null。</summary>
    static Node Ext(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField owner, string field)
    {
        try
        {
            var ptr = owner[field];
            if (ptr == null) return null;
            long pid; try { pid = ptr["m_PathID"].AsLong; } catch { return null; }
            if (pid == 0) return null;
            var ext = am.GetExtAsset(inst, ptr);   // AssetExternal 是 struct 不能和 null 比
            if (ext.baseField == null) return null;
            var f = ext.file ?? inst;
            return new Node(f, ext.info, ext.baseField);
        }
        catch { return null; }
    }

    /// <summary>取 Transform 所属 GameObject 的名字（Transform 自己没有名字 名字在 GameObject 上）</summary>
    static string GoName(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField t)
    {
        var go = Ext(am, inst, t, "m_GameObject");
        if (go == null) return null;
        try { return go.bf["m_Name"].AsString; } catch { return null; }
    }

    /// <summary>该 Transform 的 GameObject 上有没有某个组件（如 MeshFilter）</summary>
    static Node ComponentOf(AssetsManager am, Node t, string className)
    {
        var go = Ext(am, t.inst, t.bf, "m_GameObject");
        if (go == null) return null;
        AssetTypeValueField arr;
        try { arr = go.bf["m_Component"]; } catch { return null; }
        if (arr == null) return null;
        // ⚠️ 数组在序列化里多包一层 `Array`（dump 里是 `m_Component.Array.data`）⇒ 必须先解开
        arr = ArrayNode(arr) ?? arr;
        foreach (var item in arr.Children)
        {
            // 每项形如 { component: PPtr }（新版）；老版字段名可能直接是 PPtr
            // ⚠️ `item["component"]` 取不到会抛异常（不是返回 null）⇒ 必须 try
            AssetTypeValueField ptrField = null;
            try { ptrField = item["component"]; } catch { }
            if (ptrField == null || ptrField.Children.Count == 0) ptrField = item;
            try
            {
                if (ptrField == null || ptrField["m_PathID"] == null) continue;
                var ext = am.GetExtAsset(go.inst, ptrField);
                if (ext.baseField == null) continue;
                var f = ext.file ?? go.inst;
                var cn = ext.info != null ? ClassNameOf(am, f, ext.info) : null;
                if (string.Equals(cn, className, StringComparison.Ordinal)) return new Node(f, ext.info, ext.baseField);
            }
            catch { }
        }
        return null;
    }

    /// <summary>按名字取某个引用字段（支持 `a.b` 路径）</summary>
    static AssetTypeValueField ExtField(AssetTypeValueField f, string path)
    {
        var cur = f;
        foreach (var seg in path.Split('.'))
        {
            try { cur = cur[seg]; } catch { return null; }
            if (cur == null) return null;
        }
        return cur;
    }

    /// <summary>子节点：⚠️ 不能靠 `m_Children` —— 它在 `classdata.tpk` 里是不透明数组
    /// （dump 出来只有 `Array = AssetTypeArrayInfo` 元素根本取不到；`--follow` 也不展开它）。
    /// ⇒ ⭐ 反着来：扫同一文件里所有 Transform 谁的 `m_Father` 指着我，谁就是我的孩子
    /// （场景层级都在同一份 `levelN` 里 ⇒ 够用）</summary>
    static List<Node> ChildrenOf(AssetsManager am, Node t)
    {
        var kids = new List<Node>();
        long myPath = t.info.PathId;
        foreach (var (inst, info) in AllInfos(new List<AssetsFileInstance> { t.inst }))
        {
            if (!string.Equals(ClassNameOf(am, inst, info), "Transform", StringComparison.Ordinal)) continue;
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            var father = ExtField(bf, "m_Father");
            if (father == null) continue;
            long fid, pid;
            try { fid = father["m_FileID"].AsLong; pid = father["m_PathID"].AsLong; } catch { continue; }
            if (pid != myPath || fid != 0) continue;
            kids.Add(new Node(inst, info, bf));
        }
        return kids;
    }

    static void Walk(AssetsManager am, Node t, int maxDepth, int d, Out outp)
    {
        if (outp.Truncated) return;              // stop early at the cap
        if (d > maxDepth) return;
        var pad = new string(' ', d * 2);
        foreach (var node in ChildrenOf(am, t))
        {
            {
                var f = node.inst;
                outp.Add($"{pad}- {GoName(am, f, node.bf) ?? "(no name)"}   pos={V3Str(node.bf, "m_LocalPosition")}  scale={V3Str(node.bf, "m_LocalScale")}");
                var mf = ComponentOf(am, node, "MeshFilter");
                if (mf != null)
                {
                    var mesh = Ext(am, mf.inst, mf.bf, "m_Mesh");
                    var e = mesh != null ? ExtField(mesh.bf, "m_LocalAABB.m_Extent") : null;
                    if (e != null)
                        outp.Add($"{pad}    mesh={AssetNameOrDash(mesh.bf)} local {e["x"].AsFloat * 2:0.###} x {e["y"].AsFloat * 2:0.###} x {e["z"].AsFloat * 2:0.###} m");
                }
                Walk(am, node, maxDepth, d + 1, outp);
            }
        }
    }

    /// <summary>local TRS → 矩阵（System.Numerics 是行向量 ⇒ 等价于 Unity 的 T·R·S）</summary>
    static Matrix4x4 Local(AssetTypeValueField t)
    {
        return Matrix4x4.CreateScale(V3(t, "m_LocalScale"))
             * Matrix4x4.CreateFromQuaternion(Q(t, "m_LocalRotation"))
             * Matrix4x4.CreateTranslation(V3(t, "m_LocalPosition"));
    }

    static Vector3 V3(AssetTypeValueField t, string field)
    {
        var f = ExtField(t, field);
        if (f == null) return Vector3.Zero;
        try { return new Vector3(f["x"].AsFloat, f["y"].AsFloat, f["z"].AsFloat); } catch { return Vector3.Zero; }
    }

    static string V3Str(AssetTypeValueField t, string field)
    {
        var v = V3(t, field);
        return string.Format(CultureInfo.InvariantCulture, "({0:0.####}, {1:0.####}, {2:0.####})", v.X, v.Y, v.Z);
    }

    static Quaternion Q(AssetTypeValueField t, string field)
    {
        var f = ExtField(t, field);
        if (f == null) return Quaternion.Identity;
        try { return new Quaternion(f["x"].AsFloat, f["y"].AsFloat, f["z"].AsFloat, f["w"].AsFloat); } catch { return Quaternion.Identity; }
    }

    static string QStr(AssetTypeValueField t, string field)
    {
        var q = Q(t, field);
        // 人看的角度更有用：把四元数换回 YXZ 欧拉（度）
        var e = EulerYXZ(q);
        return string.Format(CultureInfo.InvariantCulture, "(x{0:0.#} deg, y{1:0.#} deg, z{2:0.#} deg)", e.X, e.Y, e.Z);
    }

    /// <summary>四元数 → 欧拉（YXZ 顺序 与 Unity Inspector 显示一致）</summary>
    static Vector3 EulerYXZ(Quaternion q)
    {
        double sinr = 2.0 * (q.W * q.X + q.Y * q.Z);
        double cosr = 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
        double x = Math.Atan2(sinr, cosr);
        double sinp = 2.0 * (q.W * q.Y - q.Z * q.X);
        double y = Math.Abs(sinp) >= 1 ? Math.Sign(sinp) * Math.PI / 2 : Math.Asin(sinp);
        double siny = 2.0 * (q.W * q.Z + q.X * q.Y);
        double cosy = 1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);
        double z = Math.Atan2(siny, cosy);
        const double R = 180.0 / Math.PI;
        return new Vector3((float)(x * R), (float)(y * R), (float)(z * R));
    }
}
