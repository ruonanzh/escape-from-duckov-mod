import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, extname, isAbsolute, resolve } from "node:path";
import { readState } from "../lib/game-paths";

/**
 * generate_model —— 用 Tripo 从「一句话」或「一张图」生成 3D 模型（GLB），存到你正在做的 mod 目录里。
 *
 * 自己实现（不引第三方）：Tripo 就是 HTTP + JSON ✓，Node 自带 fetch ✓ → **零依赖** ✓
 * （查过现成的：官方 `tripo-mcp` ★208 / npm `tripo-ai-mcp-server` / 官方 `@vastai/tripo-sdk` /
 *  官方 `tripo-cli` —— 都能用 ✓，但它们要么多一个进程、要么还要自己串"下载 + 转朝向"；
 *  这里一次调用就产出**Unity 就绪**的 GLB ✓。若哪天想走 MCP：`pi mcp add tripo -- npx -y tripo-ai-mcp-server` ✓）
 *
 * 实测要点（doc 仓 docs/unity-3d-assets/03-tripo-api.md）：
 *   · 中国站 api.tripo3d.com（key 是哪个站的就用哪个；--global 用 .ai）
 *   · 模型 URL **5 分钟后过期** → 成功必须**立刻下载** ✓
 *   · 轮询 2 秒一次（官方限流）
 *   · 导出 Unity 朝向：为道具/武器设 `export_orientation: "-x"` → 枪口/正面 = +Z（Unity 前向）✓
 *   · `task_id` 可复用：convert 不重新生成、不重新上传 ✓
 *   · ⚠️ 别用 .NET 调这个 API（本机网络会 DPI 重置它的 TLS 握手 → unexpected EOF ✗）；Node/Python/curl 都通 ✓
 */
