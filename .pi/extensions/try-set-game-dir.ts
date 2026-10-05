/**
 * try_set_game_dir —— **确保游戏目录已就绪**（无参、幂等）。
 *
 * 语义（判据与发现都在 `.pi/lib/game-paths.ts`，本工具只是入口）：
 *   ① 记住的 gameDir 仍有效，且派生的 mod 安装目录 / 创意工坊目录与**当前平台**一致 → 什么都不写
 *      → `PASS: gameDir already correct`
 *   ② 无效 / 过期 / 从没记过 → 自动发现（每个候选都过哨兵）→ 记录 gameDir + 派生的两条
 *      → `PASS: gameDir discovered and recorded`
 *   ③ 找不到 → `FAIL`（不写状态）
 *
 * 为什么无参：agent 不需要先知道路径 —— "确保它已就绪"这件事由工具自己做；它自己找过之后仍然失败，
 * 才轮到 agent 去想办法（找线索、问玩家）。
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { ensureGameDir, readModRepoConfig, readState } from "../lib/game-paths";

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "try_set_game_dir",
    label: "Ensure Game Directory",
    description:
      "Makes sure the game directory is recorded and still valid for this platform, together with the mod install directory and the Steam Workshop directory derived from it. Takes no arguments: it either confirms the recorded paths are already correct, or locates the game and records them.",
    promptSnippet: "Ensure the game directory is recorded (locates it when needed)",
    promptGuidelines: [
      "Use try_set_game_dir when a task needs the game location (compiling, installing) and you are not sure the paths are recorded or still valid.",
      "try_set_game_dir takes no arguments: it confirms the recorded paths or locates and records them, so you do not have to find the path yourself first.",
      "try_set_game_dir only records paths in .gamer-agent.local.json; it never creates or changes anything inside the game.",
    ],
    parameters: Type.Object({}),
    async execute(_id, _params, _s, _u, ctx) {
      const cwd = ctx.cwd;
      let cfg;
      try {
        cfg = readModRepoConfig(cwd);
      } catch {
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace; do not edit its maintainer configuration.",
        );
      }
      const r = ensureGameDir(cwd, cfg);
      if (r.status === "missing") {
        return {
          content: [
            {
              type: "text",
              text: `FAIL: gameDir not found - ${r.reason}.\nNEXT: see the 'setup-workspace' skill.`,
            },
          ],
          details: {
            ...readState(cwd),
            ok: false,
            reason: "GAME_DIRECTORY_NOT_FOUND",
            wroteState: false,
          },
        };
      }
      const lines = [
        `PASS: gameDir ${r.status === "already" ? "already correct" : "discovered and recorded"} (${r.gameDir})`,
        r.modInstallDir
          ? `modInstallDir (derived from gameDir) ${r.modInstallDir}`
          : "modInstallDir (none - mod-repo.json declares no mod install path for this platform)",
        r.workshopDir
          ? `workshopDir (derived, read-only reference) ${r.workshopDir}`
          : "workshopDir (not found; optional)",
      ];
      for (const w of r.warnings) lines.push(`WARN: ${w}`);
      return {
        content: [{ type: "text", text: lines.join("\n") }],
        details: {
          ...readState(cwd),
          ok: true,
          status: r.status,
          wroteState: r.status === "recorded",
        },
      };
    },
  });
}
