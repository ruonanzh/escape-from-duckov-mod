import {
  cpSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  utimesSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const root = mkdtempSync(join(tmpdir(), "duckov-mod-install-"));

function copyWithTsExtensions(from, to) {
  cpSync(from, to, { recursive: true, preserveTimestamps: true });
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

function png(width, height) {
  const bytes = Buffer.alloc(24);
  Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]).copy(bytes, 0);
  bytes.writeUInt32BE(13, 8);
  bytes.write("IHDR", 12, "ascii");
  bytes.writeUInt32BE(width, 16);
  bytes.writeUInt32BE(height, 20);
  return bytes;
}

const textOf = (result) => result?.content?.map((item) => item.text).join("\n") ?? String(result);
let failed = 0;
const check = (name, condition, result) => {
  console.log(`${condition ? "PASS" : "FAIL"}  ${name}`);
  if (!condition) {
    failed += 1;
    if (result !== undefined) console.log(textOf(result));
  }
};

try {
  copyWithTsExtensions(join(repoRoot, ".pi"), join(root, ".pi"));
  cpSync(join(repoRoot, "mod-repo.json"), join(root, "mod-repo.json"));
  writeStubs(root);

  const tools = new Map();
  const pi = { registerTool(definition) { tools.set(definition.name, definition); } };
  const installRegistration = (await import(pathToFileURL(join(root, ".pi", "extensions", "mod-install.ts")).href)).default;
  const validateRegistration = (await import(pathToFileURL(join(root, ".pi", "extensions", "mod-lint.ts")).href)).default;
  installRegistration(pi);
  validateRegistration(pi);
  const install = (modDir) => tools.get("install_mod").execute("test", { modDir }, undefined, undefined, { cwd: root });
  const validate = (modDir, signal) => tools.get("validate_mod").execute("test", { modDir }, signal, undefined, { cwd: root });

  const { managedDirFor, modInstallDirFor, platformKey, readModRepoConfig } = await import(
    pathToFileURL(join(root, ".pi", "lib", "game-paths.ts")).href
  );
  const { pickInstallDir } = await import(pathToFileURL(join(root, ".pi", "extensions", "mod-install.ts")).href);
  const { replaceInstallDir } = await import(pathToFileURL(join(root, ".pi", "lib", "mod-products.ts")).href);
  const config = readModRepoConfig(root);
  const platform = platformKey();
  const gameDir = join(root, "game");
  const managedDir = managedDirFor(gameDir, config, platform);
  const modRoot = modInstallDirFor(gameDir, config, platform);
  if (!managedDir || !modRoot) throw new Error("TEST_SETUP: platform paths are unavailable");
  mkdirSync(managedDir, { recursive: true });
  writeFileSync(join(managedDir, "TeamSoda.Duckov.Core.dll"), "sentinel");
  const writeState = (state) => writeFileSync(join(root, ".gamer-agent.local.json"), JSON.stringify(state));
  const readyState = () => ({ gameDir, modInstallDir: modRoot, runtime: { dotnet: process.execPath, dotnetVersion: "8.0.0" } });
  writeState(readyState());

  const makeMod = (name) => {
    const dir = join(root, "your_mods", name);
    mkdirSync(join(dir, "bin", "Release"), { recursive: true });
    mkdirSync(join(dir, "obj"), { recursive: true });
    mkdirSync(join(dir, ".preview"), { recursive: true });
    mkdirSync(join(dir, "data"), { recursive: true });
    writeFileSync(join(dir, "info.ini"), `name = ${name}\ndisplayName = ${name}\ndescription = Test mod\nversion = 1.0.0\n`);
    writeFileSync(join(dir, "ModBehaviour.cs"), `namespace ${name} { public class ModBehaviour {} }\n`);
    writeFileSync(join(dir, `${name}.csproj`), "<Project />\n");
    writeFileSync(join(dir, "preview.png"), png(256, 256));
    writeFileSync(join(dir, "data", "config.json"), "{}\n");
    writeFileSync(join(dir, "ordinary.jsonl"), "{\"keep\":true}\n");
    writeFileSync(join(dir, "asset_manifest.jsonl"), "{\"exclude\":true}\n");
    writeFileSync(join(dir, ".pi-mod.json"), "{}\n");
    writeFileSync(join(dir, ".preview", "concept.png"), "exclude");
    writeFileSync(join(dir, "obj", "ignored.dll"), "exclude");
    writeFileSync(join(dir, "bin", "Release", `${name}.dll`), "built");
    writeFileSync(join(dir, "bin", "Release", "0Harmony.dll"), "dependency");
    const past = new Date(Date.now() - 10_000);
    const future = new Date(Date.now() + 10_000);
    utimesSync(join(dir, "ModBehaviour.cs"), past, past);
    utimesSync(join(dir, `${name}.csproj`), past, past);
    utimesSync(join(dir, "bin", "Release", `${name}.dll`), future, future);
    return dir;
  };

  const myMod = makeMod("MyMod");
  const first = await install("your_mods/MyMod");
  const myTarget = join(modRoot, "MyMod");
  check(
    "empty install root is created and runtime products are installed",
    first.details.ok === true && existsSync(join(myTarget, "MyMod.dll")) && existsSync(join(myTarget, "0Harmony.dll")),
    first,
  );
  check(
    "copy keeps ordinary data and JSONL but excludes source, build, preview, record, and manifest",
    existsSync(join(myTarget, "data", "config.json")) &&
      existsSync(join(myTarget, "ordinary.jsonl")) &&
      !existsSync(join(myTarget, "asset_manifest.jsonl")) &&
      !existsSync(join(myTarget, ".pi-mod.json")) &&
      !existsSync(join(myTarget, "ModBehaviour.cs")) &&
      !existsSync(join(myTarget, "MyMod.csproj")) &&
      !existsSync(join(myTarget, "bin")) &&
      !existsSync(join(myTarget, "obj")) &&
      !existsSync(join(myTarget, ".preview")),
    first,
  );

  writeFileSync(join(myTarget, "obsolete.txt"), "old");
  writeFileSync(join(myMod, "fresh.txt"), "new");
  const update = await install("your_mods/MyMod");
  check(
    "reinstall updates the recorded directory in place and removes obsolete files",
    update.details.ok === true && update.details.updated === true && update.details.targetDir === myTarget &&
      existsSync(join(myTarget, "fresh.txt")) && !existsSync(join(myTarget, "obsolete.txt")),
    update,
  );

  rmSync(myTarget, { recursive: true, force: true });
  const deleted = await install("your_mods/MyMod");
  check(
    "a record whose installed directory was deleted rebuilds the same target",
    deleted.details.ok === true && deleted.details.targetDir === myTarget && existsSync(join(myTarget, "MyMod.dll")),
    deleted,
  );

  writeFileSync(join(myTarget, "before-stale.txt"), "keep");
  const staleTime = new Date(Date.now() + 30_000);
  utimesSync(join(myMod, "ModBehaviour.cs"), staleTime, staleTime);
  const stale = await install("your_mods/MyMod");
  check(
    "stale DLL is rejected without changing the installed copy",
    stale.details.ok === false && stale.details.reason === "STALE_PRODUCT" && existsSync(join(myTarget, "before-stale.txt")),
    stale,
  );

  const conflict = makeMod("Conflict");
  mkdirSync(join(modRoot, "Conflict"), { recursive: true });
  writeFileSync(join(modRoot, "Conflict", "foreign.txt"), "keep");
  const renamed = await install("your_mods/Conflict");
  check(
    "foreign occupation uses _pimod and leaves the foreign directory untouched",
    renamed.details.ok === true && renamed.details.renamed === true &&
      renamed.details.targetDir === join(modRoot, "Conflict_pimod") &&
      readFileSync(join(modRoot, "Conflict", "foreign.txt"), "utf8") === "keep",
    renamed,
  );
  const second = pickInstallDir(modRoot, "Conflict", null, join(root, "your_mods"));
  check(
    "primary and _pimod occupation advances to _pimod2",
    second.dir === join(modRoot, "Conflict_pimod2") && second.renamed === true,
    second,
  );

  const rollbackDir = join(modRoot, "Rollback");
  mkdirSync(rollbackDir, { recursive: true });
  writeFileSync(join(rollbackDir, "old.txt"), "keep");
  let rollbackThrew = false;
  try {
    replaceInstallDir(rollbackDir, join(modRoot, "missing-staging"), join(modRoot, "Rollback.previous"));
  } catch {
    rollbackThrew = true;
  }
  check(
    "failed staging swap restores the previous installation",
    rollbackThrew && readFileSync(join(rollbackDir, "old.txt"), "utf8") === "keep" &&
      !existsSync(join(modRoot, "Rollback.previous")),
    "rollback helper did not restore the old directory",
  );

  const valid = makeMod("Valid");
  writeFileSync(
    join(valid, "build"),
    `const fs = require("node:fs");
const path = require("node:path");
const active = path.join(process.cwd(), "build-active.txt");
if (fs.existsSync(active)) fs.writeFileSync(path.join(process.cwd(), "build-overlap.txt"), "overlap");
fs.writeFileSync(active, "active");
setTimeout(() => {
  const out = path.join(process.cwd(), "bin", "Release");
  fs.mkdirSync(out, { recursive: true });
  fs.writeFileSync(path.join(out, "Valid.dll"), "fresh-build\\0TeamSoda.Duckov.Core\\0ModBehaviour");
  fs.writeFileSync(path.join(process.cwd(), "build-ran.txt"), String(Date.now()));
  fs.rmSync(active, { force: true });
}, 25);
`,
  );
  writeFileSync(join(valid, "bin", "Release", "Valid.dll"), "old-dll");
  const validResult = await validate("your_mods/Valid");
  check(
    "validate PASS comes from the build executed in this run, not an old DLL",
    validResult.details.ok === true && existsSync(join(valid, "build-ran.txt")) &&
      readFileSync(join(valid, "bin", "Release", "Valid.dll"), "utf8").includes("fresh-build"),
    validResult,
  );
  check(
    "256x256 preview has no size or PNG warning",
    !validResult.details.warnings.some((warning) => warning.includes("256x256") || warning.includes("signature/IHDR")),
    validResult,
  );

  rmSync(join(valid, "build-ran.txt"), { force: true });
  const concurrentBuilds = await Promise.all([validate("your_mods/Valid"), validate("your_mods/Valid")]);
  check(
    "concurrent validation of one csproj serializes dotnet builds",
    concurrentBuilds.every((result) => result.details.ok) && !existsSync(join(valid, "build-overlap.txt")),
    concurrentBuilds,
  );

  const buildController = new AbortController();
  buildController.abort();
  rmSync(join(valid, "build-ran.txt"), { force: true });
  rmSync(join(valid, "build-active.txt"), { force: true });
  let buildCancellationThrew = false;
  try {
    await validate("your_mods/Valid", buildController.signal);
  } catch {
    buildCancellationThrew = true;
  }
  check(
    "caller cancellation rejects instead of returning a build-failure verdict",
    buildCancellationThrew && !existsSync(join(valid, "build-ran.txt")),
  );

  writeState({ gameDir, modInstallDir: modRoot, runtime: { dotnet: process.execPath, dotnetVersion: "7.0.0" } });
  rmSync(join(valid, "build-ran.txt"), { force: true });
  const oldSdk = await validate("your_mods/Valid");
  check(
    "SDK below 8 explicitly degrades to PARTIAL without building",
    oldSdk.details.ok === false && oldSdk.details.errors.length === 0 && textOf(oldSdk).includes("PARTIAL:") &&
      oldSdk.details.warnings.some((warning) => warning.includes("too old")) &&
      !existsSync(join(valid, "build-ran.txt")),
    oldSdk,
  );
  writeState(readyState());

  const bad = makeMod("Bad");
  writeFileSync(join(bad, "info.ini"), "name = Bad\ndescription = Missing displayName\n");
  const badResult = await validate("your_mods/Bad");
  check(
    "validate FAIL fixture reports static validation errors",
    badResult.details.ok === false && badResult.details.errors.some((error) => error.includes("displayName")),
    badResult,
  );

  makeMod("Partial");
  writeState({});
  const partialResult = await validate("your_mods/Partial");
  check(
    "validate PARTIAL fixture does not treat an existing DLL as a completed build",
    partialResult.details.ok === false && partialResult.details.errors.length === 0 &&
      textOf(partialResult).includes("PARTIAL:") && textOf(partialResult).includes("An existing DLL is not proof"),
    partialResult,
  );
  writeState(readyState());

  writeFileSync(join(valid, "preview.png"), png(512, 512));
  const largePreview = await validate("your_mods/Valid");
  check(
    "512x512 preview warns without invalidating a fresh build",
    largePreview.details.ok === true && largePreview.details.warnings.some((warning) => warning.includes("512x512")),
    largePreview,
  );
  writeFileSync(join(valid, "preview.png"), "not a png");
  const damagedPreview = await validate("your_mods/Valid");
  check(
    "damaged preview warns without invalidating a fresh build",
    damagedPreview.details.ok === true && damagedPreview.details.warnings.some((warning) => warning.includes("signature/IHDR")),
    damagedPreview,
  );
  const localInstall = await install("your_mods/Valid");
  check(
    "damaged preview does not block local DLL installation",
    localInstall.details.ok === true && existsSync(join(modRoot, "Valid", "Valid.dll")),
    localInstall,
  );
} finally {
  rmSync(root, { recursive: true, force: true });
}

console.log(failed ? `FAIL: ${failed} mod-install regression check(s)` : "PASS: all mod-install regression checks");
process.exit(failed ? 1 : 0);
