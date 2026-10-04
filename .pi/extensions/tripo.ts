import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, extname, isAbsolute, join, resolve } from "node:path";

/**
 * generate_model —— 用 Tripo 从「一句话」或「一张图」生成 3D 模型（GLB），存到你正在做的 mod 目录里。
 *
 * 自己实现（不引第三方）：Tripo 就是 HTTP + JSON ✓，Node 自带 fetch ✓ → **零依赖** ✓
 * 为什么主干自己做（判据见 doc 仓 03-tripo-api.md §9.2）：
 *   ① **产物一次到位** —— 成功立刻下载（URL 5 分钟过期）+ 存预览图/图标 + 朝向校验，固化成代码才不会漏
 *   ② 零依赖（Node 内置 fetch）；③ 按本仓约定返回 PASS/NEXT + 日志，便于验收
 *   广度（stylize / 分割 / retarget 等我们没用到的能力）可以挂现成 MCP：
 *   `pi mcp add tripo -- npx -y tripo-ai-mcp-server`（并用 exposure 只暴露需要的几个 ✓）
 * （查过现成的：官方 `tripo-mcp` ★208 / npm `tripo-ai-mcp-server` / 官方 `@vastai/tripo-sdk` /
 *  官方 `tripo-cli` —— 都能用 ✓，但它们要么多一个进程、要么还要自己串"下载 + 转朝向"；
 *  这里一次调用就产出**Unity 就绪**的 GLB ✓。若哪天想走 MCP：`pi mcp add tripo -- npx -y tripo-ai-mcp-server` ✓）
 *
 * 实测要点（doc 仓 docs/unity-3d-assets/03-tripo-api.md）：
 *   · 中国站 api.tripo3d.com（key 是哪个站的就用哪个；--global 用 .ai）
 *   · 模型 URL **5 分钟后过期** → 成功必须**立刻下载** ✓
 *   · 轮询 2 秒一次（官方限流）
 *   · 朝向**由提示词决定**（唯一可靠）：prompt 里写 "the barrel and muzzle point to the LEFT" → 枪口落在 +Z（Unity 前向）✓
 *     ⚠️ `export_orientation` 参数不可靠（同一批模型结果不一致 ✗）；几何判定对"两端都细"的武器（火箭筒）也不可用 ✗
 *   · `task_id` 可复用：convert 不重新生成、不重新上传 ✓
 *   · ⚠️ 别用 .NET 调这个 API（本机网络会 DPI 重置它的 TLS 握手 → unexpected EOF ✗）；Node/Python/curl 都通 ✓
 *
 * API key（临时方案，将来由 app 的「管理 API keys」界面接管）：
 *   1) 环境变量 TRIPO_API_KEY
 *   2) ~/.gamer-agent-pi/api-keys.json  →  { "tripo": "tsk_..." }
 * 单独一个文件、用户级（所有游戏仓库共用）；不写进仓库里的任何文件 ✓
 */

