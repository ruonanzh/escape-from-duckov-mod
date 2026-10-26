import { existsSync, readFileSync, rmSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { probeDotnet } from "../.pi/lib/dotnet.ts";
import { discoverGameDir, managedDirFor, platformKey, readModRepoConfig, readState } from "../.pi/lib/game-paths.ts";
import { readInfoFields } from "../.pi/lib/mod-identity.ts";
import { runAsync } from "../.pi/lib/proc.ts";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dotnet = probeDotnet();
if (!dotnet.found || !dotnet.path || (dotnet.major ?? 0) < 8) {
  console.log("SKIP: .NET SDK >= 8 is unavailable; reference build checks did not run");
  process.exit(0);
}
const config = readModRepoConfig(repoRoot);
const platform = platformKey();
const gameDir = discoverGameDir(config, readState(repoRoot), platform).gameDir;
const managedDir = gameDir ? managedDirFor(gameDir, config, platform) : null;
if (!gameDir || !managedDir || !existsSync(join(managedDir, "TeamSoda.Duckov.Core.dll"))) {
  console.log("SKIP: game DLLs are unavailable; reference build checks did not run");
  process.exit(0);
}

const gameVersion = readInfoFields(join(gameDir, "Info.ini")).version ?? "unknown";
console.log(`Environment: dotnet ${dotnet.version}; game ${gameVersion}; managed ${managedDir}`);

const references = [
  ["example_mod", "ExampleMod"],
  ["weapon_model", "WeaponModelSwap"],
  ["item_model", "ItemModelSwap"],
  ["character_model", "CharacterModelSwap"],
];
let failed = 0;
const check = (name, condition, detail = "") => {
  console.log(`${condition ? "PASS" : "FAIL"}  ${name}`);
  if (!condition) {
    failed += 1;
    if (detail) console.log(detail);
  }
};

for (const [directory, assembly] of references) {
  const modDir = join(repoRoot, "reference", directory);
  const project = join(modDir, `${assembly}.csproj`);
  const dll = join(modDir, "bin", "Release", `${assembly}.dll`);
  rmSync(dll, { force: true });
  try {
    await runAsync(dotnet.path, ["build", project, "-c", "Release"], {
      cwd: modDir,
      env: { ...process.env, DUCKOV_DIR: gameDir, DOTNET_CLI_TELEMETRY_OPTOUT: "1" },
      timeoutMs: 300_000,
    });
    const bytes = existsSync(dll) ? readFileSync(dll) : Buffer.alloc(0);
    check(
      `${directory} builds a fresh DLL against the installed game`,
      bytes.includes("ModBehaviour") && bytes.includes("TeamSoda.Duckov.Core"),
    );
  } catch (error) {
    check(`${directory} builds a fresh DLL against the installed game`, false, error.stderr || error.stdout || error.message);
  }

  const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: "1" };
  delete env.DUCKOV_DIR;
  try {
    const result = await runAsync(dotnet.path, ["build", project, "-c", "Release", "--no-restore"], {
      cwd: modDir,
      env,
      timeoutMs: 300_000,
    });
    check(`${directory} negative fixture rejects a missing DUCKOV_DIR`, false, result.stdout || "build unexpectedly passed");
  } catch (error) {
    const output = `${error.stdout ?? ""}\n${error.stderr ?? ""}\n${error.message ?? ""}`;
    check(
      `${directory} negative fixture rejects a missing DUCKOV_DIR`,
      output.includes("DUCKOV_DIR is not set"),
      output.slice(0, 1200),
    );
  }
}

console.log(failed ? `FAIL: ${failed} reference build check(s)` : "PASS: all reference build checks");
process.exit(failed ? 1 : 0);
