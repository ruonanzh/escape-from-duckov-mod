/**
 * The decompiled dump for a Mono game: where it lives, whether it is current,
 * how to rebuild it, and how to read it.
 *
 * The dumper is the vendored `tools/api-probe` (ICSharpCode.Decompiler). It reads
 * the game's DLLs and writes one file per top-level type, so a question like
 * "who calls this" becomes a single grep instead of one type query per call.
 *
 * The dump is a cache: the evidence is `MANIFEST.json` (the sha256 of every DLL
 * it was built from, plus the tool that read them). It lives outside every
 * repository, under the product data root, in a folder named after this one.
 */
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from "node:fs";
import { join, relative, resolve, sep } from "node:path";
import { createHash } from "node:crypto";
import os from "node:os";
import { runAsync } from "./proc";
import { checkGameDir, managedDirFor, platformKey, readModRepoConfig, readState, expandHome } from "./game-paths";
import { probeUpToDate } from "./probe";
import { probeDotnet } from "./dotnet";
import { readInfoFields } from "./mod-identity";

// ── configuration ───────────────────────────────────────────────────────────

export interface DumpConfig {
  /**
   * What to dump: DLL names (no `.dll`), globs, or keys of `paths`.
   * An entry that matches nothing is reported, not silently dropped.
   */
  assemblies: string[];
  /** DLLs that do not live in the game's Managed folder, keyed by name. */
  paths?: Record<string, string>;
  /** Nested types are already inside the enclosing type's file. */
  skipNestedTypes?: boolean;
  /** Compiler-generated `<>` types hold inlined lambdas. */
  skipCompilerGenerated?: boolean;
  /** Refuse a dump larger than this. */
  budgetBytes?: number;
}

export interface DecompileConfig {
  version: number;
  runtime: "mono";
  dump: DumpConfig;
}

export const configPath = (cwd: string): string => join(cwd, ".pi/lib/decompile.json");

export function loadConfig(cwd: string): DecompileConfig {
  const file = configPath(cwd);
  if (!existsSync(file)) {
    throw new Error(`DECOMPILE_CONFIG_MISSING: ${file} does not exist. This repository has no statement of what to dump.`);
  }
  const raw = JSON.parse(readFileSync(file, "utf8")) as DecompileConfig;
  if (raw?.runtime !== "mono") {
    throw new Error(`DECOMPILE_CONFIG_INVALID: runtime "${String(raw?.runtime)}" is not what this repository dumps.`);
  }
  if (!Array.isArray(raw?.dump?.assemblies) || raw.dump.assemblies.length === 0) {
    throw new Error("DECOMPILE_CONFIG_INVALID: .pi/lib/decompile.json needs a non-empty dump.assemblies array.");
  }
  return raw;
}

/** The parts of the config that decide the dump's shape (not its budget). */
export function configSignature(cfg: DecompileConfig): string {
  const paths = Object.entries(cfg.dump.paths ?? {}).sort(([a], [b]) => a.localeCompare(b));
  return JSON.stringify({
    assemblies: [...cfg.dump.assemblies].sort(),
    paths,
    skipNestedTypes: cfg.dump.skipNestedTypes !== false,
    skipCompilerGenerated: cfg.dump.skipCompilerGenerated !== false,
  });
}

// ── paths ───────────────────────────────────────────────────────────────────

export function decompileRoot(): string {
  if (process.env.DECOMPILED_ROOT) return process.env.DECOMPILED_ROOT;
  const data = process.env.PI_DESKTOP_DATA_DIR || join(os.homedir(), ".gamer-agent-pi");
  return join(data, "decompiled");
}

const repoName = (cwd: string): string => cwd.split(sep).filter(Boolean).pop() ?? "repo";

export const dumpDir = (cwd: string): string => join(decompileRoot(), repoName(cwd));

export const treeDir = (cwd: string): string => join(dumpDir(cwd), "cs");

// ── hashing and small fs helpers ────────────────────────────────────────────

const hashCache = new Map<string, { key: string; value: string }>();

