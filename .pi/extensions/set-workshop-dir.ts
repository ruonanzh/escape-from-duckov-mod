/**
 * set_workshop_dir —— **agent 入口**：记住玩家给的 Workshop 内容目录。
 *
 * 语义（与其它 setter 一致）：
 *   ① 给的路径**通过判据** → 落库（PASS）
 *   ② 没通过 → 内部**发现/派生**（无参那一层，不注册为工具）→ 成功 → 落库 + **WARN**（说明为什么没用你给的）
 *   ③ 内部也失败 → **FAIL**（不落库），并给出下一步
 */
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { formatPathOutcome, readModRepoConfig, resolveUserPath, setPathWithFallback } from "../lib/game-paths";

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "set_workshop_dir",
    label: "Set Workshop Directory",
    description: "Record the Steam Workshop content directory for this game. Validates its shape (…/steamapps/workshop/content/<appid>); if it does not validate, the tool derives the right one from the verified game directory and records that instead (returning WARN).",
    promptSnippet: "Remember the Workshop content directory (validates; falls back to derivation)",
    promptGuidelines: [
      "Use set_workshop_dir when the player gives you a Workshop path: it validates the path and records it.",
      "If it returns WARN, the path did not validate and a derived path was recorded instead - tell the player which one is in use.",
      "If it returns FAIL, the game directory is probably not recorded yet; set that first."
    ],
    parameters: Type.Object({
      path: Type.String({ description: "The path the player gave you; relative paths use the workspace root." }),
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
      const out = setPathWithFallback(cwd, cfg, "workshopDir", resolveUserPath(cwd, params.path));
      const { text, ok, status } = formatPathOutcome(out);
      return { content: [{ type: "text", text }], details: { ok, status, kind: out.kind, given: out.given, recorded: out.recorded, wroteState: status !== "FAIL" } };
    },
  });
}
