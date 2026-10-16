#!/usr/bin/env node
/**
 * mod-kit 接口台账自检：文档里 `Class.Member` 写的东西，代码里必须真的存在。
 *
 * 为什么需要它：`check-doc-refs.mjs` 只查**路径**，查不到**接口名**。
 * 踩过的坑：`docs/mod-kit-api.md` 写了 `CharacterSwap.Install()`，还配了一句「`ModBehaviour` 里调一次」。
 * 实际代码里 `Install()` 是嵌套私有类 `CharacterSwap.ModelHooks` 的成员（`CharacterSwap.cs:134`），
 * 对外的 `CharacterSwap.Install` 不存在；而且不需要谁去调 —— 第一次 `Apply` 内部
 * （`if (!_hooked)`）自己调 `ItemHooks.Install()` 与 `ModelHooks.Install()`。
 * 名字错 + 用法错，两条都没人拦。
 *
 * 判据：把 `libs/mod-kit/*.cs` 的每个类型**第一层**的声明名收下来（嵌套类的成员不算外层类型的成员，
 * 所以 `ModelHooks.Install` 不会被记成 `CharacterSwap.Install`），再拿文档里
 * `类型.成员` 的写法去对。类型不在 mod-kit 里（游戏自己的类，例如 `Item.Repairable`）不查。
 *
 * 扫描范围：本仓会引用 mod-kit 接口的文档（台账、cookbook、AGENTS、技能、参考 mod 的 README）。
 *
 * 已知边界（实测 2026-10-16；当前仓库收下 323 个成员、无污染，以下都是「可能漏」的方向）：
 * - 只剥 `//` 行注释；C 风格块注释与字符串字面量原样保留。`declName` 取行内**最后一个**
 *   标识符，所以同一行的块注释里写了标识符（例如 catch 后接一个块注释、注释里写 ignore）
 *   会被记成成员。关键字本身（`catch` / `try` / `finally` …）在 NOT_A_NAME 里，不会被记。
 * - 一行多个声明只记一个：`int a, b;` 记的是 `b`（枚举的多行写法 `a,` 才取行首）。
 * - `Type.A.B` 只校验到 `Type.A`，第三段不查（正则只捕两段）。
 * - 只认 `libs/mod-kit/` 里的类型：游戏侧接口（`ItemUtilities`、`Teams` …）不受本脚本保护，
 *   手抄的游戏侧签名靠 docs/api 的版本戳与人工复核。
 *
 * 用法：node scripts/check-mod-kit-api.mjs   （exit 1 = 有问题）
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const KIT_DIR = join(root, "libs", "mod-kit");

/** 非声明名：修饰符、语句关键字、字面量 */
const NOT_A_NAME = new Set([
  "public", "private", "protected", "internal", "static", "readonly", "const", "sealed", "abstract",
  "override", "virtual", "partial", "new", "get", "set", "using", "namespace", "return", "if", "else",
  "try", "catch", "finally", "foreach", "for", "while", "switch", "case", "break", "continue", "throw",
  "class", "struct", "enum", "interface", "void", "var", "true", "false", "null", "this", "base",
  "out", "ref", "in", "async", "await", "yield", "operator", "implicit", "explicit", "event", "delegate",
]);

/** 取一个类型的**第一层**文本行（嵌套块的整段丢掉，免得把嵌套类成员算成外层成员） */
function firstLevelLines(body) {
  const keep = [];
  let depth = 0;
  for (const raw of body.split("\n")) {
    const line = raw.replace(/\/\/.*$/, "");
    if (depth === 0) keep.push(line);
    depth += (line.match(/\{/g) ?? []).length;
    depth -= (line.match(/\}/g) ?? []).length;
    if (depth < 0) depth = 0;
  }
  return keep;
}

/** 从一个第一层行里取声明名：方法取 `(` 前的名字，属性/字段截掉 `=` 与 `=>`，枚举成员取逗号前的名字 */
function declName(line) {
  const t = line.trim();
  if (!t || /^[[#/*]/.test(t)) return null;
  const paren = t.indexOf("(");
  const arrow = t.indexOf("=>");
  const eq = t.search(/(?<![=!<>])=(?!=)/);
  const cuts = [paren, arrow, eq].filter((x) => x >= 0);
  const head = cuts.length ? t.slice(0, Math.min(...cuts)) : t;
  const ids = head.match(/[A-Za-z_][A-Za-z0-9_]*/g) ?? [];
  if (!ids.length) return null;
  // 行尾是 `,` 且这一行没有括号时，才算「枚举成员/多个字段」那种写法。
  // 有括号时行尾的逗号只是**多行签名**换行（`void Foo(int a,` 的下一行还有参数）。
  const name = paren < 0 && /,\s*$/.test(t) ? ids[0] : ids[ids.length - 1];
  return NOT_A_NAME.has(name) ? null : name;
}

/** type -> Set(member)，来自 libs/mod-kit/*.cs */
const types = new Map();
for (const f of readdirSync(KIT_DIR)) {
  if (!f.endsWith(".cs")) continue;
  const text = readFileSync(join(KIT_DIR, f), "utf8");
  const re = /\b(class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)/g;
  let m;
  while ((m = re.exec(text))) {
    const open = text.indexOf("{", re.lastIndex - 1);
    if (open < 0) continue;
    let depth = 0, end = -1;
    for (let j = open; j < text.length; j++) {
      if (text[j] === "{") depth++;
      else if (text[j] === "}" && --depth === 0) { end = j; break; }
    }
    if (end < 0) continue;
    const name = m[2];
    if (!types.has(name)) types.set(name, new Set());
    for (const line of firstLevelLines(text.slice(open + 1, end))) {
      const n = declName(line);
      if (n) types.get(name).add(n);
    }
  }
}

/** 会引用 mod-kit 接口的文档 */
function docs() {
  const out = [join(root, "AGENTS.md")];
  for (const d of [join(root, "docs"), join(root, "reference"), join(root, ".pi", "skills")]) {
    (function walk(dir) {
      for (const e of readdirSync(dir, { withFileTypes: true })) {
        const p = join(dir, e.name);
        if (e.isDirectory()) walk(p);
        else if (e.name.endsWith(".md")) out.push(p);
      }
    })(d);
  }
  return out.filter((f) => {
    try { return statSync(f).isFile(); } catch { return false; }
  });
}

const problems = [];
let checked = 0;
for (const f of docs()) {
  const text = readFileSync(f, "utf8");
  const seen = new Set();
  for (const m of text.matchAll(/`([A-Z][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)/g)) {
    const [, type, member] = m;
    if (!types.has(type)) continue;
    if (/^(cs|md|json|jsonl|png|glb|dll|ini|assets|exe|mjs|ts)$/.test(member)) continue;
    const key = type + "." + member;
    if (seen.has(key)) continue;
    seen.add(key);
    checked++;
    if (!types.get(type).has(member)) {
      problems.push(`${relative(root, f)} 写了 \`${key}\`，但 ${type} 的第一层里没有这个成员`);
    }
  }
}

if (problems.length) {
  for (const p of problems) console.error("  ❌ " + p);
  console.error(`  共 ${problems.length} 处`);
  process.exit(1);
}
console.log(`  ✅ mod-kit 接口台账自检通过（${types.size} 个类型；核对了 ${checked} 处 \`类型.成员\`）`);
