// tripo —— 调 Tripo（AI 3D 生成）的 CLI：文本/图片 → 3D 模型（GLB），给"做 mod"用。
//
// 为什么要有这个工具：agent 需要"从一句话或一张图得到模型"这一步**可执行**，
// 而不是"去调用 Tripo"这种空话 ✓（也避免每次临时写脚本 ✗）。
//
// 关键实测（详见 doc 仓 docs/unity-3d-assets/03-tripo-api.md）：
//   · 端点：中国站 https://api.tripo3d.com/v2/openapi（--global 用 .ai）
//   · 流程：POST /task 建任务 → GET /task/{id} 轮询（2 秒一次）→ **立刻下载**（URL 5 分钟过期 ✗）
//   · 游戏资产用 P 系列（低面数，face_limit 精确生效）
//   · 导出 Unity 就绪朝向：convert_model(export_orientation="-x") → 枪口/正面 = +Z（Unity 前向）
//   · task_id 复用：convert/rig/texture/… 都只传 task_id，不重新生成 ✓
//
// 用法：
//   tripo balance                                   # 看余额（不花积分）
//   tripo generate --prompt "..." --out gun.glb [--face-limit 3000] [--no-texture]
//   tripo generate --image ref.png  --out gun.glb   # 单图 → 3D
//   tripo convert --task <id> --out gun.glb [--export-orientation -x] [--texture-size 1024] [--static]
//   tripo delete-models --older-than-days 1         # 清理远端模型（省磁盘配额）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using ModelKit;

class Program
{
    const string BaseCn = "https://api.tripo3d.com/v2/openapi";
    const string BaseGlobal = "https://api.tripo3d.ai/v2/openapi";

    static string _key;
    static string _base = BaseCn;
    static readonly HttpClient Http = new HttpClient(MakeHandler()) { Timeout = TimeSpan.FromMinutes(5) };

