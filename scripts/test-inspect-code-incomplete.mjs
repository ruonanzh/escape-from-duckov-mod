import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { discoverGameDir, managedDirFor, platformKey, readModRepoConfig } from "../.pi/lib/game-paths.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repoConfig = readModRepoConfig(repoRoot);
const platform = platformKey();
const gameDir = discoverGameDir(repoConfig, {}, platform).gameDir;
if (!gameDir) {
  console.log("SKIP: no usable game directory; incomplete-dump integration checks did not run");
  process.exit(0);
}
const managedDir = managedDirFor(gameDir, repoConfig, platform);
const coveredDll = managedDir ? join(managedDir, "TeamSoda.MiniLocalizor.dll") : null;
if (!coveredDll || !existsSync(coveredDll)) {
  console.log("SKIP: TeamSoda.MiniLocalizor.dll is not in the game's Managed folder; incomplete-dump checks did not run");
  process.exit(0);
}
const probeDll = join(repoRoot, "tools", "api-probe", "bin", "Release", "net8.0", "api-probe.dll");
if (!existsSync(probeDll)) {
  console.log("SKIP: tools/api-probe is not built; incomplete-dump integration checks did not run");
  process.exit(0);
}
const root = mkdtempSync(join(tmpdir(), "duckov-incomplete-dump-"));
const previousRoot = process.env.DECOMPILED_ROOT;

function copyWithTsExtensions(from, to) {
  cpSync(from, to, { recursive: true });
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const path = join(dir, entry.name);
      if (entry.isDirectory()) walk(path);
      else if (entry.name.endsWith(".ts")) {
        writeFileSync(
          path,
          readFileSync(path, "utf8").replace(/(from\s+")(\.\.?\/[^"]+?)(")/g, (match, start, spec, end) =>
            spec.endsWith(".ts") || spec.endsWith(".js") ? match : `${start}${spec}.ts${end}`,
          ),
        );
      }
    }
  };
  walk(to);
}

function writeStubs(dir) {
  const typebox = join(dir, "node_modules", "typebox");
  mkdirSync(typebox, { recursive: true });
  writeFileSync(join(typebox, "package.json"), JSON.stringify({ name: "typebox", type: "module", main: "index.js" }));
  writeFileSync(
    join(typebox, "index.js"),
    `const s = (kind) => (opts) => ({ kind, ...(opts || {}) });
export const Type = { Object: s("Object"), String: s("String"), Boolean: s("Boolean"),
  Optional: (inner) => ({ kind: "Optional", inner }), Union: s("Union"), Literal: s("Literal") };
`,
  );
  const pi = join(dir, "node_modules", "@earendil-works", "pi-coding-agent");
  mkdirSync(pi, { recursive: true });
  writeFileSync(
    join(pi, "package.json"),
    JSON.stringify({ name: "@earendil-works/pi-coding-agent", type: "module", main: "index.js" }),
  );
  writeFileSync(join(pi, "index.js"), "export {};\n");
}

const textOf = (result) => result.content.map((item) => item.text).join("\n");
let failed = 0;
const check = (name, condition, result) => {
  console.log(`${condition ? "PASS" : "FAIL"}  ${name}`);
  if (!condition) {
    failed += 1;
    console.log(textOf(result));
  }
};

