import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { runAsync, serialize } from "../lib/proc";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { managedDirFor, platformKey, readModRepoConfig, readState } from "../lib/game-paths";
import { probeUpToDate } from "../lib/probe";

/**
 * inspect_game_data —— **只读**读取 Unity3D 游戏的内容数据（`tools/data-probe` 的薄封装）。
 *
 * 读的是游戏数据文件里**序列化对象**（prefab / ScriptableObject / MonoBehaviour）的字段与值：
 * 物品数值、任务条件、敌人预设等 —— 用来回答"这个值现在是多少"。
 *
 * 与 inspect_game_api 的分工：
 *   · inspect_game_api  读**代码**（DLL：类型/字段/方法/IL）→ 找"改哪个字段 / patch 哪个方法"
 *   · inspect_game_data 读**内容数据**（资产字段+值）     → 读"现在是多少"
 *
 * 只读、离线；不修改任何文件（改数值由 mod 运行时 C# 完成）。
 */
export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "inspect_game_data",
    label: "Inspect Game Data",
    description:
      "Read the Unity3D game's content data (read-only): the serialized objects in its data files - prefabs, ScriptableObjects, MonoBehaviours, and scene files (levelN) - with their field values: item stats, quest conditions, enemy presets, a scene's GameObjects and transforms, and so on. Actions: classes / search / list / dump / refs (dump with follow resolves references). It only reads: it never changes the game, the mod, or any scene - to change behaviour or values you patch the game from the mod's C# (see inspect_game_api). It builds its own vendored C# probe under tools/ (build output is gitignored).",
    promptSnippet: "Read game content data (prefab/ScriptableObject field values)",
    promptGuidelines: [
      "Use inspect_game_data to read the current value of game content (item stats, prices, quest conditions, enemy presets, ...) from the game's data files.",
      "inspect_game_data is read-only; it never changes the game or any scene. Changing a value or behaviour is done by the mod's C# at runtime (see inspect_game_api to find the field/method to change).",
      "It reads serialized Unity objects (prefab / ScriptableObject / MonoBehaviour, and scene files). Start with action=classes or action=search, then action=dump (add follow=true to resolve references such as an item's stats).",
      "Prefer narrow queries over wide enumeration: do not list a whole large class (list/search cap at 500 per call - especially built-in types like GameObject/Transform in a scene, where most entries have no name). Use action=search with a pattern, or action=dump with a concrete class + name/typeid/pathid, to reach a target directly. To page through a large class that has no names, pass offset (500, 1000, ...) - use this only when you truly must enumerate, not as the default.",
      "A scene is a file named levelN in the game's data dir: pass file=levelN to inspect one. Read a scene only to learn what exists at runtime (which objects and scripts it contains, their transforms) so the mod's C# can find or patch them - you do not edit scenes.",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [
          Type.Literal("classes"),
          Type.Literal("search"),
          Type.Literal("list"),
          Type.Literal("dump"),
          Type.Literal("refs"),
        ],
        {
          description:
            "classes: list asset class names + counts. search: find assets by name (caps at 500). list: assets of one class (caps at 500). dump: an asset's fields/values. refs: what an object references.",
        },
      ),
      class: Type.Optional(Type.String({ description: "Asset class name, e.g. Item / Quest / CharacterRandomPreset." })),
      name: Type.Optional(Type.String({ description: "Asset name (or a substring), for dump/refs." })),
      typeid: Type.Optional(Type.Number({ description: "Asset typeID, for dump/refs (e.g. an item's typeID)." })),
      pathid: Type.Optional(Type.Number({ description: "Asset pathID, for dump/refs." })),
      pattern: Type.Optional(Type.String({ description: "Search pattern (name/class/typeID substring), for search." })),
      file: Type.Optional(Type.String({ description: "Limit to one data file, e.g. resources.assets - or levelN to inspect a scene." })),
      depth: Type.Optional(Type.Number({ description: "dump depth (default 3)." })),
      follow: Type.Optional(Type.Boolean({ description: "For dump: also resolve referenced objects (e.g. item stats)." })),
      limit: Type.Optional(Type.Number({ description: "Max output lines (default 2000)." })),
      offset: Type.Optional(Type.Number({ description: "Skip the first N matches, for paging a large list/search (default 0)." })),
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
              text: "FAIL: game data not found (no valid gameDir/managedDir is recorded).\nNEXT: run try_set_game_dir (no arguments) to locate and record the game, then retry.",
            },
          ],
          details: { ok: false, reason: "GAME_DIRECTORY_NOT_FOUND" },
        };
      }
      const data = dirname(managed);
      const dotnet = (state.runtime?.dotnet as string | undefined) ?? "dotnet";
      const probeDir = join(cwd, "tools", "data-probe");
      const probeDll = join(probeDir, "bin", "Release", "net8.0", "data-probe.dll");
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
      const args = [probeDll, "--managed", managed, "--data", data, "--action", params.action];
      if (params.class) args.push("--class", params.class);
      if (params.name) args.push("--name", params.name);
      if (params.typeid !== undefined) args.push("--typeid", String(params.typeid));
      if (params.pathid !== undefined) args.push("--pathid", String(params.pathid));
      if (params.pattern) args.push("--pattern", params.pattern);
      if (params.file) args.push("--file", params.file);
      if (params.depth !== undefined) args.push("--depth", String(params.depth));
      if (params.follow) args.push("--follow");
      args.push("--limit", String(params.limit ?? 2000));
      if (params.offset !== undefined) args.push("--offset", String(params.offset));
      let out: string;
      try {
        out = (await runAsync(dotnet, args)).stdout;
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        return {
          content: [{ type: "text", text: `FAIL: data-probe failed: ${message.slice(0, 600)}` }],
          details: { ok: false, reason: "PROBE_FAILED", action: params.action },
        };
      }
      return {
        content: [{ type: "text", text: (buildWarning + (out.trim() || "(no output)")).trim() }],
        details: { ok: true, action: params.action, dataDir: data },
      };
    },
  });
}
