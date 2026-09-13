/**
 * try_set_game_paths —— **扫描/发现**游戏目录并记入状态（有副作用：写 .gamer-agent.local.json）。
 *
 * 与其它工具的分工：
 *   · check_game_paths     只验给定/已记住的路径（只读，无副作用）
 *   · try_set_game_paths   位置未知时**去找**，找到就落库                ← 本工具
 *   · check_runtime        一次跑完整流程（SDK + 发现 + 校验 + 汇总）
 *   · install_mod          安装（复用同一份判据；目标不存在则创建）
 *
 * 发现顺序：显式 gameDir → 缓存里的 gameDir → mod-repo.json 的 installDirHint。
 * 每个候选都要过 `checkGameDir`（看游戏自带的哨兵程序集）——
 * **发现不等于信任**：目录存在、但里面不是这个游戏，照样不算找到。
 *
 * 失败时**不写状态**（不做假成功），并明确告诉 agent 去找：
 * 玩家能在 Steam → 库 → 右键游戏 → 管理 → 浏览本地文件 里看到游戏目录。
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  checkModInstallDir,
  discoverGameDir,
  pathsFromGameDir,
  platformKey,
  readModRepoConfig,
  readState,
  resolveUserPath,
  writeState,
  GAME_SENTINEL,
  type ModRepoConfig,
} from "../lib/game-paths";

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "try_set_game_paths",
    label: "Set Game Paths",
    description:
      "Locate the game install directory (explicit gameDir, remembered path, or the platform hint in mod-repo.json), verify it with the game's own marker file, and record the discovered paths (gameDir / managedDir / modInstallDir / workshopDir) into .gamer-agent.local.json. Use it when the game location is unknown or has changed; use check_game_paths instead when you already have a path to verify (that one is read-only). Each candidate is validated, so a directory that exists but is not this game does not count as found. On failure it writes nothing and tells you what to ask the player.",
    promptSnippet: "Find and record the game/mod directories (writes state)",
    promptGuidelines: [
      "Use try_set_game_paths when the game location is unknown: it scans the known candidates and records what it verifies.",
      "try_set_game_paths writes .gamer-agent.local.json - do not use it to re-check an already remembered path; use check_game_paths for that (read-only).",
      "If try_set_game_paths fails, ask the player for the game install directory (Steam -> Library -> right-click the game -> Manage -> Browse local files), then either pass it as gameDir to this tool or verify it first with check_game_paths.",
      "After try_set_game_paths succeeds, install_mod can use the recorded paths; no need to call check_runtime just to refresh them.",
    ],
    parameters: Type.Object({
      gameDir: Type.Optional(
        Type.String({
          description:
            "Game installation directory to try first (relative paths use the workspace root). Omit to try the remembered path and the platform hint.",
        }),
      ),
    }),
    async execute(_id, params, _s, _u, ctx) {
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
      const explicit = params.gameDir?.trim() ? resolveUserPath(cwd, params.gameDir) : undefined;

      const { gameDir, tried } = discoverGameDir(cfg, state, platform, explicit);
      const triedText = tried.length
        ? tried.map((t) => `  - ${t.path} - ${t.reason ?? "did not pass validation"}`).join("\n")
        : "  (no usable candidate paths)";

      if (!gameDir) {
        return {
          content: [
            {
              type: "text",
              text:
                "FAIL: GAME_DIRECTORY_NOT_FOUND - could not find the game install directory.\n" +
                `Tried:\n${triedText}\n` +
                `Each candidate was checked for the game's own ${GAME_SENTINEL}.\n` +
                "NOTE: nothing was written - the remembered paths (if any) were left untouched and may be stale.\n" +
                "NEXT: ask the player for the game install directory (Steam -> Library -> right-click the game -> Manage -> Browse local files), then call try_set_game_paths with that gameDir (or verify it first with check_game_paths). If the game is not installed at all, install it first.",
            },
          ],
          details: { ok: false, reason: "GAME_DIRECTORY_NOT_FOUND", wroteState: false, tried },
        };
      }

      const { managedDir, modInstallDir, workshopDir, notes } = pathsFromGameDir(gameDir, cfg, platform);
      const problems: string[] = [];
      if (!managedDir)
        problems.push("FAIL: INVALID_WORKSPACE_CONFIG: mod-repo.json has no compile.managedDir entry for this platform");
      if (!modInstallDir)
        problems.push("FAIL: INVALID_WORKSPACE_CONFIG: mod-repo.json has no modInstall.path for this platform");

      if (problems.length) {
        return {
          content: [{ type: "text", text: `${problems.join("\n")}\nNOTE: nothing was written.\nNEXT: Reopen or update this game workspace (do not edit the maintainer configuration).` }],
          details: { ok: false, reason: "INVALID_WORKSPACE_CONFIG", wroteState: false },
        };
      }

      // 目标目录不存在是正常初始状态（安装时创建）；但若它已经存在，顺手验一下是否真在游戏目录里。
      const targetNote =
        modInstallDir && readState(cwd).modInstallDir && checkModInstallDir(modInstallDir).ok === false
          ? `\nWARN: modInstallDir ${modInstallDir} is not a new value and does not look like it is inside the game folder - it may be recreated on install; confirm with the player.`
          : "";

      writeState(cwd, {
        gameDir,
        managedDir,
        modInstallDir,
        workshopDir: workshopDir ?? null,
      });

      return {
        content: [
          {
            type: "text",
            text:
              `PASS: gameDir ${gameDir}\n` +
              `managedDir ${managedDir}\n` +
              `modInstallDir ${modInstallDir}${workshopDir ? `\nworkshopDir ${workshopDir}` : ""}` +
              (notes.length ? `\n${notes.join("\n")}` : "") +
              targetNote +
              "\nNEXT: install_mod can now install into that directory (it creates the target if it does not exist yet).",
          },
        ],
        details: { ok: true, wroteState: true, gameDir, managedDir, modInstallDir, workshopDir, tried },
      };
    },
  });
}
