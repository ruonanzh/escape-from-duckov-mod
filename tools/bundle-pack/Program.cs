// bundle-pack —— 离线打 AssetBundle（不装 Unity）。第一阶段：摸清 mold 容器的结构。
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
        string mold = args.Length > 0 ? args[0] : null;
        if (mold == null || !File.Exists(mold)) { Console.WriteLine("用法: bundle-pack <mold.bundle>"); return 1; }

        var am = new AssetsManager();
        var bun = am.LoadBundleFile(mold, true);          // unpackIfPacked: true
        var f = bun.file;
        Console.WriteLine($"== mold: {Path.GetFileName(mold)} ==");
        Console.WriteLine($"  Header: sig={f.Header.Signature} ver={f.Header.Version} gen={f.Header.GenerationVersion} engine={f.Header.EngineVersion}");
        Console.WriteLine($"  DataIsCompressed={f.DataIsCompressed}");
        var fs = f.Header.FileStreamHeader;
        Console.WriteLine($"  FStreamHeader: flags={fs.Flags} total={fs.TotalFileSize} compressed={fs.CompressedSize} decompressed={fs.DecompressedSize}");

        var names = f.GetAllFileNames();
        Console.WriteLine($"  目录项 {names.Count} 个：");
        for (int i = 0; i < names.Count; i++)
        {
            f.GetFileRange(i, out long off, out long len);
            var d = f.BlockAndDirInfo.DirectoryInfos[i];
            Console.WriteLine($"    [{i}] name={names[i]} offset={off} length={len} flags=0x{d.Flags:x} decomp={d.DecompressedSize}");
        }
        var blocks = f.BlockAndDirInfo.BlockInfos;
        Console.WriteLine($"  压缩块 {blocks.Length} 个：" + string.Join(", ", blocks.Take(4).Select(b => $"(c={b.CompressedSize},d={b.DecompressedSize},f=0x{b.Flags:x})")));

        // 尝试读第一个序列化文件
        int idx = names.FindIndex(n => f.IsAssetsFile(names.IndexOf(n)));
        Console.WriteLine($"  第一个 assets 文件 index={idx}");
        if (idx >= 0)
        {
            var inst = am.LoadAssetsFileFromBundle(bun, idx, false);
            Console.WriteLine($"  ✓ 读到 assets：unityVer={inst.file.Metadata.UnityVersion} format={inst.file.Header.Version} endian={inst.file.Header.Endianness} typeTree={inst.file.Metadata.TypeTreeEnabled} 对象数={inst.file.AssetInfos.Count}");
            Console.WriteLine($"    externals={inst.file.Metadata.Externals.Count}  targets={string.Join(",", inst.file.Metadata.Externals.Select(e => e.PathName))}");
        }
        return 0;
    }
}