export function sha256File(path: string): string {
  const st = statSync(path);
  const key = `${st.size}:${st.mtimeMs}`;
  const hit = hashCache.get(path);
  if (hit && hit.key === key) return hit.value;
  const value = createHash("sha256").update(readFileSync(path)).digest("hex");
  hashCache.set(path, { key, value });
  return value;
}

const dirBytes = (dir: string): number => {
  let total = 0;
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, entry.name);
    if (entry.isDirectory()) total += dirBytes(p);
    else if (entry.isFile()) total += statSync(p).size;
  }
  return total;
};

const countFiles = (dir: string, ext: string): number => {
  let n = 0;
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const p = join(dir, entry.name);
    if (entry.isDirectory()) n += countFiles(p, ext);
    else if (entry.name.endsWith(ext)) n += 1;
  }
  return n;
};

export const folderBytes = (dir: string): number => (existsSync(dir) ? dirBytes(dir) : 0);

// ── the probe and the game's DLLs ───────────────────────────────────────────

export interface Probe {
  projectDir: string;
  dll: string;
}

const probeProject = (cwd: string): string => join(cwd, "tools/api-probe");
const probeDll = (cwd: string): string => join(probeProject(cwd), "bin/Release/net8.0/api-probe.dll");

/**
 * Build the probe when its source is newer than its output. It takes a few
 * seconds, so it is not rebuilt for every call.
 */
export async function ensureProbe(cwd: string, onProgress: (m: string) => void = () => undefined): Promise<Probe> {
  const projectDir = probeProject(cwd);
  const dll = probeDll(cwd);
  if (!existsSync(join(projectDir, "api-probe.csproj"))) {
    throw new Error(`PROBE_MISSING: ${projectDir} does not exist. This repository has no dumper.\nNEXT: restore tools/api-probe (it is part of the repository), or run setup-workspace.`);
  }
  if (probeUpToDate(projectDir, dll)) return { projectDir, dll };
  const dotnet = probeDotnet();
  if (!dotnet.found || (dotnet.major ?? 0) < 8) {
    throw new Error(
      dotnet.version
        ? `DOTNET_TOO_OLD: dotnet ${dotnet.version} cannot build the dumper (need >= 8).\nNEXT: run install_runtime, then check_runtime.`
        : "DOTNET_MISSING: the dumper needs the .NET SDK.\nNEXT: run check_runtime, then install_runtime.",
    );
  }
  onProgress("Building the dumper (tools/api-probe)");
  await runAsync(dotnet.path as string, ["build", "-c", "Release"], { cwd: projectDir, timeoutMs: 300_000 });
  if (!existsSync(dll)) throw new Error(`PROBE_BUILD_FAILED: ${dll} was not produced.`);
  return { projectDir, dll };
}

export function requireManagedDir(cwd: string): { gameDir: string; managedDir: string } {
  const cfg = readModRepoConfig(cwd);
  // One verdict: the same check that `check_game_paths` prints decides whether the game is usable.
  const verdict = checkGameDir(readState(cwd).gameDir, cfg, platformKey());
  if (!verdict.ok || !verdict.path) {
    const code = verdict.path ? "MANAGED_DIR_MISSING" : "GAME_DIRECTORY_UNKNOWN";
    throw new Error(
      `${code}: ${verdict.reason ?? "the game directory is not usable"}\nNEXT: ${verdict.next ?? "run try_set_game_dir."}`,
    );
  }
  const managed = managedDirFor(verdict.path, cfg, platformKey());
  if (!managed) {
    throw new Error(
      `MANAGED_DIR_MISSING: mod-repo.json has no compile.managedDir for ${platformKey()}.\nNEXT: reopen or update this game workspace.`,
    );
  }
  return { gameDir: verdict.path, managedDir: managed };
}

export interface ResolvedAssembly {
  /** The name we asked for (may be a glob or a paths key). */
  request: string;
  /** The assembly name written to the dump (DLL stem). */
  name: string;
  dllPath: string;
}