/** 临时：从独立文件读 key（将来 app 的「管理 API keys」会接管这里） */
function readTripoKey(): string | undefined {
  const env = process.env.TRIPO_API_KEY;
  if (env) return env;
  try {
    const p = join(homedir(), ".gamer-agent-pi", "api-keys.json");
    if (!existsSync(p)) return undefined;
    const j = JSON.parse(readFileSync(p, "utf8")) as { tripo?: string };
    return typeof j.tripo === "string" && j.tripo.trim() ? j.tripo.trim() : undefined;
  } catch {
    return undefined;
  }
}
export default function (pi: ExtensionAPI) {
  const BASES = {
    cn: "https://api.tripo3d.com/v2/openapi",
    global: "https://api.tripo3d.ai/v2/openapi",
  } as const;

  pi.registerTool({
    name: "generate_model",
    label: "Generate Model (Tripo)",
    description:
      "Generate a 3D model (GLB) with Tripo from a text prompt or a reference image, saved next to the mod you are building. Downloads it immediately (the remote URL expires in 5 minutes) as a Unity-ready GLB, so no rotation is needed later. Costs credits (~40 per textured low-poly model) - do not call it in a loop.",
    promptSnippet: "Generate a 3D model (GLB) with Tripo from a prompt or image",
    promptGuidelines: [
      "Use it only when no model file exists yet - if the user already has a .glb, use that.",
      "Always put the orientation in the prompt, e.g. '..., the barrel and muzzle point to the LEFT' - that is the only reliable way to control which way it faces (it lands the muzzle at +Z, Unity forward).",
      "Never retry in a loop for a better result (each call costs credits); if it FAILs about a missing API key, follow the NEXT in its message.",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [Type.Literal("generate"), Type.Literal("icon"), Type.Literal("convert"), Type.Literal("balance")],
        {
          description:
            "generate = prompt/image -> a new model; icon = make the inventory-card icon (icon.png) for a model; convert = re-export an existing task; balance = credits left.",
        },
      ),
      prompt: Type.Optional(
        Type.String({ description: 'What the model is, e.g. "PPSh-41 style submachine gun with drum magazine, game asset, side view, no hands".' }),
      ),
      image: Type.Optional(Type.String({ description: "Path to a reference image (png/jpg) for image-to-model." })),
      out: Type.Optional(Type.String({ description: "Where to save the GLB (usually inside your mod folder). Default: model.glb." })),
      taskId: Type.Optional(Type.String({ description: "For action=convert: task id from an earlier generate." })),
      model: Type.Optional(Type.String({ description: "For action=icon: the model file to make an icon for (default: the same name as out)." })),
      faceLimit: Type.Optional(Type.Number({ description: "Max triangles (default 3000). Game assets: 1500-4000." })),
      animated: Type.Optional(Type.Boolean({ description: "Keep skeleton/animation data (default false = static, correct for props and weapons)." })),
      noTexture: Type.Optional(Type.Boolean({ description: "Skip texturing (cheaper, faster)." })),
      region: Type.Optional(Type.Union([Type.Literal("cn"), Type.Literal("global")], { description: "API region (default cn)." })),
    }),

    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const key = readTripoKey();
      if (!key) {
        return {
          content: [
            {
              type: "text",
              text:
                'FAIL: no Tripo API key.\nNEXT: ask the user for their Tripo API key, then write it to ~/.gamer-agent-pi/api-keys.json as {"tripo":"tsk_..."} (separate user-level file, shared by all game repos, never committed), or set TRIPO_API_KEY.',
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
          // 预览图（Tripo 免费附带）→ 放在模型同目录的 .preview/ 下 ✓
          // 这个子目录**不会被 install_mod 装进游戏**（见 mod-install 的 EXCLUDED_DIRS ✓）—— 只是给人/agent 看的 ✓
          const base = firstOut.replace(/\.glb$/i, "");
          const shotDir = join(dirname(firstOut), ".preview");
          const shotBase = join(shotDir, basename(base));
          const shots: string[] = [];
          if (done.output?.rendered_image) {
            try {
              await download(done.output.rendered_image, `${shotBase}.preview.png`);
              shots.push(`${shotBase}.preview.png`);
              // 同一张渲染图再存一份到 mod 根做**图标**（背包卡片用 ✓ 白底会被运行时抠成透明 ✓）
              const iconPath = join(dirname(firstOut), "icon.png");
              copyFileSync(`${shotBase}.preview.png`, iconPath);
              shots.push(iconPath);
              // 记下 task id（`.preview/<模型名>.task`）→ 以后要重新取图标/换尺寸时**不再调 API、不花积分** ✓
              writeFileSync(`${shotBase}.task`, String(taskId));
            } catch { /* 预览图/图标下不到不影响主流程 */ }
          }
          if (done.output?.generated_image) {
            try { await download(done.output.generated_image, `${shotBase}.concept.jpg`); shots.push(`${shotBase}.concept.jpg`); } catch { /* 同上 */ }
          }

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
                  (shots.length ? `Preview: ${shots.join(", ")} - read/look at it and show it to the user BEFORE installing: shape is up to the prompt, orientation is already right. The plain icon.png next to the model is what the weapon card uses in game (the runtime keys its white background out).\n` : "") +
                  `NEXT: put it in your mod folder and point config.json at it (e.g. {"target":"MP5","model":"${firstOut.split("/").pop()}"}), then build and install the mod.`,
              },
            ],
          };
        }

        // action=icon：为物品生成卡片图标（512² 透明 PNG；尺寸/PPU 由运行时对齐游戏 ✓）
        if (params.action === "icon") {
          const modelPath = params.model
            ? (isAbsolute(params.model) ? params.model : resolve(cwd, params.model))
            : firstOut;
          const sidecar = join(dirname(modelPath), ".preview", `${basename(modelPath).replace(/\.glb$/i, "")}.task`);
          let renderUrl: string | null = null;

          if (existsSync(sidecar)) {
            // 这个模型是 Tripo 生成的 → 直接取它已有的渲染图（**不花积分** ✓）
            const t = await api(`/task/${readFileSync(sidecar, "utf8").trim()}`);
            renderUrl = t.output?.rendered_image ?? null;
          } else {
            // 用户自带的 GLB → 传上去（import_model）再取渲染图
            if (!existsSync(modelPath)) throw new Error(`model not found: ${modelPath}`);
            const b64 = `data:model/gltf-binary;base64,${readFileSync(modelPath).toString("base64")}`;
            const imp = await api("/task", { type: "import_model", file: b64 });
            const doneImp = await waitTask(String(imp.task_id), "import");
            renderUrl = doneImp.output?.rendered_image ?? null;
          }
          if (!renderUrl) throw new Error("no rendered_image for this model (Tripo did not return one)");

          const iconOut = join(dirname(modelPath), "icon.png");
          const size = await download(renderUrl, iconOut);
          return {
            content: [
              {
                type: "text",
                text:
                  `PASS: icon saved to ${iconOut} (${size}, 512x512 transparent PNG).\n` +
                  `NEXT: keep it named icon.png in the mod folder (the runtime shrinks it to 256 and sets the weapon-card sprite, matching the game's own icons).`,
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

/** 转静态 + 1024 PNG。**不传 export_orientation** ✗（那个参数不可靠；朝向已由提示词定好 ✓） */
async function convert(api: (p: string, b?: unknown) => Promise<any>, waitTask: (id: string, l: string) => Promise<any>, taskId: string, animated: boolean) {
  const created = await api("/task", {
    type: "convert_model",
    original_model_task_id: taskId,
    format: "GLTF",
    with_animation: animated,
    texture_size: 1024,
    texture_format: "PNG",
  });
  return waitTask(String(created.task_id), "convert");
}
