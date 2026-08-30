#!/usr/bin/env node
/**
 * Runtime check for Escape From Duckov (csharp-dll mod type).
 *
 * Checks, in order:
 *   1. dotnet SDK present (needed to compile the mod DLL)
 *   2. Game install dir discoverable (needed to reference game DLLs)
 *
 * Contract (docs/mod-repo-guide.md §4):
 *   - exit 0 = ready; non-zero = missing (print what + how to fix)
 *   - ASCII output; idempotent; zero third-party deps (Node built-ins only)
 *
 * Discovery result is cached to <repo>/.gamer-agent.local.json (gitignored),
 * so the next run reuses it without re-probing.
 */
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import os from "node:os";
import path from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, "..");
const STATE_FILE = join(REPO_ROOT, ".gamer-agent.local.json");

const PLATFORM = process.platform === "win32" ? "windows" : process.platform === "darwin" ? "mac" : "linux";

function loadConfig() {
  return JSON.parse(readFileSync(join(REPO_ROOT, "mod-repo.json"), "utf8"));
}

function loadState() {
  try {
    return JSON.parse(readFileSync(STATE_FILE, "utf8"));
  } catch {
    return {};
  }
}

function saveState(state) {
  writeFileSync(STATE_FILE, `${JSON.stringify(state, null, 2)}\n`);
}

function expandHome(p) {
  if (p.startsWith("~/") || p === "~") {
    return join(os.homedir(), p.slice(2));
  }
  return p;
}

/** Probe game install dir: hint first, then verify Managed dir contains a game-owned DLL. */
function discoverGameDir(cfg) {
  const hints = [];
  const hint = cfg.game?.installDirHint?.[PLATFORM];
  if (hint) hints.push(expandHome(hint));

  for (const dir of hints) {
    const managed = join(dir, cfg.compile?.managedDir?.[PLATFORM] ?? "Duckov_Data/Managed");
    if (existsSync(join(managed, "TeamSoda.Duckov.Core.dll"))) {
      return { gameDir: dir, managedDir: managed };
    }
  }
  return null;
}

function findDotnet() {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [
    join(os.homedir(), ".dotnet", bin), // dotnet-install.sh/ps1 default (user-level)
  ];
  if (process.platform === "win32") {
    candidates.push("C:\\Program Files\\dotnet\\dotnet.exe"); // system-level
    if (process.env.LOCALAPPDATA) candidates.push(join(process.env.LOCALAPPDATA, "Microsoft", "dotnet", "dotnet.exe"));
  } else if (process.platform === "darwin") {
    candidates.push("/usr/local/share/dotnet/dotnet"); // brew cask
  }

  const tryRun = (p) => {
    try {
      return execFileSync(p, ["--version"], { encoding: "utf8" }).trim();
    } catch {
      return null;
    }
  };

  // 1. PATH 里的 dotnet（系统级安装通常已加 PATH）
  const fromPath = tryRun("dotnet");
  if (fromPath) return { path: "dotnet", version: fromPath };

  // 2. 常见安装位置
  for (const c of candidates) {
    if (!existsSync(c)) continue;
    const v = tryRun(c);
    if (v) return { path: c, version: v };
  }
  return null;
}

function checkDotnet() {
  const found = findDotnet();
  if (!found) return { ok: false, version: null, path: null };
  const major = parseInt(found.version.split(".")[0], 10);
  return { ok: !Number.isNaN(major) && major >= 8, version: found.version, path: found.path };
}

function main() {
  const cfg = loadConfig();
  const state = loadState();
  const problems = [];

  // 1. dotnet
  const dotnet = checkDotnet();
  if (!dotnet.ok) {
    problems.push(
      dotnet.version
        ? `FAIL: dotnet SDK ${dotnet.version} is too old (need >= 8.0)`
        : "FAIL: dotnet SDK not found. Run `node tools/install_runtime.mjs` for install instructions.",
    );
  }

  // 2. game dir
  let found = state.gameDir ? discoverGameDir(cfg) ?? null : null;
  if (!found) {
    found = discoverGameDir(cfg);
  }
  if (found) {
    state.gameDir = found.gameDir;
    state.managedDir = found.managedDir;
    state.modInstallDir = join(found.gameDir, cfg.modInstall?.path?.[PLATFORM] ?? "");
    state.runtime = { dotnet: dotnet.path, dotnetVersion: dotnet.version };
    saveState(state);
  } else {
    problems.push(
      `FAIL: game install dir not found (${cfg.game?.name}). Install the game via Steam, then re-run. Expected near: ${cfg.game?.installDirHint?.[PLATFORM] ?? "(no hint)"}`,
    );
  }

  if (problems.length) {
    for (const p of problems) console.log(p);
    process.exitCode = 1;
    return;
  }

  console.log(`PASS: dotnet ${dotnet.version} (${dotnet.path}); gameDir ${state.gameDir}`);
  process.exitCode = 0;
}

main();