/** Expand the config's assembly list against the game's Managed folder. */
export function resolveAssemblies(cwd: string, cfg: DecompileConfig, managedDir: string): { resolved: ResolvedAssembly[]; missing: string[] } {
  const resolved: ResolvedAssembly[] = [];
  const missing: string[] = [];
  const seen = new Set<string>();
  const push = (request: string, dllPath: string) => {
    const name = dllPath.split(sep).pop()!.replace(/\.dll$/i, "");
    if (seen.has(name.toLowerCase())) return;
    seen.add(name.toLowerCase());
    resolved.push({ request, name, dllPath });
  };
  const entries = (() => {
    try {
      return readdirSync(managedDir).filter((n) => n.toLowerCase().endsWith(".dll"));
    } catch {
      return [];
    }
  })();

  for (const request of cfg.dump.assemblies) {
    const explicit = cfg.dump.paths?.[request];
    if (explicit) {
      const p = resolve(cwd, expandHome(explicit));
      if (existsSync(p)) push(request, p);
      else missing.push(`${request} (expected ${p})`);
      continue;
    }
    if (request.includes("*")) {
      const rx = new RegExp(`^${request.replace(/[.+^${}()|[\]\\]/g, "\\$&").replace(/\*/g, ".*")}\\.dll$`, "i");
      const hits = entries.filter((n) => rx.test(n));
      if (hits.length === 0) missing.push(request);
      for (const hit of hits) push(request, join(managedDir, hit));
      continue;
    }
    const p = join(managedDir, request.toLowerCase().endsWith(".dll") ? request : `${request}.dll`);
    if (existsSync(p)) push(request, p);
    else missing.push(request);
  }
  return { resolved, missing };
}

// ── manifest and freshness ──────────────────────────────────────────────────

export interface AssemblyRecord {
  name: string;
  request: string;
  dllPath: string;
  sha256: string;
  files: number;
  bytes: number;
  skipped: number;
  failed: number;
}

export interface Manifest {
  game?: { name?: string; engine?: string; state?: string };
  build?: { label?: string; createdAt?: string };
  tools?: Record<string, { path: string; sha256: string; version: string }>;
  config?: { signature?: string; assemblies?: string[] };
  assemblies?: AssemblyRecord[];
  contents?: { typeFiles?: number; totalBytes?: number };
}

export const manifestPath = (dir: string): string => join(dir, "MANIFEST.json");

export function readManifest(dir: string): Manifest | null {
  try {
    return JSON.parse(readFileSync(manifestPath(dir), "utf8")) as Manifest;
  } catch {
    return null;
  }
}

export type FreshState = "missing" | "stale" | "fresh";

export interface Freshness {
  state: FreshState;
  dir: string;
  reasons: string[];
  /** Configured assemblies that are unavailable or absent from the dump. */
  missing: string[];
  /** Missing entries that the current environment cannot rebuild. */
  unavailable: string[];
  manifest: Manifest | null;
}

/**
 * Compare the dump with the config and with the game's DLLs on disk.
 * A DLL that cannot be found is not a reason to rebuild: it is a reason to say
 * the comparison could not be made.
 */
export function checkFreshness(cwd: string, cfg: DecompileConfig, managedDir: string | null): Freshness {
  const dir = dumpDir(cwd);
  const manifest = readManifest(dir);
  if (!manifest) {
    return {
      state: "missing",
      dir,
      reasons: ["no MANIFEST.json in the dump folder"],
      missing: [],
      unavailable: [],
      manifest: null,
    };
  }
  const reasons: string[] = [];
  if (!manifest.config?.signature) reasons.push("the dump does not record the decompile.json it was built from");
  else if (manifest.config.signature !== configSignature(cfg)) reasons.push("decompile.json changed");
  if (!manifest.assemblies?.length) reasons.push("the dump records no assemblies");
  const missing: string[] = [];
  const unavailable: string[] = [];
  if (managedDir) {
    const coverage = resolveAssemblies(cwd, cfg, managedDir);
    for (const name of coverage.missing) {
      reasons.push(`configured assembly is unavailable: ${name}`);
      missing.push(name);
      unavailable.push(name);
    }
    const recorded = new Set((manifest.assemblies ?? []).map((a) => a.name.toLowerCase()));
    for (const assembly of coverage.resolved) {
      if (recorded.has(assembly.name.toLowerCase())) continue;
      reasons.push(`dump lacks configured assembly: ${assembly.name}`);
      missing.push(assembly.name);
    }
    const changed = (manifest.assemblies ?? []).filter((a) => {
      if (!existsSync(a.dllPath)) return false;
      return sha256File(a.dllPath) !== a.sha256;
    });
    for (const a of changed) reasons.push(`${a.name}.dll changed since this dump`);
  } else {
    const recorded = new Set((manifest.assemblies ?? []).map((a) => a.request));
    for (const request of cfg.dump.assemblies) {
      if (recorded.has(request)) continue;
      reasons.push(`dump has no record for configured assembly: ${request}`);
      missing.push(request);
      unavailable.push(request);
    }
  }
  return { state: reasons.length ? "stale" : "fresh", dir, reasons, missing, unavailable, manifest };
}

