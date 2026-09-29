import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { probeDotnet } from "../lib/dotnet";
import { readState, writeState } from "../lib/game-paths";

/**
 * check_runtime —— **只查运行时**：.NET SDK >= 8 是否已装（并把 dotnet 路径记进状态文件）。
 *
 * 职责边界（2026-09-27 起，一个工具只干一件事）：
 *   · check_runtime      **只查 .NET SDK**（本工具）—— 不找游戏、不碰三条路径
 *   · check_game_paths   只**验证**给定/已记住的路径（只读、不扫描、不写状态）
 *   · try_set_game_dir  **确保游戏目录已就绪**（无参、幂等）：自己找并记录，连带记录派生的两条（"发现"的入口）
 *   · set_game_dir      **记录一个具体的游戏安装目录**（玩家给的或自己找到的）
 *   · install_runtime    只给 SDK 安装指引（不执行安装）
 *   · install_mod        安装（目标目录不存在则创建）
 *
 * 判据：路径在 .pi/lib/game-paths.ts，.NET SDK 在 .pi/lib/dotnet.ts（单一事实源，与 install_runtime 共用）；本工具只做 SDK 这一件事。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "check_runtime",
    label: "Check Runtime",
    description:
      "Checks that the .NET SDK (>= 8.0) is installed and records the resolved dotnet path in .gamer-agent.local.json. It does not locate the game - game/mod paths belong to check_game_paths (verify) and try_set_game_dir / set_game_dir (ensure / record).",
    promptSnippet: "Check the .NET SDK when compilation needs it or the player asks about setup",
    promptGuidelines: [
      "Use check_runtime when the .NET SDK (>= 8) is needed for compilation, or when the player asks whether the environment is ready.",
      "check_runtime only checks the SDK and records the dotnet path - game/mod paths are not its job: verify them with check_game_paths, ensure or record them with try_set_game_dir / set_game_dir.",
      "Only SDK problems need install_runtime; a missing game directory is not an SDK problem.",
    ],
    parameters: Type.Object({}),
    async execute(_toolCallId, _params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const dotnet = probeDotnet();
      if (!dotnet.found || (dotnet.major ?? 0) < 8) {
        return {
          content: [
            {
              type: "text",
              text: dotnet.version
                ? `FAIL: dotnet SDK ${dotnet.version} is too old (need >= 8.0). Call install_runtime for install instructions.`
                : "FAIL: dotnet SDK not found. Call install_runtime for install instructions.",
            },
          ],
          details: { ...readState(cwd), ok: false },
        };
      }
      // 只写 runtime 这一段；路径由 try_set_game_dir / set_game_dir 负责（同一个原子读改写 helper）
      writeState(cwd, {
        runtime: { dotnet: dotnet.path, dotnetVersion: dotnet.version },
      });
      return {
        content: [
          {
            type: "text",
            text: `PASS: dotnet ${dotnet.version} (${dotnet.path}) - .NET SDK is ready.`,
          },
        ],
        details: { ...readState(cwd), ok: true },
      };
    },
  });
}
