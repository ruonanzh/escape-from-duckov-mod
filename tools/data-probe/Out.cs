// Out.cs —— data-probe 的输出收集器（`tools/data-probe` 的一部分）
//
// 为什么要这个 ✗：以前所有 action 往一个 `List<string>` 里堆 ✓ 只在**最后**才按 `--limit` 截断 ✓
//   ⇒ `dump --follow --depth 7` 这种**指数展开**会在"收集阶段"就把内存吃光 ✗（实测 Out of memory ✓），
//     根本走不到截断那一步 ✓。
//
// 现在的两条路 ✓：
//   · **给了 `--out <file>`** ⇒ ⭐ **边生成边写文件**（流式 ✓ 内存恒定 ✓）⇒ **数据一条不少** ✓
//     stdout 只回前 `preview` 行 ⇒ 既不丢证据 ✓ 又不刷屏 ✓。
//   · **没给 `--out`** ⇒ 存内存 ✓ 到 `limit` 行就**停止收集**（`Truncated = true` ✓）
//     ⇒ 不炸 ✓；末尾提示"用 `--out` 拿全部"✓（**显示截断 ≠ 数据丢失** —— 数据在文件里 ✓）。
//
// `Add` = 正式数据（会进文件 ✓）；`Note` = 只给 stdout 的说明行（如表头 / 分页提示 ✓）。

using System;
using System.Collections.Generic;
using System.IO;

sealed class Out : IDisposable
{
    readonly string path;
    readonly StreamWriter file;          // 非 null ⇒ 流式写盘模式 ✓
    readonly List<string> mem = new List<string>();
    readonly List<string> head = new List<string>();
    readonly int limit, preview;
    int written;

    public bool Truncated { get; private set; }
    public int LineCount => file != null ? written : mem.Count;

    public Out(string path, int limit, int preview = 40)
    {
        this.path = path;
        this.preview = Math.Max(1, preview);
        // ⭐ 落盘也要有上限 ✗ —— 否则 `dump --depth 7 --follow` 这种**指数展开**会写出上亿行 ✗
        //   （实测：141,181,293 行 / 几 GB ✓ 磁盘直接爆 ✓）
        //   ⇒ 默认给到 **100 万行**（够"完整证据"✓），想更多就显式 `--limit N` ✓
        //   ⚠️ 上限到了会**明确告知** ✗（不静默丢 ✓）
        this.limit = string.IsNullOrEmpty(path) ? Math.Max(1, limit) : Math.Max(Math.Max(1, limit), 1_000_000);
        if (!string.IsNullOrEmpty(path))
            file = new StreamWriter(path, false) { AutoFlush = false };
    }

    /// <summary>正式数据行（`--out` 时直接落盘 ✓ 不占内存 ✓）。</summary>
    public void Add(string line)
    {
        if (file != null)
        {
            if (written >= limit) { Truncated = true; return; }     // ⭐ 到达上限立刻停（磁盘也是有限资源 ✓）
            file.WriteLine(line);
            written++;
            if (head.Count < preview) head.Add(line);
            return;
        }
        if (mem.Count >= limit) { Truncated = true; return; }   // ⭐ 到上限**立刻停** ✗ 不再无限涨 ✓
        mem.Add(line);
    }

    public void AddRange(IEnumerable<string> lines)
    {
        foreach (var l in lines) Add(l);
    }

    /// <summary>只给 stdout 的说明行（合并进上面的预览 ✓ 不写进数据文件 ✓）。</summary>
    public void Note(string line)
    {
        if (head.Count < preview) head.Add(line);
        else mem.Add(line);              // 无文件时也保留（数量少 ✓ 不影响上限判断 ✓）
    }

    /// <summary>收尾：把该给 stdout 的东西拼出来 ✓。</summary>
    public string Render()
    {
        if (file != null)
        {
            file.Flush();
            var sb = new System.Text.StringBuilder();
            foreach (var l in head) sb.Append(l).Append('\n');
            var rest = written - head.Count;
            if (rest > 0) sb.Append($"... (+{rest} more lines in the file)\n");
            sb.Append($"# 结果已写入 {path}（{written} 行）—— 用 read/grep 去查它 ✓");
            if (Truncated) sb.Append($"\n# ⚠️ 到达上限 {limit} 行就停了（指数展开很危险 ✓）—— 需要更多请显式加 --limit <N> ✓");
            return sb.ToString();
        }
        var text = string.Join("\n", mem);
        if (Truncated)
            text += $"\n... (stopped at {limit} lines — rerun with --out <file> to capture everything)";
        return text;
    }

    public void Dispose()
    {
        try { file?.Flush(); file?.Dispose(); } catch { }
    }
}
