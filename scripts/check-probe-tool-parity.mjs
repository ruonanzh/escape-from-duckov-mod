#!/usr/bin/env node
/**
 * check-probe-tool-parity.mjs —— 校验 **data-probe CLI** 与 **inspect_game_data 工具面** 是否对齐。
 *
 * 为什么要有这个脚本：
 *   CLI 认什么参数写在 `tools/data-probe/*.cs`（`Opt(o,"x")` / `o.ContainsKey("x")`），
 *   而 agent 能传什么参数写在 `.pi/extensions/inspect-game-data.ts`（`params.x` + `Type.Union` 的 action）。
 *   两处各写各的，**没有任何东西保证一致** —— 这个错已经犯过两次：
 *     · `9b26ae5`：CLI 加了 `export` + `match/field/rows`，扩展没跟上 ⇒ agent 用不了
 *     · 后来：CLI 加了 `--mesh/--exact/--has/refs --by`，扩展又没跟上 ⇒ 只能绕道 bash 跑 CLI
 *   靠"记得同步"是不行的 ⇒ 用这个脚本把它变成**机器检查**。
 *
 * 用法：node scripts/check-probe-tool-parity.mjs
 * 退出码：0 = 对齐；1 = 有缺口（打印缺哪个 / 多哪个）
 */

import { readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const PROBE_DIR = join(root, "tools", "data-probe");
const EXT_FILE = join(root, ".pi", "extensions", "inspect-game-data.ts");

/**
 * 扩展**自己算出来**的、不是 CLI 参数的东西（游戏路径）——
 * 它们只出现在 `params` 里、CLI 里没有对应 `--x`，属于正常的"工具面多于 CLI"。
 */
const EXTENSION_INTERNAL = new Set(["data", "managed"]);

function readProbeSources() {
  const names = readdirSync(PROBE_DIR).filter((f) => f.endsWith(".cs"));
  return names.map((f) => ({ name: f, text: readFileSync(join(PROBE_DIR, f), "utf8") }));
}

/** CLI 认的参数名（`Opt(o,"x")` 的值参数 + `o.ContainsKey("x")` 的 flag）。 */
function cliParams(sources) {
  const found = new Set();
  for (const { text } of sources) {
    for (const m of text.matchAll(/Opt\(o,\s*"([a-zA-Z]+)"\)/g)) found.add(m[1]);
    for (const m of text.matchAll(/o\.ContainsKey\("([a-zA-Z]+)"\)/g)) found.add(m[1]);
    // 可重复参数走 AllOpts(args, "x")（如 --match / --field）—— 也算 CLI 参数
    for (const m of text.matchAll(/AllOpts\(args,\s*"([a-zA-Z]+)"\)/g)) found.add(m[1]);
  }
  return found;
}

/** CLI 支持的 action：从 `Usage()` 里的 `--action <a|b|c>` 读（比抓 switch 更稳）。 */
function cliActions(sources) {
  // 不能取第一个匹配：文件头注释里也有 `--action <a>` 占位符，取「分支最多」的那个才准
  let best = "";
  for (const { text } of sources)
    for (const m of text.matchAll(/--action\s*<([a-z|]+)>/g))
      if (m[1].split("|").length > best.split("|").length) best = m[1];
  return new Set(best.split("|").filter(Boolean));
}

/** 工具面暴露的参数：扩展里用到的 `params.x`。 */
function extParams(text) {
  const found = new Set();
  for (const m of text.matchAll(/params\.([a-zA-Z]+)/g)) found.add(m[1]);
  return found;
}

/** 工具面支持的 action：`Type.Union` 里的 `Type.Literal("x")`。 */
function extActions(text) {
  const found = new Set();
  for (const m of text.matchAll(/Type\.Literal\("([a-z_]+)"\)/g)) found.add(m[1]);
  return found;
}

function diff(a, b) {
  return [...a].filter((x) => !b.has(x)).sort();
}

function main() {
  const sources = readProbeSources();
  const extText = readFileSync(EXT_FILE, "utf8");

  const problems = [];

  const cliP = cliParams(sources);
  const extP = extParams(extText);
  const missingParams = diff(cliP, new Set([...extP, ...EXTENSION_INTERNAL]));
  const extraParams = diff(new Set([...extP].filter((p) => !EXTENSION_INTERNAL.has(p))), cliP);

  const cliA = cliActions(sources);
  const extA = extActions(extText);
  const missingActions = diff(cliA, extA);
  const extraActions = diff(extA, cliA);

  if (missingParams.length)
    problems.push(
      `CLI 认这些参数、但工具面暴露不了（agent 传不进去）：${missingParams.join(", ")}\n` +
        `  -> 在 ${EXT_FILE.replace(root + "/", "")} 的 parameters 里加声明 + execute 里加 args.push`,
    );
  if (missingActions.length)
    problems.push(
      `CLI 支持这些 action、但工具面的 Type.Union 里没有：${missingActions.join(", ")}\n` +
        `  -> 在 inspect-game-data.ts 的 Type.Union 里加 Type.Literal("<action>")`,
    );
  if (extraParams.length)
    problems.push(`工具面暴露了 CLI 不认的参数（传下去会报 unknown option）：${extraParams.join(", ")}`);
  if (extraActions.length)
    problems.push(`工具面暴露了 CLI 不支持的 action：${extraActions.join(", ")}`);

  const label = "data-probe CLI <-> inspect_game_data";
  if (problems.length === 0) {
    console.log(
      `OK  ${label} 对齐  ` +
        `(params ${cliP.size} 个 · actions ${cliA.size} 个 · 扩展内部 ${EXTENSION_INTERNAL.size} 个已白名单)`,
    );
    return 0;
  }
  console.error(`FAIL  ${label} 有缺口：`);
  for (const p of problems) console.error("  - " + p);
  console.error('\n（这份检查就是为了防「加了 CLI 忘了扩展」这种重演）');
  return 1;
}

process.exit(main());
