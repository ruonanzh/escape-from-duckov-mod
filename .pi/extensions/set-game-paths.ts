/**
 * set_game_paths —— **agent 入口**（批量）：一次记住若干条路径。
 *
 * 两种用法：
 *   · **给了路径**：每条走与单条 setter 相同的语义 —— 验 → 过则落库（PASS）/ 不过则内部发现或派生 → WARN / 都失败 → FAIL。
 *   · **不带参数**：**自动发现并记录三条**（发现 → 校验 → 记录）。这是"发现"的入口 ——
 *     `check_runtime` 只查 .NET SDK，不再负责找游戏（2026-09-27 单一职责）。
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import {
  discoverAndRecordPaths,
  formatPathOutcome,
  readModRepoConfig,
  readState,
  resolveUserPath,
  setPathWithFallback,
  type PathKind,
} from "../lib/game-paths";

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "set_game_paths",
    label: "Set Game Paths",
    description:
      "Record several game-related directories at once (game install / Steam Workshop content / mod install target). Each value is validated; a value that does not validate makes the tool locate or derive the right one and record that instead (that line comes back as WARN). At least one path must be provided. Use the single setters when you only have one path.",
    promptSnippet: "Remember several game paths at once (validates each; falls back to discovery)",
    promptGuidelines: [
      "Use set_game_paths when the player gives you more than one path at once; each one is validated before it is recorded.",
      "Lines that come back WARN mean the path the player gave did not validate and a different path was recorded - tell the player which is in use.",
      "Use check_game_paths instead when you only want to verify paths without recording them.",
      "Call set_game_paths with no arguments to have the game directory discovered and all three paths recorded (gameDir, then the mod install target and the Workshop directory derived from it).",
    ],
    parameters: Type.Object({
      gameDir: Type.Optional(
        Type.String({ description: "Game install directory the player gave you. Omit all three to auto-discover." }),
      ),
      workshopDir: Type.Optional(Type.String({ description: "Steam Workshop content directory the player gave you." })),
      modInstallDir: Type.Optional(Type.String({ description: "Mod install directory the player gave you." })),
    }),
    async execute(_id, params, _s, _u, ctx) {
      const cwd = ctx.cwd;
      let cfg;
      try {
        cfg = readModRepoConfig(cwd);
      } catch {
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace; do not edit its maintainer configuration.",
        );
      }
      const wanted: Array<[PathKind, string | undefined]> = [
        ["gameDir", params.gameDir],
        ["workshopDir", params.workshopDir],
        ["modInstallDir", params.modInstallDir],
      ];
      const given = wanted.filter(([, v]) => typeof v === "string" && v.trim() !== "");
      if (given.length === 0) {
        // 无参 = 发现并记录三条（"发现"的唯一入口）
        const d = discoverAndRecordPaths(cwd, cfg);
        if (!d.ok) {
          return {
            content: [
              {
                type: "text",
                text: "FAIL: GAME_DIRECTORY_NOT_FOUND - automatic discovery did not find the game (it already searched this platform's Steam locations and every library).\nNEXT: verify candidates from the player's hints with check_game_paths, or ask the player for the install directory (say what you already tried). The 'setup-workspace' skill has the full procedure.",
              },
            ],
            details: { ...readState(cwd), ok: false, reason: "GAME_DIRECTORY_NOT_FOUND", wroteState: false },
          };
        }
        const lines = [
          `PASS: gameDir ${d.gameDir}`,
          `modInstallDir ${d.modInstallDir ?? "(none - mod-repo.json declares no mod install path)"}`,
          `workshopDir ${d.workshopDir ?? "(not found; optional)"}`,
        ];
        for (const w of d.warnings) lines.push(`WARN: ${w}`);
        return {
          content: [{ type: "text", text: lines.join("\n") }],
          details: { ...readState(cwd), ok: true, discovered: true, wroteState: true },
        };
      }
      const lines: string[] = [];
      let anyFail = false;
      let anyWarn = false;
      for (const [kind, value] of given) {
        const out = setPathWithFallback(cwd, cfg, kind, resolveUserPath(cwd, value as string));
        const { text, ok, status } = formatPathOutcome(out);
        lines.push(text);
        if (!ok) anyFail = true;
        if (status === "WARN") anyWarn = true;
      }
      if (anyWarn) lines.push("NOTE: WARN lines mean the path the player gave was not usable; the recorded one is what the tools will use.");
      return {
        content: [{ type: "text", text: lines.join("\n") }],
        details: { ok: !anyFail, warned: anyWarn, wroteState: !anyFail || anyWarn },
      };
    },
  });
}
