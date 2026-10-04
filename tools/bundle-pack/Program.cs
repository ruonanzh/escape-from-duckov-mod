// bundle-pack —— 离线打 AssetBundle（不装 Unity）。第一步：**改名 + 原样重打**。
//
// 为什么第一步是这个：Unity 里**同名的 bundle 在同一个进程只能加载一次**（第二次 LoadFromFile 返回 null），
// 所以"两个 mod 共用同一套模型"必然有一个失效。要解决就必须给包**唯一的名字** —— 而包内部有**两处**要一起改：
//   ① 目录项名：`CAB-xxxx` 与 `CAB-xxxx.resS`
//   ② 贴图里的 `m_StreamData.path`：`archive:/CAB-xxxx/CAB-xxxx.resS`（不改 → 贴图丢）
//
// 用法：
//   bundle-pack --mold <输入.bundle> --out <输出.bundle> [--cab CAB-xxxx] [--tpk <classdata.tpk>]
//
// 之后（同一条路走下去）：把 Mesh 数据换掉 → 贴图换掉 → 加蒙皮 = 我们自己的模型包。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BundlePack;

class Program
{
    static int Main(string[] args)
    {
        var o = Parse(args);
        string mold = o.GetValueOrDefault("mold");
        string outp = o.GetValueOrDefault("out");
        if (mold == null || outp == null)
        {
            Console.WriteLine("用法: bundle-pack --mold <输入.bundle> --out <输出.bundle> [--cab CAB-xxxx] [--tpk <classdata.tpk>]");
            return 1;
        }
        if (!File.Exists(mold)) { Console.WriteLine($"找不到输入：{mold}"); return 1; }

        string tpk = o.GetValueOrDefault("tpk");
        if (tpk == null)
        {
            foreach (var c in new[] { "classdata.tpk", Path.Combine(AppContext.BaseDirectory, "classdata.tpk"),
                                      Path.Combine(AppContext.BaseDirectory, "../../../../data-probe/lib/classdata.tpk") })
                if (File.Exists(c)) { tpk = c; break; }
        }
        if (tpk == null || !File.Exists(tpk)) { Console.WriteLine("找不到 classdata.tpk（用 --tpk 指定）"); return 1; }

        var am = new AssetsManager();
        am.LoadClassPackage(tpk);                           // .tpk 是"类包"，按游戏版本取类库

        var bun = am.LoadBundleFile(mold, true);            // unpackIfPacked: true
        var names = bun.file.GetAllFileNames();
        Console.WriteLine($"mold: {Path.GetFileName(mold)}  目录项: {string.Join(", ", names)}");

        int idx = -1;
        for (int i = 0; i < names.Count; i++) if (bun.file.IsAssetsFile(i)) { idx = i; break; }
        if (idx < 0) { Console.WriteLine("mold 里找不到序列化文件"); return 1; }

        var inst = am.LoadAssetsFileFromBundle(bun, idx, false);
        am.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);   // 用游戏自己的 Unity 版本取类库
        var cldb = am.ClassDatabase;
        string oldCab = names[idx];
        string newCab = o.GetValueOrDefault("cab") ?? ("CAB-" + Guid.NewGuid().ToString("N"));
        Console.WriteLine($"CAB: {oldCab}  →  {newCab}");
        Console.WriteLine($"对象数: {inst.file.AssetInfos.Count}");

        // ── 诊断模式：--inspect <pathID?> 打印指定 Mesh 的字段结构 ──
        if (o.ContainsKey("inspect"))
        {
            long want = 0; long.TryParse(o["inspect"], out want);
            foreach (var info in inst.file.AssetInfos)
            {
                AssetTypeValueField b;
                try { b = am.GetBaseField(inst, info); } catch { continue; }
                if (b == null || b.IsDummy) continue;
                var v = b["m_VertexData"];
                if (v == null || v.IsDummy) continue;
                if (want != 0 && info.PathId != want) continue;
                Console.WriteLine($"=== Mesh pathID={info.PathId} name={b["m_Name"].Value.AsString} ===");
                DumpFields(v, "m_VertexData", 0, 3);
                foreach (var other in new[] { "m_StreamData", "m_IndexBuffer", "m_IndexFormat", "m_SubMeshes", "m_MeshCompression" })
                {
                    var f = b[other];
                    if (f != null && !f.IsDummy) DumpFields(f, other, 0, 2);
                }
                if (want != 0) return 0;
            }
            return 0;
        }

