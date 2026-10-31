import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, extname, isAbsolute, join, resolve } from "node:path";
import type { ManifestRec } from "../lib/asset-manifest";
import { appendManifest, manifestRec, modRootOf, pickSeed, relToModRoot } from "../lib/asset-manifest";
import { serialize } from "../lib/proc";
import {
  TRIPO_API_TIMEOUT_MS,
  TRIPO_DOWNLOAD_TIMEOUT_MS,
  throwIfTripoCancelled,
  tripoNetworkRequest,
  tripoSleep,
} from "../lib/tripo-http";

/**
 * generate_model —— 用 Tripo 从「一句话」或「一张图」生成 3D 模型（GLB），存到你正在做的 mod 目录里。
 *
 * 自己实现（不引第三方）：Tripo 就是 HTTP + JSON ✓，Node 自带 fetch ✓ → **零依赖** ✓
 * 为什么主干自己做（判据见设计仓 desktop-gamer-agent-pi 的 docs/unity-3d-assets/03-tripo-api.md §9.2）：
 *   ① **产物一次到位** —— 成功立刻下载（URL 5 分钟过期）+ 存预览图/图标 + 朝向校验，固化成代码才不会漏
 *   ② 零依赖（Node 内置 fetch）；③ 按本仓约定返回 PASS/NEXT + 日志，便于验收
 *   广度（stylize / 分割 / retarget 等我们没用到的能力）可以挂现成 MCP：
 *   `pi mcp add tripo -- npx -y tripo-ai-mcp-server`（并用 exposure 只暴露需要的几个 ✓）
 * （查过现成的：官方 `tripo-mcp` ★208 / npm `tripo-ai-mcp-server` / 官方 `@vastai/tripo-sdk` /
 *  官方 `tripo-cli` —— 都能用 ✓，但它们要么多一个进程、要么还要自己串"下载 + 转朝向"；
 *  这里一次调用就产出**Unity 就绪**的 GLB ✓。若哪天想走 MCP：`pi mcp add tripo -- npx -y tripo-ai-mcp-server` ✓）
 *
 * 实测要点（设计仓 desktop-gamer-agent-pi 的 docs/unity-3d-assets/03-tripo-api.md）：
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
/**
 * 没 key 就抛 ✗ —— 属于「前置条件不满足 / 没法执行」→ **throw**（isError ✓）
 * （按仓库标准：工具执行错误 throw、业务结果才 return PASS/FAIL+NEXT ✓）
 */
function tripoRequireKey(): string {
  const key = readTripoKey();
  if (!key) {
    throw new Error(
      'TRIPO_NO_API_KEY: ask the player for the Tripo API key, then save it to ~/.gamer-agent-pi/api-keys.json as {"tripo":"tsk_..."} (a user-level file shared by all game repositories, never committed), or set TRIPO_API_KEY',
    );
  }
  return key;
}

/**
 * ⭐ v3 端点（当前 API ✓ 2026-10-05 实测迁移）。
 * 实测确认（设计仓 desktop-gamer-agent-pi 的 docs/unity-3d-assets/03-tripo-api.md §11 + 官方 43 页文档 ✓）：
 *   · 建任务：POST /v3/generation/{text-to-model|image-to-model|text-to-image|image-to-image}
 *   · 查任务：GET /v3/tasks/{id} → data.status / data.progress / data.output（键名带 _url 后缀 ✓）
 *   · 余额：GET /v3/account/balance ✓（v3 **有**这个端点 ✓；/user/balance 是 v2 的 ✗）
 *   · 转换：POST /v3/models/convert { input: <task_id> }（v2 叫 original_model_task_id ✗）
 *   · 上传：POST /v3/files（multipart 字段 file）→ **file_token**（v2 返回 image_token ✗ 且 v2 token v3 不认 ✗）
 *   · 图片输入（**文档形式**✓）：`input: <string>`（主参考，必选）+ `inputs: [<string>…]`（追加参考）
 *     每个 string 可以是 公开 URL / file_token / **task_id** ✓（task_id 直传可省一次上传 ✓）
 *     `file: {url|object|file_token}` 是 v2 形态 ✓ 仍能用 ✓ 但与文档不一致 ✗ 且不接受 task_id ✗
 *   · 限流：code 2000 / HTTP 429 = "exceeded the limit of generation" → 别连发 ✓
 */
const TRIPO_V3 = "https://openapi.tripo3d.com/v3";
const TRIPO_V2 = "https://api.tripo3d.com/v2/openapi";

/** 图片扩展名 → Tripo 认的 type（只有 png/jpg 两种）*/
function tripoImageType(p: string): string {
  return extname(p).toLowerCase() === ".png" ? "png" : "jpg";
}

/** 一次 API 调用。v3 把结果包在 `data` 里 ✓；限流单独给一句 NEXT ✓ */
async function tripoApi(base: string, key: string, path: string, body?: unknown, signal?: AbortSignal): Promise<any> {
  return tripoNetworkRequest(
    { timeoutMs: TRIPO_API_TIMEOUT_MS, timeoutCode: "TRIPO_API_TIMEOUT", signal },
    async (requestSignal) => {
      const r = await fetch(base + path, {
        method: body === undefined ? "GET" : "POST",
        headers: { Authorization: `Bearer ${key}`, "Content-Type": "application/json" },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: requestSignal,
      });
      const j: any = await r.json().catch(() => ({}));
      if (j.code === 2000 || r.status === 429) {
        throw new Error("TRIPO_RATE_LIMIT: the API limit was hit (exceeded the limit of generation) - wait 30-60s before the next call and do not retry in a loop");
      }
      if (j.code !== 0) {
        throw new Error(`TRIPO_API_ERROR: code=${j.code} ${j.message ?? ""} ${j.suggestion ?? ""}`.trim());
      }
      if (!r.ok) throw new Error(`TRIPO_HTTP_ERROR: status=${r.status} on ${path}`);
      return j.data ?? j; // v3: {code,status,data} ✓ / v2 兜底: 已经是 data ✓
    },
  );
}

