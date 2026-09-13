import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import os from "node:os";
import {
  discoverGameDir,
  pathsFromGameDir,
  platformKey,
  readModRepoConfig,
  readState,
  resolveUserPath,
  writeState,
  type ModRepoConfig,
} from "../lib/game-paths";

/**
 * check_runtime — 运行时契约（docs/mod-repo-guide.md §4）的一次跑完入口。
 *
 * 拆分后的分工（本工具只做**编排**，判据与发现都在 .pi/lib/game-paths.ts）：
 *   · check_game_paths     只验给定/已记住的路径（只读）
 *   · set_game_dir / set_game_paths   记住玩家给的路径（不过判据则走内部发现/派生）
 *   · check_runtime        SDK 检查 + 发现 + 校验 + 落库 + 汇总   ← 本工具
 *   · install_mod          安装（同一份判据；目标不存在则创建）
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "check_runtime",
    label: "Check Runtime",
    description:
      "One-stop runtime check: verifies the .NET SDK (>= 8), locates the installed game, verifies the game/mod paths with the game's own marker, and records the result in .gamer-agent.local.json. Uses an optional gameDir, then the remembered path, then the platform hint in mod-repo.json. Does not install software or modify mod source files. For a read-only path check use check_game_paths; to only locate and record the game use set_game_paths.",
    promptSnippet: "Check SDK and game location when compilation needs them or the player asks about setup",
    promptGuidelines: [
      "Use check_runtime when runtime readiness is unknown or has changed: it checks the SDK, locates the game and records the verified paths in one go.",
      "Only SDK problems need install_runtime; a missing game directory needs a valid installation path, not SDK installation.",
      "If the player already gave you a path, prefer check_game_paths (read-only) to verify it, or pass gameDir here to locate and record it.",
    ],
    parameters: Type.Object({
      gameDir: Type.Optional(
        Type.String({
          description:
            "Game installation directory supplied by the player; relative paths use the workspace root. Omit to try the remembered path and the platform hint.",
        }),
      ),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      let cfg: ModRepoConfig;
      try {
        cfg = readModRepoConfig(cwd);
      } catch {
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace; do not edit its maintainer configuration.",
        );
      }
      const platform = platformKey();
      const state = readState(cwd);
      const problems: string[] = [];

      // 1) dotnet SDK（与路径无关，留在本工具）
      const dotnet = checkDotnet();
      if (!dotnet.ok) {
        problems.push(
          dotnet.version
            ? `FAIL: dotnet SDK ${dotnet.version} is too old (need >= 8.0)`
            : "FAIL: dotnet SDK not found. Call install_runtime for install instructions.",
        );
      }

      // 2) 发现 + 校验（与 set_game_paths 共用同一份实现：每个候选都要过哨兵检查）
      const explicit = params.gameDir?.trim() ? resolveUserPath(cwd, params.gameDir) : undefined;
      const { gameDir } = discoverGameDir(cfg, state, platform, explicit);

      const lines: string[] = [];
      if (!gameDir) {
        problems.push(
          `FAIL: GAME_DIRECTORY_NOT_FOUND (${cfg.game?.name ?? "the game"}). Ask the player for the installed game directory and re-run check_runtime with gameDir; if not installed, install the game first. install_runtime only guides SDK setup and cannot fix this.`,
        );
      } else {
        const { managedDir, modInstallDir, workshopDir, notes } = pathsFromGameDir(gameDir, cfg, platform);
        writeState(cwd, {
          gameDir,
          managedDir,
          modInstallDir,
          workshopDir: workshopDir ?? null,
          runtime: { dotnet: dotnet.path, dotnetVersion: dotnet.version },
        });
        lines.push(`PASS: gameDir ${gameDir}`);
        lines.push(workshopDir ? `workshopDir ${workshopDir}` : "workshopDir (not found; optional)");
        if (modInstallDir) {
          lines.push(`modInstallDir ${modInstallDir}`);
          if (!existsSync(modInstallDir)) {
            lines.push(
              "WARN: that mod directory does not exist yet - normal on a first install (install_mod creates it). If the player moved the game, confirm this is where the game expects mods.",
            );
          }
        } else {
          problems.push("FAIL: INVALID_WORKSPACE_CONFIG: mod-repo.json has no modInstall.path for this platform");
        }
        lines.push(...notes);
      }

      if (problems.length) {
        return {
          content: [{ type: "text", text: problems.join("\n") }],
          details: { ...readState(cwd), ok: false, errors: problems },
        };
      }
      return {
        content: [
          {
            type: "text",
            text: `PASS: dotnet ${dotnet.version} (${dotnet.path}); ${lines.join("; ")}`,
          },
        ],
        details: { ...readState(cwd), ok: true },
      };
    },
  });
}

/** .NET SDK >= 8 检查（本工具特有；游戏目录的判据已移到 lib） */
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
