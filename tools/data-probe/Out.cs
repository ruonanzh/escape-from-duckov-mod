// Out.cs —— data-probe 的输出收集器（`tools/data-probe` 的一部分）
//
// 为什么要这个：以前所有 action 往一个 `List<string>` 里堆 只在最后才按 `--limit` 截断
//   so `dump --follow --depth 7` (exponential fan-out) ate all memory DURING collection,
//     根本走不到截断那一步。
//
// 现在的两条路：
//   · 给了 `--out <file>` ⇒ ⭐ 边生成边写文件（流式 内存恒定）⇒ 数据一条不少
//     stdout 只回前 `preview` 行 ⇒ 既不丢证据 又不刷屏。
//   · 没给 `--out` ⇒ 存内存 到 `limit` 行就停止收集（`Truncated = true`）
//     never OOMs, and says "rerun with --out to capture everything" (display truncation != data loss).
//
// `Add` = 正式数据（会进文件）；`Note` = 只给 stdout 的说明行（如表头 / 分页提示）。

using System;
using System.Collections.Generic;
using System.IO;

sealed class Out : IDisposable
{
    readonly string path;
    readonly StreamWriter file;          // non-null => stream to disk
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
        // ⭐ 落盘也要有上限 —— 否则 `dump --depth 7 --follow` 这种指数展开会写出上亿行
        //   （实测：141,181,293 行 / 几 GB 磁盘直接爆）
        //   default cap is 1,000,000 lines; pass an explicit --limit N for more
        //   ⚠️ 上限到了会明确告知（不静默丢）
        this.limit = string.IsNullOrEmpty(path) ? Math.Max(1, limit) : Math.Max(Math.Max(1, limit), 1_000_000);
        if (!string.IsNullOrEmpty(path))
            file = new StreamWriter(path, false) { AutoFlush = false };
    }

    /// <summary>Real data line (streamed straight to disk when --out is set).</summary>
    public void Add(string line)
    {
        if (file != null)
        {
            if (written >= limit) { Truncated = true; return; }     // disk is a finite resource too
            file.WriteLine(line);
            written++;
            if (head.Count < preview) head.Add(line);
            return;
        }
        if (mem.Count >= limit) { Truncated = true; return; }   // stop at the cap instead of growing forever
        mem.Add(line);
    }

    public void AddRange(IEnumerable<string> lines)
    {
        foreach (var l in lines) Add(l);
    }

    /// <summary>stdout-only note (merged into the preview, never written to the data file).</summary>
    public void Note(string line)
    {
        if (head.Count < preview) head.Add(line);
        else mem.Add(line);              // 无文件时也保留（数量少 不影响上限判断）
    }

    /// <summary>Finish: build what stdout should show.</summary>
    public string Render()
    {
        if (file != null)
        {
            file.Flush();
            var sb = new System.Text.StringBuilder();
            foreach (var l in head) sb.Append(l).Append('\n');
            var rest = written - head.Count;
            if (rest > 0) sb.Append($"... (+{rest} more lines in the file)\n");
            sb.Append($"# wrote {written} line(s) to {path} - read/grep that file for the full result");
            if (Truncated) sb.Append($"\n# WARNING: stopped at the {limit}-line cap (exponential fan-out is dangerous) - pass an explicit --limit <N> for more");
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
