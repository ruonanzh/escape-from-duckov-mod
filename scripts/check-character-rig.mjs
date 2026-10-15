#!/usr/bin/env node
/**
 * check-character-rig.mjs —— 校验 `libs/mod-kit/CharacterRig.cs` 的骨骼映射表。
 *
 * 为什么要有这个脚本：
 *   角色替换的成败全压在这张表上。表错一条，游戏里就是**某个部位不动**或**整块歪掉**，
 *   而且不会报错 —— 又一次「静默无效果」。表是手写的，所以要机器查。
 *
 * 实测值的唯一来源：设计仓 `docs/unity-3d-assets/10-character-replacement-research.md` §4.5
 * （探针 `CharacterProbe` 在游戏里读出来的 `DuckBody.bones`，26 个槽 / 24 根实骨 + 2 个 null）。
 * 下面 `MEASURED_DUCK_BONES` 就是那份实测名单，**按运行时 bones[] 的顺序**。
 * 游戏更新后如果骨骼变了，先重跑探针，再改这里与 `CharacterRig.cs`。
 *
 * 用法：node scripts/check-character-rig.mjs
 * 退出码：0 = 表自洽；1 = 有问题（逐条打印）
 */

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const SRC = join(root, "libs", "mod-kit", "CharacterRig.cs");

/** 实测名单（顺序 = 运行时 `bones[]` 的顺序）。来源见文件头。 */
const MEASURED_DUCK_BONES = [
  "Root", "Pelvis",
  "Spine.001", "Spine.002", "Spine.003", "Spine.004",
  "Head", "HeadTip",
  "UpperArm.L", "Elbow.L", "ForeArm.L", "Hand.L", "Hand.Soket.L",
  "UpperArm.R", "Elbow.R", "ForeArm.R", "Hand.R", "Hand.Soket.R",
  "Thigh.R", "Foot.R", "Tail", "Tail.001",
  "Thigh.L", "Foot.L",
];
/** 运行时骨骼槽总数（末尾两个是 null 槽）。 */
const MEASURED_SLOT_COUNT = 26;
/** Tripo `spec=mixamo` + `rig_type=biped` 实测给的 22 个关节（不含 `mixamorig:` 前缀）。 */
const MIXAMO_JOINTS = [
  "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
  "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
  "RightShoulder", "RightArm", "RightForeArm", "RightHand",
  "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
  "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
];