        // ① 贴图/资源流路径里的 CAB 名
        var assetReplacers = new List<AssetsReplacer>();
        _replacerByPathId.Clear();
        int patched = 0;
        foreach (var info in inst.file.AssetInfos)
        {
            AssetTypeValueField bf;
            try { bf = am.GetBaseField(inst, info); } catch { continue; }
            if (bf == null) continue;
            var sd = bf["m_StreamData"];
            if (sd == null || sd.IsDummy) continue;
            var p = sd["path"];
            if (p == null || p.IsDummy) continue;
            string cur = p.Value != null ? p.Value.AsString : null;
            if (string.IsNullOrEmpty(cur) || cur.IndexOf(oldCab, StringComparison.Ordinal) < 0) continue;

            string now = cur.Replace(oldCab, newCab);
            p.Value.AsString = now;
            AddReplacer(inst, info, bf);
            patched++;
        }
        Console.WriteLine($"改过 m_StreamData.path 的资产: {patched} 个");

        // ── 可选：把 GLB 的网格写进包里（第 2 步）──────────────────────────
        string glb = o.GetValueOrDefault("glb");
        if (glb != null)
        {
            var gm = Gltf.ReadGlb(glb);
            // 朝向/位置修正（在打包阶段做 → 包本身是对的 ✓，任何消费方拿到都对）
            string rot = o.GetValueOrDefault("rotate"), off = o.GetValueOrDefault("offset");
            if (rot != null)
            {
                var d = rot.Split(','); float rx = float.Parse(d[0]), ry = d.Length > 1 ? float.Parse(d[1]) : 0f, rz = d.Length > 2 ? float.Parse(d[2]) : 0f;
                ApplyRotate(gm, rx, ry, rz);
                Console.WriteLine($"已旋转网格：({rx}, {ry}, {rz}) 度");
            }
            // 握把（原点）：--grip auto | "x,y,z"（米，网格自身坐标）→ 平移网格使该点成为原点 ✓
            string gripArg = o.GetValueOrDefault("grip");
            if (gripArg != null)
            {
                float gx, gy, gz;
                if (gripArg == "auto")
                {
                    var g = AutoGrip(gm);
                    gx = g[0]; gy = g[1]; gz = g[2];
                    Console.WriteLine($"自动测握把：({gx:0.###}, {gy:0.###}, {gz:0.###})  ← 枪托端起 8%~35% 区间内的最低点（X 取中）");
                }
                else { var d = gripArg.Split(','); gx = float.Parse(d[0]); gy = d.Length > 1 ? float.Parse(d[1]) : 0f; gz = d.Length > 2 ? float.Parse(d[2]) : 0f; }
                foreach (var p2 in gm.Positions) { p2[0] -= gx; p2[1] -= gy; p2[2] -= gz; }
                Console.WriteLine($"已把握把移到原点：({gx:0.###}, {gy:0.###}, {gz:0.###})");
            }
            if (off != null)
            {
                var d = off.Split(','); float ox = float.Parse(d[0]), oy = d.Length > 1 ? float.Parse(d[1]) : 0f, oz = d.Length > 2 ? float.Parse(d[2]) : 0f;
                foreach (var p2 in gm.Positions) { p2[0] += ox; p2[1] += oy; p2[2] += oz; }
                Console.WriteLine($"已平移网格：({ox}, {oy}, {oz}) 米");
            }
            long donor = 0;
            if (o.ContainsKey("donor") && long.TryParse(o["donor"], out long dv)) donor = dv;
            else
            {
                // 自动挑：包里顶点数最多的 Mesh
                int best = -1;
                foreach (var info in inst.file.AssetInfos)
                {
                    AssetTypeValueField b;
                    try { b = am.GetBaseField(inst, info); } catch { continue; }
                    if (b == null || b.IsDummy) continue;
                    var v = b["m_VertexData"];
                    if (v == null || v.IsDummy) continue;
                    var cnt = v["m_VertexCount"];
                    if (cnt == null || cnt.Value == null) continue;
                    int vc = cnt.Value.AsInt;
                    if (vc > best) { best = vc; donor = info.PathId; }
                }
                Console.WriteLine($"自动挑 donor：顶点最多的 Mesh = pathID {donor}（{best} 顶点）");
            }
            // 先把 .resS 读进来（网格顶点数据在里面）
            int ri = names.FindIndex(n => n.EndsWith(".resS"));
            if (ri < 0) throw new Exception("这个包里没有 .resS 资源流，无法写网格");
            bun.file.GetFileRange(ri, out long rOff, out long rLen);
            var rdr0 = bun.file.DataReader; long savePos = rdr0.Position;
            rdr0.Position = rOff; byte[] stream = rdr0.ReadBytes((int)rLen); rdr0.Position = savePos;
            WriteMeshInto(am, inst, gm, donor, stream, oldCab, newCab);
            assetReplacers = _replacerByPathId.Values.ToList();
            Console.WriteLine($"最终 replacer：{assetReplacers.Count} 个（按 pathID 去重后）");
            _patchedStream = stream;
        }

