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
  extractMethod,
  memberListHeader,
  searchHeader,
  searchTree,
  takeCapped,
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