    // 实测：本机网络会对 .NET 的 TLS 握手做 DPI 重置（"unexpected EOF"）→ 强制 TLS1.2 + HTTP/1.1 才通
    static HttpMessageHandler MakeHandler()
    {
        var h = new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            UseProxy = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        };
        return h;
    }

    static int Main(string[] args)
    {
        var o = Parse(args);
        _key = o.GetValueOrDefault("key") ?? Environment.GetEnvironmentVariable("TRIPO_API_KEY");
        if (o.ContainsKey("global")) _base = BaseGlobal;

        string action = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "help";
        if (action == "help" || o.ContainsKey("help")) { Usage(); return 0; }
        if (string.IsNullOrEmpty(_key)) { Console.Error.WriteLine("ERR: 缺 API key（--key 或环境变量 TRIPO_API_KEY；也可放 .gamer-agent.local.json 的 tripo.apiKey）"); return 2; }
        Http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _key);

        try
        {
            switch (action)
            {
                case "balance": return Balance();
                case "generate": return Generate(o);
                case "convert": return ConvertOnly(o);
                case "delete-models": return DeleteModels(o);
                default: Usage(); return 1;
            }
        }
        catch (AggregateException ae) { foreach (var e in ae.Flatten().InnerExceptions) Console.Error.WriteLine("ERR: " + e.GetType().Name + ": " + e.Message + (e.InnerException != null ? " ← " + e.InnerException.Message : "")); return 1; }
        catch (Exception e) { Console.Error.WriteLine("ERR: " + e.GetType().Name + ": " + e.Message + (e.InnerException != null ? " ← " + e.InnerException.Message : "")); return 1; }
    }

    static void Usage() => Console.WriteLine(
@"tripo —— Tripo 3D 生成 CLI（给做 mod 用）

  tripo balance
  tripo generate --prompt ""一把 AK 样子的步枪，游戏资产，侧视"" --out gun.glb [--face-limit 3000] [--no-texture] [--pbr]
  tripo generate --image ref.png --out gun.glb
  tripo convert --task <task_id> --out gun.glb [--export-orientation -x] [--texture-size 1024] [--static] [--image-format PNG]
  tripo delete-models --older-than-days 1

通用：--key <k>（或 TRIPO_API_KEY）· --global（用 .ai 全球站；默认中国站 .com）");

    // ── 接口 ────────────────────────────────────────────────────────────────
    static JsonValue Post(string path, string json)
    {
        var resp = Http.PostAsync(_base + path, new StringContent(json, Encoding.UTF8, "application/json")).Result;
        var text = resp.Content.ReadAsStringAsync().Result;
        var j = Json.Parse(text);
        if (j["code"].AsInt(-1) != 0)
            throw new Exception($"API 返回 code={j["code"].AsInt(-1)} message={j["message"].AsString("")} suggestion={j["suggestion"].AsString("")}");
        return j["data"];
    }

    static JsonValue Get(string path)
    {
        var resp = Http.GetAsync(_base + path).Result;
        var text = resp.Content.ReadAsStringAsync().Result;
        var j = Json.Parse(text);
        if (j["code"].AsInt(-1) != 0)
            throw new Exception($"API 返回 code={j["code"].AsInt(-1)} message={j["message"].AsString("")}");
        return j["data"];
    }

    static int Balance()
    {
        var d = Get("/user/balance");
        Console.WriteLine($"PASS: 余额 {d["balance"].AsInt(0)} 积分（冻结 {d["frozen"].AsInt(0)}）");
        return 0;
    }

    static JsonValue WaitTask(string taskId, string label)
    {
        var t0 = DateTime.UtcNow;
        while ((DateTime.UtcNow - t0).TotalSeconds < 900)
        {
            var d = Get("/task/" + taskId);
            var st = d["status"].AsString("");
            Console.Error.WriteLine($"  [{label}] {st} {d["progress"].AsInt(0)}% { (int)(DateTime.UtcNow - t0).TotalSeconds }s");
            if (st == "success") return d;
            if (st == "failed" || st == "banned" || st == "expired" || st == "cancelled")
                throw new Exception($"任务 {label} {st}：{d["task"].ToString()}");
            Thread.Sleep(2000);
        }
        throw new Exception($"任务 {label} 超时（15 分钟）");
    }

    static void Download(string url, string outPath)
    {
        var bytes = Http.GetByteArrayAsync(url).Result;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
        File.WriteAllBytes(outPath, bytes);
        Console.Error.WriteLine($"  ↓ {outPath}  {bytes.Length / 1024} KB");
    }

    // ── generate ────────────────────────────────────────────────────────────
    static int Generate(Dictionary<string, string> o)
    {
        string prompt = o.GetValueOrDefault("prompt");
        string image = o.GetValueOrDefault("image");
        string outPath = o.GetValueOrDefault("out") ?? "model.glb";
        int faceLimit = int.Parse(o.GetValueOrDefault("face-limit") ?? "3000");
        bool texture = !o.ContainsKey("no-texture");
        bool pbr = o.ContainsKey("pbr");

        string type = image != null ? "image_to_model" : "text_to_model";
        var sb = new StringBuilder("{");
        sb.Append($"\"type\":\"{type}\",");
        if (image != null)
        {
            var bytes = File.ReadAllBytes(image);
            var b64 = System.Convert.ToBase64String(bytes);
            var mime = image.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            sb.Append($"\"image\":\"data:{mime};base64,{b64}\",");
        }
        else if (prompt != null)
        {
            sb.Append("\"prompt\":\"").Append(Escape(prompt)).Append("\",");
        }
        else { Console.Error.WriteLine("ERR: 需要 --prompt 或 --image"); return 1; }
        sb.Append($"\"model_version\":\"P1-20260311\",\"face_limit\":{faceLimit},\"texture\":{(texture ? "true" : "false")},\"pbr\":{ (pbr ? "true" : "false") }");
        sb.Append("}");

        Console.Error.WriteLine($"建任务：{type} face_limit={faceLimit} texture={texture} pbr={pbr}");
        var data = Post("/task", sb.ToString());
        var taskId = data["task_id"].AsString("");
        Console.Error.WriteLine($"  task_id={taskId}");
        var done = WaitTask(taskId, "model");
        var outp = done["output"];
        string url = outp["pbr_model"].AsString(null) ?? outp["model"].AsString(null);
        if (url == null) throw new Exception("任务成功但没有模型 URL（输出字段：" + outp.ToString() + "）");
        Download(url, outPath);                                   // ⚠️ URL 5 分钟过期 → 立刻下 ✓
        Console.WriteLine($"PASS: 模型已保存 {outPath}  task_id={taskId}");
        return 0;
    }

    // ── convert ─────────────────────────────────────────────────────────────
    static int ConvertOnly(Dictionary<string, string> o)
    {
        string task = o.GetValueOrDefault("task");
        if (task == null) { Console.Error.WriteLine("ERR: 需要 --task <task_id>"); return 1; }
        string outPath = o.GetValueOrDefault("out") ?? "model.glb";
        string body = "{"
            + "\"type\":\"convert_model\","
            + $"\"original_model_task_id\":\"{task}\","
            + "\"format\":\"GLTF\","
            + $"\"with_animation\":{(o.ContainsKey("static") ? "false" : "true")},"
            + $"\"texture_size\":{o.GetValueOrDefault("texture-size") ?? "1024"},"
            + $"\"texture_format\":\"{o.GetValueOrDefault("image-format") ?? "PNG"}\","
            + $"\"export_orientation\":\"{o.GetValueOrDefault("export-orientation") ?? "-x"}\""
            + "}";
        var data = Post("/task", body);
        var id = data["task_id"].AsString("");
        var done = WaitTask(id, "convert");
        string url = null;
        foreach (var kv in done["output"].Object ?? new Dictionary<string, JsonValue>())
            if (kv.Value.Kind == JsonKind.String && kv.Value.Str.StartsWith("http")) { url = kv.Value.Str; break; }
        if (url == null) throw new Exception("转换成功但没有 URL");
        Download(url, outPath);
        Console.WriteLine($"PASS: 已转换并保存 {outPath}（export_orientation={o.GetValueOrDefault("export-orientation") ?? "-x"}）");
        return 0;
    }

    // ── 清理远端模型 ────────────────────────────────────────────────────────
    static int DeleteModels(Dictionary<string, string> o)
    {
        int days = int.Parse(o.GetValueOrDefault("older-than-days") ?? "1");
        var d = Get("/user/models?page_size=100");
        int n = 0;
        foreach (var m in d["list"].Array ?? new List<JsonValue>())
        {
            var created = m["create_time"].AsInt(0);
            var age = DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(created).UtcDateTime;
            if (age.TotalDays < days) continue;
            var id = m["id"] == null ? null : (m["id"].Kind == JsonKind.String ? m["id"].Str : m["id"].Number.ToString());
            if (id == null) continue;
            try { Post($"/user/model/delete?model_id={id}", "{}"); n++; } catch (Exception e) { Console.Error.WriteLine($"  删 {id} 失败：{e.Message}"); }
        }
        Console.WriteLine($"PASS: 已删 {n} 个 {days} 天前的远端模型");
        return 0;
    }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

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