        var bundleReplacers = new List<BundleReplacer>
        {
            new BundleReplacerFromAssets(oldCab, newCab, inst.file, assetReplacers, idx, am.ClassDatabase)
        };

        // ② .resS 资源流目录项改名（内容原样搬运）
        string oldResS = oldCab + ".resS";
        // 若上面改过流（写网格），用改后的字节
        int ridx = names.FindIndex(n => n == oldResS);
        if (ridx >= 0)
        {
            bun.file.GetFileRange(ridx, out long off, out long len);
            var rdr = bun.file.DataReader;
            long save = rdr.Position;
            rdr.Position = off;
            byte[] data = _patchedStream ?? rdr.ReadBytes((int)len);
            rdr.Position = save;
            Console.WriteLine($"  写出资源流：{data.Length / 1024} KB{(_patchedStream != null ? "（含我们改过的网格数据 ✓）" : "")}");
            bundleReplacers.Add(new BundleReplacerFromMemory(oldResS, newCab + ".resS", false, data, data.Length, ridx));
            Console.WriteLine($"资源流: {oldResS} → {newCab}.resS（{len / 1024} KB）");
        }
        else Console.WriteLine($"⚠ 没有 {oldResS}（该包无资源流）");

        // ③ 写出新包
        using (var writer = new AssetsFileWriter(File.Open(outp, FileMode.Create, FileAccess.Write)))
        {
            bun.file.Write(writer, bundleReplacers, am.ClassDatabase);
        }
        Console.WriteLine($"✓ 已写出: {outp}  ({new FileInfo(outp).Length / 1024} KB)");

        // ④ 回读校验（换个 AssetsManager，模拟"别人读我们的包"）
        var am2 = new AssetsManager();
        am2.LoadClassPackage(tpk);
        var b2 = am2.LoadBundleFile(outp, true);
        var n2 = b2.file.GetAllFileNames();
        Console.WriteLine($"回读目录项: {string.Join(", ", n2)}");
        int i2 = -1;
        for (int i = 0; i < n2.Count; i++) if (b2.file.IsAssetsFile(i)) { i2 = i; break; }
        var inst2 = am2.LoadAssetsFileFromBundle(b2, i2, false);
        am2.LoadClassDatabaseFromPackage(inst2.file.Metadata.UnityVersion);
        Console.WriteLine($"回读对象数: {inst2.file.AssetInfos.Count}（原 {inst.file.AssetInfos.Count}）");

