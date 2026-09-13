/**
 * set_game_paths —— **agent 入口**（批量）：一次记住玩家给的若干条路径。
 *
 * 每条走与单条 setter 完全相同的语义：验 → 过则落库（PASS）/ 不过则内部发现或派生 → WARN /
 * 都失败 → FAIL（该条不落库）。至少要传一条路径（否则 FAIL 提示）。
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { formatPathOutcome, readModRepoConfig, resolveUserPath, setPathWithFallback, type PathKind } from "../lib/game-paths";

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
    ],
    parameters: Type.Object({
      gameDir: Type.Optional(Type.String({ description: "Game install directory the player gave you." })),
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
        return {
          content: [
            {
              type: "text",
              text: "FAIL: no paths were given - pass at least one of gameDir / workshopDir / modInstallDir.\nNEXT: ask the player for the path(s), then call this again.",
            },
          ],
          details: { ok: false, reason: "NOTHING_GIVEN", wroteState: false },
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
