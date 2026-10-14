/**
 * asset_manifest.jsonl —— 素材清单的判据。
 *
 * 设计：设计仓 `desktop-gamer-agent-pi/docs/unity-3d-assets/09-asset-manifest.md`。
 *
 * **为什么单独一个文件**（不写在 `.pi/extensions/tripo.ts` 里）：
 *   ① 判据只能一份。扩展负责写清单，`scripts/check-asset-manifest.mjs` 负责验它，两边导入同一份。
 *   ② 这里不碰 pi 的 SDK（只用 `node:fs` / `node:path`）→ check 脚本能用
 *      `node --experimental-strip-types` 直接跑。先例：`.pi/lib/mod-products.ts`。
 *
 * 清单：**一个 mod 一份**，放 mod 根，每次生成**追加一行**。
 * 编码 UTF-8；行尾 `\n`；一行一个完整 JSON 对象（`JSON.stringify` 会把字符串里的换行转义 ✓）。
 */
import { appendFileSync, mkdirSync } from "node:fs";
import { basename, dirname, join, relative } from "node:path";

/** 清单文件名。也是 `install_mod` 排除名单里的那一个（精确文件名 ✓ 不是 `.jsonl` 扩展名 ✗）*/
export const MANIFEST_FILE = "asset_manifest.jsonl";

/** 提供方名。将来同时用别家服务时，这个键用来区分 ✗ 文件名不换 ✓ */
export const PROVIDER = "tripo";

/**
 * 一行清单。
 *
 * 前 7 个键抄上游 `um/fal.py:262-264` 的写法；后 3 个是我们加的（设计 §4.1）：
 *   · `provider` —— 上游靠文件名区分提供方，我们的文件名固定 ✗
 *   · `parent_request_id` —— 我们的链路是多步的（图 → 模型 → 转换）✓ 上游一次调用一步 ✗
 *   · `error` —— 只在失败行出现 ✓ 成功行没有这个键 ✓
 */
export type ManifestRec = {
  /** 本地时间，秒级，不带时区。与上游一致。 */
  t: string;
  provider: string;
  /** 调用路径，带版本前缀。例：`/v3/generation/image-to-model`、`/v2/openapi/task`。 */
  endpoint: string;
  /** 产物名（输出文件名去扩展名）。 */
  name: string;
  /** 提供方给的任务 id。拿不到时是 null。 */
  request_id: string | null;
  /** 上一步的任务 id。没有上一步时是 null。 */
  parent_request_id: string | null;
  /** 模型或贴图的 seed。接口没返回时是 null。 */
  seed: number | null;
  /** 这一步落盘的文件，路径相对 mod 根。失败时是空数组。 */
  files: string[];
  /** 这次的输入。**不得**出现 base64 或 data URI ✗ —— data URI 会把一行撑成几百 KB。 */
  input: Record<string, unknown>;
  /** 只在失败行出现。 */
  error?: string;
};

/** 本地时间，格式 `2026-10-14T15:04:05`（秒级、不带时区 ✓ 与上游一致）。 */
export function nowLocal(d: Date = new Date()): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return (
    `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}` +
    `T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
  );
}

/**
 * 从「本次调用的主产物」推 mod 根（设计 §5.1 的 4 步）。
 *
 * 产物落在 `your_mods/<段>/…` 下 → mod 根是 `<cwd>/your_mods/<段>`。
 * 其它情况 → mod 根是产物所在目录。
 *
 * 注意 `<段>` 后面**必须**还有一层分隔符：`your_mods/x.glb` 是「文件直接放在 `your_mods/`」，
 * 这时走兜底分支，不把 `x.glb` 当成 mod 名。
 */
export function modRootOf(cwd: string, primaryOut: string): string {
  const rel = relative(cwd, primaryOut);
  const m = /^your_mods[\\/]([^\\/]+)[\\/]/.exec(rel);
  return m ? join(cwd, "your_mods", m[1]) : dirname(primaryOut);
}

/** 清单里的本地路径都相对 mod 根 ✓（绝对路径在 mod 被移动后失效 ✗）。统一用正斜杠，Windows 上两边一致。 */
export function relToModRoot(modRoot: string, file: string): string {
  const r = relative(modRoot, file);
  return (r || basename(file)).split("\\").join("/");
}

/** 补上时间与提供方，其余键由调用点给。键的顺序跟设计 §3.3 的示例一致。 */
export function manifestRec(rec: Omit<ManifestRec, "t" | "provider">): ManifestRec {
  const { endpoint, name, request_id, parent_request_id, seed, files, input, error } = rec;
  const out: ManifestRec = {
    t: nowLocal(),
    provider: PROVIDER,
    endpoint,
    name,
    request_id,
    parent_request_id,
    seed,
    files,
    input,
  };
  if (error) out.error = error;
  return out;
}

/** 从接口响应里取 seed（设计 §3.2：Tripo 的 `model_seed` 或 `texture_seed`）。拿不到就是 null。 */
export function pickSeed(o: unknown): number | null {
  const v = (o as { model_seed?: unknown; texture_seed?: unknown } | null | undefined)?.model_seed
    ?? (o as { texture_seed?: unknown } | null | undefined)?.texture_seed
    ?? null;
  return typeof v === "number" ? v : null;
}

/**
 * 追加一行到 `<mod 根>/<MANIFEST_FILE>`。
 *
 * 返回 `null` = 成功；返回一句话 = 失败原因（调用点把它拼进 `WARN:` 文案）。
 *
 * **写清单失败不得让工具报错** ✗（设计 §3.4 规则 6）：产物已经落盘，清单只是附加信息。
 * 先 `mkdirSync`：失败行可能在任何文件落盘**之前**写，那时 mod 根还不存在。对已存在的目录不抛。
 */
export function appendManifest(modRoot: string, rec: ManifestRec): string | null {
  try {
    mkdirSync(modRoot, { recursive: true });
    appendFileSync(join(modRoot, MANIFEST_FILE), JSON.stringify(rec) + "\n", "utf8");
    return null;
  } catch (e) {
    return `could not write ${MANIFEST_FILE}: ${(e as Error).message}`;
  }
}