        int okTex = 0, badTex = 0;
        foreach (var info in inst2.file.AssetInfos)
        {
            AssetTypeValueField bf;
            try { bf = am2.GetBaseField(inst2, info); } catch { continue; }
            if (bf == null || bf.IsDummy) continue;
            var sd = bf["m_StreamData"];
            if (sd == null || sd.IsDummy) continue;
            var p = sd["path"];
            if (p == null || p.IsDummy) continue;
            string cur = p.Value != null ? p.Value.AsString : null;
            if (string.IsNullOrEmpty(cur)) continue;
            if (cur.IndexOf(newCab, StringComparison.Ordinal) >= 0) okTex++;
            else if (cur.IndexOf(oldCab, StringComparison.Ordinal) >= 0) { badTex++; Console.WriteLine($"  ⚠ 仍指向旧 CAB: {cur}"); }
        }
        Console.WriteLine($"回读校验：指向新 CAB 的贴图 {okTex} 个，仍指向旧 CAB 的 {badTex} 个");
        return (badTex == 0 && inst2.file.AssetInfos.Count == inst.file.AssetInfos.Count) ? 0 : 2;
    }

    // ── 第 2 步：把 GLB 的网格写进包里指定的 donor Mesh ─────────────────────
    // 关键实测：Mesh 的顶点数据**不在序列化文件里**，而在 `.resS` 资源流（`Mesh.m_StreamData` = offset/size/path）；
    // 索引（`m_IndexBuffer`）在文件里 ✓。我们的网格顶点数通常比 donor 少 → **就地覆盖流里那一段**（不挪偏移、最稳）。
    static void WriteMeshInto(AssetsManager am, AssetsFileInstance inst, GltfMesh gm, long donorPathId, byte[] stream,
                              string oldCab = null, string newCab = null)
    {
        var info = inst.file.GetAssetInfo(donorPathId);
        if (info == null) throw new Exception($"包里没有 pathID={donorPathId}");
        var bf = am.GetBaseField(inst, info);
        var vd = bf["m_VertexData"];
        int oldCount = vd[0].Value.AsInt;                       // m_VertexCount
        var chans = vd[1][0];                                   // m_Channels[14] {stream,offset,format,dimension}（[1] 是数组容器，[1][0] 才是元素表）
        var sd = bf["m_StreamData"];
        long sOff = sd[0].Value.AsLong;                         // offset (UInt64)
        int sSize = (int)sd[1].Value.AsLong;                    // size   (UInt32)
        int stride = oldCount > 0 ? sSize / oldCount : 0;
        Console.WriteLine($"donor Mesh pathID={donorPathId}  顶点={oldCount} 流段=({sOff},{sSize}) 步长={stride}");
        if (stride <= 0) throw new Exception("donor 顶点步长算不出来");

        // 通道语义**按序号**（Unity 固定：0=位置 1=法线 2=切线 3=颜色 4=UV0 …）——
        // 不能靠解析出的 dimension 猜（实测那个值不可靠 ✗），offset/format 可靠 ✓
        int posCh = 0, nrmCh = 1, uvCh = 4;
        Console.WriteLine("通道表（offset/format）：");
        for (int i = 0; i < chans.Children.Count; i++)
        {
            int st = chans[i][0].Value.AsInt, of = chans[i][1].Value.AsInt, fm = chans[i][2].Value.AsInt;
            if (of != 0 || i < 6) Console.WriteLine($"  ch{i}: stream={st} offset={of} format={fm}");
        }
        Console.WriteLine($"  采用：位置→ch{posCh}(offset={chans[posCh][1].Value.AsInt}) 法线→ch{nrmCh}(offset={chans[nrmCh][1].Value.AsInt},format={chans[nrmCh][2].Value.AsInt}) UV→ch{uvCh}(offset={chans[uvCh][1].Value.AsInt},format={chans[uvCh][2].Value.AsInt})");

        int n = gm.Positions.Count, idxCount = gm.Indices.Count;
        int need = stride * n;
        if (sOff < 0 || sOff + need > stream.Length) throw new Exception($"流不够大：需要 {need} 字节 @ {sOff}，流只有 {stream.Length}");
        Console.WriteLine($"我们的网格：顶点={n} 三角面={gm.TriangleCount} 索引={idxCount} → 需要 {need} 字节（原 {sSize}）{(need <= sSize ? "✓ 放得下" : "✗ 太大")}");

        Array.Clear(stream, (int)sOff, sSize);                  // 清掉旧顶点数据（含尾部）
        for (int v = 0; v < n; v++)
        {
            int row = (int)sOff + v * stride;
            if (posCh >= 0)
            {
                int o = chans[posCh][1].Value.AsInt;
                for (int c = 0; c < 3; c++) WriteFloat(stream, row + o + c * 4, gm.Positions[v][c]);
            }
            if (nrmCh >= 0)
            {
                int o = chans[nrmCh][1].Value.AsInt, fmt = chans[nrmCh][2].Value.AsInt;
                for (int c = 0; c < 3; c++)
                {
                    if (fmt == 1) WriteHalf(stream, row + o + c * 2, gm.Normals[v][c]);
                    else WriteFloat(stream, row + o + c * 4, gm.Normals[v][c]);
                }
            }
            if (uvCh >= 0)
            {
                int o = chans[uvCh][1].Value.AsInt, fmt = chans[uvCh][2].Value.AsInt;
                for (int c = 0; c < 2; c++)
                {
                    if (fmt == 1) WriteHalf(stream, row + o + c * 2, gm.Uvs[v][c]);
                    else WriteFloat(stream, row + o + c * 4, gm.Uvs[v][c]);
                }
            }
        }

        bool is32 = n > 65535;
        var idxBytes = new byte[idxCount * (is32 ? 4 : 2)];
        for (int i = 0; i < idxCount; i++)
        {
            if (is32) WriteU32(idxBytes, i * 4, (uint)gm.Indices[i]);
            else WriteU16(idxBytes, i * 2, (ushort)gm.Indices[i]);
        }

        vd[0].Value.AsInt = n;                                  // 顶点数
        sd[1].Value.AsLong = need;                              // 流段大小
        bf["m_IndexBuffer"][0].Value.AsByteArray = idxBytes;
        bf["m_IndexFormat"].Value.AsInt = is32 ? 1 : 0;

        var min = new float[3] { float.MaxValue, float.MaxValue, float.MaxValue };
        var max = new float[3] { float.MinValue, float.MinValue, float.MinValue };
        foreach (var p in gm.Positions) for (int c = 0; c < 3; c++) { if (p[c] < min[c]) min[c] = p[c]; if (p[c] > max[c]) max[c] = p[c]; }
        var aabb = bf["m_LocalAABB"]; var cen = aabb["m_Center"]; var ext = aabb["m_Extent"];
        for (int c = 0; c < 3; c++)
        {
            cen[c].Value.AsFloat = (min[c] + max[c]) * 0.5f;
            ext[c].Value.AsFloat = (max[c] - min[c]) * 0.5f;
        }
        var subs = bf["m_SubMeshes"][0];
        Console.Write("  原 submesh：");
        for (int i = 0; i < subs.Children.Count; i++) Console.Write($" [{i}]=" + string.Join("/", Enumerable.Range(0, subs[i].Children.Count).Select(k => subs[i][k].Value != null ? subs[i][k].Value.AsInt.ToString() : "?")));
        Console.WriteLine();
        for (int i = 0; i < subs.Children.Count; i++)
        {
            subs[i][0].Value.AsInt = 0;                         // firstByte
            subs[i][1].Value.AsInt = i == 0 ? idxCount : 0;      // indexCount
            subs[i][2].Value.AsInt = 0;                          // topology
            subs[i][3].Value.AsInt = 0;                          // baseVertex
            subs[i][4].Value.AsInt = 0;                          // firstVertex
            subs[i][5].Value.AsInt = i == 0 ? n : 0;             // vertexCount
        }
        // 顶点数据在 .resS 流里 → 这个 Mesh 的 streamData 路径也必须同步改（否则找不到数据）
        if (oldCab != null && newCab != null)
        {
            var sp = sd[2];
            string cur = sp.Value != null ? sp.Value.AsString : null;
            if (!string.IsNullOrEmpty(cur) && cur.IndexOf(oldCab, StringComparison.Ordinal) >= 0)
            {
                sp.Value.AsString = cur.Replace(oldCab, newCab);
                Console.WriteLine($"  已同步 mesh 的 streamData 路径 → {sp.Value.AsString}");
            }
        }
        AddReplacer(inst, info, bf);
        Console.WriteLine($"✓ 网格已替换：顶点 {oldCount} → {n}，索引 {idxCount}，submesh {subs.Children.Count} 个（第 2 个清零），包围盒 max={string.Join(",", max)}");
    }

    static void DumpFields(AssetTypeValueField f, string path, int depth, int maxDepth)
    {
        if (f == null || f.IsDummy) return;
        string vt = f.Value != null ? f.Value.ValueType.ToString() : "-";
        string extra = "";
        try
        {
            if (f.Value != null && f.Value.ValueType == AssetValueType.ByteArray)
                extra = $"  bytes={f.Value.AsByteArray?.Length ?? 0}";
            else if (f.Value != null && f.Value.ValueType == AssetValueType.String)
                extra = $"  \"{f.Value.AsString}\"";
            else if (f.Value != null && f.Value.ValueType == AssetValueType.Int32) extra = $"  {f.Value.AsInt}";
            else if (f.Value != null && f.Value.ValueType == AssetValueType.Int64) extra = $"  {f.Value.AsLong}";
            else if (f.Value != null && f.Value.ValueType == AssetValueType.Float) extra = $"  {f.Value.AsFloat}";
        }
        catch (Exception e) { extra = $"  (读取失败: {e.Message})"; }
        Console.WriteLine($"{new string(' ', depth * 2)}{path}  [{vt}]{extra}  children={f.Children?.Count ?? 0}");
        if (depth >= maxDepth) return;
        if (f.Children != null)
            for (int i = 0; i < f.Children.Count; i++)
                DumpFields(f.Children[i], $"{path}[{i}]", depth + 1, maxDepth);
    }

    static byte[] _patchedStream = null;
    static readonly Dictionary<long, AssetsReplacer> _replacerByPathId = new Dictionary<long, AssetsReplacer>();

    static void AddReplacer(AssetsFileInstance inst, AssetFileInfo info, AssetTypeValueField bf)
        => _replacerByPathId[info.PathId] = new AssetsReplacerFromMemory(inst.file, info, bf);

    static readonly List<AssetsReplacer> _pendingAssetReplacers = new List<AssetsReplacer>();

    /// <summary>自动测握把：① 最长轴 = 枪管轴 ② 两端横截面细的 = 枪口 → 另一端 = 枪托
    /// ③ 枪托端起 8%~35% 这段（扳机/握把所在区）里的**最低点** = 握把底部，X 取中 ✓</summary>
    static float[] AutoGrip(GltfMesh gm)
    {
        var min = new float[3] { float.MaxValue, float.MaxValue, float.MaxValue };
        var max = new float[3] { float.MinValue, float.MinValue, float.MinValue };
        foreach (var p in gm.Positions) for (int c = 0; c < 3; c++) { if (p[c] < min[c]) min[c] = p[c]; if (p[c] > max[c]) max[c] = p[c]; }
        var span = new float[3]; for (int c = 0; c < 3; c++) span[c] = max[c] - min[c];
        int ax = span[0] > span[1] ? (span[0] > span[2] ? 0 : 2) : (span[1] > span[2] ? 1 : 2);
        var oth = new List<int>(); for (int c = 0; c < 3; c++) if (c != ax) oth.Add(c);
        double Area(float lo, float hi)
        {
            var sel = gm.Positions.Where(p => p[ax] >= lo && p[ax] <= hi).ToList();
            if (sel.Count == 0) return double.MaxValue;
            return (sel.Max(p => p[oth[0]]) - sel.Min(p => p[oth[0]])) * (sel.Max(p => p[oth[1]]) - sel.Min(p => p[oth[1]]));
        }
        double aLo = Area(min[ax], min[ax] + span[ax] * 0.06f), aHi = Area(max[ax] - span[ax] * 0.06f, max[ax]);
        bool muzzleAtMin = aLo < aHi;                     // 细端 = 枪口
        // 枪托 = 枪口的另一端；从枪托端起算 8%~35%
        float stock = muzzleAtMin ? max[ax] : min[ax];
        float dir = muzzleAtMin ? -1f : 1f;                // 从枪托朝枪口
        float lo2 = stock + dir * span[ax] * 0.08f, hi2 = stock + dir * span[ax] * 0.35f;
        if (lo2 > hi2) { var t = lo2; lo2 = hi2; hi2 = t; }
        var band = gm.Positions.Where(p => p[ax] >= lo2 && p[ax] <= hi2).ToList();
        if (band.Count == 0) band = gm.Positions;
        var low = band.OrderBy(p => p[1]).First();         // 最低点（Y 最小）
        var g = new float[3];
        g[ax] = low[ax];
        g[1] = low[1] + 0.02f;                             // 从"最低"抬 2cm ≈ 握把中心
        for (int c = 0; c < 3; c++) if (c != ax && c != 1) g[c] = (min[c] + max[c]) * 0.5f;   // 其余轴取中
        if (ax == 1) g[1] = low[1] + 0.02f;
        Console.WriteLine($"  模型包围盒 min=({min[0]:0.###},{min[1]:0.###},{min[2]:0.###}) max=({max[0]:0.###},{max[1]:0.###},{max[2]:0.###}) 尺寸=({span[0]:0.###},{span[1]:0.###},{span[2]:0.###})");
        string axName = "XYZ"[ax].ToString();
        Console.WriteLine($"  最长轴={axName}  枪口在 {(!muzzleAtMin ? "+" : "-")}{axName}（细端面积 {Math.Min(aLo, aHi):0.#####} vs 粗端 {Math.Max(aLo, aHi):0.#####}）");
        return g;
    }

    /// <summary>绕 X→Y→Z 旋转网格（位置与法线；度）</summary>
    static void ApplyRotate(GltfMesh gm, float xd, float yd, float zd)
    {
        double rx = xd * Math.PI / 180, ry = yd * Math.PI / 180, rz = zd * Math.PI / 180;
        // 依次 X、Y、Z
        float[] Rx(float[] v) => new[] { v[0], (float)(v[1] * Math.Cos(rx) - v[2] * Math.Sin(rx)), (float)(v[1] * Math.Sin(rx) + v[2] * Math.Cos(rx)) };
        float[] Ry(float[] v) => new[] { (float)(v[0] * Math.Cos(ry) + v[2] * Math.Sin(ry)), v[1], (float)(-v[0] * Math.Sin(ry) + v[2] * Math.Cos(ry)) };
        float[] Rz(float[] v) => new[] { (float)(v[0] * Math.Cos(rz) - v[1] * Math.Sin(rz)), (float)(v[0] * Math.Sin(rz) + v[1] * Math.Cos(rz)), v[2] };
        for (int i = 0; i < gm.Positions.Count; i++) gm.Positions[i] = Rz(Ry(Rx(gm.Positions[i])));
        for (int i = 0; i < gm.Normals.Count; i++) gm.Normals[i] = Rz(Ry(Rx(gm.Normals[i])));
    }

    static void WriteFloat(byte[] d, int off, float v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, d, off, 4); }
    static void WriteHalf(byte[] d, int off, float v) { Buffer.BlockCopy(BitConverter.GetBytes(Gltf.ToHalf(v)), 0, d, off, 2); }
    static void WriteU16(byte[] d, int off, ushort v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, d, off, 2); }
    static void WriteU32(byte[] d, int off, uint v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, d, off, 4); }

    static Dictionary<string, string> Parse(string[] args)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var k = args[i].Substring(2);
            var v = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "1";
            d[k] = v;
        }
        return d;
    }
}
