#!/usr/bin/env node
/**
 * Escape From Duckov mod validator.
 *
 * Usage: node lint/check_mod.mjs <mod-path>
 *
 * Validates:
 *   1. info.ini required fields + name is a valid C# namespace
 *   2. <ModName>.csproj + ModBehaviour.cs present (compile sources)
 *   3. dotnet build succeeds (when dotnet + game dir are available)
 *   4. <ModName>.dll produced (compile output)
 *
 * Contract (docs/mod-repo-guide.md §3.1): single entry, zero third-party
 * deps, read-only (writes only under your_mods via dotnet build), stdout +
 * exit code 0/1/2, ASCII output.
 */
import { readFileSync, existsSync, statSync, readdirSync } from "node:fs";
import { join, basename, dirname, resolve } from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import os from "node:os";

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, "..");
const NAME_RE = /^[A-Za-z_][A-Za-z0-9_]*$/;

function parseIni(text) {
  const result = {};
  for (const line of text.split(/\r?\n/)) {
    const m = line.match(/^\s*([^=]+?)\s*=\s*(.*)$/);
    if (m) result[m[1].trim()] = m[2].trim();
  }
  return result;
}

function loadState() {
  try {
    return JSON.parse(readFileSync(join(REPO_ROOT, ".gamer-agent.local.json"), "utf8"));
  } catch {
    return {};
  }
}

function findDotnet(state) {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [];
  if (state.dotnet) candidates.push(state.dotnet);
  candidates.push(join(os.homedir(), ".dotnet", bin));
  candidates.push("dotnet"); // PATH
  for (const c of candidates) {
    try {
      execFileSync(c, ["--version"], { encoding: "utf8" });
      return c;
    } catch {
      /* try next */
    }
  }
  return null;
}

function main() {
  if (process.argv.length !== 3) {
    console.log("Usage: node lint/check_mod.mjs <mod-path>");
    process.exitCode = 2;
    return;
  }
  const modDir = process.argv[2];
  const isDir = statSync(modDir, { throwIfNoEntry: false })?.isDirectory();
  if (!isDir) {
    console.log(`FAIL: ${modDir} is not a directory`);
    process.exitCode = 1;
    return;
  }

  const errors = [];
  const warnings = [];
  let modName = basename(modDir);

  // 1. info.ini
  const iniPath = join(modDir, "info.ini");
  if (!existsSync(iniPath)) {
    errors.push("missing info.ini");
  } else {
    const ini = parseIni(readFileSync(iniPath, "utf8"));
    if (ini.name) modName = ini.name;
    if (!ini.name) errors.push("info.ini: name is missing");
    if (!ini.displayName) errors.push("info.ini: displayName is missing");
    if (!ini.description) errors.push("info.ini: description is missing");
    if (ini.name && !NAME_RE.test(ini.name)) {
      errors.push(`info.ini: name (${ini.name}) is not a valid namespace (${NAME_RE})`);
    }
  }

  // 2. compile sources
  const csproj = readdirSync(modDir).find((f) => f.endsWith(".csproj"));
  if (!csproj) errors.push("missing <ModName>.csproj");
  if (!existsSync(join(modDir, "ModBehaviour.cs"))) errors.push("missing ModBehaviour.cs");

  // 3. compile (dotnet + game dir available)
  const state = loadState();
  const dotnet = findDotnet(state);
  if (!dotnet) {
    warnings.push("dotnet not found — skip compile check (call the check_runtime tool)");
  } else if (!state.gameDir) {
    warnings.push("game dir not found — skip compile check (call the check_runtime tool)");
  } else if (csproj) {
    try {
      execFileSync(dotnet, ["build", csproj, "-c", "Release"], {
        cwd: modDir,
        env: { ...process.env, DUCKOV_DIR: state.gameDir },
        encoding: "utf8",
        stdio: ["ignore", "pipe", "pipe"],
      });
      console.log("PASS: dotnet build succeeded");
    } catch (e) {
      errors.push(`dotnet build failed: ${String(e.stderr || e.stdout || e.message).slice(0, 600)}`);
    }
  }

  // 4. dll produced
  const dllCandidates = [join(modDir, `${modName}.dll`), join(modDir, "bin", "Release", `${modName}.dll`)];
  if (!dllCandidates.some((p) => existsSync(p))) {
    errors.push(`missing ${modName}.dll (compile first)`);
  }

  // preview.png (warn only — image asset)
  if (!existsSync(join(modDir, "preview.png"))) {
    warnings.push("missing preview.png (256x256, needed for Steam Workshop upload)");
  }

  for (const w of warnings) console.log(`WARN: ${w}`);
  if (errors.length) {
    for (const e of errors) console.log(`FAIL: ${e}`);
    process.exitCode = 1;
    return;
  }
  console.log(`PASS: ${basename(modDir)} is valid`);
  process.exitCode = 0;
}

main();
