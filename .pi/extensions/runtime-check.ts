import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import os from "node:os";

/**
 * check_runtime — 运行时契约（docs/mod-repo-guide.md §4）
 * 检查：1) dotnet SDK；2) 游戏安装目录。
 * 发现结果缓存到 <repo>/.gamer-agent.local.json（gitignored）。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "check_runtime",
    label: "Check Runtime",
    description: "Check whether the mod runtime is ready (dotnet SDK + game install dir)",
    promptSnippet: "Check mod runtime readiness (dotnet + game dir)",
    promptGuidelines: [
      "Use check_runtime before writing mod code; if it reports missing dotnet or game dir, call install_runtime.",
    ],
    parameters: Type.Object({}),
    async execute(_toolCallId, _params, _signal, _onUpdate, ctx) {
      const repoRoot = ctx.cwd;
      const stateFile = join(repoRoot, ".gamer-agent.local.json");
      const platform = process.platform === "win32" ? "windows" : process.platform === "darwin" ? "mac" : "linux";

      const cfg = JSON.parse(readFileSync(join(repoRoot, "mod-repo.json"), "utf8"));
      const loadState = () => {
        try {
          return JSON.parse(readFileSync(stateFile, "utf8"));
        } catch {
          return {};
        }
      };
      const state = loadState();
      const problems: string[] = [];

      // 1. dotnet
      const dotnet = checkDotnet();
      if (!dotnet.ok) {
        problems.push(
          dotnet.version
            ? `FAIL: dotnet SDK ${dotnet.version} is too old (need >= 8.0)`
            : "FAIL: dotnet SDK not found. Call install_runtime for install instructions.",
        );
      }

      // 2. game dir
      const expandHome = (p: string) => (p.startsWith("~/") || p === "~" ? join(os.homedir(), p.slice(2)) : p);
      const discoverGameDir = () => {
        const hint = cfg.game?.installDirHint?.[platform];
        if (!hint) return null;
        const dir = expandHome(hint);
        const managed = join(dir, cfg.compile?.managedDir?.[platform] ?? "Duckov_Data/Managed");
        return existsSync(join(managed, "TeamSoda.Duckov.Core.dll")) ? { gameDir: dir, managedDir: managed } : null;
      };

      const found = discoverGameDir();
      if (found) {
        state.gameDir = found.gameDir;
        state.managedDir = found.managedDir;
        state.modInstallDir = join(found.gameDir, cfg.modInstall?.path?.[platform] ?? "");
        state.runtime = { dotnet: dotnet.path, dotnetVersion: dotnet.version };
        writeFileSync(stateFile, `${JSON.stringify(state, null, 2)}\n`);
      } else {
        problems.push(
          `FAIL: game install dir not found (${cfg.game?.name}). Install the game via Steam, then re-run. Expected near: ${cfg.game?.installDirHint?.[platform] ?? "(no hint)"}`,
        );
      }

      if (problems.length) {
        return {
          content: [{ type: "text", text: problems.join("\n") }],
          details: { ok: false },
        };
      }
      return {
        content: [{ type: "text", text: `PASS: dotnet ${dotnet.version} (${dotnet.path}); gameDir ${state.gameDir}` }],
        details: { ok: true, ...state },
      };
    },
  });
}

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
  if (fromPath) return { ok: true, version: fromPath, path: "dotnet" };
  for (const c of candidates) {
    if (!existsSync(c)) continue;
    const v = tryRun(c);
    if (v) return { ok: parseInt(v.split(".")[0], 10) >= 8, version: v, path: c };
  }
  return { ok: false, version: null, path: null };
}
