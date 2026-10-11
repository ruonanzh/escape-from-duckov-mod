/**
 * set_game_dir —— **agent 入口**：**记录一个具体的游戏安装目录**（玩家给的，或你自己找到的）。
 *
 * 与 `try_set_game_dir` 的分工：这里是你**已经有路径**、要把它记下来；
 * 没有路径、只想「确保游戏目录已就绪」时用 `try_set_game_dir`（无参，它自己会找）。
 *
 *   ① 给的路径**通过判据** → 落库（PASS）
 *   ② 没通过 → 自动发现 → 找到就落库 + **WARN**（说明为什么没用你给的那条）
 *   ③ 都失败 → **FAIL**（不落库）
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { formatPathOutcome, readModRepoConfig, resolveUserPath, setPathWithFallback } from "../lib/game-paths";

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "set_game_dir",
    label: "Set Game Directory",
    description:
      "Record one game directory. The tool validates it with the game's own marker file. If the path does not validate, the tool locates the game and records that path instead, and returns WARN. Use try_set_game_dir when you have no path.",
    promptSnippet: "Record one game directory. It validates the path, or locates the game.",
    promptGuidelines: [
      "Use set_game_dir when you have one game directory to record, and the player gave it or you found it. It validates the path and records it.",
      "If set_game_dir returns WARN, your path did not validate. The tool recorded a different path. Tell the player which path is in use.",
      "If set_game_dir returns FAIL, the tool recorded nothing. Your path did not validate, and discovery failed.",
      "Use try_set_game_dir when you have no path. It locates the game and records the derived paths.",
    ],
    parameters: Type.Object({
      path: Type.String({ description: "Game directory to record. Relative paths use the workspace root." }),
    }),
    async execute(_id, params, _s, _u, ctx) {
      const cwd = ctx.cwd;
      let cfg;
      try {
        cfg = readModRepoConfig(cwd);
      } catch {
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace. Do not edit its maintainer configuration.",
        );
      }
      const out = setPathWithFallback(cwd, cfg, "gameDir", resolveUserPath(cwd, params.path));
      const { text, ok, status } = formatPathOutcome(out);
      return { content: [{ type: "text", text }], details: { ok, status, recorded: out.recorded, wroteState: status !== "FAIL" } };
    },
  });
}
