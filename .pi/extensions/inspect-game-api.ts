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
      "Read the game's managed DLLs when docs/api is not enough: find types/members, see private members, decompile to C#, dump IL, or scan string literals. Actions: search / members / decompile / il / strings. To read them it builds its own vendored C# probe under tools/ (build output is gitignored); it never changes the game, the mod, or workspace sources.",
    promptSnippet: "Inspect game managed DLLs (members/C#/IL) when docs/api is not enough",
    promptGuidelines: [
      "Use inspect_game_api when docs/api does not answer it: private members, the real implementation/behavior, or to confirm the current game version.",
      "It is read-only (it reads the DLL files, it does not run the game code). start with action=search to find the type, then members/decompile/il on it.",
      "Prefer docs/api and docs/data first; reach for inspect_game_api when they are insufficient or possibly stale.",
      "search and strings cap at 500 rows per call. Narrow the target, or page with offset (500, 1000, ...) - paging is the fallback, not the default.",
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
            "search: find types/members by name (caps at 500). members: list a type's members (incl. private). decompile: type/member to C#. il: type/method to IL. strings: string literals (caps at 500).",
        },
      ),
      target: Type.Optional(
        Type.String({
          description:
            "Type name (e.g. 'Duckov.BlackMarkets.BlackMarket' or short 'BlackMarket'), or a substring for action=search.",
        }),
      ),
      member: Type.Optional(
        Type.String({ description: "Member (method/field) name, for action=decompile or action=il." }),
      ),
      dll: Type.Optional(
        Type.String({
          description:
            "Managed DLL name to read (default TeamSoda.Duckov.Core). Comma-separate several, or use '*' for every DLL in the Managed folder.",
        }),
      ),
      limit: Type.Optional(Type.Number({ description: "Max output lines (default 2000)." })),
      offset: Type.Optional(Type.Number({ description: "Skip the first N matches, for paging a large search/strings (default 0)." })),
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
            buildWarning = `WARN: could not rebuild the inspection probe; using the existing build.\n${detail.slice(0, 200)}\n`;
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
      if (params.dll) args.push("--dll", params.dll);
      args.push("--limit", String(params.limit ?? 2000));
      if (params.offset !== undefined) args.push("--offset", String(params.offset));
      let out: string;
      try {
        out = (await runAsync(dotnet, args)).stdout;
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        return {
          content: [{ type: "text", text: `FAIL: api-probe failed: ${message.slice(0, 600)}` }],
          details: { ok: false, reason: "PROBE_FAILED", action: params.action },
        };
      }
      return {
        content: [{ type: "text", text: (buildWarning + (out.trim() || "(no output)")).trim() }],
        details: {
          ok: true,
          action: params.action,
          dll: params.dll ?? "TeamSoda.Duckov.Core",
          managedDir: managed,
        },
      };
    },
  });
}