let failed = 0;
const check = (name, expected, got) => {
  const ok = JSON.stringify(expected) === JSON.stringify(got);
  if (!ok) failed += 1;
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}`);
  if (!ok) console.log(`        期望 ${JSON.stringify(expected)}，得到 ${JSON.stringify(got)}`);
};
const checkTrue = (name, cond, detail = "") => {
  if (!cond) failed += 1;
  console.log(`  ${cond ? "PASS" : "FAIL"}  ${name}${cond ? "" : "  " + detail}`);
};

const src = readFileSync(SRC, "utf8");

// ── 解析 DuckBones ──
const duckBlock = src.match(/public static readonly string\[\] DuckBones\s*=\s*\{([\s\S]*?)\};/);
if (!duckBlock) {
  console.error("FAIL  cannot find the DuckBones array in CharacterRig.cs");
  process.exit(1);
}
const duckBones = [...duckBlock[1].matchAll(/"([^"]+)"/g)].map((m) => m[1]);

// ── 解析 Table（每行一条 { "a", "b" }）──
// 声明后面隔着 `new Dictionary<...>(...)`，所以从声明处往后找第一段花括号块
const tableStart = src.indexOf("Table = new Dictionary");
const tableOpen = tableStart >= 0 ? src.indexOf("{", tableStart) : -1;
const tableClose = tableOpen >= 0 ? src.indexOf("\n        };", tableOpen) : -1;
const tableBlock = tableOpen >= 0 && tableClose > tableOpen ? [null, src.slice(tableOpen + 1, tableClose)] : null;
if (!tableBlock) {
  console.error("FAIL  cannot find the Table dictionary in CharacterRig.cs");
  process.exit(1);
}
const table = [...tableBlock[1].matchAll(/\{\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\}/g)].map((m) => [m[1], m[2]]);

// ── 解析 ExtraDuckBones ──
const extraStart = src.indexOf("ExtraDuckBones = new Dictionary");
const extraOpen = extraStart >= 0 ? src.indexOf("{", extraStart) : -1;
const extraClose = extraOpen >= 0 ? src.indexOf("\n        };", extraOpen) : -1;
const extraBlock = extraOpen >= 0 && extraClose > extraOpen ? [null, src.slice(extraOpen + 1, extraClose)] : null;
if (!extraBlock) {
  console.error("FAIL  cannot find the ExtraDuckBones dictionary in CharacterRig.cs");
  process.exit(1);
}
const extras = [...extraBlock[1].matchAll(/\{\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\}/g)].map((m) => [m[1], m[2]]);

console.log("骨骼名单（对着运行时实测值）：");
check("DuckBones 与实测名单逐项一致（含顺序）", MEASURED_DUCK_BONES, duckBones);

const slotCount = src.match(/public const int DuckSlotCount\s*=\s*(\d+);/);
check("DuckSlotCount 是 26", String(MEASURED_SLOT_COUNT), slotCount ? slotCount[1] : "(缺)");
check("槽数 = 实骨数 + 2 个 null 槽", MEASURED_SLOT_COUNT, duckBones.length + 2);

console.log("\n解析结果：");
checkTrue("解析到 DuckBones（24 项）", duckBones.length === 24, `得到 ${duckBones.length}`);
checkTrue("解析到 Table（23 条）", table.length === 23, `得到 ${table.length}`);
checkTrue("解析到 ExtraDuckBones（7 条）", extras.length === 7, `得到 ${extras.length}`);

console.log("\n映射表（对着实测的 Mixamo 名单）：");
const mapped = new Map(table);
check("Root 有映射", "Root", mapped.get("Root"));
check(
  "每个 Mixamo 关节都有映射（可带或不带 mixamorig: 前缀）",
  [],
  MIXAMO_JOINTS.filter((m) => !mapped.has(`mixamorig:${m}`) && !mapped.has(m)),
);
checkTrue("映射没有指向名单外的骨头", table.every(([, to]) => duckBones.includes(to)),
  JSON.stringify(table.filter(([, to]) => !duckBones.includes(to))));

// 运行时候选查表（与 `CharacterRig.TryMap` 的三步一致）：`mixamorig:` 前缀可有可无。
// 只试带前缀的写法会漏掉 `mixamorig:Root`（表里 `Root` 是无前缀的）—— L4。
const tryMap = (name) => {
  if (mapped.has(name)) return mapped.get(name);
  const stripped = name.includes(":") ? name.slice(name.indexOf(":") + 1) : name;
  return mapped.get(stripped) ?? mapped.get(`mixamorig:${stripped}`) ?? null;
};
check("运行时候选查表：mixamorig:Root 命中 Root", "Root", tryMap("mixamorig:Root"));
check("运行时候选查表：无前缀的 Hips 命中 Pelvis", "Pelvis", tryMap("Hips"));
check("运行时候选查表：mixamorig:LeftHand 命中 Hand.L", "Hand.L", tryMap("mixamorig:LeftHand"));
check("运行时候选查表：查不到的返回 null", null, tryMap("mixamorig:RightEye"));
checkTrue("TryMap 会用去掉前缀的名字兜底（否则 mixamorig:Root 查不到）",
  /Table\.TryGetValue\(StripPrefix\(/.test(src), "找不到 StripPrefix 兜底");

console.log("\n多出来的骨（游戏有、Mixamo 没有的）：");
checkTrue("每个多出骨都在名单里", extras.every(([from]) => duckBones.includes(from)),
  JSON.stringify(extras.filter(([from]) => !duckBones.includes(from))));
checkTrue("每个多出骨都挂到了名单里的骨头", extras.every(([, to]) => duckBones.includes(to)),
  JSON.stringify(extras.filter(([, to]) => !duckBones.includes(to))));
checkTrue("多出骨没有被映射表重复指到",
  extras.every(([from]) => !table.some(([k, to]) => to === from && k !== "Root")),
  JSON.stringify(extras.filter(([from]) => table.some(([k, to]) => to === from && k !== "Root"))));

console.log("\n覆盖度：");
const covered = new Set([...table.map(([, to]) => to), ...extras.map(([from]) => from)]);
check("24 根实骨全部被覆盖（要么有映射，要么算多出）", [], MEASURED_DUCK_BONES.filter((b) => !covered.has(b)));

console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