/** Read the installed game's advisory version without making it a path requirement. */
export function readGameVersion(gameDir: string): string | null {
  return readInfoFields(join(gameDir, "Info.ini")).version?.trim() || null;
}

/** Compare the installed version with repository declarations and generated API snapshots. */
export function versionWarnings(cwd: string, gameDir: string): string[] {
  const installed = readGameVersion(gameDir);
  if (!installed) return [];
  const warnings: string[] = [];
  const configured = readModRepoConfig(cwd).game?.version?.trim();
  if (configured && configured !== installed) {
    warnings.push(`WARN: installed game version ${installed} differs from mod-repo.json version ${configured}.`);
  }
  const snapshots = new Map<string, number>();
  const apiDir = join(cwd, "docs", "api");
  if (existsSync(apiDir)) {
    for (const entry of readdirSync(apiDir, { withFileTypes: true })) {
      if (!entry.isFile() || !entry.name.endsWith(".md")) continue;
      const source = readFileSync(join(apiDir, entry.name), "utf8");
      const version = /^> game (\S+)/m.exec(source)?.[1];
      if (version) snapshots.set(version, (snapshots.get(version) ?? 0) + 1);
    }
  }
  for (const [snapshot, count] of [...snapshots.entries()].sort(([a], [b]) => a.localeCompare(b))) {
    if (snapshot === installed) continue;
    warnings.push(`WARN: installed game version ${installed} differs from docs/api snapshot version ${snapshot} (${count} files).`);
  }
  return warnings;
}

// ── building the dump ───────────────────────────────────────────────────────

export interface BuildResult {
  dir: string;
  seconds: number;
  manifest: Manifest;
  missing: string[];
}

const today = (): string => new Date().toISOString().slice(0, 10);

/** A budget of 1000 bytes is 0 MB; say KB below one megabyte. */
const size = (bytes: number): string =>
  bytes >= 1048576 ? `${Math.round(bytes / 1048576)} MB` : `${Math.round(bytes / 1024)} KB`;

interface DumpSummary {
  assembly: string;
  files: number;
  bytes: number;
  skipped: number;
  failed: number;
  sha256: string;
}