export default function (pi: ExtensionAPI) {
  const BASES = {
    cn: "https://api.tripo3d.com/v2/openapi",
    global: "https://api.tripo3d.ai/v2/openapi",
  } as const;

  pi.registerTool({
    name: "generate_model",
    label: "Generate Model (Tripo)",
    description:
      "Generate a 3D model file (GLB) with Tripo from a text prompt or a reference image and save it where you are building the mod. This is how a model-mod gets its model (e.g. replacing a weapon's or an item's model) when the user has no model file. The tool creates the task, polls it, downloads the result immediately (the remote URL expires in 5 minutes) and re-exports it as a Unity-ready GLB (faces +Z), so the saved file needs no rotation later. Also converts an existing task again and reports the account balance. Costs credits (a low-poly model with texture is roughly 40, a conversion roughly 10) - do not call it in a loop. Needs a Tripo API key in .gamer-agent.local.json as tripo.apiKey (local to this workspace, never committed).",
    promptSnippet: "Generate a 3D model (GLB) with Tripo from a prompt or image",
    promptGuidelines: [
      "Use generate_model when a model-mod needs a model and the user has no .glb yet (ask 'what should it look like?', or use their reference image). If they already have a .glb, use that file instead - do not call this tool.",
      'Low-poly game assets come from Tripo\'s P series (the tool defaults to it, faceLimit ~3000). Pass image= for image-to-model when the user has a reference picture (better shape fidelity than text alone).',
      "The tool saves the GLB and returns PASS with the path. The result is already Unity-ready (faces +Z), so no rotation step is needed later. If you need another size/format, call it again with action=convert and the same taskId - it does not re-generate.",
      "It costs credits (~40 per textured low-poly model). Check action=balance if the user cares; never retry in a loop to 'get a better one' - improve the prompt or ask the user.",
      'If it returns FAIL because no key is configured: ask the user for their Tripo API key and explain that you will put it in .gamer-agent.local.json as {"tripo":{"apiKey":"..."}} - that file is local to the workspace and never committed.',
    ],
    parameters: Type.Object({
      action: Type.Union(
        [Type.Literal("generate"), Type.Literal("convert"), Type.Literal("balance")],
        { description: "generate = prompt/image -> a new model; convert = re-export an existing task; balance = credits left." },
      ),
      prompt: Type.Optional(
        Type.String({ description: 'What the model is, e.g. "PPSh-41 style submachine gun with drum magazine, game asset, side view, no hands".' }),
      ),
      image: Type.Optional(Type.String({ description: "Path to a reference image (png/jpg) for image-to-model." })),
      out: Type.Optional(Type.String({ description: "Where to save the GLB (usually inside your mod folder). Default: model.glb." })),
      taskId: Type.Optional(Type.String({ description: "For action=convert: task id from an earlier generate." })),
      faceLimit: Type.Optional(Type.Number({ description: "Max triangles (default 3000). Game assets: 1500-4000." })),
      animated: Type.Optional(Type.Boolean({ description: "Keep skeleton/animation data (default false = static, correct for props and weapons)." })),
      noTexture: Type.Optional(Type.Boolean({ description: "Skip texturing (cheaper, faster)." })),
      region: Type.Optional(Type.Union([Type.Literal("cn"), Type.Literal("global")], { description: "API region (default cn)." })),
    }),

    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const st = readState(cwd) as { tripo?: { apiKey?: string } };
      const key = st.tripo?.apiKey ?? process.env.TRIPO_API_KEY;
      if (!key) {
        return {
          content: [
            {
              type: "text",
              text:
                'FAIL: no Tripo API key.\nNEXT: ask the user for their Tripo API key, then write it to .gamer-agent.local.json as {"tripo":{"apiKey":"..."}} (workspace-local, never committed), or set TRIPO_API_KEY.',
            },
          ],
        };
      }
      const base = BASES[params.region === "global" ? "global" : "cn"];

      const api = async (path: string, body?: unknown): Promise<any> => {
        const r = await fetch(base + path, {
          method: body === undefined ? "GET" : "POST",
          headers: { Authorization: `Bearer ${key}`, "Content-Type": "application/json" },
          body: body === undefined ? undefined : JSON.stringify(body),
        });
        const j: any = await r.json().catch(() => ({}));
        if (j.code !== 0) throw new Error(`Tripo API code=${j.code} ${j.message ?? ""} ${j.suggestion ?? ""}`.trim());
        return j.data;
      };
      const waitTask = async (id: string, label: string): Promise<any> => {
        const t0 = Date.now();
        for (;;) {
          const d = await api(`/task/${id}`);
          const status = String(d.status);
          if (status === "success") return d;
          if (["failed", "banned", "expired", "cancelled"].includes(status))
            throw new Error(`task ${label} ${status}: ${JSON.stringify(d.task ?? d).slice(0, 300)}`);
          if (Date.now() - t0 > 900_000) throw new Error(`task ${label} timed out (15 min)`);
          await new Promise((r) => setTimeout(r, 2000));
        }
      };
      const download = async (url: string, outPath: string): Promise<string> => {
        const r = await fetch(url);
        if (!r.ok) throw new Error(`download failed HTTP ${r.status}`);
        const buf = Buffer.from(await r.arrayBuffer());
        mkdirSync(dirname(outPath), { recursive: true });
        writeFileSync(outPath, buf);
        return `${(buf.length / 1024).toFixed(0)} KB`;
      };
      const pickUrl = (output: any): string | null => {
        for (const v of Object.values(output ?? {})) if (typeof v === "string" && v.startsWith("http")) return v;
        return null;
      };

      try {
        if (params.action === "balance") {
          const d = await api("/user/balance");
          return { content: [{ type: "text", text: `PASS: Tripo balance ${d.balance} credits (frozen ${d.frozen ?? 0}).` }] };
        }

        let taskId = params.taskId ?? "";
        let firstOut = params.out && !isAbsolute(params.out) ? resolve(cwd, params.out) : params.out ?? resolve(cwd, "model.glb");

        if (params.action === "generate") {
          const body: Record<string, unknown> = {
            type: params.image ? "image_to_model" : "text_to_model",
            model_version: "P1-20260311",
            face_limit: params.faceLimit ?? 3000,
            texture: !params.noTexture,
            pbr: !params.noTexture,
          };
          if (params.image) {
            const p = isAbsolute(params.image) ? params.image : resolve(cwd, params.image);
            if (!existsSync(p)) throw new Error(`reference image not found: ${p}`);
            const mime = extname(p).toLowerCase() === ".png" ? "image/png" : "image/jpeg";
            body.image = `data:${mime};base64,${readFileSync(p).toString("base64")}`;
          } else if (params.prompt) {
            body.prompt = params.prompt;
          } else {
            throw new Error("generate needs either prompt= or image=");
          }
          const created = await api("/task", body);
          taskId = String(created.task_id);
          const done = await waitTask(taskId, "model");
          const url = done.output?.pbr_model ?? done.output?.model ?? pickUrl(done.output);
          if (!url) throw new Error(`task succeeded but no model URL: ${JSON.stringify(done.output).slice(0, 200)}`);
          const rawPath = firstOut.replace(/\.glb$/i, "") + ".raw.glb";
          const size = await download(url, rawPath);                      // ⚠️ 5 分钟过期 → 立刻下
          const converted = await convert(api, waitTask, taskId, params.animated === true);
          const url2 = pickUrl(converted.output);
          if (!url2) throw new Error("convert succeeded but no URL");
          const size2 = await download(url2, firstOut);                   // Unity 就绪朝向 ✓
          return {
            content: [
              {
                type: "text",
                text:
                  `PASS: model saved to ${firstOut} (${size2}, raw also at ${rawPath} ${size}). task_id=${taskId}\n` +
                  `NEXT: put it in your mod folder and point config.json at it (e.g. {"target":"MP5","model":"${firstOut.split("/").pop()}"}), then build and install the mod.`,
              },
            ],
          };
        }

        // action=convert
        if (!taskId) throw new Error("convert needs taskId= (from an earlier generate)");
        const converted = await convert(api, waitTask, taskId, params.animated === true);
        const url = pickUrl(converted.output);
        if (!url) throw new Error("convert succeeded but no URL");
        const size = await download(url, firstOut);
        return { content: [{ type: "text", text: `PASS: converted and saved ${firstOut} (${size}) task_id=${taskId}` }] };
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        return { content: [{ type: "text", text: `FAIL: generate_model (${params.action}): ${msg.slice(0, 800)}` }] };
      }
    },
  });
}

/** 转成 Unity 就绪：export_orientation="-x" → 枪口/正面 = +Z（Unity 前向）✓ */
async function convert(api: (p: string, b?: unknown) => Promise<any>, waitTask: (id: string, l: string) => Promise<any>, taskId: string, animated: boolean) {
  const created = await api("/task", {
    type: "convert_model",
    original_model_task_id: taskId,
    format: "GLTF",
    with_animation: animated,
    texture_size: 1024,
    texture_format: "PNG",
    export_orientation: "-x",
  });
  return waitTask(String(created.task_id), "convert");
}
