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
const NO_KEY_FAIL = {
  content: [
    {
      type: "text" as const,
      text:
        'FAIL: no Tripo API key.\nNEXT: ask the user for their Tripo API key, then write it to ~/.gamer-agent-pi/api-keys.json as {"tripo":"tsk_..."}, or set TRIPO_API_KEY.',
    },
  ],
};

/** 区域 → 端点（key 是哪个站的就用哪个站）*/
function tripoBase(region?: string): string {
  return region === "global" ? "https://api.tripo3d.ai/v2/openapi" : "https://api.tripo3d.com/v2/openapi";
}

/** 图片扩展名 → Tripo 认的 type（只有 png/jpg 两种）*/
function tripoImageType(p: string): string {
  return extname(p).toLowerCase() === ".png" ? "png" : "jpg";
}

/** 一次 API 调用（POST/GET，非 0 code 一律抛错）*/
async function tripoApi(base: string, key: string, path: string, body?: unknown): Promise<any> {
  const r = await fetch(base + path, {
    method: body === undefined ? "GET" : "POST",
    headers: { Authorization: `Bearer ${key}`, "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const j: any = await r.json().catch(() => ({}));
  if (j.code !== 0) throw new Error(`Tripo API code=${j.code} ${j.message ?? ""}`.trim());
  return j.data;
}

/** 轮询任务（2 秒一次；失败状态带上详情 ✓）*/
async function tripoWait(api: (p: string, b?: unknown) => Promise<any>, id: string, label: string): Promise<any> {
  const t0 = Date.now();
  for (;;) {
    const d = await api(`/task/${id}`);
    const status = String(d.status);
    if (status === "success") return d;
    if (["failed", "banned", "expired", "cancelled"].includes(status)) {
      throw new Error(`task ${label} ${status}: ${JSON.stringify(d.task ?? d).slice(0, 300)}`);
    }
    if (Date.now() - t0 > 900_000) throw new Error(`task ${label} timed out (15 min)`);
    await new Promise((r) => setTimeout(r, 2000));
  }
}

/** 从 output 里取第一个 http 链接（Tripo 各 task 的返回字段名不统一）*/
function tripoPickUrl(o: any): string | null {
  for (const v of Object.values(o ?? {})) if (typeof v === "string" && v.startsWith("http")) return v;
  return null;
}

/** 下载并落盘（URL 5 分钟过期 → 拿到就立刻下 ✓）*/
async function tripoSave(url: string, outPath: string): Promise<string> {
  const r = await fetch(url);
  if (!r.ok) throw new Error(`download failed HTTP ${r.status}`);
  const buf = Buffer.from(await r.arrayBuffer());
  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, buf);
  return `${(buf.length / 1024).toFixed(0)} KB`;
}

/**
 * 上传本地图片 → 返回 token（v2 /upload，multipart 字段名是 file ✓）。
 * ⚠️ **返回的字段叫 image_token，但使用时要写成 file_token** ✗✓（实测，见 doc 仓 03-tripo-api.md §10.1）
 * 单张参考图**不需要**上传（data URL 直传就行 ✓）；只有要同时给 2–4 张时才走这里 ✓
 */
async function tripoUpload(base: string, key: string, filePath: string): Promise<string> {
  const buf = readFileSync(filePath);
  const fd = new FormData();
  fd.append("file", new Blob([new Uint8Array(buf)]), basename(filePath));
  const r = await fetch(`${base}/upload`, {
    method: "POST",
    headers: { Authorization: `Bearer ${key}` },
    body: fd,
  });
  const j: any = await r.json().catch(() => ({}));
  if (j.code !== 0) throw new Error(`upload failed code=${j.code} ${j.message ?? ""}`.trim());
  const token = j.data?.image_token ?? j.data?.file_token;
  if (!token) throw new Error(`upload returned no token: ${JSON.stringify(j.data ?? {}).slice(0, 200)}`);
  return String(token);
}

export default function (pi: ExtensionAPI) {
  // ── generate_image：把「模型 / 任务 / 参考图」变成一张方形 PNG ────────────────────────
  //    一个通用动作 ✓（对应"少暴露"）。**icon 还是 preview 由 SKILL 决定** ✓：
  //      · preview（给人看风格 ✓ 便宜 ✓ 可反复改）→ out= 写 .preview/xxx.preview.png ✓
  //      · icon（游戏里那格图 ✓ 会进游戏）      → out= 写 icon.png ✓
  //    不加 style = 直接拿模型自己的渲染图（**免费** ✓ 形状必然一致 ✓）
  //    加 style / styleRef = 生成一张新图（约 5 积分 ✓）
  //    两张参考图（玩家图 + 游戏内风格锚点）→ 先 v2 /upload 拿 token（返回叫 image_token，
  //    使用时 key 必须写 file_token ✗✓）；单张则 data URL 直传（已实测 ✓ 最简）
  pi.registerTool({
    name: "generate_image",
    label: "Generate Image (Tripo)",
    description:
      "Make one square PNG from a 3D model, a Tripo task id, or a reference picture. Without style you get the model's own render (free, always matches the model); with style (and/or extra references) a new image is generated (a few credits). Saves the PNG where you ask and returns PASS with the path. Needs the Tripo API key (~/.gamer-agent-pi/api-keys.json, key 'tripo').",
    promptSnippet: "Make a square PNG (preview or icon) from a model or a reference picture",
    promptGuidelines: [
      "Use style= when you need a new look or a style match (a few credits): a preview to agree on with the user before spending ~40 credits on generate_model, or a stylized icon for an item.",
      "What to pass as reference, what to write in style=, and where the file must go differ between a preview and an icon - follow the capability skill. Previews belong under .preview/ and are never installed into the game; item icons are the file the game actually shows.",
      "Pass styleRef= with a picture of the game's own assets (an existing icon, a screenshot) so the result matches the game's look, and put the orientation wish in style= too (e.g. the barrel and muzzle point to the LEFT).",
    ],
    parameters: Type.Object({
      model: Type.Optional(Type.String({ description: "A .glb to work from (its own render, or the base for a styled image)." })),
      taskId: Type.Optional(Type.String({ description: "A Tripo task id (when the model came from generate_model)." })),
      image: Type.Optional(Type.String({ description: "A reference picture (the user's image / screenshot / concept art)." })),
      styleRef: Type.Optional(
        Type.String({ description: "A second reference showing how the game's own assets look (existing icon or screenshot)." }),
      ),
      style: Type.Optional(
        Type.String({
          description:
            'What to aim for. Omit it to just get the model\'s own render (free). Example: "clean game inventory icon, side profile, white background, centered, the barrel and muzzle point to the LEFT".',
        }),
      ),
      out: Type.Optional(
        Type.String({ description: "Where to save the PNG (e.g. icon.png to ship it, or .preview/x.preview.png for a preview)." }),
      ),
      region: Type.Optional(Type.Union([Type.Literal("cn"), Type.Literal("global")], { description: "API region (default cn)." })),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const key = readTripoKey();
      if (!key) return NO_KEY_FAIL;
      const base = tripoBase(params.region);
      const api = (path: string, body?: unknown) => tripoApi(base, key, path, body);
      const waitTask = (id: string) => tripoWait(api, id, "image");

      try {
        const modelPath = params.model ? (isAbsolute(params.model) ? params.model : resolve(cwd, params.model)) : null;
        const imagePath = params.image ? (isAbsolute(params.image) ? params.image : resolve(cwd, params.image)) : null;
        const styleRefPath = params.styleRef ? (isAbsolute(params.styleRef) ? params.styleRef : resolve(cwd, params.styleRef)) : null;
        for (const p of [modelPath, imagePath, styleRefPath]) if (p && !existsSync(p)) throw new Error(`not found: ${p}`);
        if (!modelPath && !params.taskId && !imagePath) {
          throw new Error("nothing to work from: pass model= (a .glb), taskId=, or image= (a picture)");
        }

        // ① 参考：模型的渲染图（免费 ✓ 从 task 或现算）
        let renderUrl: string | null = null;
        if (params.taskId) {
          const t = await api(`/task/${params.taskId}`);
          renderUrl = t.output?.rendered_image ?? tripoPickUrl(t.output);
        } else if (modelPath) {
          const sidecar = join(dirname(modelPath), ".preview", `${basename(modelPath).replace(/\.glb$/i, "")}.task`);
          if (existsSync(sidecar)) {
            const t = await api(`/task/${readFileSync(sidecar, "utf8").trim()}`);
            renderUrl = t.output?.rendered_image ?? null;
          } else {
            const b64 = `data:model/gltf-binary;base64,${readFileSync(modelPath).toString("base64")}`;
            const imp = await api("/task", { type: "import_model", file: b64 });
            renderUrl = (await waitTask(String(imp.task_id))).output?.rendered_image ?? null;
          }
        }

        // ② 要不要生成新图：有 style 或 有第二张参考 → 生成 ✓；否则直接给渲染图（免费 ✓）
        let url: string | null = renderUrl;
        const wantsNew = Boolean(params.style || styleRefPath);
        if (wantsNew) {
          const body: Record<string, unknown> = {
            type: "generate_image",
            prompt: params.style ?? "clean game asset image, side profile, plain background, centered, no hands",
          };
          const files: Array<Record<string, string>> = [];
          if (styleRefPath) files.push({ type: tripoImageType(styleRefPath), file_token: await tripoUpload(base, key, styleRefPath) });
          if (imagePath && styleRefPath) {
            files.unshift({ type: tripoImageType(imagePath), file_token: await tripoUpload(base, key, imagePath) });
          }
          if (files.length) {
            body.files = files;
          } else if (imagePath) {
            const mime = tripoImageType(imagePath) === "png" ? "image/png" : "image/jpeg";
            body.file = `data:${mime};base64,${readFileSync(imagePath).toString("base64")}`;
          } else if (renderUrl) {
            body.file = renderUrl;
          } else {
            throw new Error("style/styleRef needs something to work from (model=, taskId= or image=)");
          }
          const gen = await api("/task", body);
          url = tripoPickUrl((await waitTask(String(gen.task_id))).output);
        }
        if (!url) throw new Error("nothing to save (no render for this model, and no style/styleRef given)");

        // ③ 落盘：默认放 .preview/（中间图 ✓ 永远不会被装进游戏）；要当图标就让 SKILL 传 out=icon.png ✓
        const name = modelPath ? basename(modelPath).replace(/\.glb$/i, "") : "preview";
        const defaultOut = modelPath
          ? join(dirname(modelPath), ".preview", `${name}.preview.png`)
          : resolve(cwd, ".preview", `${name}.preview.png`);
        const outPath = params.out ? (isAbsolute(params.out) ? params.out : resolve(cwd, params.out)) : defaultOut;
        const size = await tripoSave(url, outPath);
        return {
          content: [
            {
              type: "text",
              text:
                `PASS: image saved to ${outPath} (${size}, square PNG). ` +
                (wantsNew ? "New image generated (a few credits spent)." : "The model's own render - no credits spent.") +
                "\nNEXT: look at it yourself (read the image) and show the user; for a preview, get their OK before generate_model (~40 credits).",
            },
          ],
        };
      } catch (e) {
        const msg = e instanceof Error ? e.message : String(e);
        return { content: [{ type: "text", text: `FAIL: generate_image: ${msg.slice(0, 600)}` }] };
      }
    },
  });

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
      const base = tripoBase(params.region);

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
              // 记下 task id（`.preview/<模型名>.task`）→ generate_image 可直接复用它的渲染图（不花积分 ✓）
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
                  (shots.length ? `Preview: ${shots.join(", ")} - read/look at it and show it to the user BEFORE installing: shape is up to the prompt, orientation is already right.\n` : "") +
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