/** 轮询任务（2 秒一次；失败状态带上详情 ✓）*/
async function tripoWait(api: (p: string, b?: unknown) => Promise<any>, id: string, label: string, signal?: AbortSignal): Promise<any> {
  const t0 = Date.now();
  for (;;) {
    throwIfTripoCancelled(signal);
    const d = await api(`/tasks/${id}`); // ⭐ v3 路径（v2 是 /task/{id} ✗）
    const status = String(d?.status);
    if (status === "success") return d;
    if (["failed", "banned", "expired", "cancelled"].includes(status)) {
      throw new Error(`TRIPO_TASK_FAILED: ${label} ${status}: ${JSON.stringify(d.task ?? d).slice(0, 300)}`);
    }
    if (Date.now() - t0 > 900_000) throw new Error(`TRIPO_TASK_TIMEOUT: ${label} did not finish within 15 minutes`);
    await tripoSleep(2000, signal);
  }
}

/** 从 output 里取第一个 http 链接（v3 的键名带 _url 后缀：model_url / rendered_image_url … ✓）*/
function tripoPickUrl(o: any): string | null {
  for (const v of Object.values(o ?? {})) if (typeof v === "string" && v.startsWith("http")) return v;
  return null;
}

/**
 * 落盘前的保护：目标文件已存在就**报错** ✓（绝不静默覆盖玩家/自己产出的文件 ✗）
 * 两种解法都写进错误里：① 换个名字（例如按武器命名 ✓）② 已确认旧文件没用了就先删掉 ✓
 */
function guardOutNotExists(outPath: string): void {
  if (!existsSync(outPath)) return;
  throw new Error(
    `OUT_FILE_EXISTS: ${outPath} already exists - nothing was written. ` +
      `Either pick a different file name (for example name files after the weapon: ak103.glb or ak103.preview.png), ` +
      `or delete that file first if you have checked it is not used any more.`,
  );
}

/** 下载并落盘（URL 会过期 → 拿到就立刻下 ✓）*/
async function tripoSave(url: string, outPath: string, signal?: AbortSignal): Promise<string> {
  const buf = await tripoNetworkRequest(
    { timeoutMs: TRIPO_DOWNLOAD_TIMEOUT_MS, timeoutCode: "TRIPO_DOWNLOAD_TIMEOUT", signal },
    async (requestSignal) => {
      const r = await fetch(url, { signal: requestSignal });
      if (!r.ok) throw new Error(`TRIPO_DOWNLOAD_FAILED: HTTP ${r.status} for ${url.slice(0, 120)}`);
      return Buffer.from(await r.arrayBuffer());
    },
  );
  mkdirSync(dirname(outPath), { recursive: true });
  writeFileSync(outPath, buf);
  return `${(buf.length / 1024).toFixed(0)} KB`;
}

/**
 * 图片引用 → v3 的 file 描述符。
 * · 已经是 http(s) → { url } ✓（Tripo 托管的渲染图/上一张图的 URL ✓ 不用上传 ✓）
 * · 本地文件 → POST /v3/files 上传 → { file_token } ✓（返回字段就叫 file_token ✓）
 * ⚠️ 只接受 url / file_token / object ✗ —— **不能传 {task_id}** ✗（链路要传上游那张图的 URL ✓）
 */
