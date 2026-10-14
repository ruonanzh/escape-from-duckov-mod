#!/usr/bin/env node
/**
 * check-prompt-standard-zh.mjs — machine check for the Chinese prompt rules.
 *
 * Why this script exists:
 *   `docs/duckov-harness/prompt-standard.md` (in the design repo) fixes the writing rules:
 *   only 必须 / 不得 / 可以 for modality, no arrows in prose, and one wording per concept.
 *   Those rules were re-broken several times by hand. A rule that only lives in a document
 *   comes back. This script makes three of them machine-checkable.
 *
 * What it checks (per file line, inside the standard's scope):
 *   1. modality words: 不要, 别X, 请X are banned. Use 不得 / 必须 / 可以 / 需要.
 *   2. arrows: -> <- => <= and double arrows are banned in prose. A code block may keep them.
 *   3. symbols: check mark, cross, warning sign and star are banned in prose.
 *   4. one wording per concept (the mechanically decidable part of the term table).
 *
 * Scope (prompt-standard.md section 1):
 *   AGENTS.md, .pi/skills/*\/SKILL.md, docs/**\/*.md, reference/*\/README.md.
 *   docs/api/** is a generated snapshot, so it is out of scope.
 *   Code comments are out of scope, but a markdown file can hold a fenced code block,
 *   so fences and inline code spans are stripped before matching.
 *
 * What it deliberately does NOT check, and why:
 *   The term table bans some words only for ONE concept. A grep cannot tell the concepts
 *   apart, so these stay a human review duty:
 *     贴图   banned for the in-game 2D image (write 图标), correct for a 3D texture
 *     模板   banned for the mod skeleton (write mod 骨架), correct for a Unity prefab
 *     数据   banned for what `inspect_game_data` reads (write 游戏数据), correct elsewhere
 *     API    banned for what `inspect_game_api` reads (write 游戏 api), correct elsewhere
 *     实现   banned for the same concept, correct elsewhere
 *     gameDir / repo  they are real identifiers here (the state file field, mod-repo.json)
 *
 * Usage: node scripts/check-prompt-standard-zh.mjs
 * Exit code: 0 = clean; 1 = at least one violation (each hit is printed with file:line).
 */

import { readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

/** Collect the files that prompt-standard.md section 1 puts in scope. */
function collect() {
  const out = [];
  const push = (p) => {
    try {
      if (statSync(p).isFile()) out.push(p);
    } catch {
      /* missing file: ignore */
    }
  };
  const walk = (dir) => {
    let entries;
    try {
      entries = readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries) {
      const p = join(dir, e.name);
      if (e.isDirectory()) {
        if (e.name === "api") continue; // docs/api is generated
        walk(p);
      } else if (e.name.endsWith(".md")) push(p);
    }
  };

  push(join(root, "AGENTS.md"));
  push(join(root, "README.md"));
  walk(join(root, "docs"));
  try {
    for (const d of readdirSync(join(root, ".pi", "skills"), { withFileTypes: true })) {
      if (d.isDirectory()) push(join(root, ".pi", "skills", d.name, "SKILL.md"));
    }
  } catch {
    /* no skills dir */
  }
  try {
    for (const d of readdirSync(join(root, "reference"), { withFileTypes: true })) {
      if (d.isDirectory()) push(join(root, "reference", d.name, "README.md"));
    }
  } catch {
    /* no reference dir */
  }
  return out;
}

/**
 * Blank out fenced code blocks and inline code spans, so a command output may keep its arrows.
 * Newlines are preserved so line numbers stay correct.
 */
function stripCode(text) {
  const lines = text.split("\n");
  let inFence = false;
  return lines.map((line) => {
    if (/^\s*(?:>\s?)*(?:```|~~~)/.test(line)) {
      inFence = !inFence;
      return "";
    }
    if (inFence) return "";
    // inline code spans: keep the length so columns stay put, drop the content
    return line.replace(/`[^`]*`/g, (m) => "`" + " ".repeat(Math.max(0, m.length - 2)) + "`");
  });
}

/**
 * The rules. `re` runs against the stripped line, so code is already gone.
 * Keep every hint short: it tells the writer what to use instead.
 */
const RULES = [
  // 1. modality words (prompt-standard section 2.1)
  { id: "modality", re: /(?<![要需])不要(?!紧)/g, hint: 'write "不得" (or "不需要" when it means "does not need")' },
  { id: "modality", re: /别(?:猜|用|忘|把|让|自己|再|只|去|改|写|说|丢|动|提|碰|抄)/g, hint: 'write "不得"' },
  { id: "modality", re: /请(?:用|先|把|勿|看|改|在|执行|确认|检查|确保|注意|问)/g, hint: 'write "必须" or "不得"' },

  // 2. arrows in prose (prompt-standard section 2.2)
  { id: "arrow", re: /[→←⇒⇐⇔]/g, hint: "write a full sentence; only command output keeps an arrow" },

  // 3. symbols in prose (prompt-standard section 2.2)
  { id: "symbol", re: /[✓✗⚠️⭐✅❌]/g, hint: "write a full sentence; keep symbols inside command output only" },

  // 4. one wording per concept (the decidable part of prompt-standard section 4.1)
  { id: "term", re: /用户/g, hint: 'write "玩家"' },
  { id: "term", re: /游戏安装目录/g, hint: 'write "游戏目录"' },
  { id: "term", re: /本仓(?!库)/g, hint: 'write "这个仓库" for this repo, or "工作区" for a clone' },
  { id: "term", re: /本仓库/g, hint: 'write "这个仓库"' },
  { id: "term", re: /卡片图(?!标)/g, hint: 'write "图标"' },
  { id: "term", re: /原图/g, hint: 'write "玩家参考图", or "游戏原本的图标" for the item\'s own icon' },
  { id: "term", re: /素材图/g, hint: 'write "玩家参考图"' },
  { id: "term", re: /概念图|风格图/g, hint: 'write "Tripo 预览图"' },
  { id: "term", re: /素材|底模/g, hint: 'write "glb 模型"' },
  { id: "term", re: /游戏档位|实物尺寸/g, hint: 'write "游戏内尺寸"' },
  { id: "term", re: /内容数据/g, hint: 'write "游戏数据"' },
  { id: "term", re: /模型自身原点|局部坐标/g, hint: 'write "模型局部空间"' },
  { id: "term", re: /脚手架/g, hint: 'write "mod 骨架"' },
  { id: "term", re: /名称|身份/g, hint: 'write "mod 名" for info.ini name, or "目录名"' },
  { id: "term", re: /选中/g, hint: 'write "命中", or "在界面上选它" for UI selection' },
  { id: "term", re: /匹配/g, hint: 'write "命中"' },
];

function main() {
  const files = collect();
  const hits = [];

  for (const file of files) {
    const raw = readFileSync(file, "utf8");
    const lines = stripCode(raw);
    lines.forEach((line, i) => {
      if (!line) return;
      for (const rule of RULES) {
        rule.re.lastIndex = 0;
        const m = rule.re.exec(line);
        if (m) {
          hits.push({
            file: relative(root, file),
            line: i + 1,
            rule: rule.id,
            found: m[0],
            hint: rule.hint,
          });
        }
      }
    });
  }

  const label = "prompt-standard (Chinese rules)";
  if (hits.length === 0) {
    console.log(`OK  ${label} clean  (${files.length} files scanned, ${RULES.length} rules)`);
    return 0;
  }
  console.error(`FAIL  ${label} has ${hits.length} violation(s):`);
  for (const h of hits) {
    console.error(`  ${h.file}:${h.line}  [${h.rule}]  found "${h.found}"; ${h.hint}.`);
  }
  console.error(
    "\n(The rules live in the design repo: docs/duckov-harness/prompt-standard.md. " +
      "Code blocks and inline code are skipped, so command output may keep its arrows.)",
  );
  return 1;
}

process.exit(main());
