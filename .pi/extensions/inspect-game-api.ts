import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { runAsync, serialize } from "../lib/proc";
import { existsSync } from "node:fs";
import { join } from "node:path";
import { managedDirFor, platformKey, readModRepoConfig, readState } from "../lib/game-paths";
import { probeUpToDate } from "../lib/probe";

/**
 * inspect_game_api —— **只读**检查游戏托管 DLL（`docs/api` 不够时的升级手段）。
 *
 * `docs/api/` 是维护者反射 dump 的**公开**签名；私有成员 / 实际行为 / 当前版本要读托管 DLL。
 * 本工具是 `tools/api-probe`（ILSpy `ICSharpCode.Decompiler`，vendored）的薄封装：
 * 只**读取**程序集文件，**不加载、不执行**目标 DLL。
 *
 * 五个 action（统一接口，agent 自己组合）：
 *   · search      按名字找类型/成员（子串）
 *   · members     某类型全部成员（含 private）+ 基类
 *   · decompile   反编译类型/成员为 C#
 *   · il          反编译类型/方法为 IL
 *   · strings     扫程序集的字符串字面量
 *
 * 边界：只管"读代码"，不修改、不安装。路径取自运行时状态（`try_set_game_dir` 记录）。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "inspect_game_api",
    label: "Inspect Game API",
    description:
      "Read the game's managed DLLs when docs/api is not enough. Find types or members, see private members, decompile to C#, dump IL, or scan string literals. The actions are search, members, decompile, il, and strings. The tool builds its own vendored C# probe under tools/. The build output is gitignored. It never changes the game, the mod, or workspace sources.",
    promptSnippet: "Inspect game managed DLLs (members, C#, IL) when docs/api is not enough",
    promptGuidelines: [
      "Use inspect_game_api when docs/api does not answer the question. Use it for private members, the real implementation or behavior, or to confirm the current game version.",
      "inspect_game_api is read-only. It reads the DLL files, and it does not run the game code. Start with action=search to find the type. Then use members, decompile, or il on it.",
      "Prefer docs/api and inspect_game_data first. Use inspect_game_api when they are insufficient or possibly stale.",
      "inspect_game_api search and strings cap at 500 rows per call. Narrow the target, or page with offset. Increase the offset by 500 each time. Paging is the fallback, not the default.",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [
          Type.Literal("search"),
          Type.Literal("members"),
          Type.Literal("decompile"),
          Type.Literal("il"),
          Type.Literal("strings"),
        ],
        {
          description:
            "search: find types or members by name (caps at 500). members: list a type's members, including private. decompile: a type or member to C#. il: a type or method to IL. strings: string literals (caps at 500).",
        },
      ),
      target: Type.Optional(
        Type.String({
          description:
            "Type name (for example 'Duckov.BlackMarkets.BlackMarket' or the short form 'BlackMarket'), or a substring for action=search.",
        }),
      ),
      member: Type.Optional(
        Type.String({ description: "Member name (a method or a field), for action=decompile or action=il." }),
      ),
      dll: Type.Optional(
        Type.String({
          description:
            "Which managed DLL to read: a DLL name without the extension, a comma list, a wildcard (for example 'TeamSoda.*'), an absolute path, or '*' for every non-framework DLL under the Managed directory. Omit to use the repo's configured compile references. A type can live in several DLLs; if a lookup misses, search another DLL instead of assuming the type does not exist.",
        }),
      ),
      limit: Type.Optional(Type.Number({ description: "Max output lines (default 2000)." })),
      offset: Type.Optional(Type.Number({ description: "Skip the first N matches, to page a large search or strings result (default 0)." })),
    }),
    async execute(_toolCallId, params, _signal, _onUpdate, ctx) {
      const cwd = ctx.cwd;
      let cfg;
      try {
        cfg = readModRepoConfig(cwd);
      } catch {
        throw new Error(
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace; do not edit its maintainer configuration.",
        );
      }
      const state = readState(cwd);
      const gameDir = typeof state.gameDir === "string" && state.gameDir.trim() ? state.gameDir : null;
      const managed =
        typeof state.managedDir === "string" && state.managedDir.trim()
          ? state.managedDir
          : gameDir
            ? managedDirFor(gameDir, cfg, platformKey())
            : null;
      if (!managed || !existsSync(managed)) {
        return {
          content: [
            {
              type: "text",
              text: "FAIL: game managed DLLs not found (no valid gameDir/managedDir is recorded).\nNEXT: run try_set_game_dir (no arguments) to locate and record the game, then retry.",
            },
          ],
          details: { ok: false, reason: "GAME_DIRECTORY_NOT_FOUND" },
        };
      }
      const dotnet = (state.runtime?.dotnet as string | undefined) ?? "dotnet";
      const probeDir = join(cwd, "tools", "api-probe");
      const probeDll = join(probeDir, "bin", "Release", "net8.0", "api-probe.dll");
      let buildWarning = "";
      try {
        // 串行化：同一回合多个并行 tool call 会同时构建同一个工程。
        await serialize(probeDir, async () => {
          if (probeUpToDate(probeDir, probeDll)) return;
          try {
            await runAsync(dotnet, ["build", probeDir, "-c", "Release", "-v", "q", "-nologo"]);
          } catch (error) {
            const e = error as { stderr?: string; stdout?: string; message?: string };
            const detail = (e.stderr || e.stdout || e.message || String(error)).trim().slice(0, 400);
            if (!existsSync(probeDll)) throw new Error(detail);
            buildWarning = `WARN: could not rebuild the inspection probe. Using the existing build.\n${detail.slice(0, 200)}\n`;
          }
        });
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `FAIL: could not build the inspection probe (${probeDir}).\nNEXT: make sure the .NET SDK is installed (run check_runtime), then retry.\n${String(error).slice(0, 400)}`,
            },
          ],
          details: { ok: false, reason: "PROBE_BUILD_FAILED" },
        };
      }
      const args = [probeDll, "--managed", managed, "--action", params.action];
      if (params.target) args.push("--target", params.target);
      if (params.member) args.push("--member", params.member);
      // 探针不自带默认（它不得知道任何具体游戏的程序集名）⇒ 默认值来自本仓配置的 `compile.references`。
      // 排除 `Unity*.dll`：Unity 的程序集很大，默认扫它们只为少数 Unity 类型查询付出太高代价；
      // 真要看 Unity 类型时显式传 `--dll 'UnityEngine.CoreModule'` 这种。
      const cfgRefs = ((cfg as { compile?: { references?: string[] } }).compile?.references ?? [])
        .filter((r) => typeof r === "string" && r.length > 0)
        .filter((r) => !/^Unity\*?\.dll$/i.test(r))
        .map((r) => r.replace(/\.dll$/i, ""));
      const defaultDll = cfgRefs.length > 0 ? cfgRefs.join(",") : "*";
      args.push("--dll", params.dll ?? defaultDll);
      args.push("--limit", String(params.limit ?? 2000));
      if (params.offset !== undefined) args.push("--offset", String(params.offset));
      let out: string;
      try {
        out = (await runAsync(dotnet, args)).stdout;
      } catch (error) {
        const e = error as { stdout?: string; stderr?: string; message?: string };
        const detail = [e.stderr, e.stdout, e.message].filter((s) => s && String(s).trim()).join("\n").trim().slice(0, 800);
        return {
          content: [
            {
              type: "text",
              text: `FAIL: api-probe failed (${params.action}): ${detail}\nNEXT: if the message names a DLL, add --dll <name> (or --dll '*'); if it says the target is missing, use a shorter type-name substring.`,
            },
          ],
          details: { ok: false, reason: "PROBE_FAILED", action: params.action },
        };
      }
      const outText = out.trim();
      return {
        content: [
          {
            type: "text",
            text: (buildWarning + (outText || "# 0 row(s) (no output)")).trim(),
          },
        ],
        details: {
          ok: true,
          action: params.action,
          dll: params.dll ?? defaultDll,
          managedDir: managed,
        },
      };
    },
  });
}
