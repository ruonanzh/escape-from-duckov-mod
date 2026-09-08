import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { readFileSync, existsSync, statSync, readdirSync } from "node:fs";
import { join, basename, resolve } from "node:path";
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

/** 截断长输出，避免爆 LLM 上下文（pi 标准 50KB/2000 行，这里取保守的 200 行）。 */
function truncateLines(text: string, maxLines = 200): string {
  const lines = text.split("\n");
  if (lines.length <= maxLines) return text;
  return `${lines.slice(0, maxLines).join("\n")}\n... (${lines.length - maxLines} more lines truncated)`;
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
    description: "Validate info.ini and C# source files in an existing mod directory (relative modDir uses workspace root). With ready SDK/game paths, runs dotnet build, which may restore dependencies and write build outputs; does not edit source or test gameplay. Output text uses PASS/FAIL/PARTIAL and a NEXT line for next steps. Skipped compilation is PARTIAL, never full PASS.",
    promptSnippet: "Check an existing C# mod and compile it when runtime prerequisites are ready",
    promptGuidelines: [
      "Use validate_mod for changed code or requested validation. It can compile, so do not mechanically duplicate the same dotnet build. Inspect the PARTIAL status in output text; resolve skipped-compilation prerequisites before claiming success.",
    ],
    parameters: Type.Object({
      modDir: Type.String({ minLength: 1, description: "Existing mod directory; absolute or relative to the workspace root, e.g. your_mods/MyMod (not relative to the skill)." }),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const repoRoot = ctx.cwd;
      if (!params.modDir.trim()) throw new Error("INVALID_MOD_DIR: Provide an existing mod directory, e.g. your_mods/MyMod.");
      const modDir = resolve(repoRoot, params.modDir);
      const errors: string[] = [];
      const warnings: string[] = [];
      let modName = basename(modDir);

      // 0. mod 目录存在（不存在 = 参数错误/前置条件不满足，throw 标记 isError）
      if (!statSync(modDir, { throwIfNoEntry: false })?.isDirectory()) {
        throw new Error(`INVALID_MOD_DIR: ${modDir} is not a directory. Check the session's bound path or ask the player to restore it; do not create a replacement merely to validate.`);
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
      const staticOk = errors.length === 0;
      let compilation: "skipped" | "passed" | "failed" = "skipped";
      const state = loadState(repoRoot);
      // check_runtime writes runtime.dotnet; accept the historical flat field too.
      const cachedDotnet = state.runtime?.dotnet ?? state.dotnet;
      const dotnet = typeof cachedDotnet === "string" && cachedDotnet ? cachedDotnet : findDotnet();
      if (!dotnet) {
        warnings.push("dotnet not found - compile check skipped. Run check_runtime; use install_runtime only for SDK installation instructions.");
      } else if (!state.gameDir) {
        warnings.push("game dir not found - compile check skipped. Run check_runtime with the installed game directory; SDK installation cannot fix game location.");
      } else if (csproj && staticOk) {
        try {
          execFileSync(dotnet, ["build", csproj, "-c", "Release"], {
            cwd: modDir,
            env: { ...process.env, DUCKOV_DIR: state.gameDir },
            encoding: "utf8",
            stdio: ["ignore", "pipe", "pipe"],
          });
          compilation = "passed";
        } catch (e) {
          compilation = "failed";
          errors.push(`dotnet build failed: ${String(e.stderr || e.stdout || e.message).slice(0, 600)}`);
        }
      }

      // 4. dll produced
      const dllCandidates = [join(modDir, `${modName}.dll`), join(modDir, "bin", "Release", `${modName}.dll`)];
      const artifactPresent = dllCandidates.some((p) => existsSync(p));
      if (compilation === "passed" && !artifactPresent) {
        errors.push(`missing ${modName}.dll after build; check AssemblyName and the output directory`);
      }

      // preview.png (warn only)
      if (!existsSync(join(modDir, "preview.png"))) {
        warnings.push("missing preview.png (256x256, needed for Steam Workshop upload)");
      }

      const ok = errors.length === 0 && compilation === "passed";
      const status = ok ? "passed" : errors.length ? "failed" : "partial";
      const nextAction = ok
        ? "Report validation and compilation passed; in-game loading and behavior still require testing."
        : errors.length
          ? "Inspect the reported errors and specs/mod-spec.md. Fix only in an authorized mod-development session, then validate again."
          : "Run check_runtime to resolve the skipped compilation prerequisites, then re-run validate_mod. An existing DLL is not proof of a build in this run.";
      const lines = [...warnings.map((w) => `WARN: ${w}`), ...errors.map((e) => `FAIL: ${e}`)];
      if (ok) lines.push(`PASS: ${basename(modDir)} is valid (compiled; not tested in-game).`);
      else if (status === "partial") lines.push("PARTIAL: Static checks passed, but compilation was not performed.");
      lines.push(`NEXT: ${nextAction}`);
      return {
        content: [{ type: "text", text: truncateLines(lines.join("\n")) }],
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
