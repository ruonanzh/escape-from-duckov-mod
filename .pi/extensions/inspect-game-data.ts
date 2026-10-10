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
      "Read the Unity3D game's content data: the serialized objects in its data files or AssetBundles - prefabs, ScriptableObjects, MonoBehaviours, scene files (levelN), and a mod's AssetBundle - with their field values (item stats, quest conditions, enemy presets, a scene's GameObjects and transforms, and so on). Actions: classes / search / list / dump / refs / export (dump with follow resolves references and walks them across data files; refs and any PPtr line carry the fileId plus the resolved target class+name; export makes one table row per asset of a class). Read-only for the game, the mod and any scene - it never changes them: the only things it writes are its own probe build output under tools/ (gitignored) and, for a large export, the table file you name via out. To change behaviour or values you patch the game from the mod's C# (see inspect_game_api).",
    promptSnippet: "Read game content data (prefab/ScriptableObject field values)",
    promptGuidelines: [
      "Use inspect_game_data to read the current value of game content (item stats, prices, quest conditions, enemy presets, ...) from the game's data files.",
      "inspect_game_data is read-only; it never changes the game or any scene. Changing a value or behaviour is done by the mod's C# at runtime (see inspect_game_api to find the field/method to change).",
      "inspect_game_data reads serialized Unity objects (prefab / ScriptableObject / MonoBehaviour, scene files, and AssetBundles - e.g. a mod's bundle). Start with action=classes or action=search, then action=dump (add follow=true to resolve references such as an item's stats, and keep going across data files - e.g. a prefab's renderer -> material -> shader). A reference the game stores cross-file is marked [外部 fileId=N] and the line also shows the resolved target class+name.",
      "With inspect_game_data, follow stops at array elements such as a renderer's m_Materials (the array shows as an opaque AssetsTools.NET.AssetTypeArrayInfo) - to reach a material that way, search action=search class=Material with a name pattern instead, then action=refs on it to read its m_Shader.",
      "With inspect_game_data, prefer narrow queries over wide enumeration: do not list a whole large class (list/search cap at 500 per call - especially built-in types like GameObject/Transform in a scene, where most entries have no name). Use action=search with a pattern, or action=dump with a concrete class + name/typeid/pathid, to reach a target directly. To page through a large class that has no names, pass offset (500, 1000, ...) - use this only when you truly must enumerate, not as the default.",
      "With inspect_game_data, a scene is a file named levelN in the game's data dir: pass file=levelN to inspect one. An AssetBundle (e.g. a workshop mod's bundle) is read the same way: pass data=<the dir containing it> and file=<the bundle file name> - useful to see what a mod ships (meshes, materials, textures). Read a scene only to learn what exists at runtime (which objects and scripts it contains, their transforms) so the mod's C# can find or patch them - you do not edit scenes.",
      "With inspect_game_data, pathid= is an *asset* pathID (the pathID column that list/search print) while typeid= is the game's own typeID - different numbers with the same shape. Passing the wrong one silently reads a different asset, so use class= together with typeid= for data lookups.",
      "With inspect_game_data, raise depth= (e.g. 5) when you need to walk into arrays such as m_Component or renderers - the default 3 stops at an opaque AssetsTools.NET.AssetTypeArrayInfo.",
      "To read a whole class of values at once with inspect_game_data (e.g. every weapon's stats, every quest's requirement), use action=export instead of many dump calls: match filters rows (value>=100, displayName~UAK, m_GameObject.m_Component[].component.#class=ItemSetting_Gun) and field picks columns (paths may traverse references and arrays, e.g. stats.list[].key / stats.list[].baseValue). For a LARGE export pass out=<file> - the table goes to the file and you only get a preview, keeping the context small; then read/transform the file with bash.",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [
          Type.Literal("classes"),
          Type.Literal("search"),
          Type.Literal("list"),
          Type.Literal("dump"),
          Type.Literal("refs"),
          Type.Literal("export"),
          Type.Literal("transform"),
        ],
        {
          description:
            "classes: list asset class names + counts. search: find assets by name (caps at 500). list: assets of one class (caps at 500). dump: an asset's fields/values. refs: what an object references. export: one table row per asset of a class, with match filters + field paths - use it to pull a whole class's values in ONE call. transform: a Transform's local TRS + parent chain + accumulated world scale, plus its child tree and - when a MeshFilter is attached - the mesh AABB times that world scale, i.e. the model's REAL in-game size (Unity Transforms are all local, so this is the only way to get true sizes/positions; the prefabs live in the levelN scene files).",
        },
      ),
      class: Type.Optional(Type.String({ description: "Asset class name, e.g. Item / Quest / CharacterRandomPreset." })),
      name: Type.Optional(Type.String({ description: "Asset name (or a substring), for dump/refs/transform." })),
      mesh: Type.Optional(
        Type.String({
          description:
            "For transform: locate a Transform by the MESH it renders (reverse lookup through MeshFilter). Use this to get a model's real in-game size in one call, e.g. mesh=\"Rifle02\" with file=level4.",
        }),
      ),
      exact: Type.Optional(
        Type.Boolean({ description: "For transform/refs: require an exact name/mesh match instead of a substring (e.g. Rifle02 vs Rifle02_Sight)." }),
      ),
      has: Type.Optional(
        Type.String({ description: "For transform: keep only objects whose GameObject carries this component, e.g. has=MeshFilter." }),
      ),
      by: Type.Optional(
        Type.String({
          description:
            "For refs: REVERSE lookup - list the objects of this class that reference the target, e.g. by=MeshFilter. Plain refs answers 'what does it reference'; by= answers 'who references it'.",
        }),
      ),
      typeid: Type.Optional(Type.Number({ description: "Asset typeID, for dump/refs (e.g. an item's typeID)." })),
      pathid: Type.Optional(Type.Number({ description: "Asset pathID, for dump/refs." })),
      pattern: Type.Optional(Type.String({ description: "Search pattern (name/class/typeID substring), for search." })),
      file: Type.Optional(Type.String({ description: "Limit to one data file: resources.assets, levelN (a scene), or an AssetBundle (e.g. a mod's bundle in its own dir). Comma-separate several to load them together - required for cross-file reverse lookups (e.g. 'sharedassets4.assets,level4')." })),
      depth: Type.Optional(Type.Number({ description: "dump depth (default 3)." })),
      follow: Type.Optional(Type.Boolean({ description: "For dump: also resolve referenced objects (e.g. item stats), walking them across data files (prefab -> renderer -> material -> shader)." })),
      limit: Type.Optional(Type.Number({ description: "Max output lines (default 2000)." })),
      offset: Type.Optional(Type.Number({ description: "Skip the first N matches, for paging a large list/search (default 0)." })),
      match: Type.Optional(
        Type.Array(Type.String(), {
          description:
            'For export: row filters, e.g. "value>=100", "displayName~UAK", or "m_GameObject.m_Component[].component.#class=ItemSetting_Gun". Repeatable (AND).',
        }),
      ),
      field: Type.Optional(
        Type.Array(Type.String(), {
          description:
            'For export: column paths, e.g. ["displayName","value","stats.list[].key","stats.list[].baseValue"]. A path may traverse PPtrs and arrays (a[] / a[i]) and use #class / #name.',
        }),
      ),
      rows: Type.Optional(Type.Number({ description: "For export: max rows (default 500; unlimited when out is set)." })),
      out: Type.Optional(
        Type.String({
          description:
            "Write the full result to this file (e.g. /tmp/weapons.tsv) and return only a preview - use this for large result sets so the data does not flood the context; then process the file with bash/tools. Works for every action (streamed, so memory stays flat).",
        }),
      ),
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
      if (params.mesh) args.push("--mesh", params.mesh);
      if (params.exact) args.push("--exact");
      if (params.has) args.push("--has", params.has);
      if (params.by) args.push("--by", params.by);
      if (params.typeid !== undefined) args.push("--typeid", String(params.typeid));
      if (params.pathid !== undefined) args.push("--pathid", String(params.pathid));
      if (params.pattern) args.push("--pattern", params.pattern);
      if (params.file) args.push("--file", params.file);
      if (params.depth !== undefined) args.push("--depth", String(params.depth));
      if (params.follow) args.push("--follow");
      args.push("--limit", String(params.limit ?? 2000));
      if (params.offset !== undefined) args.push("--offset", String(params.offset));
      if (params.match) for (const m of params.match) args.push("--match", m);
      if (params.field) for (const f of params.field) args.push("--field", f);
      if (params.rows !== undefined) args.push("--rows", String(params.rows));
      if (params.out) args.push("--out", params.out);
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
