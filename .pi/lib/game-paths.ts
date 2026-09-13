/**
 * 路径判据 + 运行时状态的共享实现（game repo 内部库，**不是**工具）。
 *
 * 为什么放在 `.pi/lib/`：pi 会自动加载 `.pi/extensions/*.ts` 和 子目录里的 index.ts，
 * 共享模块放进 extensions/ 会被当成扩展加载。这里由各工具用相对路径 import。
 *
 * 这里的判据是**游戏专属知识**（Duckov 自带的程序集名、Steam 目录形状等）：
 * check_game_paths / try_set_game_paths / check_runtime / install_mod 共用同一份，
 * 保证"验"与"装"不会各写一套、各自漂移。
 *
 * 三类路径的性质不同，判据也不同：
 *   · gameDir       只读参考 → 必须**验出真身**（找不到就失败）
 *   · workshopDir   只读参考、可选 → 不存在就不设
 *   · modInstallDir 我方写入目标 → 不存在是**正常的初始状态**（装的时候创建），
 *                   但要验"这条路径确实长在游戏目录里"（向上找兄弟目录里的哨兵）
 */
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

/** 游戏自带的程序集（哨兵）：它在，才说明这个目录确实是 Duckov。 */
export const GAME_SENTINEL = "TeamSoda.Duckov.Core.dll";

export interface ModRepoConfig {
  game?: { name?: string; steamAppId?: string | number; installDirHint?: Record<string, string> };
  compile?: { managedDir?: Record<string, string> };
  modInstall?: { path?: Record<string, string>; relativeTo?: string };
}

export interface RuntimeState {
  gameDir?: string | null;
  managedDir?: string | null;
  workshopDir?: string | null;
  modInstallDir?: string | null;
  runtime?: unknown;
  [key: string]: unknown;
}

export interface PathVerdict {
  ok: boolean;
  path: string | null;
  reason?: string;
  /** 给 agent 的下一步（可执行、面向玩家可读） */
  next?: string;
}

/** 配置里用的平台键（mod-repo.json 的 windows/mac/linux） */
export const platformKey = (): string =>
  process.platform === "win32" ? "windows" : process.platform === "darwin" ? "mac" : "linux";

export const statePath = (cwd: string): string => join(cwd, ".gamer-agent.local.json");

export function readModRepoConfig(cwd: string): ModRepoConfig {
  return JSON.parse(readFileSync(join(cwd, "mod-repo.json"), "utf8")) as ModRepoConfig;
}

export function readState(cwd: string): RuntimeState {
  try {
    return JSON.parse(readFileSync(statePath(cwd), "utf8")) as RuntimeState;
  } catch {
    return {};
  }
}

/**
 * 读改写 + **原子替换**。以前只有 check_runtime 写这个文件；现在多了 try_set_game_paths，
 * 所以统一走这里：写临时文件再 rename，避免留下半个 JSON（同盘 rename 是原子的）。
 */
export function writeState(cwd: string, patch: RuntimeState): RuntimeState {
  const next = { ...readState(cwd), ...patch };
  const file = statePath(cwd);
  const tmp = `${file}.tmp-${process.pid}`;
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(tmp, `${JSON.stringify(next, null, 2)}\n`);
  renameSync(tmp, file);
  return next;
}

/** 展开 `~` / `~/…`（mod-repo.json 的 installDirHint 是这么写的；不展开会永远验不过） */
export const expandHome = (p: string): string => {
  const t = p.trim();
  if (t !== "~" && !t.startsWith("~/")) return t;
  return join(process.env.HOME ?? process.env.USERPROFILE ?? "", t.slice(2));
};

/** 把工具入参里的相对路径按 workspace 根解析（与其它工具一致） */
export const resolveUserPath = (cwd: string, p: string): string => resolve(cwd, expandHome(p));

/** 编译要引用的程序集目录（由 gameDir + mod-repo.json 拼出来，不信任状态里存的值） */
export function managedDirFor(gameDir: string, cfg: ModRepoConfig, platform = platformKey()): string | null {
  const rel = cfg.compile?.managedDir?.[platform];
  return typeof rel === "string" && rel ? join(gameDir, rel) : null;
}

/** mod 安装目标（相对 gameDir，见 mod-repo.json 的 modInstall.relativeTo=gameDir） */
export function modInstallDirFor(gameDir: string, cfg: ModRepoConfig, platform = platformKey()): string | null {
  const rel = cfg.modInstall?.path?.[platform];
  return typeof rel === "string" && rel.trim() ? join(gameDir, rel) : null;
}

const NEXT_STEAM = "The player can find it in Steam -> Library -> right-click the game -> Manage -> Browse local files.";

/** 判据 1：这个目录是不是 Duckov 的安装目录（看游戏自带的哨兵程序集） */
export function checkGameDir(dir: string | null | undefined, cfg: ModRepoConfig, platform = platformKey()): PathVerdict {
  const p = dir ? expandHome(dir) : null;
  if (!p)
    return {
      ok: false,
      path: null,
      reason: "no game directory was given",
      next: `Ask the player for the game install directory, then re-run this with gameDir. ${NEXT_STEAM}`,
    };
  const managed = managedDirFor(p, cfg, platform);
  if (!managed)
    return {
      ok: false,
      path: p,
      reason: "mod-repo.json has no compile.managedDir entry for this platform",
      next: "Reopen or update this game workspace (do not edit the maintainer configuration).",
    };
  if (!existsSync(join(managed, GAME_SENTINEL)))
    return {
      ok: false,
      path: p,
      reason: `the game's own ${GAME_SENTINEL} was not found under this directory (expected in ${managed})`,
      next: `Confirm this is the game install directory, not a save folder or another version. ${NEXT_STEAM}`,
    };
  return { ok: true, path: p };
}

