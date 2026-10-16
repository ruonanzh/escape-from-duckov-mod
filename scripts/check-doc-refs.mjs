#!/usr/bin/env node
/**
 * 文档引用自检 —— 收藏我们踩过的一类坑：
 *
 *  A. **markdown 链接**：`[..](x.md)` 按**文件相对路径**解析，必须存在。
 *     踩过的坑：`.pi/skills/<skill>/SKILL.md` 里写 `../../docs/mod-kit-api.md`。
 *     从 `.pi/skills/add-item/` 往上两级是 `.pi/`，于是解析成 `.pi/docs/mod-kit-api.md` ——
 *     这个文件不存在，正确写法是 `../../../docs/mod-kit-api.md`。同类错在 4 份 SKILL 里出现过 5 次，
 *     在 `editors/<name>/README.md` 里出现过 2 次（指向设计仓的文件）。
 *
 *  B. **仓库根相对的路径**（反引号里那种）：`docs/…`、`scripts/…`、`libs/…`、`.pi/…` 等开头、
 *     并且带扩展名的，按**仓库根**解析，存在才通过。
 *     本仓只有这一种约定（引用别的仓库时写那边的相对路径，但那种通常带 `../` 或仓库名），
 *     所以这一条误报很低。**代码块里不查**（命令与游戏路径不属于这个约定）。
 *
 * 为什么**不**查 `../` 开头的反引号路径：那是我方两种合法写法的交集区（见上），机械查会误报。
 *
 * 用法：node scripts/check-doc-refs.mjs   （exit 1 = 有问题）
 */
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";

const root = resolve(import.meta.dirname, "..");
/** 不查的目录：工作区（app 托管，内容不可控）、构建产物、依赖 */
const SKIP_DIRS = new Set([".git", "node_modules", "your_mods", "bin", "obj"]);
/** 反引号路径里认得的仓库顶层目录 */
const REPO_TOP_DIRS = ["docs", "scripts", "libs", "reference", "editors", "tools", ".pi", "templates"];
/** 行内写明是别的仓库时，`docs/…` 这类路径按那边的仓库算，本仓不查 */
const OTHER_REPO = /设计仓|desktop-gamer-agent-pi|duckov 仓|eu5 仓|pi-desktop/;

const problems = [];
const files = [];
(function walk(dir) {
  for (const e of readdirSync(dir, { withFileTypes: true })) {
    if (e.isDirectory()) {
      if (SKIP_DIRS.has(e.name)) continue;
      walk(join(dir, e.name));
    } else if (e.name.endsWith(".md")) {
      files.push(join(dir, e.name));
    }
  }
})(root);

/** 把代码围栏（``` 与 ~~~）去掉，保留行数，免得规则 B 去查命令与示例 */
function stripFences(text) {
  const out = [];
  let inFence = false;
  let marker = "";
  for (const line of text.split("\n")) {
    const m = /^\s*(```+|~~~+)/.exec(line);
    if (m) {
      if (!inFence) {
        inFence = true;
        marker = m[1][0];
      } else if (m[1][0] === marker) {
        inFence = false;
      }
      out.push("");
      continue;
    }
    out.push(inFence ? "" : line);
  }
  return out.join("\n");
}

for (const f of files) {
  const text = readFileSync(f, "utf8");
  const where = relative(root, f);

  // A. markdown 链接
  for (const m of text.matchAll(/\[[^\]]*\]\(([^)\s]+\.md)(#[^)]*)?\)/g)) {
    const target = m[1];
    if (/^(https?:|\/\/|mailto:)/.test(target)) continue;
    if (!existsSync(resolve(dirname(f), target))) {
      problems.push(`A 断链：${where} → ${target}（解析到 ${relative(root, resolve(dirname(f), target))}）`);
    }
  }

  // B. 反引号里的仓库根相对路径
  //    写别的仓库的文件时，行内会先写明是哪个仓库（例如「设计仓 `docs/…`」）—— 那种跳过，
  //    否则本仓与别的仓库都用 `docs/` 开头，会互相误报。
  const naked = stripFences(text);
  const nakedLines = naked.split("\n");
  for (let i = 0; i < nakedLines.length; i++) {
    const line = nakedLines[i];
    // 中文会折行，「设计仓」常在上一行末尾 —— 一起看
    if (OTHER_REPO.test(line) || OTHER_REPO.test(nakedLines[i - 1] ?? "")) continue;
    for (const m of line.matchAll(/`([A-Za-z0-9_./-]+\.(?:md|mjs|py|cs|json|ini|ts|png|glb|jsonl))`/g)) {
      const p = m[1];
      if (p.includes("..")) continue; // 相对路径，另一种合法写法
      const top = p.split("/")[0];
      if (!REPO_TOP_DIRS.includes(top)) continue;
      if (!existsSync(join(root, p))) {
        problems.push(`B 路径不存在：${where} → \`${p}\``);
      }
    }
  }
}

if (problems.length) {
  for (const p of problems) console.error("  ❌ " + p);
  console.error(`  共 ${problems.length} 处`);
  process.exit(1);
}
console.log(`  ✅ 文档引用自检通过（${files.length} 个 md 文件；规则：markdown 链接可达 + 反引号里的仓库根路径存在）`);
