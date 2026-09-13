/**
 * set_mod_install_dir —— **agent 入口**：记住玩家给的 mod 安装目录。
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
    name: "set_mod_install_dir",
    label: "Set Mod Install Directory",
    description: "Record where mods should be installed for this game. Validates it with the same rule install_mod uses (the target must live inside the game folder); if it does not validate, the tool derives the conventional path and records that instead (returning WARN).",
    promptSnippet: "Remember the mod install directory (validates; falls back to derivation)",
    promptGuidelines: [
      "Use set_mod_install_dir when the player insists mods go somewhere specific: it validates that path and records it.",
      "If it returns WARN, the path did not validate and the conventional path was recorded instead - tell the player which one is in use.",
      "install_mod always installs into what is recorded here, so get this right before installing."
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
      const out = setPathWithFallback(cwd, cfg, "modInstallDir", resolveUserPath(cwd, params.path));
      const { text, ok, status } = formatPathOutcome(out);
      return { content: [{ type: "text", text }], details: { ok, status, kind: out.kind, given: out.given, recorded: out.recorded, wroteState: status !== "FAIL" } };
    },
  });
}
