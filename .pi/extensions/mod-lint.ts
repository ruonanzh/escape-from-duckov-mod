import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { CODE_IDENTIFIER_RE, isCodeIdentifier } from "../lib/mod-identity";
import { Type } from "typebox";
import { readFileSync, existsSync, statSync, readdirSync } from "node:fs";
import { join, basename, resolve, dirname } from "node:path";
import { execFileSync } from "node:child_process";
import { runAsync } from "../lib/proc";
import { NEW_ACTIONS, stripJsonComments, validateDataLayer } from "../lib/mod-config-data";
import os from "node:os";

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

/** 游戏里全部物品的 typeID（`data-probe` 分页读）。
 * `dotnet` 是 `state.runtime.dotnet` 里的那个可执行文件（探针是 apphost，找不到运行时就跑不起来）。
 * 拿不到时把**原因**一并返回 —— 只报“跳过了”没法排查。 */
async function gameTypeIDs(repoRoot: string, gameDir: string, dotnet: string): Promise<{ ids: Set<number> | null; reason: string }> {
  try {
    const isWin = process.platform === "win32";
    const probe = join(repoRoot, "tools", "data-probe", "bin", "Release", "net8.0", isWin ? "data-probe.exe" : "data-probe");
    if (!existsSync(probe)) return { ids: null, reason: "tools/data-probe is not built (run: dotnet build tools/data-probe -c Release)" };
    const dataDir = join(gameDir, isWin ? "Duckov_Data" : join("Duckov.app", "Contents", "Resources", "Data"));
    const managed = join(dataDir, "Managed");
    if (!existsSync(managed)) return { ids: null, reason: `the game's Managed directory is missing: ${managed}` };

    // apphost 要能找到 .NET 运行时：把缓存的 dotnet 目录放到 DOTNET_ROOT 与 PATH 前面
    const env: NodeJS.ProcessEnv = { ...process.env };
    if (dotnet) {
      const root = dirname(dotnet);
      env.DOTNET_ROOT = root;
      env.PATH = `${root}${isWin ? ";" : ":"}${env.PATH ?? ""}`;
    }

    const ids = new Set<number>();
    // 分页的停止条件不得用「匹配数 < 500」—— 一页是 500 个**资产**，
    // 而里面有些行不是物品（`typeID=-1`），于是第一页只会匹到 494 个 ✗（实测踩到：后面的号全看不到）。
    // 改用「这一页没带来新号就停」。
    for (let offset = 0, page = 0; offset < 20_000 && page < 40; offset += 500, page++) {
      const r = await runAsync(
        probe,
        ["--managed", managed, "--data", dataDir, "--action", "list", "--class", "Item", "--file", "resources.assets", "--offset", String(offset)],
        { timeoutMs: 60_000, env },
      );
      const before = ids.size;
      for (const m of r.stdout.matchAll(/typeID=(\d+)/g)) ids.add(Number(m[1]));
      if (ids.size === before) break;      // 没有新号 = 到末页了
    }
    if (!ids.size) return { ids: null, reason: "data-probe returned no typeIDs" };
    return { ids, reason: "" };
  } catch (e) {
    return { ids: null, reason: `data-probe failed: ${(e as Error).message.split("\n")[0]}` };
  }
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
    description:
      "Validate info.ini and the C# source files in an existing mod directory. A relative modDir uses the workspace root. When the SDK and the game paths are ready, it runs dotnet build. The build may restore dependencies and write build output. It does not edit source and does not test gameplay. The output text uses PASS, FAIL, or PARTIAL, and a NEXT line. Skipped compilation is PARTIAL, never a full PASS.",
    promptSnippet:
      "Validate an existing C# mod and compile it when runtime prerequisites are ready",
    promptGuidelines: [
      "Use validate_mod for changed code or requested validation. It can compile, so do not repeat the same dotnet build. Read the PARTIAL status in the output text. Resolve the skipped-compilation prerequisites before you claim success.",
    ],
    parameters: Type.Object({
      modDir: Type.String({
        minLength: 1,
        description:
          "Existing mod directory. It may be absolute or relative to the workspace root, for example your_mods/MyMod. It is not relative to the skill.",
      }),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const repoRoot = ctx.cwd;
      if (!params.modDir?.trim())
        throw new Error(
          "INVALID_MOD_DIR: Provide an existing mod directory, for example your_mods/MyMod.",
        );
      const modDir = resolve(repoRoot, params.modDir);
      const errors: string[] = [];
      const warnings: string[] = [];
      let modName = basename(modDir);

      // 0. mod 目录存在（不存在 = 参数错误/前置条件不满足，throw 标记 isError）
      if (!statSync(modDir, { throwIfNoEntry: false })?.isDirectory()) {
        throw new Error(
          `INVALID_MOD_DIR: ${modDir} is not a directory. Check the session's bound path, or ask the player to restore it. Do not create a replacement only to validate.`,
        );
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
        if (ini.name && !isCodeIdentifier(ini.name)) {
          errors.push(
            `info.ini: name (${ini.name}) is not a valid namespace (${CODE_IDENTIFIER_RE})`,
          );
        }
      }

      // 2. compile sources
      const csproj = readdirSync(modDir).find((f) => f.endsWith(".csproj"));
      if (!csproj) errors.push("missing <ModName>.csproj");
      if (!existsSync(join(modDir, "ModBehaviour.cs")))
        errors.push("missing ModBehaviour.cs");

      // 2.5 ⭐ 游戏硬规则：游戏用 Assembly.LoadFrom(dll).GetType(<info.ini 的 name> + ".ModBehaviour")
      //     找入口类 → info.ini 的 name 必须与 ModBehaviour.cs 的 namespace 一致（类名必须是 ModBehaviour）。
      //     ⚠️ 不一致时**编译照样过** ✗、游戏只是静默不加载 ✗ —— 所以这条必须在这里静态拦下。
      if (existsSync(join(modDir, "ModBehaviour.cs"))) {
        const src = readFileSync(join(modDir, "ModBehaviour.cs"), "utf8");
        // 先剥注释再扫 ✓：注释里写 "class ModBehaviour" 不该算通过 ✗
        const code = src.replace(/\/\*[\s\S]*?\*\//g, " ").replace(/\/\/[^\n]*/g, " ");
        // ⚠ 不能用「行首」当边界 ✗ —— `namespace X { public class ModBehaviour {} }` 写一行是合法 C# ✓
        //   → 用「前面不是标识符字符」当边界 ✓；`\b` 保证不匹到 ModBehaviourExtra ✓
        const ns = /(?:^|[^\w$])namespace\s+([A-Za-z_][A-Za-z0-9_.]*)/.exec(code)?.[1];
        const hasClass = /(?:^|[^\w$])class\s+ModBehaviour\b/.test(code);
        if (!hasClass) {
          errors.push(`ModBehaviour.cs: no "class ModBehaviour" found - the game looks for ${modName}.ModBehaviour`);
        } else if (!ns) {
          errors.push(
            `ModBehaviour.cs: class ModBehaviour is in no namespace - it must be in namespace ${modName} (the game looks for ${modName}.ModBehaviour)`,
          );
        } else if (ns !== modName) {
          errors.push(
            `info.ini name (${modName}) does not match ModBehaviour.cs namespace (${ns}) - the game looks for ${modName}.ModBehaviour and will silently not load this mod`,
          );
        }
      }

      // 2.7 数据层（新增武器 / 新增物品的 config）：与 libs/mod-kit/ItemFactory.cs 对齐
      const cfgPath = join(modDir, "config.json");
      if (existsSync(cfgPath)) {
        try {
          const cfg = JSON.parse(stripJsonComments(readFileSync(cfgPath, "utf8")));
          const list = (cfg as { entries?: unknown }).entries;
          const hasNew =
            Array.isArray(list) &&
            list.some((e) => e && typeof e === "object" && NEW_ACTIONS.has(String((e as Record<string, unknown>).action)));
          let ids: Set<number> | null = null;
          if (hasNew) {
            const st = loadState(repoRoot);
            const cached = st.runtime?.dotnet ?? st.dotnet;
            const gameDir = typeof st.gameDir === "string" ? st.gameDir : "";
            if (!gameDir) warnings.push("the game directory is unknown, so the typeID checks were skipped (run try_set_game_dir)");
            else {
              const r0 = await gameTypeIDs(repoRoot, gameDir, typeof cached === "string" ? cached : "");
              ids = r0.ids;
              if (!ids) warnings.push(`${r0.reason} - the typeID checks were skipped`);
            }
          }
          const r = validateDataLayer(cfg, ids);
          errors.push(...r.errors);
          warnings.push(...r.warnings);
        } catch (e) {
          errors.push(`config.json is not valid JSON: ${(e as Error).message}`);
        }
      }

      // 3. compile（读状态文件拿 dotnet（check_runtime 写）+ gameDir（路径工具写））
      const staticOk = errors.length === 0;
      let compilation: "skipped" | "passed" | "failed" = "skipped";
      const state = loadState(repoRoot);
      // check_runtime writes runtime.dotnet; accept the historical flat field too.
      const cachedDotnet = state.runtime?.dotnet ?? state.dotnet;
      const dotnet =
        typeof cachedDotnet === "string" && cachedDotnet
          ? cachedDotnet
          : findDotnet();
      if (!dotnet) {
        warnings.push(
           "dotnet not found - compile check skipped. Run check_runtime (SDK only). Use install_runtime only for SDK installation instructions.",
        );
      } else if (!state.gameDir) {
        warnings.push(
           "game directory not found - compile check skipped. Run try_set_game_dir to discover and record the game directory (or set_game_dir with an explicit path). SDK installation cannot fix the game location.",
        );
      } else if (csproj && staticOk) {
        try {
          await runAsync(dotnet, ["build", csproj, "-c", "Release"], {
            cwd: modDir,
            env: { ...process.env, DUCKOV_DIR: state.gameDir },
            timeoutMs: 300_000,
          });
          compilation = "passed";
        } catch (e) {
          compilation = "failed";
          const err = e as { stderr?: string; stdout?: string; message?: string };
          errors.push(
            `dotnet build failed: ${String(err.stderr || err.stdout || err.message).slice(0, 600)}`,
          );
        }
      }

      // 4. dll produced
      const dllCandidates = [
        join(modDir, `${modName}.dll`),
        join(modDir, "bin", "Release", `${modName}.dll`),
      ];
      const artifactPresent = dllCandidates.some((p) => existsSync(p));
      if (compilation === "passed" && !artifactPresent) {
        errors.push(
          `missing ${modName}.dll after build. Check AssemblyName and the output directory.`,
        );
      }

      // 4b. 产物**内容**校验（U25）：`dotnet build` 退出 0 **不等于**真的编译了我们的源文件。
      // 真实踩坑：csproj 把 OutputPath 指到项目根 → SDK 的默认排除规则把项目根下的 *.cs 全排掉
      // → "编译成功"但产出 3.5KB 空壳 dll（游戏里能看到、却勾不上）。
      if (compilation === "passed" && artifactPresent) {
        const dllPath = dllCandidates.find((candidate) =>
          existsSync(candidate),
        ) as string;
        const bytes = readFileSync(dllPath);
        if (
          !bytes.includes("TeamSoda.Duckov.Core") ||
          !bytes.includes("ModBehaviour")
        ) {
          errors.push(
            `${modName}.dll looks like an empty stub (no reference to the game assembly and/or no ModBehaviour type). ` +
              "Common cause: csproj sets OutputPath to the project root, so the SDK excludes the project's own *.cs files and compiles nothing - " +
              "remove that OutputPath (or add EnableDefaultCompileItems=false + an explicit Compile item), then validate again.",
          );
        }
      }

      // preview.png (warn only)
      if (!existsSync(join(modDir, "preview.png"))) {
        warnings.push(
          "missing preview.png (256x256, needed for Steam Workshop upload)",
        );
      }

      const ok = errors.length === 0 && compilation === "passed";
      const status = ok ? "passed" : errors.length ? "failed" : "partial";
      const nextAction = ok
        ? "Report that validation and compilation passed. In-game loading and behavior still require testing."
        : errors.length
          ? "Inspect the reported errors. Fix only in an authorized mod-development session, then validate again."
           : "Resolve the skipped compilation prerequisites first (check_runtime for the .NET SDK, try_set_game_dir for the game directory), then re-run validate_mod. An existing DLL is not proof of a build in this run.";
      const lines = [
        ...warnings.map((w) => `WARN: ${w}`),
        ...errors.map((e) => `FAIL: ${e}`),
      ];
      if (ok)
        lines.push(
          `PASS: ${basename(modDir)} is valid (compiled, not tested in-game).`,
        );
      else if (status === "partial")
        lines.push(
          "PARTIAL: Static checks passed, but compilation was not performed.",
        );
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
    return JSON.parse(
      readFileSync(join(repoRoot, ".gamer-agent.local.json"), "utf8"),
    );
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
