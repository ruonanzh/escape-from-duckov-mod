import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";
import { Type } from "typebox";
import { probeDotnet } from "../lib/dotnet";
import { serialize } from "../lib/proc";
import {
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

const MB = (bytes: number): string =>
  bytes >= 1048576 ? `${Math.round(bytes / 1048576)} MB` : `${Math.round(bytes / 1024)} KB`;

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
      try {
        managedDir = requireManagedDir(cwd).managedDir;
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

      const refreshNote = async (): Promise<string> => {
        if (freshness.state === "fresh") return "";
        const built = await rebuild([]);
        return `# refreshed the dump in ${built.seconds.toFixed(1)}s (${freshness.reasons.join("; ")})\n`;
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
          const coverage = (manifest?.assemblies ?? []).map((a) => `${a.name} (${a.files} files)`).join(", ");
          const facts = manifest ? [`  build: ${manifest.build?.label ?? "unknown"}`, `  coverage: ${coverage}`] : [];
          const fresh = freshness.state === "fresh";
          const head = !fresh && problem
            ? `FAIL: the dump cannot be used yet. Expected ${dir}.`
            : freshness.state === "missing"
              ? `FAIL: no dump for this repository. Expected ${dir}.`
              : !fresh
                ? "PARTIAL: the dump is stale."
                : problem
                  ? `PARTIAL: the dump is current, but a rebuild would stop at ${problem.name}.`
                  : `PASS: the dump is current. ${dir}`;
          const next = problem
            ? problemText(problem)
            : fresh
              ? ""
              : "NEXT: action=search/type/body rebuild it by themselves; action=dump rebuilds now.";
          const text = [head, ...table, ...facts, next].filter(Boolean).join("\n");
          return fresh && !problem
            ? wrap(text, { dir, freshness: "fresh" })
            : failed(text, { dir, freshness: freshness.state, reason: problem?.name ?? "DUMP" });
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
