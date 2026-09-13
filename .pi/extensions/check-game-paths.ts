/**
 * check_game_paths —— **只验**：校验游戏/工坊/mod 安装目录（只读、幂等、不扫描、不写状态）。
 *
 * 与其它工具的分工（别混用）：
 *   · check_game_paths   只验给定或已记住的路径            ← 本工具（无副作用，helper 只读会话也能用）
 *   · try_set_game_paths 扫描发现路径并写入状态            ← 位置未知时用它
 *   · check_runtime      一次跑完整流程（SDK + 发现 + 校验）
 *   · install_mod        安装（复用同一份判据，目标不存在则创建）
 *
 * 典型用途：玩家说"我的游戏装在 D:\Games\Duckov" → 带上 gameDir 验一下就知道对不对，
 * 不需要重跑扫描（重且会覆盖缓存）。
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import {
  checkGameDir,
  checkModInstallDir,
  checkWorkshopDir,
  platformKey,
  readModRepoConfig,
  readState,
  resolveUserPath,
  type ModRepoConfig,
  type PathVerdict,
  type RuntimeState,
} from "../lib/game-paths";

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "check_game_paths",
    description:
      "Verify game-related directories (game install, Steam Workshop content, mod install target). Read-only: it never writes .gamer-agent.local.json, never scans Steam, never creates directories. Pass any subset of gameDir/workshopDir/modInstallDir to verify just those; omit them all to verify the paths currently remembered in .gamer-agent.local.json. Use it when the player tells you a path (to find out whether it is right) or to re-check remembered paths; use try_set_game_paths when the location is unknown and needs scanning.",
    promptSnippet: "Verify game/mod paths without scanning or writing",
    promptGuidelines: [
      "Use check_game_paths (not check_runtime) when the player gives you a path: it verifies without scanning Steam and without writing state.",
      "check_game_paths never writes .gamer-agent.local.json - if a path is wrong, ask the player for the real one (Steam -> Library -> right-click the game -> Manage -> Browse local files) and verify again with gameDir.",
      "If nothing is known yet about the game location, run try_set_game_paths first; check_game_paths only verifies what you pass it or what is already remembered.",
      "Do not create or install anything as a result of a failed check_game_paths: report the FAIL text and its NEXT line to the player.",
    ],
    parameters: Type.Object({
      gameDir: Type.Optional(
        Type.String({ description: "Game install directory to verify; relative paths use the workspace root." }),
      ),
      workshopDir: Type.Optional(Type.String({ description: "Steam Workshop content directory to verify (optional)." })),
      modInstallDir: Type.Optional(Type.String({ description: "Mod install target directory to verify (optional)." })),
    }),
    async execute(_id, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const cfg: ModRepoConfig = readModRepoConfig(cwd);
      const state: RuntimeState = readState(cwd);
      const appId = String(cfg.game?.steamAppId ?? "");
      const platform = platformKey();

      const explicit = [params.gameDir, params.workshopDir, params.modInstallDir].some(
        (v) => typeof v === "string" && v.trim() !== "",
      );
      const remembered = (v: unknown): string | null =>
        typeof v === "string" && v.trim() ? v : null;
      const asPath = (v: string | undefined): string | null => (v?.trim() ? resolveUserPath(cwd, v) : null);

      // 契约：**传谁验谁**；一个都不传 = 验缓存里记住的那几条。
      // （明确传入时不掺入缓存的其它路径 —— 否则"只验 modInstallDir"会被一个过期的 gameDir 带崩。）
      const targets: Array<[string, string | null]> = explicit
        ? [
            ["gameDir", asPath(params.gameDir)],
            ["workshopDir", asPath(params.workshopDir)],
            ["modInstallDir", asPath(params.modInstallDir)],
          ]
        : [
            ["gameDir", remembered(state.gameDir)],
            ["workshopDir", remembered(state.workshopDir)],
            ["modInstallDir", remembered(state.modInstallDir)],
          ];

      if (targets.every(([, path]) => path === null)) {
        return {
          content: [
            {
              type: "text",
              text: "FAIL: nothing to check - no paths were given and none are remembered in .gamer-agent.local.json.\nNEXT: run try_set_game_paths to locate the game, or pass gameDir explicitly (ask the player where the game is).",
            },
          ],
          details: { ok: false, reason: "NOTHING_TO_CHECK" },
        };
      }

      const verdicts: Record<string, PathVerdict> = {};
      for (const [key, path] of targets) {
        if (path === null) continue;
        if (key === "gameDir") verdicts[key] = checkGameDir(path, cfg, platform);
        else if (key === "workshopDir") verdicts[key] = checkWorkshopDir(path, appId);
        else verdicts[key] = checkModInstallDir(path);
      }

      const lines: string[] = [];
      const failed: string[] = [];
      for (const [key, v] of Object.entries(verdicts)) {
        if (v.ok) lines.push(`PASS: ${key} ${v.path} (verified)`);
        else {
          lines.push(`FAIL: ${key} ${v.path ?? "(not given)"} - ${v.reason}`);
          if (v.next) lines.push(`  NEXT: ${v.next}`);
          failed.push(key);
        }
      }
      const ok = failed.length === 0;
      if (!ok) {
        lines.push(
          "NOTE: nothing was changed - this tool never writes state or creates directories. Report the above to the player; once you have a correct path, try_set_game_paths can record it (or install_mod can use it as-is if it is already remembered).",
        );
      }

      return {
        content: [{ type: "text", text: lines.join("\n") }],
        details: { ok, checked: verdicts, failed, wroteState: false },
      };
    },
  });
}