try {
  copyWithTsExtensions(join(repoRoot, ".pi"), join(root, ".pi"));
  cpSync(join(repoRoot, "tools", "api-probe"), join(root, "tools", "api-probe"), {
    recursive: true,
    preserveTimestamps: true,
  });
  cpSync(join(repoRoot, "mod-repo.json"), join(root, "mod-repo.json"));
  writeFileSync(join(root, ".gamer-agent.local.json"), JSON.stringify({ gameDir }));
  writeFileSync(
    join(root, ".pi", "lib", "decompile.json"),
    JSON.stringify({
      version: 1,
      runtime: "mono",
      dump: {
        assemblies: ["covered", "missing"],
        paths: { covered: coveredDll, missing: join(root, "missing.dll") },
        budgetBytes: 2000000,
      },
    }),
  );
  writeStubs(root);
  process.env.DECOMPILED_ROOT = join(root, "decompiled");
  const { default: register } = await import(pathToFileURL(join(root, ".pi", "extensions", "inspect-game-code.ts")).href);
  let tool;
  register({ registerTool: (definition) => { tool = definition; } });
  const execute = (params) => tool.execute("call", params, undefined, () => {}, { cwd: root });

  const dump = await execute({ action: "dump", force: true });
  const dumpText = textOf(dump);
  check(
    "dump returns PARTIAL, INCOMPLETE_DUMP, NEXT, coverage, and failed counts",
    dump.details.ok === false &&
      dump.details.reason === "INCOMPLETE_DUMP" &&
      dumpText.startsWith("PARTIAL:") &&
      dumpText.includes("NEXT:") &&
      dumpText.includes("coverage: TeamSoda.MiniLocalizor") &&
      dumpText.includes("0 failed"),
    dump,
  );

  const manifestFile = join(root, "decompiled", basename(root), "MANIFEST.json");
  const initialCreatedAt = JSON.parse(readFileSync(manifestFile, "utf8")).build.createdAt;

  const status = await execute({ action: "status" });
  const statusText = textOf(status);
  check(
    "status returns INCOMPLETE_DUMP instead of stale or fresh",
    status.details.ok === false &&
      status.details.reason === "INCOMPLETE_DUMP" &&
      statusText.startsWith("PARTIAL:") &&
      statusText.includes("configured assembly is unavailable") &&
      statusText.includes("0 failed") &&
      statusText.includes("NEXT:"),
    status,
  );

  const search = await execute({ action: "search", target: "DefinitelyMissingSymbol" });
  const searchText = textOf(search);
  check(
    "search reports incomplete coverage instead of a zero-hit absence claim",
    search.details.ok === false &&
      search.details.reason === "INCOMPLETE_DUMP" &&
      searchText.startsWith("PARTIAL:") &&
      searchText.includes("coverage: TeamSoda.MiniLocalizor") &&
      !searchText.includes("0 matches") &&
      searchText.includes("NEXT:"),
    search,
  );

  const type = await execute({ action: "type", target: "DefinitelyMissingType" });
  check(
    "type incomplete coverage outranks TYPE_NOT_FOUND",
    type.details.ok === false && type.details.reason === "INCOMPLETE_DUMP" && textOf(type).startsWith("PARTIAL:"),
    type,
  );

  const body = await execute({ action: "body", target: "DefinitelyMissingType", member: "MissingMember" });
  check(
    "body incomplete coverage outranks METHOD_NOT_FOUND",
    body.details.ok === false && body.details.reason === "INCOMPLETE_DUMP" && textOf(body).startsWith("PARTIAL:"),
    body,
  );

  check(
    "unavailable coverage does not rebuild the dump on every read",
    JSON.parse(readFileSync(manifestFile, "utf8")).build.createdAt === initialCreatedAt,
    body,
  );

  rmSync(join(root, ".gamer-agent.local.json"));
  const offline = await execute({ action: "search", target: "DefinitelyMissingSymbol" });
  check(
    "missing game directory cannot hide incomplete configured coverage",
    offline.details.ok === false &&
      offline.details.reason === "INCOMPLETE_DUMP" &&
      textOf(offline).startsWith("PARTIAL:") &&
      textOf(offline).includes("coverage: TeamSoda.MiniLocalizor"),
    offline,
  );
  writeFileSync(join(root, ".gamer-agent.local.json"), JSON.stringify({ gameDir }));

  writeFileSync(
    join(root, ".pi", "lib", "decompile.json"),
    JSON.stringify({
      version: 1,
      runtime: "mono",
      dump: { assemblies: ["missing"], paths: { missing: join(root, "missing.dll") }, budgetBytes: 2000000 },
    }),
  );
  const allMissing = await execute({ action: "dump", force: true });
  check(
    "all-missing dump returns NO_ASSEMBLIES_FOUND with NEXT",
    allMissing.details.ok === false &&
      textOf(allMissing).includes("NO_ASSEMBLIES_FOUND") &&
      textOf(allMissing).includes("NEXT: correct dump.assemblies or dump.paths"),
    allMissing,
  );

  writeFileSync(
    join(root, ".pi", "lib", "decompile.json"),
    JSON.stringify({
      version: 1,
      runtime: "mono",
      dump: { assemblies: ["covered"], paths: { covered: coveredDll }, budgetBytes: 2000000 },
    }),
  );
  const complete = await execute({ action: "dump", force: true });
  check(
    "complete dump stays PASS and shows failed counts",
    complete.details.ok === true && textOf(complete).startsWith("PASS:") && textOf(complete).includes("0 failed"),
    complete,
  );

  const extraMissing = await execute({ action: "dump", force: true, assemblies: "extraMissing" });
  check(
    "missing assemblies parameter uses requested wording and corrective NEXT",
    extraMissing.details.ok === false &&
      textOf(extraMissing).includes("requested assembly(s) are missing: extraMissing") &&
      textOf(extraMissing).includes("NEXT: correct the missing assembly names or restore the DLLs"),
    extraMissing,
  );

  const zero = await execute({ action: "search", target: "DefinitelyMissingSymbol" });
  check(
    "complete zero-hit search shows actual coverage",
    zero.details.ok === true &&
      zero.details.matches === 0 &&
      textOf(zero).includes("0 matches in the recorded coverage") &&
      textOf(zero).includes("coverage: TeamSoda.MiniLocalizor"),
    zero,
  );

  const mismatchedRepo = JSON.parse(readFileSync(join(root, "mod-repo.json"), "utf8"));
  mismatchedRepo.game.version = "0.0.0";
  writeFileSync(join(root, "mod-repo.json"), JSON.stringify(mismatchedRepo));
  mkdirSync(join(root, "docs", "api"), { recursive: true });
  writeFileSync(join(root, "docs", "api", "old.md"), "> game 0.0.0 · generated 2026-10-16\n");
  const advisory = await execute({ action: "status" });
  check(
    "version mismatches warn without blocking a fresh dump",
    advisory.details.ok === true &&
      textOf(advisory).startsWith("PASS:") &&
      textOf(advisory).includes("differs from mod-repo.json version 0.0.0") &&
      textOf(advisory).includes("differs from docs/api snapshot version 0.0.0 (1 files)"),
    advisory,
  );

  if (failed) process.exitCode = 1;
} finally {
  if (previousRoot === undefined) delete process.env.DECOMPILED_ROOT;
  else process.env.DECOMPILED_ROOT = previousRoot;
  rmSync(root, { recursive: true, force: true });
}
