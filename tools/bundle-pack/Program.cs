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

        // ① 贴图/资源流路径里的 CAB 名
        var assetReplacers = new List<AssetsReplacer>();
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
            assetReplacers.Add(new AssetsReplacerFromMemory(inst.file, info, bf));
            patched++;
        }
        Console.WriteLine($"改过 m_StreamData.path 的资产: {patched} 个");

        var bundleReplacers = new List<BundleReplacer>
        {
            new BundleReplacerFromAssets(oldCab, newCab, inst.file, assetReplacers, idx, am.ClassDatabase)
        };

        // ② .resS 资源流目录项改名（内容原样搬运）
        string oldResS = oldCab + ".resS";
        int ridx = names.FindIndex(n => n == oldResS);
        if (ridx >= 0)
        {
            bun.file.GetFileRange(ridx, out long off, out long len);
            var rdr = bun.file.DataReader;
            long save = rdr.Position;
            rdr.Position = off;
            byte[] data = rdr.ReadBytes((int)len);
            rdr.Position = save;
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
