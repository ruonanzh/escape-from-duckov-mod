import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import os from "node:os";
import { readState, writeState } from "../lib/game-paths";

/**
 * check_runtime —— **只查运行时**：.NET SDK >= 8 是否已装（并把 dotnet 路径记进状态文件）。
 *
 * 职责边界（2026-09-27 起，一个工具只干一件事）：
 *   · check_runtime      **只查 .NET SDK**（本工具）—— 不找游戏、不碰三条路径
 *   · check_game_paths   只**验证**给定/已记住的路径（只读、不扫描、不写状态）
 *   · set_game_paths     **记录**路径；**不带参数时自动发现并记录三条**（这就是"发现"的入口）
 *   · set_game_dir / set_workshop_dir / set_mod_install_dir   单条路径的记录
 *   · install_runtime    只给 SDK 安装指引（不执行安装）
 *   · install_mod        安装（目标目录不存在则创建）
 *
 * 判据与发现都在 .pi/lib/game-paths.ts（单一事实源）；本工具只做 SDK 这一件事。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "check_runtime",
    label: "Check Runtime",
    description:
      "Checks that the .NET SDK (>= 8.0) is installed and records the resolved dotnet path in .gamer-agent.local.json. It does not locate the game - game/mod paths belong to check_game_paths (verify) and set_game_paths (record).",
    promptSnippet: "Check the .NET SDK when compilation needs it or the player asks about setup",
    promptGuidelines: [
      "Use check_runtime when the .NET SDK (>= 8) is needed for compilation, or when the player asks whether the environment is ready.",
      "check_runtime only checks the SDK and records the dotnet path - game/mod paths are not its job: verify them with check_game_paths, record them with set_game_paths.",
      "Only SDK problems need install_runtime; a missing game directory is not an SDK problem.",
    ],
    parameters: Type.Object({}),
    async execute(_toolCallId, _params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      const dotnet = checkDotnet();
      if (!dotnet.ok) {
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
      // 只写 runtime 这一段；路径由 set_game_paths / set_* 负责（同一个原子读改写 helper）
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

/** .NET SDK >= 8 检查（本工具唯一的判据） */
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