function parseSummaries(stdout: string): DumpSummary[] {
  const out: DumpSummary[] = [];
  for (const line of stdout.split("\n")) {
    if (!line.startsWith("# summary ")) continue;
    for (const part of line.slice("# summary ".length).trim().split(/\s+(?=\{)/)) {
      try {
        out.push(JSON.parse(part) as DumpSummary);
      } catch {
        // a truncated line is not fatal: the counts are also in the manifest we write
      }
    }
  }
  return out;
}

/**
 * Rebuild into a temporary folder and swap it in. A failure leaves the previous
 * dump untouched: the agent may be reading it.
 */
export async function buildDump(
  cwd: string,
  cfg: DecompileConfig,
  extraAssemblies: string[] = [],
  onProgress: (message: string) => void = () => undefined,
): Promise<BuildResult> {
  const started = Date.now();
  const { gameDir, managedDir } = requireManagedDir(cwd);
  const probe = await ensureProbe(cwd, onProgress);
  const dotnet = probeDotnet();
  if (!dotnet.found) throw new Error("DOTNET_MISSING: the dumper needs the .NET SDK.\nNEXT: run check_runtime.");

  const requested: ResolvedAssembly[] = [];
  const missing: string[] = [];
  const withExtras: DecompileConfig = {
    ...cfg,
    dump: { ...cfg.dump, assemblies: [...cfg.dump.assemblies, ...extraAssemblies] },
  };
  const { resolved, missing: absent } = resolveAssemblies(cwd, withExtras, managedDir);
  requested.push(...resolved);
  missing.push(...absent);
  if (requested.length === 0) {
    throw new Error(
      `NO_ASSEMBLIES_FOUND: none of [${withExtras.dump.assemblies.join(", ")}] exist under ${managedDir}.\nNEXT: correct dump.assemblies or dump.paths in .pi/lib/decompile.json, or restore the game's DLLs.`,
    );
  }

  const dir = dumpDir(cwd);
  const parent = join(dir, "..");
  mkdirSync(parent, { recursive: true });
  const tmp = join(parent, `.tmp-${repoName(cwd)}-${process.pid}`);
  rmSync(tmp, { recursive: true, force: true });
  mkdirSync(tmp, { recursive: true });

  try {
    const budget = cfg.dump.budgetBytes ?? 0;
    onProgress(`Dumping ${requested.length} assembly(ies) with the probe`);
    const args = [
      probe.dll,
      "--managed",
      managedDir,
      "--dll",
      requested.map((r) => r.dllPath).join(","),
      "--action",
      "dump",
      "--out",
      join(tmp, "cs"),
    ];
    if (budget > 0) args.push("--budget", String(budget));
    const run = await runAsync(dotnet.path as string, args, { timeoutMs: 900_000 }).catch(
      (error: { stderr?: string; stdout?: string; message?: string }) => {
        const detail = [error.stderr, error.stdout].filter((s) => s && s.trim()).join("\n").trim();
        const over = detail.match(/dump is already (\d+) bytes and --budget is (\d+)/);
        if (over) {
          throw new Error(
            `DUMP_OVER_BUDGET: the dump reached ${size(Number(over[1]))} and .pi/lib/decompile.json allows ${size(Number(over[2]))}. The previous dump is untouched.\nNEXT: raise dump.budgetBytes, or narrow dump.assemblies.`,
          );
        }
        throw new Error(`PROBE_DUMP_FAILED: ${detail.split("\n").slice(-3).join(" | ").slice(0, 400) || error.message || "no output"}`);
      },
    );
    const summaries = parseSummaries(run.stdout);
    const tree = join(tmp, "cs");
    if (!existsSync(tree)) throw new Error(`PROBE_DUMP_FAILED: ${tree} was not produced.\n${run.stderr.slice(-400)}`);

    const records: AssemblyRecord[] = requested.map((r) => {
      const s = summaries.find((x) => x.assembly.toLowerCase() === r.name.toLowerCase());
      return {
        name: r.name,
        request: r.request,
        dllPath: r.dllPath,
        sha256: sha256File(r.dllPath),
        files: s?.files ?? 0,
        bytes: s?.bytes ?? 0,
        skipped: s?.skipped ?? 0,
        failed: s?.failed ?? 0,
      };
    });

    const bytes = dirBytes(tmp);
    if (budget > 0 && bytes > budget) {
      throw new Error(
        `DUMP_OVER_BUDGET: this dump is ${size(bytes)} and the config allows ${size(budget)}. The previous dump is untouched.\nNEXT: raise dump.budgetBytes, or narrow dump.assemblies.`,
      );
    }
    mkdirSync(join(tmp, "logs"), { recursive: true });
    writeFileSync(join(tmp, "logs/api-probe.log"), `${run.stdout}\n${run.stderr}`);

    const manifest: Manifest = {
      game: { name: readModRepoConfig(cwd).game?.name, state: gameDir },
      build: {
        label: (() => {
          const previous = readManifest(dir);
          const same =
            previous?.assemblies?.length === records.length &&
            records.every((r) => previous?.assemblies?.some((p) => p.name === r.name && p.sha256 === r.sha256));
          return same ? (previous?.build?.label ?? today()) : today();
        })(),
        createdAt: new Date().toISOString(),
      },
      tools: {
        dumper: {
          path: relative(cwd, probe.dll),
          sha256: sha256File(probe.dll),
          version: (/^# tool (.+)$/m.exec(run.stdout) ?? [])[1] ?? "",
        },
      },
      config: { signature: configSignature(cfg), assemblies: [...cfg.dump.assemblies] },
      assemblies: records,
      contents: { typeFiles: countFiles(tree, ".cs"), totalBytes: bytes },
    };
    writeFileSync(manifestPath(tmp), `${JSON.stringify(manifest, null, 2)}\n`);

    const old = join(parent, `.old-${repoName(cwd)}-${process.pid}`);
    rmSync(old, { recursive: true, force: true });
    if (existsSync(dir)) renameSync(dir, old);
    renameSync(tmp, dir);
    rmSync(old, { recursive: true, force: true });

    return { dir, seconds: (Date.now() - started) / 1000, manifest, missing };
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
}

// ── reading the dump ────────────────────────────────────────────────────────

export function truncate(text: string, maxLines = 200, maxBytes = 48 * 1024): string {
  let out = text;
  const lines = out.split("\n");
  if (lines.length > maxLines) out = `${lines.slice(0, maxLines).join("\n")}\n... (${lines.length - maxLines} more lines truncated)`;
  if (Buffer.byteLength(out) > maxBytes) out = `${out.slice(0, maxBytes)}\n... (output truncated at ${maxBytes} bytes)`;
  return out;
}

/** Every `.cs` file under the dump, as paths relative to it. */
export function listTree(dir: string, depth = 0, prefix = ""): string[] {
  if (depth > 12 || !existsSync(dir)) return [];
  const out: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const rel = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) out.push(...listTree(join(dir, entry.name), depth + 1, rel));
    else if (entry.name.endsWith(".cs")) out.push(rel);
  }
  return out;
}

export interface TreeHit {
  file: string;
  line: number;
  text: string;
}

/** Search the tree for a name: file paths first, then lines. */
export function searchTree(dir: string, query: string, limit = 50): TreeHit[] {
  const needle = query.toLowerCase();
  const hits: TreeHit[] = [];
  for (const rel of listTree(dir)) {
    if (rel.toLowerCase().includes(needle)) {
      hits.push({ file: rel, line: 0, text: "" });
      if (hits.length >= limit) return hits;
    }
  }
  for (const rel of listTree(dir)) {
    const lines = readFileSync(join(dir, rel), "utf8").split("\n");
    for (let i = 0; i < lines.length; i++) {
      if (!lines[i].toLowerCase().includes(needle)) continue;
      hits.push({ file: rel, line: i + 1, text: lines[i].trim() });
      if (hits.length >= limit) return hits;
    }
  }
  return hits;
}

/**
 * Ask a search for one hit more than you show. A list that ended and a list that
 * was cut then differ, and the header can say which one it is.
 */
export function takeCapped<T>(hits: T[], limit: number): { hits: T[]; capped: boolean } {
  return { hits: hits.slice(0, limit), capped: hits.length > limit };
}

/** First line of a tree search. Only files seen before the cap are counted. */
export function searchHeader(target: string, matches: number, files: number, capped: boolean, limit = 50): string {
  const cut = capped ? ` - capped at ${limit} hits, so more may match` : "";
  return `# search "${target}": ${matches} match(es) in ${files} file(s)${cut}`;
}

/** First line of one member's bodies. */
export function bodyHeader(
  rel: string,
  member: string | undefined,
  blocks: number,
  capped: boolean,
  limit = 20,
): string {
  const cut = capped ? ` - capped at ${limit} blocks, so more overloads may match` : "";
  return `# ${rel}${member ? ` :: ${member}` : ""}: ${blocks} block(s)${cut}`;
}

/** Heading of the member list a failed body lookup returns. */
export function memberListHeader(members: number, capped: boolean, limit = 60): string {
  const cut = capped ? ` - capped at ${limit} members; search for the member instead` : "";
  return `# members in this file: ${members} listed${cut}`;
}

/** A type's file, by short name, `Namespace.Type`, or a path suffix. */
export function findTypeFile(dir: string, typeName: string): string | null {
  const wanted = typeName.trim().replace(/\./g, "/").toLowerCase();
  const short = typeName.split(".").pop()!.toLowerCase();
  const files = listTree(dir);
  const exact = files.find((f) => f.toLowerCase() === `${wanted}.cs`);
  if (exact) return exact;
  const suffix = files.find((f) => f.toLowerCase().endsWith(`/${wanted}.cs`));
  if (suffix) return suffix;
  const byShort = files.filter((f) => f.toLowerCase().endsWith(`/${short}.cs`));
  return byShort.length === 1 ? byShort[0] : null;
}

/** Declaration-shaped lines: what a reader can ask for by name. */
export function listMembers(source: string, limit = 60): string[] {
  const out: string[] = [];
  for (const line of source.split("\n")) {
    const t = line.trim();
    if (!/^(public|private|protected|internal|static|sealed|abstract|override|virtual|async|readonly|event|partial)\b/.test(t)) continue;
    if (!/[(=>]/.test(t)) continue;
    if (/\b(class|struct|interface|enum|delegate)\b/.test(t)) continue;
    out.push(`  ${t}`);
    if (out.length >= limit) break;
  }
  return out;
}

export interface MethodBlock {
  signature: string;
  text: string;
}

const DECL_START = /^(public|private|protected|internal|static|sealed|abstract|override|virtual|async|readonly|event|partial|extern|unsafe|new|const)\b/;

/**
 * Cut one member out of a decompiled file: the declaration through its matching
 * brace. Only declarations are matched, never call sites, so asking for a method
 * name does not return the places that call it. Overloads all come back.
 */
export function extractMethod(source: string, member: string, limit = 20): MethodBlock[] {
  const lines = source.split("\n");
  const needle = member.toLowerCase();
  // A plain identifier must match as a word, so "Capacity" does not match
  // "CapacityHash". Anything with spaces or punctuation stays a substring test.
  const matcher = /^[a-z0-9_.<>$]+$/.test(needle)
    ? new RegExp(`\\b${needle.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\b`)
    : null;
  const matches = (text: string): boolean => (matcher ? matcher.test(text.toLowerCase()) : text.toLowerCase().includes(needle));
  const blocks: MethodBlock[] = [];
  for (let i = 0; i < lines.length && blocks.length < limit; i++) {
    if (!DECL_START.test(lines[i].trim())) continue;
    // A signature may wrap; look a few lines ahead for the name before giving up.
    let head = "";
    let last = i;
    for (; last < Math.min(i + 5, lines.length); last++) {
      head += `${lines[last]} `;
      if (/[{(;]|=>/.test(lines[last])) break;
    }
    // Only the part before the body counts: a name that appears after `=>`, `=`
    // or `(` is a reference (a property that returns PortraitPath is not
    // PortraitPath). String literals are stripped for the same reason.
    const declarator = head.split(/=>|[({;=]/)[0].replace(/"(?:[^"\\]|\\.)*"/g, '""');
    if (!matches(declarator)) {
      i = last;
      continue;
    }
    const body: string[] = [];
    let depth = 0;
    let opened = false;
    for (let k = i; k < lines.length; k++) {
      body.push(lines[k]);
      for (const ch of lines[k]) {
        if (ch === "{") {
          depth += 1;
          opened = true;
        } else if (ch === "}") {
          depth -= 1;
        }
      }
      if (opened && depth === 0) break;
      if (!opened && (lines[k].trimEnd().endsWith(";") || lines[k].includes("=>"))) break;
    }
    blocks.push({ signature: lines[i].trim(), text: body.join("\n") });
    i += body.length - 1;
  }
  return blocks;
}