async function tripoFileRef(key: string, pathOrUrl: string, signal?: AbortSignal): Promise<Record<string, string>> {
  if (/^https?:\/\//i.test(pathOrUrl)) return { url: pathOrUrl };
  if (!existsSync(pathOrUrl)) throw new Error(`TRIPO_FILE_NOT_FOUND: ${pathOrUrl}`);
  const buf = readFileSync(pathOrUrl);
  const fd = new FormData();
  fd.append("file", new Blob([new Uint8Array(buf)]), basename(pathOrUrl));
  return tripoNetworkRequest(
    { timeoutMs: TRIPO_API_TIMEOUT_MS, timeoutCode: "TRIPO_API_TIMEOUT", signal },
    async (requestSignal) => {
      const r = await fetch(`${TRIPO_V3}/files`, {
        method: "POST",
        headers: { Authorization: `Bearer ${key}` },
        body: fd,
        signal: requestSignal,
      });
      const j: any = await r.json().catch(() => ({}));
      if (j.code !== 0) throw new Error(`TRIPO_UPLOAD_FAILED: code=${j.code} ${j.message ?? ""}`.trim());
      const token = j.data?.file_token ?? j.data?.image_token;
      if (!token) throw new Error(`TRIPO_UPLOAD_NO_TOKEN: ${JSON.stringify(j.data ?? {}).slice(0, 200)}`);
      return { file_token: String(token) };
    },
  );
}

/** Tripo task id 的形状（实测都是 uuid ✓；用形状判断 ✓ 不会误伤文件路径 ✗）*/
const TRIPO_TASK_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * v3 **文档形式**的输入引用 → 一个字符串 ✓（给 `input` / `inputs` 用）
 *   · 公开 URL → 原样 ✓
 *   · Tripo task id → 原样 ✓（API 自己推断来源 ✓ **省一次上传** ✓ 也不要再下载 ✓）
 *   · 本地文件 → POST /v3/files → `file_token` 字符串 ✓
 */
async function tripoInputRef(key: string, value: string, signal?: AbortSignal): Promise<string> {
  if (/^https?:\/\//i.test(value)) return value;
  if (TRIPO_TASK_RE.test(value)) return value;
  const ref = await tripoFileRef(key, value, signal);
  return String(ref.file_token ?? ref.url);
}

/**
 * 一个「待写清单的步骤」。
 *
 * 任务的 id 要等接口返回才知道，落盘要等下载完 —— 所以先攒起来，落盘之后统一写。
 * （设计 §5.1 时机：文件落盘之后）
 */
type PendingStep = {
  endpoint: string;
  requestId: string | null;
  parent: string | null;
  seed: number | null;
  /** 绝对路径 ✓ 写清单时再换成相对 mod 根的写法 */
  filesAbs: string[];
  /** `input` 里可能含本地路径 —— 所以延迟到写清单时再拼 */
  buildInput: (rel: (p: string) => string) => Record<string, unknown>;
};

/** 把一个待写步骤落成清单行。`error` 有值时 `files` 是空数组（设计 §5.2）。返回 null = 成功。 */
function flushStep(modRoot: string, name: string, step: PendingStep, error?: string): string | null {
  const rel = (p: string) => relToModRoot(modRoot, p);
  const rec: Omit<ManifestRec, "t" | "provider"> = {
    endpoint: step.endpoint,
    name,
    request_id: step.requestId,
    parent_request_id: step.parent,
    seed: step.seed,
    files: error ? [] : step.filesAbs.map(rel),
    input: step.buildInput(rel),
  };
  if (error) rec.error = error;
  return appendManifest(modRoot, manifestRec(rec));
}

/** 产物名 = 输出文件名去扩展名（设计 §5.2）*/
function outName(outPath: string): string {
  return basename(outPath).replace(/\.[^.]+$/, "");
}

/** 写清单失败只在返回文本里加一句 WARN，不影响主流程（设计 §3.4 规则 6）*/
function warnLine(warns: string[]): string {
  return warns.length ? `\nWARN: ${warns.join("; ")}` : "";
}

export default function (pi: ExtensionAPI) {
  // ── generate_image：把「模型 / 任务 / 参考图」变成一张方形 PNG ────────────────────────
  //    一个通用动作 ✓（对应"少暴露"）。**icon 还是 preview 由 SKILL 决定** ✓：
  //      · preview（给人看风格 ✓ 便宜 ✓ 可反复改）→ out= 写 .preview/xxx.preview.png ✓
  //      · icon（游戏里那格图 ✓ 会进游戏）      → out= 写 icon.png ✓
  //    不加 prompt = 直接拿模型自己的渲染图（**免费** ✓ 形状必然一致 ✓）
  //    加 prompt / styleRef = 生成一张新图（约 5 积分 ✓）
  //    参考图：Tripo 托管 URL 直接用 {url} ✓；本地文件 → POST /v3/files → {file_token} ✓
  //    （v3 的 file 只认 url / file_token / object ✗ —— 不能传 {task_id} ✗）
  pi.registerTool({
    name: "generate_image",
    label: "Generate Image (Tripo)",
    description:
      "Make one square PNG from a 3D model, a Tripo task id, a reference picture, or a description alone. With prompt, the tool generates a new image and spends a few credits. With model= or taskId= given and no prompt, you get that model's own render for free. The tool saves the file to out= and returns PASS with the path. It needs the Tripo API key in ~/.gamer-agent-pi/api-keys.json, under the key 'tripo'.",
    promptSnippet: "Make a square PNG (preview or icon) from a model or a reference picture",
    promptGuidelines: [
      "Use generate_image when a capability needs a 2D image file. It saves a square PNG to out= and returns PASS with the path.",
      "Use generate_image with prompt= to get a new image drawn from the description, a reference picture, or a model. This spends a few credits. Omit prompt= and pass model= or taskId= to get the model's own render for free. That render is the model's raw view, not a composed icon.",
      "Use generate_image with styleRef= only when a picture of the game's own assets is already at hand. The player gave a screenshot, or a file already lives in the workspace. Do not spend turns searching for one. The capability skill describes what to write in prompt= and where the file must go.",
      "Before generating an asset again, read asset_manifest.jsonl in the mod directory (last lines only). Reuse an earlier task_id with taskId= to avoid paying twice.",
    ],
    parameters: Type.Object({
      model: Type.Optional(Type.String({ description: "A .glb to work from (its own render, or the base for a styled image)." })),
      taskId: Type.Optional(Type.String({ description: "A Tripo task id (when the model came from generate_model)." })),
      image: Type.Optional(Type.String({ description: "A reference picture (the player's image, screenshot, or concept art)." })),
      styleRef: Type.Optional(
        Type.String({ description: "A second reference showing how the game's own assets look (existing icon or screenshot)." }),
      ),
      // 我们的"配方"（= 直接送进 API 的 `prompt` 字段的主提示词 ✓）
      // ⚠ 名字就叫 prompt ✓ 不再叫 style ✗（旧名字会让人以为它只是"风格"，其实整段就是 prompt ✓）
      prompt: Type.Optional(
        Type.String({
          description:
            'The prompt sent to the API (the main instruction - our recipe from the capability skill). With model= or taskId= given, omit it to just get that model\'s own render (free). Example: "clean game inventory icon, side profile, white background, centered, the barrel and muzzle point to the LEFT".',
        }),
      ),
      out: Type.Optional(
        Type.String({ description: "Where to save the PNG (for example icon.png to ship it, or .preview/x.preview.png for a preview)." }),
      ),
    }),
    async execute(_toolCallId, params, signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const key = tripoRequireKey(); // 没 key 直接 throw ✓（前置条件不满足）
      const api = (path: string, body?: unknown) => tripoApi(TRIPO_V3, key, path, body, signal);
      const waitTask = (id: string) => tripoWait(api, id, "image", signal);

      const modelPath = params.model ? (isAbsolute(params.model) ? params.model : resolve(cwd, params.model)) : null;
      const imagePath = params.image ? (isAbsolute(params.image) ? params.image : resolve(cwd, params.image)) : null;
      const styleRefPath = params.styleRef ? (isAbsolute(params.styleRef) ? params.styleRef : resolve(cwd, params.styleRef)) : null;
      for (const p of [modelPath, imagePath, styleRefPath]) if (p && !existsSync(p)) throw new Error(`TRIPO_FILE_NOT_FOUND: ${p}`);
      // 三个"创作输入"（提示词 / 玩家参考图 / 游戏风格参考图）+ 两个"模型指针"（model / taskId）
      // **至少给一个就能发** ✓（只给 model/taskId → 免费渲染图；其余 → 生成新图）
      // 一个都不给则明确报错 ✓（不会默默什么都不做 ✗）
      if (!modelPath && !params.taskId && !imagePath && !styleRefPath && !params.prompt) {
        throw new Error(
          "nothing to work from: pass at least one of prompt= (a description), image= (the player's picture), styleRef= (how the game's assets look), model= (a .glb) or taskId=",
        );
      }

      // ① 参考：模型的渲染图（免费 ✓ 从 task 或现算）
      let renderUrl: string | null = null;
      /** 清单：这一步（写入点 2 / 3 —— 设计 §5.2）。落盘之后再写 ✓ */
      let renderStep: PendingStep | null = null;
      if (params.taskId) {
        const t = await api(`/tasks/${params.taskId}`); // ⭐ v3
        renderUrl = t.output?.rendered_image_url ?? t.output?.rendered_image ?? tripoPickUrl(t.output);
        renderStep = { endpoint: `/v3/tasks/${params.taskId}`, requestId: params.taskId, parent: null, seed: null, filesAbs: [], buildInput: () => ({ taskId: params.taskId }) };
      } else if (modelPath) {
        const sidecar = join(dirname(modelPath), ".preview", `${basename(modelPath).replace(/\.glb$/i, "")}.task`);
        const sidecarId = existsSync(sidecar) ? readFileSync(sidecar, "utf8").trim() : null;
        if (sidecarId) {
          // ⭐ 每次都重新取一次 task → 拿到的是**新鲜的**签名 URL（老的会过期 ✗ 这正是之前 1004 的坑 ✓）
          const t = await api(`/tasks/${sidecarId}`);
          renderUrl = t.output?.rendered_image_url ?? t.output?.rendered_image ?? null;
          renderStep = { endpoint: `/v3/tasks/${sidecarId}`, requestId: sidecarId, parent: null, seed: null, filesAbs: [], buildInput: () => ({ taskId: sidecarId }) };
        } else {
          // 玩家自带的 .glb（没有 task）：v3 没有 import 端点 ✗ → 兜底仍走 v2 的 import_model ✓
          const b64 = `data:model/gltf-binary;base64,${readFileSync(modelPath).toString("base64")}`;
          const imp = await tripoApi(TRIPO_V2, key, "/task", { type: "import_model", file: b64 }, signal);
          const done = await tripoWait((pp, bb) => tripoApi(TRIPO_V2, key, pp, bb, signal), String(imp.task_id), "import", signal);
          renderUrl = done.output?.rendered_image ?? tripoPickUrl(done.output);
          renderStep = {
            // v2 的 base 已含 /v2/openapi ✓ → 写出来要补全（设计 §5.2 endpoint 的写法）
            endpoint: "/v2/openapi/task",
            requestId: String(imp.task_id),
            parent: null,
            seed: null,
            filesAbs: [],
            // **不得写 base64** ✗（设计 §5.1）：只写本地路径，写清单时换成相对 mod 根
            buildInput: (rel) => ({ type: "import_model", file: rel(modelPath) }),
          };
        }
      }

      // ② 要不要生成新图：给了 prompt / 参考图 才生成 ✓；只给 model/taskId → 直接返回渲染图（免费 ✓）
      //    ⭐ 先把输出路径定下来并检查（已存在就报错 ✗）—— 放在生成前 ✓ 否则白花积分 ✗
      //    参考图按重要性排序：模型的渲染图（它长什么样）→ 玩家给的图 → 游戏内风格锚点
      //    1 张 → `file: {url|file_token}` ✓；≥2 张 → `inputs: [...]` ✓（v3 ✓）
      const refs: Array<{ kind: "render" | "file"; value: string }> = [];
      if (renderUrl) refs.push({ kind: "render", value: renderUrl });
      if (imagePath) refs.push({ kind: "file", value: imagePath });
      if (styleRefPath) refs.push({ kind: "file", value: styleRefPath });

      // ⭐ 输出路径先定好并检查（已存在就报错 ✗）—— 必须在**发起生成之前** ✓ 否则白花积分 ✗
      const name = modelPath ? basename(modelPath).replace(/\.glb$/i, "") : "preview";
      const defaultOut = modelPath
        ? join(dirname(modelPath), ".preview", `${name}.preview.png`)
        : resolve(cwd, ".preview", `${name}.preview.png`);
      const outPath = params.out ? (isAbsolute(params.out) ? params.out : resolve(cwd, params.out)) : defaultOut;
      guardOutNotExists(outPath);

      let url: string | null = renderUrl;
      let genTaskId: string | null = null; // 记下来给模型那步复用 ✓（省一次上传+一次渲染 ✓）
      let genStep: PendingStep | null = null;
      const wantsNew = Boolean(params.prompt || imagePath || styleRefPath);
      if (wantsNew) {
        // ⭐ v3：没有参考 → /generation/text-to-image（纯提示词 ✓ 已实测）；
        //         1 张参考 → /generation/image-to-image { file: {url|file_token} } ✓；
        //         ≥2 张   → { inputs: [...] } ✓ 并在提示词里指明 image[1] 是主体、后面是风格 ✓
        // ⭐ prompt：SKILL 里那段**实测过的默认配方**（最贴合本作风格 ✓）
        //    用户只给了图 → 直接用这段话 ✓；
        //    用户另提了要求（或只给了文字）→ **由 agent 在配方基础上改** ✓（不是工具拼接 ✗）
        let prompt =
          params.prompt ?? "clean game asset image, side profile, plain background, centered, no hands";
        const body: Record<string, unknown> = { prompt };
        if (refs.length === 0) {
          var path = "/generation/text-to-image";
        } else if (refs.length === 1) {
          body.input = await tripoInputRef(key, refs[0].value, signal);
          var path = "/generation/image-to-image";
        } else {
          // ⭐ 文档形式：input = 主参考（必选 ✓）+ inputs = 追加参考（string[] ✓）
          body.input = await tripoInputRef(key, refs[0].value, signal);
          body.inputs = [];
          for (const r of refs.slice(1)) (body.inputs as string[]).push(await tripoInputRef(key, r.value, signal));
          if (!/image\[/.test(prompt)) {
            prompt = `${prompt} (use image[1] as the subject; later images are the style/look reference)`;
            body.prompt = prompt;
          }
          var path = "/generation/image-to-image";
        }
        // 生图固定参数：让图**原生带透明** ✓ ——
        //   · background=transparent 只有 chat_image_2.5_flare / _sunburst 支持（其它模型**忽略**它而非报错 ✗）
        //   · transparent **必须配 output_format=png** ✗ 否则直接报错
        //   · 实测：2048² PNG、四角 alpha=0 ✓ 边缘抗锯齿 ✓ 居中偏差 ~1px ✓（纯文字 / 1 张 / 2 张参考图都通过 ✓）
        //   → 这样运行时就不用猜背景 ✓（WeaponIcon 里已不做背景处理 ✓）
        body.model = "chat_image_2.5_flare";
        body.background = "transparent";
        body.output_format = "png";
        const generated = await serialize("tripo:paid-generation", async () => {
          const created = await api(path, body);
          const id = String(created.task_id);
          return { created, done: await waitTask(id) };
        });
        const gen = generated.created;
        const genDone = generated.done;
        genTaskId = String(gen.task_id);
        url = tripoPickUrl(genDone.output);
        // 清单：生成那一步（写入点 1 ✓ 设计 §5.2）
        genStep = {
          endpoint: `/v3${path}`,
          requestId: genTaskId,
          parent: null,
          seed: pickSeed(gen) ?? pickSeed(genDone),
          filesAbs: [],
          buildInput: (rel) => {
            const i: Record<string, unknown> = {
              prompt: body.prompt,
              model: body.model,
              background: body.background,
              output_format: body.output_format,
            };
            // 有参考图时加参考图的本地路径（设计 §5.2 写入点 1）
            const refPath = imagePath ?? styleRefPath;
            if (refPath) i.image = rel(refPath);
            return i;
          },
        };
      }
      if (!url) throw new Error("nothing to save (no render for this model, and no prompt/styleRef given)");

      // ③ 落盘：默认放 .preview/（中间图 ✓ 永远不会被装进游戏）；要当图标就让 SKILL 传 out=icon.png ✓
      const modRoot = modRootOf(cwd, outPath);
      const manifestName = outName(outPath);
      const warns: string[] = [];
      let size: string;
      try {
        size = await tripoSave(url, outPath, signal);
      } catch (e) {
        for (const step of [renderStep, genStep]) {
          if (!step) continue;
          const w = flushStep(modRoot, manifestName, step, String((e as Error).message));
          if (w) warns.push(w);
        }
        throw e;
      }
      // 清单：落盘之后再写 ✓（设计 §5.1 时机）；写失败只出 WARN ✗ 不影响主流程 ✓
      for (const step of [renderStep, genStep]) {
        if (!step) continue;
        step.filesAbs = [outPath];
        const w = flushStep(modRoot, manifestName, step);
        if (w) warns.push(w);
      }
      // 把图直接发到聊天里，让玩家看得见（app 会渲染 type:"image" 的块；当前模型看不了图时
      // app 会把它标成 deferred -> 不塞给模型，所以这不会让不支持视觉的模型报错）
      const ext = extname(outPath).toLowerCase();
      const mimeType = ext === ".png" ? "image/png" : ext === ".webp" ? "image/webp" : "image/jpeg";
      let shown = false;
      try {
        const buf = readFileSync(outPath);
        if (buf.length <= 1_500_000) {
          pi.sendMessage({
            customType: "pi-desktop-image",
            content: [
              { type: "image", data: buf.toString("base64"), mimeType },
              { type: "text", text: `image: ${outPath}` },
            ],
            display: true,
          });
          shown = true;
        }
      } catch {
        /* 显示失败不影响主流程（路径仍在返回里） */
      }
      return {
        content: [
          {
            type: "text",
            text:
              `PASS: image saved to ${outPath} (${size}, square PNG). ` +
              (wantsNew ? "New image generated (a few credits spent)." : "The model's own render - no credits spent.") +
              (genTaskId ? `\nImage task id: ${genTaskId} (pass it to generate_model as taskId= to skip re-uploading).` : "") +
              warnLine(warns) +
              (shown
                  ? "\nShown in the chat so the player can see it without opening the file."
                  : "\n(The image is too large to show inline - give the player the path above.)"),
          },
        ],
      };
    },
  });

  pi.registerTool({
    name: "generate_model",
    label: "Generate Model (Tripo)",
    description:
      "Generate a 3D model (GLB) with Tripo from a text prompt or a reference picture, and save it to out=. The tool downloads the model immediately, because the remote URL expires in 5 minutes. It exports the model in the game's orientation convention (front = +Z). With action=rig it adds bones to a finished model, and with action=retarget it bakes one preset animation into the rigged model. This costs credits: about 40 for a textured low-poly model, plus about 10 for the format conversion. Do not call it in a loop. It needs the Tripo API key in ~/.gamer-agent-pi/api-keys.json, under the key 'tripo'.",
    promptSnippet: "Generate a 3D model (GLB) with Tripo from a prompt or image",
    promptGuidelines: [
      "Use generate_model only when no model file exists yet. If the player already has a .glb, use that file.",
      "Use generate_model with image= when a picture is available to build from. An image is far more accurate than text alone. Use prompt= when there is only a description. faceLimit= caps the triangles (default 3000).",
      "Rig in three steps: action=rig-check to see whether the model can take bones, then action=rig (spec=mixamo gives the standard Mixamo humanoid names, biped only), then action=retarget with animation=preset:walk. Each call spends credits.",
      "The input pose decides the rig quality: a standing figure with clearly separated limbs rigs well, while a sitting or curled one gives a coarse skeleton. rig-check is advice only, so always read the joint list that rig= prints in its result.",
      "Do not call generate_model again for a better result. Each call costs credits, and the API rate-limits short bursts.",
      "Before generating an asset again, read asset_manifest.jsonl in the mod directory (last lines only). If the model already exists, reuse its task_id with action=convert instead of generating.",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [
          Type.Literal("generate"),
          Type.Literal("convert"),
          Type.Literal("balance"),
          Type.Literal("rig-check"),
          Type.Literal("rig"),
          Type.Literal("retarget"),
        ],
        { description: "generate: make a new model from a prompt or an image. convert: re-export an existing task. balance: show the credits left." },
      ),
      prompt: Type.Optional(
        Type.String({ description: 'What the model is, for example "PPSh-41 style submachine gun with drum magazine, game asset, side view, no hands".' }),
      ),
      image: Type.Optional(Type.String({ description: "Path to a reference image (png or jpg) for image-to-model." })),
      out: Type.Optional(Type.String({ description: "Where to save the GLB (usually inside your mod directory). Default: model.glb." })),
      taskId: Type.Optional(Type.String({ description: "A Tripo task id. For action=convert, it is the model to convert. For action=generate, it is the image source (an earlier image task)." })),
      faceLimit: Type.Optional(Type.Number({ description: "Max triangles (default 3000). Game assets: 1500-4000." })),
      animated: Type.Optional(Type.Boolean({ description: "Keep skeleton or animation data (default false). A static model is correct for props and weapons." })),
      noTexture: Type.Optional(Type.Boolean({ description: "Skip texturing (cheaper, faster)." })),
      rigType: Type.Optional(
        Type.Union(
          [
            Type.Literal("biped"),
            Type.Literal("quadruped"),
            Type.Literal("hexapod"),
            Type.Literal("octopod"),
            Type.Literal("avian"),
            Type.Literal("serpentine"),
            Type.Literal("aquatic"),
          ],
          {
            description:
              "rig= and rig-check only: skeleton type, default biped. The input pose decides the quality: a standing figure with clearly separated limbs rigs well, while a sitting or curled one gives a coarse skeleton (measured: a sitting cat gave 14 joints and no tail, a standing one gave 24 joints with four leg chains and a tail).",
          },
        ),
      ),
      spec: Type.Optional(
        Type.Union([Type.Literal("mixamo"), Type.Literal("tripo")], {
          description:
            "rig= only: bone naming, default mixamo. mixamo gives the standard Mixamo humanoid names (mixamorig:Hips, Spine, Spine1, ... LeftToeBase) and applies to biped only; a non-biped rig returns tripo:: names whatever you pass.",
        }),
      ),
      animation: Type.Optional(
        Type.String({
          description:
            "retarget= only: one preset animation such as preset:walk (90+ presets exist for biped). The animation is baked into the returned GLB.",
        }),
      ),
    }),

    async execute(_toolCallId, params, signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const key = tripoRequireKey(); // 没 key 直接 throw ✓（前置条件不满足）
      const api = (path: string, body?: unknown) => tripoApi(TRIPO_V3, key, path, body, signal);
      const waitTask = (id: string, label: string) => tripoWait(api, id, label, signal);
      const download = (url: string, outPath: string) => tripoSave(url, outPath, signal);
      const pickUrl = tripoPickUrl;

      if (params.action === "balance") {
        const d = await api("/account/balance"); // v3 的正确路径 ✓（/user/balance 是 v2 的 ✗ 我试错才误判"v3 没有" ✗）
        return { content: [{ type: "text", text: `PASS: Tripo balance ${d.balance} credits (frozen ${d.frozen ?? 0}).` }] };
      }

      let taskId = params.taskId ?? "";
      const defaultName =
        params.action === "rig" ? "model-rigged.glb"
        : params.action === "retarget" ? "model-animated.glb"
        : "model.glb";
      let firstOut = params.out && !isAbsolute(params.out) ? resolve(cwd, params.out) : params.out ?? resolve(cwd, defaultName);
      // ⭐ 已存在就报错 ✓（“绝不静默覆盖” ✓）—— 放在建任务**之前** ✓ 否则白花积分 ✗
      guardOutNotExists(firstOut);

      if (params.action === "generate") {
        return serialize("tripo:paid-generation", async () => {
        guardOutNotExists(firstOut);
        // 清单：写清单要的两样东西（设计 §5.1）—— 本次调用的主产物是 firstOut
        const modRoot = modRootOf(cwd, firstOut);
        const name = outName(firstOut);
        const warns: string[] = [];
        let genStep: PendingStep | null = null;
        // ⭐ v3：文本 → /generation/text-to-model；图片 → /generation/image-to-model
        //    （v2 的 image_to_model + image/data URL 一律 1004 ✗ —— 实测 11 种形状都不行 ✓）
        const modelParams: Record<string, unknown> = {
          model: "P1-20260311",
          face_limit: params.faceLimit ?? 3000,
          texture: !params.noTexture,
          pbr: !params.noTexture,
        };
        let genPath: string;
        if (params.image) {
          const p = isAbsolute(params.image) ? params.image : resolve(cwd, params.image);
          if (!existsSync(p)) throw new Error(`TRIPO_FILE_NOT_FOUND: ${p}`);
          genPath = "/generation/image-to-model";
          modelParams.input = await tripoInputRef(key, p, signal); // 文档形式：input = 字符串 ✓（URL / file_token / task_id ✓）
        } else if (params.taskId) {
          // 直接用上游图片任务的产物 ✓（不重新下载上传 ✓）—— 例如 generate_image 的 taskId
          genPath = "/generation/image-to-model";
          modelParams.input = params.taskId;
        } else if (params.prompt) {
          genPath = "/generation/text-to-model";
          modelParams.prompt = params.prompt;
        } else {
          throw new Error("generate needs either prompt= or image=");
        }
        const created = await api(genPath, modelParams);
        taskId = String(created.task_id);
        // 清单：生成段（写入点 4 ✓）。parent = 这次用的图像来源任务（本地图与纯文字是 null ✓）
        genStep = {
          endpoint: `/v3${genPath}`,
          requestId: taskId,
          parent: params.image ? null : (params.taskId ?? null),
          seed: pickSeed(created),
          filesAbs: [],
          buildInput: (rel) => {
            const i: Record<string, unknown> = {
              model: modelParams.model,
              face_limit: modelParams.face_limit,
              texture: modelParams.texture,
              pbr: modelParams.pbr,
            };
            if (params.prompt) i.prompt = params.prompt;
            if (params.image) i.image = rel(isAbsolute(params.image) ? params.image : resolve(cwd, params.image));
            else if (params.taskId) i.input = params.taskId;
            return i;
          },
        };
        let done: any;
        let url: string | null;
        try {
          done = await waitTask(taskId, "model");
          url = done.output?.model_url ?? done.output?.pbr_model ?? done.output?.model ?? pickUrl(done.output);
          if (!url) throw new Error(`task succeeded but no model URL: ${JSON.stringify(done.output).slice(0, 200)}`);
        } catch (e) {
          // 失败也要留一行 ✗ 否则失败只剩聊天记录（设计 §3.4 规则 4）
          const w = flushStep(modRoot, name, genStep, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        const rawPath = firstOut.replace(/\.glb$/i, "") + ".raw.glb";
        let size: string;
        try {
          size = await download(url, rawPath);                        // ⚠️ 5 分钟过期 → 立刻下
        } catch (e) {
          const w = flushStep(modRoot, name, genStep, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        // 预览图（Tripo 免费附带）→ 放在模型同目录的 .preview/ 下 ✓
        // 这个子目录**不会被 install_mod 装进游戏**（见 mod-install 的 EXCLUDED_DIRS ✓）—— 只是给人/agent 看的 ✓
        const base = firstOut.replace(/\.glb$/i, "");
        const shotDir = previewDirFor(firstOut);
        const shotBase = join(shotDir, basename(base));
        const shots: string[] = [];
        const renderKey = done.output?.rendered_image_url ?? done.output?.rendered_image;
        if (renderKey) {
          try {
            await download(renderKey, `${shotBase}.render.png`);   // 叫 .render 而不是 .preview ✗：别覆盖 ③ 那张已确认的预览图 ✓
            shots.push(`${shotBase}.render.png`);
            // 同一张渲染图再存一份到 mod 根做**图标**（背包卡片用 ✓ 白底会被运行时抠成透明 ✓）
            // 记下 task id（`.preview/<模型名>.task`）→ generate_image 可直接复用它的渲染图（不花积分 ✓）
            writeFileSync(`${shotBase}.task`, String(taskId));
          } catch { /* 预览图/图标下不到不影响主流程 */ }
        }
        const conceptKey = done.output?.generated_image_url ?? done.output?.generated_image;
        if (conceptKey) {
          try { await download(conceptKey, `${shotBase}.concept.jpg`); shots.push(`${shotBase}.concept.jpg`); } catch { /* 同上 */ }
        }

        // 清单：生成段的文件已落盘 → 先写它（写入点 4 ✓）
        genStep.filesAbs = [rawPath, ...shots];
        { const w = flushStep(modRoot, name, genStep); if (w) warns.push(w); }
        // 清单：转换段（写入点 5 ✓）。先建好，失败时才能把那一行写出来 ✓
        const cvStep: PendingStep = {
          endpoint: "/v3/models/convert",
          requestId: null,
          parent: taskId,
          seed: null,
          filesAbs: [firstOut],
          buildInput: () => ({ input: taskId, format: "GLTF", with_animation: params.animated === true, texture_size: 1024, texture_format: "PNG" }),
        };
        let converted: any;
        try {
          const cv = await convert(api, waitTask, taskId, params.animated === true);
          cvStep.requestId = cv.id;
          converted = cv.out;
        } catch (e) {
          const w = flushStep(modRoot, name, cvStep, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        const url2 = pickUrl(converted.output);
        if (!url2) throw new Error("convert succeeded but no URL");
        let size2: string;
        try {
          size2 = await download(url2, firstOut);                     // Unity 就绪朝向 ✓
        } catch (e) {
          const w = flushStep(modRoot, name, cvStep, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        { const w = flushStep(modRoot, name, cvStep); if (w) warns.push(w); }
        return {
          content: [
            {
              type: "text",
              text:
                `PASS: model saved to ${firstOut} (${size2}, raw also at ${rawPath} ${size}). task_id=${taskId}\n` +
                (shots.length ? `Preview: ${shots.join(", ")} - read the image and show it to the player before installing. The shape is up to the prompt. The orientation is already right.\n` : "") +
                warnLine(warns),
            },
          ],
        };
        });
      }

      // action=rig-check：先问能不能绑（官方建议）。⚠️ 实测它**只是建议**：坐着的猫回 false 也照样绑出来了 ✗
      if (params.action === "rig-check") {
        if (!taskId) throw new Error("rig-check needs taskId= (from an earlier generate)");
        const rigType = params.rigType ?? "biped";
        const done = await serialize("tripo:paid-generation", async () => {
          guardOutNotExists(firstOut);
          const created = await api("/animations/rig-check", { input: taskId, rig_type: rigType });
          return waitTask(String(created.task_id), "rig-check");
        });
        const o = done.output ?? {};
        const ok = o.riggable === true || o.riggable === "true";
        return {
          content: [
            {
              type: "text",
              text:
                `${ok ? "PASS" : "WARN"}: rig-check riggable=${o.riggable} rig_type=${o.rig_type ?? rigType}` +
                (ok
                  ? ""
                  : ". This check is advice only. The rig may still come out, but with a coarse skeleton. Read the joint list that action=rig prints."),
            },
          ],
        };
      }

      // action=rig：给一个成品模型加骨骼。v1.0 只支持人形，v2.5 支持四足/六足/八足/鸟/蛇/鱼
      if (params.action === "rig") {
        return serialize("tripo:paid-generation", async () => {
        guardOutNotExists(firstOut);
        if (!taskId) throw new Error("rig needs taskId= (from an earlier generate)");
        const modRoot = modRootOf(cwd, firstOut);
        const name = outName(firstOut);
        const warns: string[] = [];
        const rigType = params.rigType ?? "biped";
        const model = rigType === "biped" ? "v1.0-20240301" : "v2.5-20260210";
        const spec = params.spec ?? "mixamo";
        const body: Record<string, unknown> = { input: taskId, model, rig_type: rigType, spec, out_format: "glb" };
        const step: PendingStep = {
          endpoint: "/v3/animations/rig",
          requestId: null,
          parent: taskId,
          seed: null,
          filesAbs: [firstOut],
          buildInput: () => ({ ...body }),
        };
        let done: any;
        try {
          const created = await api("/animations/rig", body);
          step.requestId = String(created.task_id);
          done = await waitTask(step.requestId, "rig");
        } catch (e) {
          const w = flushStep(modRoot, name, step, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        const url = pickUrl(done.output);
        if (!url) throw new Error("rig succeeded but no URL");
        let size: string;
        try {
          size = await download(url, firstOut);
        } catch (e) {
          const w = flushStep(modRoot, name, step, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        { const w = flushStep(modRoot, name, step); if (w) warns.push(w); }
        const joints = tripoGlbJoints(firstOut);
        const notes: string[] = [];
        if (!joints.length) notes.push("Could not read a joint list from the GLB. Open the file and check.");
        if (spec === "mixamo" && rigType !== "biped")
          notes.push("spec=mixamo applies to biped only. This rig uses tripo:: names, so map them yourself.");
        return {
          content: [
            {
              type: "text",
              text:
                `PASS: rigged model saved to ${firstOut} (${size}) task_id=${step.requestId} rig_type=${rigType} spec=${spec} model=${model}\n` +
                (joints.length ? `Joints (${joints.length}): ${joints.join(", ")}.\n` : "") +
                (notes.length ? notes.join("\n") + "\n" : "") +
                warnLine(warns),
            },
          ],
        };
        });
      }

      // action=retarget：往骨架上套一个预置动画，烘焙进 GLB
      if (params.action === "retarget") {
        return serialize("tripo:paid-generation", async () => {
        guardOutNotExists(firstOut);
        if (!taskId) throw new Error("retarget needs taskId= (the rig task id from action=rig)");
        if (!params.animation) throw new Error("retarget needs animation= (for example preset:walk)");
        const modRoot = modRootOf(cwd, firstOut);
        const name = outName(firstOut);
        const warns: string[] = [];
        const body: Record<string, unknown> = { input: taskId, animation: params.animation, bake_animation: true };
        const step: PendingStep = {
          endpoint: "/v3/animations/retarget",
          requestId: null,
          parent: taskId,
          seed: null,
          filesAbs: [firstOut],
          buildInput: () => ({ ...body }),
        };
        let done: any;
        try {
          const created = await api("/animations/retarget", body);
          step.requestId = String(created.task_id);
          done = await waitTask(step.requestId, "retarget");
        } catch (e) {
          const w = flushStep(modRoot, name, step, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        const url = pickUrl(done.output);
        if (!url) throw new Error("retarget succeeded but no URL");
        let size: string;
        try {
          size = await download(url, firstOut);
        } catch (e) {
          const w = flushStep(modRoot, name, step, String((e as Error).message));
          if (w) warns.push(w);
          throw e;
        }
        { const w = flushStep(modRoot, name, step); if (w) warns.push(w); }
        const anims = tripoGlbAnimations(firstOut);
        return {
          content: [
            {
              type: "text",
              text:
                `PASS: animation "${params.animation}" baked into ${firstOut} (${size}) task_id=${step.requestId}\n` +
                (anims.length ? `Animations: ${anims.join(", ")}.` : "") +
                warnLine(warns),
            },
          ],
        };
        });
      }

      // action=convert
      if (!taskId) throw new Error("convert needs taskId= (from an earlier generate)");
      return serialize("tripo:paid-generation", async () => {
      guardOutNotExists(firstOut);
      // 清单：写清单要的两样东西（设计 §5.1）—— 独立 convert 没有上一步 → parent 是 null ✓
      const modRoot = modRootOf(cwd, firstOut);
      const name = outName(firstOut);
      const warns: string[] = [];
      const cvStep: PendingStep = {
        endpoint: "/v3/models/convert",
        requestId: null,
        parent: null,
        seed: null,
        filesAbs: [firstOut],
        buildInput: () => ({ input: taskId, format: "GLTF", with_animation: params.animated === true, texture_size: 1024, texture_format: "PNG" }),
      };
      let cvOut: any;
      try {
        const cv = await convert(api, waitTask, taskId, params.animated === true);
        cvStep.requestId = cv.id;
        cvOut = cv.out;
      } catch (e) {
        const w = flushStep(modRoot, name, cvStep, String((e as Error).message));
        if (w) warns.push(w);
        throw e;
      }
      const url = pickUrl(cvOut.output);
      if (!url) throw new Error("convert succeeded but no URL");
      let size: string;
      try {
        size = await download(url, firstOut);
      } catch (e) {
        const w = flushStep(modRoot, name, cvStep, String((e as Error).message));
        if (w) warns.push(w);
        throw e;
      }
      { const w = flushStep(modRoot, name, cvStep); if (w) warns.push(w); }
      return {
        content: [
          {
            type: "text",
            text: `PASS: converted and saved ${firstOut} (${size}) task_id=${taskId}` + warnLine(warns),
          },
        ],
      };
      });
    },
  });
}

/** 转静态 + 1024 PNG。**不传 export_orientation** ✗（那个参数不可靠；朝向已由提示词定好 ✓） */
async function convert(api: (p: string, b?: unknown) => Promise<any>, waitTask: (id: string, l: string) => Promise<any>, taskId: string, animated: boolean): Promise<{ id: string; out: any }> {
  const created = await api("/models/convert", {
    // ⭐ v3：字段是 input（v2 叫 original_model_task_id ✗）
    input: taskId,
    format: "GLTF",
    with_animation: animated,
    texture_size: 1024,
    texture_format: "PNG",
  });
  const id = String(created.task_id);
  // 返回值带上转换任务的 id —— 清单要它当 request_id（设计 §5.2 写入点 5）
  return { id, out: await waitTask(id, "convert") };
}

/** 预览图统一放 mod 的 .preview；中间模型已在该目录时不得再嵌套一层。 */
export function previewDirFor(modelOut: string): string {
  const modelDir = dirname(modelOut);
  return basename(modelDir) === ".preview" ? modelDir : join(modelDir, ".preview");
}

/** 只读 GLB 头里的 JSON 块（不解码几何 ✗）。用来把骨骼名与动画名报给 agent ✓ */
function tripoGlbJson(path: string): any | null {
  try {
    const buf = readFileSync(path);
    if (buf.length < 20 || buf.readUInt32LE(0) !== 0x46546c67) return null; // "glTF"
    const jsonLen = buf.readUInt32LE(12);
    return JSON.parse(buf.subarray(20, 20 + jsonLen).toString("utf8"));
  } catch {
    return null;
  }
}

/** GLB 里的骨骼名（skin 的 joints 指向的节点名）。读不出就返回空数组 ✓（不是错误 ✓） */
function tripoGlbJoints(path: string): string[] {
  const g = tripoGlbJson(path);
  if (!g) return [];
  const nodes: any[] = g.nodes ?? [];
  const out: string[] = [];
  for (const skin of g.skins ?? []) for (const j of skin.joints ?? []) out.push(nodes[j]?.name ?? "?");
  return out;
}

/** GLB 里的动画名。读不出就返回空数组 ✓ */
function tripoGlbAnimations(path: string): string[] {
  const g = tripoGlbJson(path);
  if (!g) return [];
  return (g.animations ?? []).map((a: any) => a?.name ?? "(unnamed)");
}
