#!/usr/bin/env node
/**
 * check-mod-install-staleness.mjs —— 校验「产物是否过期」的判据。
 *
 * 为什么要有这个脚本：
 *   这个判据决定 `install_mod` 要不要拒绝安装。
 *   判太松：装进游戏的是旧产物，游戏里跑旧行为。实测踩过。
 *   判太严：工作区更新一次就要求所有 mod 重编，白干活。
 *   所以把它变成机器检查。
 *
 * 它调用**同一个函数**（`.pi/lib/mod-products.ts`）。扩展也导入那个文件。
 * 判据只有一份，不会漂。
 *
 * 用法：node --experimental-strip-types scripts/check-mod-install-staleness.mjs
 * 退出码：0 = 全部符合预期；1 = 有不符合
 */

import { mkdtempSync, mkdirSync, rmSync, utimesSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const { sourcesNewerThan } = await import(join(here, "..", ".pi", "lib", "mod-products.ts"));

const root = mkdtempSync(join(tmpdir(), "mod-staleness-"));
const modDir = join(root, "your_mods", "m1");
const dll = join(modDir, "bin", "Release", "m1.dll");
const modSrc = join(modDir, "ModBehaviour.cs");
const kitSrc = join(root, "libs", "mod-kit", "WeaponModel.cs");
const refSrc = join(root, "reference", "weapon_model", "ModBehaviour.cs");

mkdirSync(dirname(dll), { recursive: true });
mkdirSync(dirname(kitSrc), { recursive: true });
mkdirSync(dirname(refSrc), { recursive: true });
writeFileSync(modSrc, "// mod\n");
writeFileSync(kitSrc, "// kit\n");
writeFileSync(refSrc, "// template\n");
writeFileSync(dll, "dll\n");

const T = (day) => new Date(Date.UTC(2026, 0, day));
const set = (p, t) => utimesSync(p, t, t);

let failed = 0;
const check = (name, expected, setter) => {
  set(modSrc, T(1));
  set(kitSrc, T(1));
  set(refSrc, T(1));
  set(dll, T(2));
  setter();
  const got = sourcesNewerThan(dll, modDir);
  const ok = got === expected;
  if (!ok) failed += 1;
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}  （期望 ${expected}，得到 ${got}）`);
};

console.log("产物过期的判据：");
check("产物比本 mod 的源码新", false, () => {});
check("本 mod 的源码比产物新", true, () => set(modSrc, T(3)));
check("kit 比产物新", true, () => set(kitSrc, T(3)));
// 这一条就是判据收窄的理由：模板不参与编译。它变新不得要求重编。
check("模板比产物新（不得判成过期）", false, () => set(refSrc, T(3)));
check("同一时刻（1 秒余量）", false, () => set(dll, T(1)));

rmSync(root, { recursive: true, force: true });
console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
