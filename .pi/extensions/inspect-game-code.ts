import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { serialize } from "../lib/proc";
import {
  asciiSafe,
  buildDump,
  checkFreshness,
  dumpDir,
  extractMethod,
  findTypeFile,
  listMembers,
  folderBytes,
  loadConfig,
  readManifest,
  requireManagedDir,
  searchTree,
  truncate,
  treeDir,
} from "../lib/decompile";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

const MB = (bytes: number): string => `${Math.round(bytes / 1048576)} MB`;

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "inspect_game_code",
    label: "Inspect Game Code",
    description:
      "Read the game's code from the local decompiled dump: one file per top-level type, so a whole-tree question is one search. Actions: status, search, type, body, dump. The dump is rebuilt automatically when the game's DLLs or the config changed. Use inspect_game_api to ask about one type without a dump, or to read IL and string literals.",
    promptSnippet: "Read game code from the local dump (whole-tree search, one type, one member)",
    promptGuidelines: [
      "Use inspect_game_code to search the dumped tree, read one type, or read one member. Use inspect_game_api for IL, string literals, or a type the dump does not cover.",
      "inspect_game_code search lists files and lines; type returns a whole file; body returns one member with its braces balanced. Prefer search when you do not know the file, and type when you do.",
      "inspect_game_code rebuilds the dump by itself when the game's DLLs or decompile.json changed, and says in the result when it ran and how long it took. Do not rebuild the dump some other way.",
      "inspect_game_code dump writes the assemblies in decompile.json. Pass assemblies= to add one for this dump; a wider set costs disk and time.",
    ],
    parameters: Type.Object({
      action: Type.Union(
        [Type.Literal("status"), Type.Literal("search"), Type.Literal("type"), Type.Literal("body"), Type.Literal("dump")],
        {
          description:
            "status: is the dump current, and what does it cover. search: find a name in file paths and lines. type: one type's file. body: one member of a type. dump: rebuild the dump, adding assemblies.",
        },
      ),
      target: Type.Optional(Type.String({ description: "A type name (short or namespace-qualified), or a name substring for search." })),
      member: Type.Optional(Type.String({ description: "A member name substring, for action=body." })),
      assemblies: Type.Optional(Type.String({ description: "Comma-separated assembly names to add to this dump (DLL names without .dll)." })),
      force: Type.Optional(Type.Boolean({ description: "Rebuild even when the dump looks current (action=dump)." })),
    }),
    async execute(_toolCallId, params, _signal, onUpdate, ctx) {
      const cwd = ctx.cwd;
      let cfg;
      try {
        cfg = loadConfig(cwd);
      } catch (error) {
        return {
          content: [{ type: "text", text: `FAIL: ${(error as Error).message}` }],
          details: { ok: false, reason: "CONFIG" },
        };
      }

      // Where the DLLs are. Its absence is a business result, not an exception:
      // another tool (try_set_game_dir) can put it right.
      let managedDir: string | null = null;
      let managedProblem: string | null = null;
      try {
        managedDir = requireManagedDir(cwd).managedDir;
      } catch (error) {
        managedProblem = (error as Error).message;
      }

      const dir = dumpDir(cwd);
      const freshness = checkFreshness(cwd, cfg, managedDir);

      const rebuild = async (extra: string[]) => {
        const started = Date.now();
        let stage = "Rebuilding the dump";
        const report = (text: string) => {
          stage = text;
          onUpdate?.({ content: [{ type: "text", text: asciiSafe(text) }], details: {} });
        };
        const beat = setInterval(() => report(`${stage} (${Math.round((Date.now() - started) / 1000)}s)`), 10_000);
        try {
          return await serialize(`dump:${dir}`, () => buildDump(cwd, cfg, extra, report));
        } finally {
          clearInterval(beat);
        }
      };

      const wrap = (text: string, details: Record<string, unknown> = {}) => ({
        content: [{ type: "text", text: asciiSafe(text) }],
        details: { ok: true, ...details },
      });
      const failed = (text: string, details: Record<string, unknown> = {}) => ({
        content: [{ type: "text", text: asciiSafe(text) }],
        details: { ok: false, ...details },
      });

      const refreshNote = async (): Promise<string> => {
        if (freshness.state === "fresh") return "";
        if (!managedDir) return `# the dump is not current (${freshness.reasons.join("; ")}), and it cannot be rebuilt: ${managedProblem}\n`;
        const built = await rebuild([]);
        return `# refreshed the dump in ${built.seconds.toFixed(1)}s (${freshness.reasons.join("; ")})\n`;
      };

      try {
        if (params.action === "status") {
          if (managedProblem && freshness.state !== "missing") {
            // The dump exists but the DLLs cannot be seen: say so, do not fail the dump.
            return failed(`PARTIAL: ${managedProblem}\n  dump: ${dir} (${MB(folderBytes(dir))})`, { dir });
          }
          const manifest = readManifest(dir);
          if (freshness.state === "missing") {
            const paths = managedDir
              ? [`  managed: ${managedDir}`, `  dumper: tools/api-probe (built on demand)`]
              : ["  managed: unknown - run try_set_game_dir"];
            return failed(
              [`FAIL: no dump for this repository. Expected ${dir}.`, ...paths, "NEXT: call action=dump."].join("\n"),
              { reason: "MISSING", dir },
            );
          }
          const coverage = (manifest?.assemblies ?? []).map((a) => `${a.name} (${a.files} files)`).join(", ");
          const head =
            freshness.state === "fresh"
              ? `PASS: the dump is current. ${dir} (${MB(folderBytes(dir))}, ${manifest?.contents?.typeFiles ?? "?"} type files)`
              : `PARTIAL: the dump is stale.\n  - ${freshness.reasons.join("\n  - ")}`;
          const resolved =
            freshness.state === "fresh"
              ? []
              : [
                  `  managed: ${managedDir ?? "unknown"}`,
                  `  dumper: tools/api-probe/bin/Release/net8.0/api-probe.dll`,
                ];
          return freshness.state === "fresh"
            ? wrap([head, `  build: ${manifest?.build?.label ?? "unknown"}`, `  coverage: ${coverage}`].join("\n"), {
                dir,
                freshness: "fresh",
              })
            : failed(
                [
                  head,
                  `  build: ${manifest?.build?.label ?? "unknown"}`,
                  `  coverage: ${coverage}`,
                  ...resolved,
                  "NEXT: action=search/type/body rebuild it by themselves; action=dump rebuilds now.",
                ].join("\n"),
                { dir, freshness: "stale" },
              );
        }

        if (params.action === "dump") {
          const extra = (params.assemblies ?? "")
            .split(",")
            .map((s) => s.trim())
            .filter(Boolean);
          if (freshness.state === "fresh" && extra.length === 0 && !params.force) {
            return wrap(`SKIP: the dump is already current (${dir}). Pass force=true to rebuild it anyway.`);
          }
          const built = await rebuild(extra);
          const added = built.missing.length ? `\n  not found under the game's DLLs: ${built.missing.join(", ")}` : "";
          return wrap(
            [
              `PASS: rebuilt the dump in ${built.seconds.toFixed(1)}s.`,
              `  ${dir} (${MB(folderBytes(dir))}, ${built.manifest.contents?.typeFiles ?? "?"} type files)`,
              `  coverage: ${(built.manifest.assemblies ?? []).map((a) => a.name).join(", ")}${added}`,
            ].join("\n"),
            { dir, seconds: built.seconds },
          );
        }

        if (!params.target) {
          return failed(`FAIL: action=${params.action} needs target=<name>.`, { reason: "TARGET_MISSING" });
        }

        const note = await refreshNote();
        const tree = treeDir(cwd);

        if (params.action === "search") {
          const hits = searchTree(tree, params.target, 50);
          const body = hits.length
            ? hits.map((h) => (h.line ? `${h.file}:${h.line}  ${h.text}` : `${h.file}`)).join("\n")
            : "0 matches. Try a shorter substring.";
          return wrap(`${note}# search "${params.target}": ${hits.length} match(es) in ${dir}\n${truncate(body)}`, {
            matches: hits.length,
          });
        }

        const rel = findTypeFile(tree, params.target);
        if (!rel) {
          return failed(
            `FAIL: no type file named "${params.target}" in this dump.\nNEXT: action=search target=<substring> to find the file name.`,
            { reason: "TYPE_NOT_FOUND" },
          );
        }
        const source = readFileSync(join(tree, rel), "utf8");

        if (params.action === "type") {
          return wrap(`${note}# ${rel}\n\n${truncate(source)}`, { file: rel });
        }

        const blocks = params.member ? extractMethod(source, params.member, 20) : [];
        if (blocks.length === 0) {
          const names = listMembers(source);
          return failed(
            `FAIL: no member matches "${params.member ?? ""}" in ${rel}.\n# members in this file:\n${truncate(names.join("\n"), 60)}`,
            { reason: "METHOD_NOT_FOUND" },
          );
        }
        return wrap(
          `${note}# ${rel}${params.member ? ` :: ${params.member}` : ""}: ${blocks.length} block(s)\n\n${truncate(
            blocks.map((b) => b.text).join("\n\n"),
          )}`,
          { methods: blocks.length },
        );
      } catch (error) {
        const message = (error as Error).message;
        return {
          content: [{ type: "text", text: `FAIL: ${message}` }],
          details: { ok: false, reason: "DUMP", dir },
        };
      }
    },
  });
}
