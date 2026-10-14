#!/usr/bin/env node
/**
 * check-asset-manifest.mjs —— 校验「素材清单」的判据。
 *
 * 为什么要有这个脚本：
 *   清单是本仓给「AI 生成的图与模型」留的唯一记录
 *   （设计仓 `desktop-gamer-agent-pi/docs/unity-3d-assets/09-asset-manifest.md`）。
 *   `.pi/extensions/tripo.ts` 写它，`install_mod` 要把它挡在游戏外面。
 *   判据写错 = 记录会静默丢、或清单被装进游戏。所以把它变成机器检查。
 *
 * 它调用**同一个文件**（`.pi/lib/asset-manifest.ts`）。扩展也导入那份。判据只有一份，不会漂。
 *
 * 这里只验**不花钱**的部分（纯函数 + 落盘）。
 * 真调 Tripo API 的那两级实测见设计 §5.4 的第 1、2 项 —— 那两项要花积分。
 *
 * 用法：node --experimental-strip-types scripts/check-asset-manifest.mjs
 * 退出码：0 = 全部符合预期；1 = 有不符合
 */

import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const { MANIFEST_FILE, appendManifest, manifestRec, modRootOf, nowLocal, pickSeed, relToModRoot } = await import(
  join(here, "..", ".pi", "lib", "asset-manifest.ts")
);

let failed = 0;
const check = (name, expected, got) => {
  const ok = JSON.stringify(expected) === JSON.stringify(got);
  if (!ok) failed += 1;
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}`);
  if (!ok) console.log(`        期望 ${JSON.stringify(expected)}，得到 ${JSON.stringify(got)}`);
};

/** 一条完整的记录（用同一个底子改一处，便于比键）*/
const base = {
  endpoint: "/v3/generation/image-to-model",
  name: "ak103",
  request_id: "a1b2c3d4-0000-0000-0000-000000000000",
  parent_request_id: "3f1a2b3c-0000-0000-0000-000000000000",
  seed: 90125,
  files: ["ak103.glb"],
  input: { image: "ak103_ref.png", face_limit: 3000, texture: true, pbr: true },
};

const root = mkdtempSync(join(tmpdir(), "asset-manifest-"));
const cwd = join(root, "ws");
const modDir = join(cwd, "your_mods", "ak103_skin_v3");
const manifestPath = join(modDir, MANIFEST_FILE);
const readLines = () => readFileSync(manifestPath, "utf8").trimEnd().split("\n");

console.log("mod 根的判据（设计 §5.1 的 4 步）：");
check("产物在 your_mods/<mod>/ 下", modDir, modRootOf(cwd, join(modDir, "ak103.glb")));
check("产物在 your_mods/<mod>/.preview/ 下", modDir, modRootOf(cwd, join(modDir, ".preview", "ak103.preview.png")));
check(
  "文件直接放在 your_mods/ 下（不把文件名当 mod 名）",
  join(cwd, "your_mods"),
  modRootOf(cwd, join(cwd, "your_mods", "x.glb"))
);
check("产物在 cwd 之外", join(root, "outside"), modRootOf(cwd, join(root, "outside", "x.glb")));

console.log("相对 mod 根的路径（设计 §3.2 的路径规范）：");
check("mod 根下的文件", "ak103.glb", relToModRoot(modDir, join(modDir, "ak103.glb")));
check("子目录用正斜杠（Windows 与 mac 一致）", ".preview/ak103.render.png", relToModRoot(modDir, join(modDir, ".preview", "ak103.render.png")));

console.log("时间格式（设计 §3.2）：");
check("本地时间、秒级、不带时区", true, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/.test(nowLocal()));

console.log("记录的键（设计 §3.2 与 §3.3 的示例）：");
const success = manifestRec(base);
check("成功行 9 个键（抄的 7 个 + 新增的 provider、parent_request_id）", 9, Object.keys(success).length);
check("键与顺序跟示例一致", ["t", "provider", "endpoint", "name", "request_id", "parent_request_id", "seed", "files", "input"], Object.keys(success));
check("provider 固定 tripo", "tripo", success.provider);
check("成功行没有 error 键", false, Object.keys(success).includes("error"));
const failure = manifestRec({ ...base, files: [], error: "TRIPO_TASK_FAILED: convert timeout" });
check("失败行多一个 error（共 10 个键）", true, Object.keys(failure).includes("error") && Object.keys(failure).length === 10);

console.log("seed 的来源（设计 §3.2）：");
check("model_seed", 7, pickSeed({ model_seed: 7 }));
check("texture_seed 兜底", 8, pickSeed({ texture_seed: 8 }));
check("接口没返回就是 null", null, pickSeed({}));
check("不是数字也是 null", null, pickSeed({ model_seed: "7" }));

console.log("落盘（设计 §3.4）：");
check("目录不存在时自己建、并返回成功", null, appendManifest(join(root, "newdir"), success));
check("目录不存在时也真写了文件", true, existsSync(join(root, "newdir", MANIFEST_FILE)));
appendManifest(modDir, success);
appendManifest(modDir, success);
check("同一产物名重跑是追加（2 次写入 = 2 行）", 2, readLines().length);
check(
  "每行都是一个完整 JSON 对象",
  true,
  readLines().every((l) => {
    try {
      JSON.parse(l);
      return true;
    } catch {
      return false;
    }
  })
);
appendManifest(modDir, manifestRec({ ...base, input: { prompt: "line one\nline two" } }));
check("input 里的换行不得把一行拆成两行", 3, readLines().length);
check("换行被转义成 \\n", true, readLines()[2].includes("\\n"));
appendManifest(modDir, manifestRec({ ...base, input: { prompt: "生锈的 AK-103，左侧视图" } }));
check("中文按 UTF-8 写、能读回", true, readFileSync(manifestPath, "utf8").includes("生锈的 AK-103"));
check("清单里不得出现 base64（设计 §5.4 第 3 项）", 0, (readFileSync(manifestPath, "utf8").match(/base64/g) ?? []).length);
const blocker = join(root, "blocker");
writeFileSync(blocker, "not a directory\n");
const w = appendManifest(join(blocker, "sub"), success);
check("写失败返回一句话（不抛、不让工具报错）", true, typeof w === "string" && w.includes(MANIFEST_FILE));

console.log("清单不进游戏（设计 §3.6）：");
const installSrc = readFileSync(join(here, "..", ".pi", "extensions", "mod-install.ts"), "utf8");
check("mod-install 用同一个常量排除", true, installSrc.includes("entry.name === MANIFEST_FILE"));
check("不得按 .jsonl 扩展名排除", false, installSrc.includes('".jsonl"'));

rmSync(root, { recursive: true, force: true });
console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