/** 判据 2：mod 安装目标 —— **向上一步**看兄弟目录里的哨兵（目标与 Managed 同级） */
export function checkModInstallDir(dir: string | null | undefined): PathVerdict {
  // 状态里可能是历史遗留的 ~/… → 统一展开后再判
  const p = dir ? expandHome(dir) : null;
  if (!p)
    return {
      ok: false,
      path: null,
      reason: "no mod install directory was given",
      next: "Run try_set_game_paths to locate it, or ask the player for the game install directory and verify again.",
    };
  const sibling = join(dirname(p), "Managed", GAME_SENTINEL);
  if (!existsSync(sibling))
    return {
      ok: false,
      path: p,
      reason: `this path does not look like it is inside the game folder (no ${GAME_SENTINEL} next to it) - the game may have been moved or uninstalled`,
      next: `Run try_set_game_paths again to re-locate the game; if the game is installed elsewhere, pass that path and verify again. ${NEXT_STEAM}`,
    };
  return { ok: true, path: p };
}

/** 判据 3：Steam Workshop 内容目录（只读参考；必须是 …/steamapps/workshop/content/<appId>） */
export function checkWorkshopDir(dir: string | null | undefined, appId: string): PathVerdict {
  // 状态里可能是历史遗留的 ~/… → 统一展开后再判
  const p = dir ? expandHome(dir) : null;
  if (!p)
    return { ok: false, path: null, reason: "no Workshop directory was given (optional)", next: "Omit it; it is only used to read Workshop content for reference." };
  if (!existsSync(p)) return { ok: false, path: p, reason: "the directory does not exist", next: "Omit this argument (the Workshop directory is an optional read-only reference)." };
  // 归一化（反斜杠/尾斜杠/大小写）后比较 —— Windows 路径不区分大小写；
  // appid 未知时只校验存在性（形状校验是"当我们知道规则"时才有意义）。
  const norm = (v: string) => v.replace(/\\/g, "/").replace(/\/+$/, "").toLowerCase();
  if (appId && !norm(p).endsWith(norm(`/workshop/content/${appId}`)))
    return {
      ok: false,
      path: p,
      reason: `the path does not end with workshop/content/${appId} (see the appid in mod-repo.json)`,
      next: "Confirm this is the Workshop content directory for this game; omit it if unsure.",
    };
  return { ok: true, path: p };
}

// ── 发现（Duckov：显式入参 → 缓存 → mod-repo.json 的平台提示；不扫 Steam 库，
//    因为它的 Workshop 目录可由游戏目录推导出来）─────────────────────────────────
export interface TriedCandidate {
  path: string;
  reason?: string;
}

/** 候选游戏目录（去重、保序） */
export function gameDirCandidates(
  cfg: ModRepoConfig,
  state: RuntimeState,
  platform = platformKey(),
  explicit?: string,
): string[] {
  const out: string[] = [];
  const push = (v: unknown) => {
    if (typeof v === "string" && v.trim()) out.push(v);
  };
  if (explicit?.trim()) push(explicit);
  push(state.gameDir);
  push(cfg.game?.installDirHint?.[platform]);
  return [...new Set(out)];
}

/** 依次验证候选，返回第一个通过的游戏目录（**发现 ≠ 信任**：每个候选都过 checkGameDir） */
export function discoverGameDir(
  cfg: ModRepoConfig,
  state: RuntimeState,
  platform = platformKey(),
  explicit?: string,
): { gameDir: string | null; tried: TriedCandidate[] } {
  const tried: TriedCandidate[] = [];
  for (const c of gameDirCandidates(cfg, state, platform, explicit)) {
    const v = checkGameDir(c, cfg, platform);
    if (v.ok) return { gameDir: v.path ?? c, tried };   // 存绝对路径：不把 ~/... 写进状态
    tried.push({ path: c, reason: v.reason });
  }
  return { gameDir: null, tried };
}

/** 由已确认的 gameDir 派生其余路径；只读参考类目录**不存在就不记**（不让状态撒谎） */
export function pathsFromGameDir(
  gameDir: string,
  cfg: ModRepoConfig,
  platform = platformKey(),
): { managedDir: string | null; modInstallDir: string | null; workshopDir: string | null; notes: string[] } {
  const notes: string[] = [];
  const managedDir = managedDirFor(gameDir, cfg, platform);
  const modInstallDir = modInstallDirFor(gameDir, cfg, platform);
  let workshopDir: string | null = null;
  const appId = cfg.game?.steamAppId;
  if (appId) {
    const ws = join(dirname(dirname(gameDir)), "workshop", "content", String(appId));
    if (existsSync(ws)) workshopDir = ws;
    else notes.push(`NOTE: the Workshop directory does not exist, so it was not recorded (optional, read-only reference): ${ws}`);
  }
  return { managedDir, modInstallDir, workshopDir, notes };
}
