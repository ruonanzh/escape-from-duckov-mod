import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { readFileSync, existsSync, statSync, readdirSync } from "node:fs";
import { join, basename } from "node:path";
import { execFileSync } from "node:child_process";
import os from "node:os";

const NAME_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

function parseIni(text: string) {
  const result: Record<string, string> = {};
  for (const line of text.split(/\r?\n/)) {
    const m = line.match(/^\s*([^=]+?)\s*=\s*(.*)$/);
    if (m) result[m[1].trim()] = m[2].trim();
  }
  return result;
}

/**
 * validate_mod — mod 校验工具（替代原 lint/check_mod.mjs 脚本）。
 * 校验 info.ini 字段、编译源文件、dotnet build 编译、dll 产物。
 * execute 在 pi 进程内执行，dotnet 用 check_runtime 缓存的绝对路径。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "validate_mod",
    label: "Validate Mod",
    description: "Validate a mod (info.ini fields + compile sources + dotnet build + dll output)",
    promptSnippet: "Validate a mod under your_mods/",
    promptGuidelines: [
      "Use validate_mod after writing mod code to check it compiles and passes the spec.",
    ],
    parameters: Type.Object({
      modDir: Type.String({ description: "Path to the mod directory (e.g. your_mods/<ModName>)" }),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const repoRoot = ctx.cwd;
      const modDir = params.modDir;
      const errors: string[] = [];
      const warnings: string[] = [];
      let modName = basename(modDir);

      // 0. mod 目录存在
      if (!statSync(modDir, { throwIfNoEntry: false })?.isDirectory()) {
        return {
          content: [{ type: "text", text: `FAIL: ${modDir} is not a directory` }],
          details: { ok: false, errors: [`${modDir} is not a directory`], warnings: [] },
        };
      }

      // 1. info.ini
      const iniPath = join(modDir, "info.ini");
      if (!existsSync(iniPath)) {
        errors.push("missing info.ini");
      } else {
        const ini = parseIni(readFileSync(iniPath, "utf8"));
        if (ini.name) modName = ini.name;
        if (!ini.name) errors.push("info.ini: name is missing");
        if (!ini.displayName) errors.push("info.ini: displayName is missing");
        if (!ini.description) errors.push("info.ini: description is missing");
        if (ini.name && !NAME_RE.test(ini.name)) {
          errors.push(`info.ini: name (${ini.name}) is not a valid namespace (${NAME_RE})`);
        }
      }

      // 2. compile sources
      const csproj = readdirSync(modDir).find((f) => f.endsWith(".csproj"));
      if (!csproj) errors.push("missing <ModName>.csproj");
      if (!existsSync(join(modDir, "ModBehaviour.cs"))) errors.push("missing ModBehaviour.cs");

      // 3. compile（读 check_runtime 缓存的状态文件拿 dotnet + gameDir）
      const state = loadState(repoRoot);
      const dotnet = state.dotnet ?? findDotnet();
      if (!dotnet) {
        warnings.push("dotnet not found — skip compile check (call the check_runtime tool)");
      } else if (!state.gameDir) {
        warnings.push("game dir not found — skip compile check (call the check_runtime tool)");
      } else if (csproj) {
        try {
          execFileSync(dotnet, ["build", csproj, "-c", "Release"], {
            cwd: modDir,
            env: { ...process.env, DUCKOV_DIR: state.gameDir },
            encoding: "utf8",
            stdio: ["ignore", "pipe", "pipe"],
          });
        } catch (e) {
          errors.push(`dotnet build failed: ${String(e.stderr || e.stdout || e.message).slice(0, 600)}`);
        }
      }

      // 4. dll produced
      const dllCandidates = [join(modDir, `${modName}.dll`), join(modDir, "bin", "Release", `${modName}.dll`)];
      if (!dllCandidates.some((p) => existsSync(p))) {
        errors.push(`missing ${modName}.dll (compile first)`);
      }

      // preview.png (warn only)
      if (!existsSync(join(modDir, "preview.png"))) {
        warnings.push("missing preview.png (256x256, needed for Steam Workshop upload)");
      }

      const ok = errors.length === 0;
      const lines = [...warnings.map((w) => `WARN: ${w}`), ...errors.map((e) => `FAIL: ${e}`)];
      if (ok) lines.push(`PASS: ${basename(modDir)} is valid`);
      return {
        content: [{ type: "text", text: lines.join("\n") }],
        details: { ok, errors, warnings },
      };
    },
  });
}

function loadState(repoRoot: string) {
  try {
    return JSON.parse(readFileSync(join(repoRoot, ".gamer-agent.local.json"), "utf8"));
  } catch {
    return {};
  }
}

function findDotnet() {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [join(os.homedir(), ".dotnet", bin)];
  if (process.platform === "win32") {
    candidates.push("C:\\Program Files\\dotnet\\dotnet.exe");
  } else if (process.platform === "darwin") {
    candidates.push("/usr/local/share/dotnet/dotnet");
  }
  for (const c of candidates) {
    try {
      execFileSync(c, ["--version"], { encoding: "utf8" });
      return c;
    } catch {
      /* try next */
    }
  }
  try {
    execFileSync("dotnet", ["--version"], { encoding: "utf8" });
    return "dotnet";
  } catch {
    return null;
  }
}
