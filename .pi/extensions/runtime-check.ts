import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import os from "node:os";

/**
 * check_runtime — 运行时契约（docs/mod-repo-guide.md §4）
 * 检查：1) dotnet SDK；2) 游戏安装目录；3) Steam Workshop 内容目录（若有 steamAppId）。
 * 发现结果缓存到 <repo>/.gamer-agent.local.json（gitignored）。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "check_runtime",
    label: "Check Runtime",
    description: "Check .NET SDK >= 8 and locate the installed game, plus its Steam Workshop content dir when a Steam app id is set. Uses an optional gameDir, cached path or platform hint; relative gameDir is resolved from the workspace. Writes discovery results to .gamer-agent.local.json; does not install software or modify mod source files.",
    promptSnippet: "Check SDK and game location when compilation needs them or the player asks about setup",
    promptGuidelines: [
      "Use check_runtime when runtime readiness is unknown or has changed. Only SDK problems need install_runtime; a missing game directory needs a valid installation path, not SDK installation.",
    ],
    parameters: Type.Object({
      gameDir: Type.Optional(Type.String({ description: "Game installation directory supplied by the player; relative paths use the workspace root. Omit to try cached paths and platform hints." })),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const repoRoot = ctx.cwd;
      const stateFile = join(repoRoot, ".gamer-agent.local.json");
      const platform = process.platform === "win32" ? "windows" : process.platform === "darwin" ? "mac" : "linux";

      let cfg;
      try {
        cfg = JSON.parse(readFileSync(join(repoRoot, "mod-repo.json"), "utf8"));
      } catch {
        throw new Error("INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace; do not edit its maintainer configuration.");
      }
      const loadState = () => {
        try {
          return JSON.parse(readFileSync(stateFile, "utf8"));
        } catch {
          return {};
        }
      };
      const state = loadState();
      const problems: string[] = [];

      // 1. dotnet
      const dotnet = checkDotnet();
      if (!dotnet.ok) {
        problems.push(
          dotnet.version
            ? `FAIL: dotnet SDK ${dotnet.version} is too old (need >= 8.0)`
            : "FAIL: dotnet SDK not found. Call install_runtime for install instructions.",
        );
      }

      // 2. game dir
      const expandHome = (p: string) => (p.startsWith("~/") || p === "~" ? join(os.homedir(), p.slice(2)) : p);
      const discoverGameDir = () => {
        const candidates = params.gameDir !== undefined
          ? [params.gameDir]
          : [state.gameDir, cfg.game?.installDirHint?.[platform]];
        const managedRel = cfg.compile?.managedDir?.[platform];
        if (typeof managedRel !== "string") return null;
        for (const candidate of candidates) {
          if (typeof candidate !== "string" || !candidate.trim()) continue;
          const dir = resolve(repoRoot, expandHome(candidate.trim()));
          const managed = join(dir, managedRel);
          if (existsSync(join(managed, "TeamSoda.Duckov.Core.dll"))) return { gameDir: dir, managedDir: managed };
        }
        return null;
      };

      const found = discoverGameDir();
      if (found) {
        state.gameDir = found.gameDir;
        state.managedDir = found.managedDir;
          // modInstall.path 缺本平台取值时**不能**退化成 gameDir：旧写法 `?? ""` 会让 join(gameDir, "") === gameDir，
          // 等于把 mod 装进游戏根目录 → 直接报配置错误（与 eu5 的 INVALID_WORKSPACE_CONFIG 对齐）。
          const modInstallRel = cfg.modInstall?.path?.[platform];
          if (typeof modInstallRel !== "string" || !modInstallRel.trim()) {
            problems.push(
              "FAIL: INVALID_WORKSPACE_CONFIG: mod-repo.json has no modInstall.path for this platform; cannot determine where mods go.",
            );
          } else {
            state.modInstallDir = join(found.gameDir, modInstallRel);
          }
          // Steam Workshop 内容目录（只读参考，可选）：gameDir 位于 steamapps/common/<game> 下，
          // workshop 在同级 steamapps/workshop/content/<steamAppId>。与 eu5 对齐：不存在就不写进状态。
          const steamAppId = cfg.game?.steamAppId;
          if (steamAppId) {
            const workshop = join(dirname(dirname(found.gameDir)), "workshop", "content", String(steamAppId));
            if (existsSync(workshop)) state.workshopDir = workshop;
          }
        state.runtime = { dotnet: dotnet.path, dotnetVersion: dotnet.version };
        writeFileSync(stateFile, `${JSON.stringify(state, null, 2)}\n`);
      } else {
        problems.push(
          `FAIL: GAME_DIRECTORY_NOT_FOUND (${cfg.game?.name}). Ask the player for the installed game directory and re-run check_runtime with gameDir; if not installed, install the game first. install_runtime only guides SDK setup and cannot fix this.`, 
        );
      }

      if (problems.length) {
        return {
          content: [{ type: "text", text: problems.join("\n") }],
          details: { ok: false, errors: problems },
        };
      }
      // 与 eu5 对齐：安装目标目录尚不存在时只给 WARN（全新机器的正常状态，install_mod 会创建）；
      // 若玩家把「文档」/游戏目录挪过位置，这里算出的路径可能是错的 → 把判断交回 agent/玩家。
      const modTarget =
        typeof state.modInstallDir === "string" && state.modInstallDir ? state.modInstallDir : null;
      const modDirWarn =
        modTarget && !existsSync(modTarget)
          ? `\nWARN: that mod directory does not exist yet — normal on a first install (install_mod creates it). If the player moved their Documents or the game folder, this path may be wrong: confirm with the player where the game expects mods.`
          : "";
      return {
        content: [
          {
            type: "text",
            text: `PASS: dotnet ${dotnet.version} (${dotnet.path}); gameDir ${state.gameDir}${
              modTarget ? `; modInstallDir ${modTarget}` : ""
            }${modDirWarn}`,
          },
        ],
        details: { ...state, ok: true },
      };
    },
  });
}

function checkDotnet() {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [join(os.homedir(), ".dotnet", bin)];
  if (process.platform === "win32") {
    candidates.push("C:\\Program Files\\dotnet\\dotnet.exe");
    if (process.env.LOCALAPPDATA) candidates.push(join(process.env.LOCALAPPDATA, "Microsoft", "dotnet", "dotnet.exe"));
  } else if (process.platform === "darwin") {
    candidates.push("/usr/local/share/dotnet/dotnet");
  }
  const tryRun = (p: string) => {
    try {
      return execFileSync(p, ["--version"], { encoding: "utf8" }).trim();
    } catch {
      return null;
    }
  };
  const fromPath = tryRun("dotnet");
  if (fromPath) return { ok: parseInt(fromPath.split(".")[0], 10) >= 8, version: fromPath, path: "dotnet" };
  for (const c of candidates) {
    if (!existsSync(c)) continue;
    const v = tryRun(c);
    if (v) return { ok: parseInt(v.split(".")[0], 10) >= 8, version: v, path: c };
  }
  return { ok: false, version: null, path: null };
}
