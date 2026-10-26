import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { probeDotnet } from "../lib/dotnet";
import { serialize } from "../lib/proc";
import {
  bodyHeader,
  buildDump,
  checkFreshness,
  dumpDir,
  extractMethod,
  findTypeFile,
  memberListHeader,
  listMembers,
  folderBytes,
  loadConfig,
  readManifest,
  requireManagedDir,
  searchHeader,
  searchTree,
  takeCapped,
  truncate,
  treeDir,
  versionWarnings,
  type Manifest,
} from "../lib/decompile";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

const MB = (bytes: number): string =>
  bytes >= 1048576 ? `${Math.round(bytes / 1048576)} MB` : `${Math.round(bytes / 1024)} KB`;

const coverageText = (manifest: Manifest | null): string => {
  const coverage = (manifest?.assemblies ?? [])
    .map((a) => `${a.name} (${a.files} files, ${a.failed} failed)`)
    .join(", ");
  return `  coverage: ${coverage || "none"}`;
};

export default function (pi: ExtensionAPI) {
  pi.registerTool({
    name: "inspect_game_code",
    label: "Inspect Game Code",
    description:
      "Read the game's code from the local decompiled dump: one file per top-level type, so a whole-tree question is one search. Actions: status, search, type, body, dump. The dump is rebuilt automatically when the game's DLLs or the config changed. Use inspect_game_api to ask about one type without a dump, or to read IL and string literals.",
    promptSnippet: "Read game code from the local dump (whole-tree search, one type, one member)",
    promptGuidelines: [
      "Use inspect_game_code to search the dumped tree, read one type, or read one member. Use inspect_game_api for IL, string literals, or a type the dump does not cover.",
      "inspect_game_code search lists files and lines. type returns a whole file. body returns one member with its braces balanced. Prefer search when you do not know the file, and type when you do.",
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
      let gameDir: string | null = null;
      try {
        const required = requireManagedDir(cwd);
        managedDir = required.managedDir;
        gameDir = required.gameDir;
      } catch {
        managedDir = null;
      }

      const dir = dumpDir(cwd);
      const freshness = checkFreshness(cwd, cfg, managedDir);

      // The order a rebuild fails in: runtime, then the dumper, then the game, then the dump.
      // Only actions that need the dump consult it: a current dump is read without these probes.
      interface Rung {
        name: string;
        state: "ok" | "missing" | "unknown";
        detail: string;
        message?: string;
        next?: string;
      }
      let ladder: Rung[] | null = null;
      const rungs = (): Rung[] => {
        if (ladder) return ladder;
        const items: Rung[] = [];
        const add = (rung: Rung): void => {
          const blocked = items.some((r) => r.state === "missing");
          items.push(blocked ? { name: rung.name, state: "unknown", detail: "not checked until the step above is ready" } : rung);
        };
        const dotnet = probeDotnet();
        if (!dotnet.found || (dotnet.major ?? 0) < 8) {
          add(
            dotnet.found
              ? {
                  name: "dotnet",
                  state: "missing",
                  detail: `dotnet ${dotnet.version} (need >= 8)`,
                  message: `DOTNET_TOO_OLD: dotnet ${dotnet.version} cannot build the dumper (need >= 8).`,
                  next: "NEXT: run install_runtime, then check_runtime.",
                }
              : {
                  name: "dotnet",
                  state: "missing",
                  detail: "not found",
                  message: "DOTNET_MISSING: the dumper needs the .NET SDK.",
                  next: "NEXT: run check_runtime, then install_runtime.",
                },
          );
        } else {
          add({ name: "dotnet", state: "ok", detail: `dotnet ${dotnet.version}` });
        }
        const project = join(cwd, "tools/api-probe");
        if (!existsSync(join(project, "api-probe.csproj"))) {
          add({
            name: "tools",
            state: "missing",
            detail: `${project} has no api-probe.csproj`,
            message: `PROBE_MISSING: ${project} does not exist. This repository has no dumper.`,
            next: "NEXT: restore tools/api-probe (it is part of the repository), or run setup-workspace.",
          });
        } else {
          add({ name: "tools", state: "ok", detail: "tools/api-probe (built on demand)" });
        }
        try {
          add({ name: "game", state: "ok", detail: requireManagedDir(cwd).managedDir });
        } catch (error) {
          const text = (error as Error).message;
          const cut = text.indexOf("\nNEXT:");
          add({
            name: "game",
            state: "missing",
            detail: "no usable game directory",
            message: cut === -1 ? text : text.slice(0, cut),
            next: cut === -1 ? "" : text.slice(cut + 1),
          });
        }
        ladder = items;
        return items;
      };
      const firstProblem = (): Rung | undefined => rungs().find((r) => r.state === "missing");
      const problemText = (r: Rung): string => [r.message, r.next].filter(Boolean).join("\n");

      const rebuild = async (extra: string[]) => {
        const problem = firstProblem();
        if (problem) throw new Error(problemText(problem));
        const started = Date.now();
        let stage = "Rebuilding the dump";
        const report = (text: string) => {
          stage = text;
          onUpdate?.({ content: [{ type: "text", text }], details: {} });
        };
        const beat = setInterval(() => report(`${stage} (${Math.round((Date.now() - started) / 1000)}s)`), 10_000);
        try {
          return await serialize(`dump:${dir}`, () => buildDump(cwd, cfg, extra, report));
        } finally {
          clearInterval(beat);
        }
      };

      const wrap = (text: string, details: Record<string, unknown> = {}) => ({
        content: [{ type: "text", text }],
        details: { ok: true, ...details },
      });
      const failed = (text: string, details: Record<string, unknown> = {}) => ({
        content: [{ type: "text", text }],
        details: { ok: false, ...details },
      });

      const refreshNote = async (): Promise<{ text: string; missing: string[] }> => {
        if (freshness.state === "fresh") return { text: "", missing: [] };
        if (freshness.unavailable.length) {
          return {
            text: `PARTIAL: the dump is incomplete (INCOMPLETE_DUMP).\n${coverageText(freshness.manifest)}\n`,
            missing: freshness.missing,
          };
        }
        const built = await rebuild([]);
        if (built.missing.length) {
          return {
            text: `PARTIAL: refreshed the dump in ${built.seconds.toFixed(1)}s, but configured assemblies are missing: ${built.missing.join(", ")}.\n${coverageText(built.manifest)}\n`,
            missing: built.missing,
          };
        }
        return { text: `# refreshed the dump in ${built.seconds.toFixed(1)}s (${freshness.reasons.join("; ")})\n`, missing: [] };
      };

      try {
        if (params.action === "status") {
          const manifest = readManifest(dir);
          const problem = firstProblem();
          const dumpRung: Rung =
            freshness.state === "fresh"
              ? {
                  name: "dump",
                  state: "ok",
                  detail: `current (${MB(folderBytes(dir))}, ${manifest?.contents?.typeFiles ?? "?"} type files)`,
                }
              : freshness.state === "stale"
                ? { name: "dump", state: "missing", detail: `stale: ${freshness.reasons.join("; ")}` }
                : { name: "dump", state: "missing", detail: `absent (expected ${dir})` };
          const table = [...rungs(), dumpRung].map((r) => {
            const mark = r.state === "ok" ? "ok  " : r.state === "missing" ? "MISS" : "?   ";
            return `  ${r.name.padEnd(7)} ${mark} ${r.detail}`;
          });
          const facts = manifest ? [`  build: ${manifest.build?.label ?? "unknown"}`, coverageText(manifest)] : [];
          const versions = gameDir ? versionWarnings(cwd, gameDir) : [];
          const fresh = freshness.state === "fresh";
          const incomplete = freshness.missing.length > 0;
          const head = incomplete
            ? "PARTIAL: the dump is incomplete (INCOMPLETE_DUMP)."
            : !fresh && problem
              ? `FAIL: the dump cannot be used yet. Expected ${dir}.`
              : freshness.state === "missing"
                ? `FAIL: no dump for this repository. Expected ${dir}.`
                : !fresh
                  ? "PARTIAL: the dump is stale."
                  : problem
                    ? `PARTIAL: the dump is current, but a rebuild would stop at ${problem.name}.`
                    : `PASS: the dump is current. ${dir}`;
          const next = incomplete
            ? "NEXT: action=dump rebuilds after the missing configured assemblies are restored."
            : problem
              ? problemText(problem)
              : fresh
                ? ""
                : "NEXT: action=search/type/body rebuild it by themselves; action=dump rebuilds now.";
          const text = [head, ...table, ...facts, ...versions, next].filter(Boolean).join("\n");
          return fresh && !problem
            ? wrap(text, { dir, freshness: "fresh" })
            : failed(text, {
                dir,
                freshness: freshness.state,
                reason: incomplete ? "INCOMPLETE_DUMP" : (problem?.name ?? "DUMP"),
                missing: freshness.missing,
              });
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
          const facts = [
            `  ${dir} (${MB(folderBytes(dir))}, ${built.manifest.contents?.typeFiles ?? "?"} type files)`,
            coverageText(built.manifest),
          ];
          if (built.missing.length) {
            return failed(
              [
                `PARTIAL: rebuilt the dump in ${built.seconds.toFixed(1)}s, but ${built.missing.length} requested assembly(s) are missing: ${built.missing.join(", ")}.`,
                ...facts,
                "NEXT: correct the missing assembly names or restore the DLLs, then run inspect_game_code action=dump again.",
              ].join("\n"),
              { dir, seconds: built.seconds, reason: "INCOMPLETE_DUMP", missing: built.missing },
            );
          }
          return wrap([`PASS: rebuilt the dump in ${built.seconds.toFixed(1)}s.`, ...facts].join("\n"), {
            dir,
            seconds: built.seconds,
          });
        }

        if (!params.target) {
          return failed(`FAIL: action=${params.action} needs target=<name>.`, { reason: "TARGET_MISSING" });
        }

        const refresh = await refreshNote();
        if (refresh.missing.length) {
          return failed(`${refresh.text}NEXT: restore the missing configured assemblies, then run inspect_game_code action=dump again.`, {
            reason: "INCOMPLETE_DUMP",
            missing: refresh.missing,
          });
        }
        const respond = (text: string, details: Record<string, unknown>) => wrap(`${refresh.text}${text}`, details);
        const tree = treeDir(cwd);

        if (params.action === "search") {
          const part = takeCapped(searchTree(tree, params.target, 51), 50);
          const files = new Set(part.hits.map((h) => h.file)).size;
          const body = part.hits.length
            ? part.hits.map((h) => (h.line ? `${h.file}:${h.line}  ${h.text}` : `${h.file}`)).join("\n")
            : `0 matches in the recorded coverage. Try a shorter substring.\n${coverageText(readManifest(dir))}`;
          return respond(`${searchHeader(params.target, part.hits.length, files, part.capped)}\n${truncate(body)}`, {
            matches: part.hits.length,
            files,
            capped: part.capped,
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
          return respond(`# ${rel}\n\n${truncate(source)}`, { file: rel });
        }

        const found = params.member ? extractMethod(source, params.member, 21) : [];
        const part = takeCapped(found, 20);
        if (part.hits.length === 0) {
          const listed = takeCapped(listMembers(source, 61), 60);
          return failed(
            `FAIL: no member matches "${params.member ?? ""}" in ${rel}.\n${memberListHeader(
              listed.hits.length,
              listed.capped,
            )}\n${truncate(listed.hits.join("\n"), 60)}`,
            { reason: "METHOD_NOT_FOUND", capped: listed.capped },
          );
        }
        return respond(
          `${bodyHeader(rel, params.member, part.hits.length, part.capped)}\n\n${truncate(
            part.hits.map((b) => b.text).join("\n\n"),
          )}`,
          { methods: part.hits.length, capped: part.capped },
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
