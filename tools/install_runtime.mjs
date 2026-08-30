#!/usr/bin/env node
/**
 * Runtime install for Escape From Duckov (csharp-dll mod type).
 *
 * Prefers the user-level dotnet-install route (~/.dotnet): no admin/root,
 * no UAC/password prompt. System-level installers are listed as alternates.
 *
 * Contract (docs/mod-repo-guide.md §4):
 *   - install target MUST be user-level global (~/.dotnet), never inside workspace
 *   - exit 0 = success/already ready; non-zero = failure (print reason)
 *   - ASCII output; idempotent; zero third-party deps
 *   - install-only: we guide installation; uninstalling is the player's /
 *     package manager's job (we do NOT remove ~/.dotnet or system dotnet)
 */
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { join } from "node:path";
import os from "node:os";

function findDotnet() {
  const bin = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const tryRun = (p) => {
    try {
      return execFileSync(p, ["--version"], { encoding: "utf8" }).trim();
    } catch {
      return null;
    }
  };
  const fromPath = tryRun("dotnet");
  if (fromPath) return fromPath;
  const userLevel = join(os.homedir(), ".dotnet", bin);
  if (existsSync(userLevel)) return tryRun(userLevel);
  return null;
}

function main() {
  const v = findDotnet();
  if (v) {
    const major = parseInt(v.split(".")[0], 10);
    if (major >= 8) {
      console.log(`SKIP: dotnet SDK ${v} already present`);
      process.exitCode = 0;
      return;
    }
  }

  console.log("Install .NET SDK 8.0 — user-level install (no admin/UAC, recommended):");
  if (process.platform === "win32") {
    console.log("  PowerShell:");
    console.log('  iex "& { $(irm https://dot.net/v1/dotnet-install.ps1) } -Channel 8.0"');
    console.log("  -> installs to %USERPROFILE%\\.dotnet, no admin required");
    console.log("");
    console.log("Alternate (system-level, needs admin/UAC):");
    console.log("  winget install Microsoft.DotNet.SDK.8");
  } else if (process.platform === "darwin") {
    console.log("  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0");
    console.log("  -> installs to ~/.dotnet, no password required");
    console.log("");
    console.log("Alternate (system-level, needs password):");
    console.log("  brew install --cask dotnet-sdk");
  } else {
    console.log("  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0");
  }

  console.log("");
  console.log("Note: we only guide the install; uninstalling is up to you");
  console.log("(delete ~/.dotnet, or `brew uninstall --cask dotnet-sdk` / `winget uninstall Microsoft.DotNet.SDK.8`).");
  console.log("After installing, re-run: node tools/check_runtime.mjs");
  process.exitCode = 0;
}

main();
