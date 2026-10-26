/**
 * Tests for the dump reader shared by the three Mono repositories.
 *
 *   bun test --cwd .pi/lib decompile.test.mjs
 *
 * The cases are pure: a temporary tree for the search, and a synthetic source
 * for the member split. Each cap is checked twice, once at the limit and once
 * one past it, because a list that ended and a list that was cut must read
 * differently to the caller.
 */
import { test, expect } from "bun:test";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import {
  bodyHeader,
  checkFreshness,
  configSignature,
  dumpDir,
  extractMethod,
  memberListHeader,
  readGameVersion,
  searchHeader,
  searchTree,
  sha256File,
  takeCapped,
  versionWarnings,
} from "./decompile.ts";

test("takeCapped separates the end of a list from a cut list", () => {
  const exact = takeCapped(Array.from({ length: 50 }, (_, i) => i), 50);
  expect(exact.hits.length).toBe(50);
  expect(exact.capped).toBe(false);

  const more = takeCapped(Array.from({ length: 51 }, (_, i) => i), 50);
  expect(more.hits.length).toBe(50);
  expect(more.capped).toBe(true);
  expect(more.hits.at(-1)).toBe(49);
});

test("searchHeader says when the list was cut", () => {
  expect(searchHeader("Item", 12, 4, false)).toBe('# search "Item": 12 match(es) in 4 file(s)');
  expect(searchHeader("Item", 50, 37, true)).toBe(
    '# search "Item": 50 match(es) in 37 file(s) - capped at 50 hits, so more may match',
  );
});

test("bodyHeader says when the overload list was cut", () => {
  expect(bodyHeader("A/B.cs", "Show", 2, false)).toBe("# A/B.cs :: Show: 2 block(s)");
  expect(bodyHeader("A/B.cs", "Show", 20, true)).toBe(
    "# A/B.cs :: Show: 20 block(s) - capped at 20 blocks, so more overloads may match",
  );
});

test("memberListHeader says when the member list was cut", () => {
  expect(memberListHeader(3, false)).toBe("# members in this file: 3 listed");
  expect(memberListHeader(60, true)).toBe(
    "# members in this file: 60 listed - capped at 60 members; search for the member instead",
  );
});

test("searchTree reports one hit past the limit, so the caller can tell", () => {
  const dir = mkdtempSync(join(tmpdir(), "tree-"));
  try {
    mkdirSync(join(dir, "Game"), { recursive: true });
    writeFileSync(join(dir, "Game", "A.cs"), "hit\nhit\nhit\n");
    writeFileSync(join(dir, "Game", "B.cs"), "hit\nhit\n");

    const cut = takeCapped(searchTree(dir, "hit", 5), 4);
    expect(cut.hits.length).toBe(4);
    expect(cut.capped).toBe(true);

    const whole = takeCapped(searchTree(dir, "hit", 6), 6);
    expect(whole.hits.length).toBe(5);
    expect(whole.capped).toBe(false);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("extractMethod reports one block past the limit, so the caller can tell", () => {
  const one = (i) => `\tpublic int Foo(int a${i})\n\t{\n\t\treturn ${i};\n\t}`;
  const src = `public class T\n{\n${Array.from({ length: 25 }, (_, i) => one(i)).join("\n")}\n}`;

  const cut = takeCapped(extractMethod(src, "Foo", 21), 20);
  expect(cut.hits.length).toBe(20);
  expect(cut.capped).toBe(true);

  const few = takeCapped(extractMethod(src, "Foo", 21).slice(0, 3), 20);
  expect(few.capped).toBe(false);
});

test("checkFreshness reports unavailable and unrecorded configured assemblies", () => {
  const root = mkdtempSync(join(tmpdir(), "freshness-"));
  const previousRoot = process.env.DECOMPILED_ROOT;
  try {
    process.env.DECOMPILED_ROOT = join(root, "decompiled");
    const managed = join(root, "Managed");
    const dll = join(managed, "Present.dll");
    mkdirSync(managed, { recursive: true });
    writeFileSync(dll, "present");
    const cfg = { version: 1, runtime: "mono", dump: { assemblies: ["Present", "Missing"] } };
    const record = {
      name: "Present",
      request: "Present",
      dllPath: dll,
      sha256: sha256File(dll),
      files: 1,
      bytes: 10,
      skipped: 0,
      failed: 0,
    };
    const dir = dumpDir(root);
    mkdirSync(dir, { recursive: true });
    const manifest = { config: { signature: configSignature(cfg) }, assemblies: [record] };
    writeFileSync(join(dir, "MANIFEST.json"), JSON.stringify(manifest));

    const unavailable = checkFreshness(root, cfg, managed);
    expect(unavailable.state).toBe("stale");
    expect(unavailable.missing).toEqual(["Missing"]);
    expect(unavailable.unavailable).toEqual(["Missing"]);

    const offline = checkFreshness(root, cfg, null);
    expect(offline.state).toBe("stale");
    expect(offline.missing).toEqual(["Missing"]);
    expect(offline.unavailable).toEqual(["Missing"]);

    const coveredCfg = { version: 1, runtime: "mono", dump: { assemblies: ["Present"] } };
    manifest.config = { signature: configSignature(coveredCfg) };
    record.failed = 1;
    writeFileSync(join(dir, "MANIFEST.json"), JSON.stringify(manifest));
    const permanentFailure = checkFreshness(root, coveredCfg, managed);
    expect(permanentFailure.state).toBe("fresh");
    expect(permanentFailure.missing).toEqual([]);
    expect(permanentFailure.unavailable).toEqual([]);
    expect(permanentFailure.manifest.assemblies[0].failed).toBe(1);

    manifest.assemblies = [];
    writeFileSync(join(dir, "MANIFEST.json"), JSON.stringify(manifest));
    const unrecorded = checkFreshness(root, cfg, managed);
    expect(unrecorded.state).toBe("stale");
    expect(unrecorded.missing).toEqual(["Missing", "Present"]);
    expect(unrecorded.unavailable).toEqual(["Missing"]);
  } finally {
    if (previousRoot === undefined) delete process.env.DECOMPILED_ROOT;
    else process.env.DECOMPILED_ROOT = previousRoot;
    rmSync(root, { recursive: true, force: true });
  }
});

test("versionWarnings compares Info.ini with config and API snapshots", () => {
  const root = mkdtempSync(join(tmpdir(), "versions-"));
  try {
    const game = join(root, "game");
    const api = join(root, "docs", "api");
    mkdirSync(game, { recursive: true });
    mkdirSync(api, { recursive: true });
    const info = join(game, "Info.ini");
    writeFileSync(info, "[metadata]\nplatform = Steam\nversion = 2.3.30\n");
    writeFileSync(join(root, "mod-repo.json"), JSON.stringify({ game: { version: "2.3.29" } }));
    writeFileSync(join(api, "current.md"), "# Current\n\n> game 2.3.30 · generated 2026-10-16\n");
    writeFileSync(join(api, "old-a.md"), "# Old A\n\n> game 2.3.29 · generated 2026-10-16\n");
    writeFileSync(join(api, "old-b.md"), "# Old B\n\n> game 2.3.29 · generated 2026-10-16\n");

    expect(readGameVersion(game)).toBe("2.3.30");
    expect(versionWarnings(root, game)).toEqual([
      "WARN: installed game version 2.3.30 differs from mod-repo.json version 2.3.29.",
      "WARN: installed game version 2.3.30 differs from docs/api snapshot version 2.3.29 (2 files).",
    ]);

    rmSync(info);
    expect(readGameVersion(game)).toBeNull();
    expect(versionWarnings(root, game)).toEqual([]);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
