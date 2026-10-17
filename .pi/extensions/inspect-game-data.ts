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
      "Read the Unity3D game's content data. This means the serialized objects in its data files or AssetBundles: prefabs, ScriptableObjects, MonoBehaviours, scene files (levelN), and a mod's AssetBundle. Each object has field values, such as item stats, quest conditions, enemy presets, and a scene's GameObjects and transforms. The actions are classes, search, list, dump, refs, and export. dump with follow resolves references and walks them across data files. refs and any PPtr line carry the fileId and the resolved target class and name. export makes one table row per asset of a class. The tool is read-only for the game, the mod, and any scene. It never changes them. It writes only its own probe build output under tools/ (gitignored), and for a large export the table file you name via out. To change behaviour or values, patch the game from the mod's C# (see inspect_game_api).",
    promptSnippet: "Read game content data (prefab and ScriptableObject field values)",
    promptGuidelines: [
      "Use inspect_game_data to read the current value of game content from the game's data files. Examples are item stats, prices, quest conditions, and enemy presets.",
      "inspect_game_data is read-only. It never changes the game or any scene. The mod's C# changes a value or behaviour at runtime. Use inspect_game_api to find the field or method to change.",
      "inspect_game_data reads serialized Unity objects: prefabs, ScriptableObjects, MonoBehaviours, scene files, and AssetBundles (for example a mod's bundle). Start with action=classes or action=search. Then use action=dump, and add follow=true to resolve references such as an item's stats. follow continues across data files, for example a prefab's renderer, then material, then shader. A reference the game stores across files is marked [external fileId=N]. The line also shows the resolved target class and name.",
      "With inspect_game_data, follow stops at array elements such as a renderer's m_Materials. The array stops there: it prints [] when empty, or <N item(s), not expanded>. To reach a material, use action=search with class=Material and a name pattern. Then use action=refs on it to read its m_Shader.",
      "With inspect_game_data, prefer narrow queries over wide enumeration. Do not list a whole large class. list and search cap at 500 per call. This matters for built-in types such as GameObject and Transform in a scene, where most entries have no name. Use action=search with a pattern, or action=dump with a concrete class and name, typeid, or pathid, to reach a target directly. To page through a large class without names, pass offset. Increase it by 500 each time. Use this only when you truly must enumerate, not as the default.",
      "With inspect_game_data, a scene is a file named levelN in the game's data directory. Pass file=levelN to inspect one. Read an AssetBundle the same way: pass data=<the directory containing it> and file=<the bundle file name>. This is useful to see what a mod ships, such as meshes, materials, and textures. Read a scene only to learn what exists at runtime: which objects and scripts it contains, and their transforms. Then the mod's C# can find or patch them. You do not edit scenes.",
      "With inspect_game_data, pathid= is an *asset* pathID. This is the pathID column that list and search print. typeid= is the game's own typeID. They are different numbers with the same shape. The wrong one silently reads a different asset. For data lookups, use class= together with typeid=.",
      "With inspect_game_data, raise depth= (for example 5) to walk into an array such as m_Component or renderers. An array can also stay unexpanded: it prints [] for empty, or <N item(s), not expanded>. That happens when the type database lacks the element type, and a higher depth= does not expand it. To reach an element, use action=search or action=refs on the element's pathID.",
      "Use inspect_game_data with action=export to read a whole class of values at once, such as every weapon's stats or every quest's requirement. Do not use many dump calls. match filters rows, for example value>=100, displayName~UAK, or m_GameObject.m_Component[].component.#class=ItemSetting_Gun. field picks columns. A path may traverse references and arrays, for example stats.list[].key or stats.list[].baseValue. For a large export, pass out=<file>. The table goes to the file, and you receive only a preview. This keeps the context small. Then read or transform the file with bash.",
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
            "classes: list asset class names and counts. search: find assets by name (caps at 500). list: assets of one class (caps at 500). dump: an asset's fields and values. refs: what an object references. export: one table row per asset of a class, with match filters and field paths. Use it to pull a whole class's values in one call. transform: a Transform's local TRS, parent chain, and accumulated world scale, plus its child tree. When a MeshFilter is attached, it also returns the mesh AABB times that world scale. That is the model's real in-game size. Unity Transforms are all local, so this is the only way to get true sizes and positions. The prefabs live in the levelN scene files.",
        },
      ),
      class: Type.Optional(Type.String({ description: "Asset class name, for example Item, Quest, or CharacterRandomPreset." })),
      name: Type.Optional(Type.String({ description: "Asset name (or a substring), for dump, refs, or transform." })),
      mesh: Type.Optional(
        Type.String({
          description:
            "For transform: locate a Transform by the MESH it renders (reverse lookup through MeshFilter). Use this to get a model's real in-game size in one call, for example mesh=\"Rifle02\" with file=level4.",
        }),
      ),
      exact: Type.Optional(
        Type.Boolean({ description: "For transform or refs: require an exact name or mesh match instead of a substring (for example Rifle02 compared with Rifle02_Sight)." }),
      ),
      has: Type.Optional(
        Type.String({ description: "For transform: keep only objects whose GameObject carries this component, for example has=MeshFilter." }),
      ),
      by: Type.Optional(
        Type.String({
          description:
            "For refs: REVERSE lookup. List the objects of this class that reference the target, for example by=MeshFilter. Plain refs answers 'what does it reference'. by= answers 'who references it'.",
        }),
      ),
      typeid: Type.Optional(Type.Number({ description: "Asset typeID, for dump or refs (for example an item's typeID)." })),
      pathid: Type.Optional(Type.Number({ description: "Asset pathID, for dump or refs." })),
      pattern: Type.Optional(Type.String({ description: "Search pattern (a name, class, or typeID substring), for search." })),
      file: Type.Optional(Type.String({ description: "Limit to one data file: resources.assets, levelN (a scene), or an AssetBundle (for example a mod's bundle in its own directory). Use commas to load several together. This is required for cross-file reverse lookups (for example 'sharedassets4.assets,level4')." })),
      depth: Type.Optional(Type.Number({ description: "dump depth (default 3)." })),
      follow: Type.Optional(Type.Boolean({ description: "For dump: also resolve referenced objects (for example item stats), and walk them across data files (prefab, then renderer, then material, then shader)." })),
      limit: Type.Optional(Type.Number({ description: "Max output lines (default 2000)." })),
      offset: Type.Optional(Type.Number({ description: "Skip the first N matches, to page a large list or search result (default 0)." })),
      match: Type.Optional(
        Type.Array(Type.String(), {
          description:
            'For export: row filters, for example "value>=100", "displayName~UAK", or "m_GameObject.m_Component[].component.#class=ItemSetting_Gun". Repeatable (AND).',
        }),
      ),
      field: Type.Optional(
        Type.Array(Type.String(), {
          description:
            'For export: column paths, for example ["displayName","value","stats.list[].key","stats.list[].baseValue"]. A path may traverse PPtrs and arrays (a[] or a[i]) and use #class or #name.',
        }),
      ),
      rows: Type.Optional(Type.Number({ description: "For export: max rows (default 500, unlimited when out is set)." })),
      out: Type.Optional(
        Type.String({
          description:
            "Write the full result to this file (for example /tmp/weapons.tsv) and return only a preview. Use this for large result sets, so the data does not flood the context. Then process the file with bash or tools. It works for every action, and streams, so memory stays flat.",
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
          "INVALID_WORKSPACE_CONFIG: Cannot read mod-repo.json. Reopen or update the game workspace. Do not edit its maintainer configuration.",
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
        const e = error as { stdout?: string; stderr?: string; message?: string };
        // `Command failed: dotnet …` 是 execFile 的错误尾巴，对 agent 没信息量（还会让 /dotnet/ 误命中）
        const raw = [e.stderr, e.stdout, e.message].filter((s) => s && String(s).trim()).join("\n").trim();
        const detail = raw.split("\n").filter((l) => !/^Command failed:/.test(l.trim())).join("\n").trim().slice(0, 600);
        // NEXT 按真实原因分：探针启动不了（dotnet 不在 PATH）不是 field 写错，别把人带偏。
        // 实测：不把 ~/.dotnet 放进 PATH 时，dotnet 没记录 runtime.dotnet 就会 `spawn dotnet ENOENT`。
        const next = /matched nothing|field path/i.test(detail)
          ? "fix the --field path (syntax: a.b, a[], a[0], #name, #class), then retry"
          // 只认「起不来」的具体证据：旧写法里的 /dotnet|SDK/ 会命中命令行本身（含 dotnet 路径），
          // 于是 `# ERROR: unknown class 'X'` 也被说成运行时问题。
          : /\bENOENT\b|is not recognized as an internal or external command|command not found/i.test(detail)
            ? "the .NET runtime is not usable here: run check_runtime (or install_runtime), then retry"
            : "read the # ERROR line above: it names the flag or class to fix (for a scene, pass file=levelN), then retry";
        return {
          content: [
            {
              type: "text",
              text: `FAIL: data-probe failed (${params.action}): ${detail}\nNEXT: ${next}.`,
            },
          ],
          details: { ok: false, reason: "PROBE_FAILED", action: params.action },
        };
      }
      return {
        content: [{ type: "text", text: (buildWarning + (out.trim() || "# 0 row(s) (no output)")).trim() }],
        details: { ok: true, action: params.action, dataDir: data },
      };
    },
  });
}
