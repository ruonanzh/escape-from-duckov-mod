#!/usr/bin/env node
/**
 * measure-skeleton-gap.mjs —— 量「模型骨架」与「游戏骨架」差多少。
 *
 * 为什么要有它：
 *   角色替换能不能用「把每个部件搬到角色对应骨上」那条路，取决于两副骨架差多少。
 *   这个差**随模型变**，不是常数（实测：鸭猫最大位移 0.478 m，穿靴子的猫 0.816 m），
 *   所以每个新模型都要量一次。判据只有一份（`libs/mod-kit/CharacterSwap.cs` 的
 *   `TryBodyAlignment`），这里按同一套算法复算，用来给 agent 一个**确定指令**。
 *
 * 用法：
 *   node scripts/measure-skeleton-gap.mjs <模型的 glb> [鸭子的绑定姿势 dump]
 *
 *   绑定姿势 dump 用 data-probe 生成（离线，不必进游戏）：
 *   data-probe --managed "<游戏>/Managed" --data "<游戏>/Data" \
 *     --action dump --class Mesh --name DuckBody --exact --depth 4 --out /tmp/duckbody.txt
 *
 *   不给第二个参数时默认读 /tmp/duckbody.txt。
 *
 * 输出两列：
 *   位移 = 模型那根骨与角色那根骨的绑定位置差（米，经过整体对齐之后）。位移越大，
 *          「搬到角色骨上」这条路对形状的破坏越大。
 *   朝向偏差 = 模型骨与角色骨的局部朝向差（度）。这一列大而整齐时，说明两副骨架的
 *          骨轴约定不同，不能直接搬（需要先按 `K_b = R_tᵀ·R_A·R_s` 求对应）。
 *
 * 退出码：0 = 量完；1 = 输入有问题（缺文件、缺关节、缺地标）。
 */

import { readFileSync } from "node:fs";
import { basename } from "node:path";

const DUCK_BONES = [
  "Root", "Pelvis", "Spine.001", "Spine.002", "Spine.003", "Spine.004", "Head", "HeadTip",
  "UpperArm.L", "Elbow.L", "ForeArm.L", "Hand.L", "Hand.Soket.L",
  "UpperArm.R", "Elbow.R", "ForeArm.R", "Hand.R", "Hand.Soket.R",
  "Thigh.R", "Foot.R", "Tail", "Tail.001", "Thigh.L", "Foot.L",
];
// 与 `libs/mod-kit/CharacterRig.cs` 的 Table 一致。改那边必须改这边（有自检盯着）。
const TABLE = {
  Root: "Root",
  "mixamorig:Hips": "Pelvis",
  "mixamorig:Spine": "Spine.001",
  "mixamorig:Spine1": "Spine.002",
  "mixamorig:Spine2": "Spine.003",
  "mixamorig:Neck": "Spine.004",
  "mixamorig:Head": "Head",
  "mixamorig:LeftShoulder": "Spine.004",
  "mixamorig:LeftArm": "UpperArm.L",
  "mixamorig:LeftForeArm": "ForeArm.L",
  "mixamorig:LeftHand": "Hand.L",
  "mixamorig:RightShoulder": "Spine.004",
  "mixamorig:RightArm": "UpperArm.R",
  "mixamorig:RightForeArm": "ForeArm.R",
  "mixamorig:RightHand": "Hand.R",
  "mixamorig:LeftUpLeg": "Thigh.L",
  "mixamorig:LeftLeg": "Foot.L",
  "mixamorig:LeftFoot": "Foot.L",
  "mixamorig:LeftToeBase": "Foot.L",
  "mixamorig:RightUpLeg": "Thigh.R",
  "mixamorig:RightLeg": "Foot.R",
  "mixamorig:RightFoot": "Foot.R",
  "mixamorig:RightToeBase": "Foot.R",
};

const [glbPath, dumpPath = "/tmp/duckbody.txt"] = process.argv.slice(2);
if (!glbPath) {
  console.error("usage: node scripts/measure-skeleton-gap.mjs <model.glb> [duckbody dump]");
  process.exit(1);
}

// ── 读鸭子的绑定姿势（data-probe 的 dump）──
function readDuckDump(path) {
  const text = readFileSync(path, "utf8");
  const start = text.indexOf("m_BindPose");
  const stop = text.indexOf("m_BoneNameHashes");
  if (start < 0 || stop < 0) throw new Error(`no m_BindPose in ${path}`);
  const mats = [];
  let cur = new Map();
  for (const line of text.slice(start, stop).split("\n")) {
    const m = line.match(/e(\d)(\d)\s*=\s*(\S+)/);
    if (!m) continue;
    cur.set(`${m[1]}${m[2]}`, Number(m[3]));
    if (cur.size === 16) {
      mats.push([0, 1, 2, 3].map((r) => [0, 1, 2, 3].map((c) => cur.get(`${r}${c}`))));
      cur = new Map();
    }
  }
  // 网格包围盒（m_LocalAABB）：头的占比要用它当分母，不能用骨架最高点
  // （Mixamo 那套骨架的最高关节往往就是头关节本身，用它当分母会算出 100%）
  const aabb = text.slice(text.indexOf("m_LocalAABB"));
  const num = (key) => {
    const m = aabb.match(new RegExp(`${key}\\s*=\\s*(-?[0-9.eE+-]+)`));
    return m ? Number(m[1]) : null;
  };
  const center = [num("x"), num("y"), num("z")];
  const extent = [...aabb.matchAll(/x = (-?[0-9.eE+-]+)\s*\n\s*y = (-?[0-9.eE+-]+)\s*\n\s*z = (-?[0-9.eE+-]+)/g)]
    .map((m) => [Number(m[1]), Number(m[2]), Number(m[3])])[1] ?? null;
  return { mats, aabb: center[0] !== null && extent ? { min: center.map((c, i) => c - extent[i]), max: center.map((c, i) => c + extent[i]) } : null };
}

// ── 4x4 求逆（仿射，高斯-约当）──
function inv4(M) {
  const A = M.map((row, i) => [...row, ...[0, 1, 2, 3].map((j) => (i === j ? 1 : 0))]);
  for (let c = 0; c < 4; c += 1) {
    let p = c;
    for (let r = c + 1; r < 4; r += 1) if (Math.abs(A[r][c]) > Math.abs(A[p][c])) p = r;
    [A[c], A[p]] = [A[p], A[c]];
    const pv = A[c][c];
    A[c] = A[c].map((x) => x / pv);
    for (let r = 0; r < 4; r += 1) {
      if (r === c || A[r][c] === 0) continue;
      const f = A[r][c];
      A[r] = A[r].map((x, k) => x - f * A[c][k]);
    }
  }
  return A.map((row) => row.slice(4));
}
const posOf = (M) => [M[0][3], M[1][3], M[2][3]];
const rot3 = (R) => [0, 1, 2].map((i) => [0, 1, 2].map((j) => R[i][j]));
const mul3 = (A, B) => A.map((r) => B[0].map((_, j) => r.reduce((s, x, k) => s + x * B[k][j], 0)));
const mul4 = (A, B) => A.map((r) => B[0].map((_, j) => r.reduce((s, x, k) => s + x * B[k][j], 0)));
const apply4 = (M, v) => M.slice(0, 3).map((r) => r.reduce((s, x, k) => s + x * v[k], 0) + r[3]);
const mv3 = (R, v) => R.map((r) => r.reduce((s, x, k) => s + x * v[k], 0));
const sub = (a, b) => a.map((x, i) => x - b[i]);
const len = (v) => Math.sqrt(v.reduce((s, x) => s + x * x, 0));
const dot = (a, b) => a.reduce((s, x, i) => s + x * b[i], 0);
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const unit = (v) => { const L = len(v) || 1; return v.map((x) => x / L); };

// 3x3 转 4x4（列 = 左 / 上 / 前）
function frame(colLeft, colUp, colFwd) {
  return [
    [colLeft[0], colUp[0], colFwd[0], 0],
    [colLeft[1], colUp[1], colFwd[1], 0],
    [colLeft[2], colUp[2], colFwd[2], 0],
    [0, 0, 0, 1],
  ];
}

// ── 读模型的 glb：只要关节名与逆绑定矩阵 ──
function readGlb(path) {
  const buf = readFileSync(path);
  if (buf.readUInt32LE(0) !== 0x46546c67) throw new Error("not a glb");
  let off = 12;
  let json = null;
  let bin = null;
  while (off + 8 <= buf.length) {
    const len = buf.readUInt32LE(off);
    const type = buf.readUInt32LE(off + 4);
    const body = buf.subarray(off + 8, off + 8 + len);
    if (type === 0x4e4f534a) json = JSON.parse(body.toString("utf8"));
    else if (type === 0x004e4942) bin = body;
    off += 8 + len;
  }
  if (!json) throw new Error("no json chunk");
  const skin = json.skins?.[0];
  if (!skin) throw new Error("no skin");
  const nodes = json.nodes ?? [];
  const acc = json.accessors[skin.inverseBindMatrices];
  const view = json.bufferViews[acc.bufferView];
  const base = (view.byteOffset ?? 0) + (acc.byteOffset ?? 0);
  const mats = [];
  for (let i = 0; i < acc.count; i += 1) {
    const f = [];
    for (let k = 0; k < 16; k += 1) f.push(bin.readFloatLE(base + i * 64 + k * 4));
    // glTF 列主序 → 行主序
    mats.push([0, 1, 2, 3].map((r) => [0, 1, 2, 3].map((c) => f[c * 4 + r])));
  }
  const prim = json.meshes?.[0]?.primitives?.[0];
  const pa = prim ? json.accessors[prim.attributes.POSITION] : null;
  const pos = pa?.min && pa?.max ? { min: pa.min, max: pa.max } : null;
  return { names: skin.joints.map((j) => nodes[j].name ?? `#${j}`), ibm: mats, pos };
}

// ── 主流程 ──
let duckMats;
let duckAabb = null;
let glb;
try {
  const duckRead = readDuckDump(dumpPath);
  duckMats = duckRead.mats;
  duckAabb = duckRead.aabb;
  glb = readGlb(glbPath);
} catch (e) {
  console.error(`FAIL  ${e.message}`);
  process.exit(1);
}
if (duckMats.length < DUCK_BONES.length) {
  console.error(`FAIL  the duck dump has ${duckMats.length} bind poses, expected ${DUCK_BONES.length}`);
  process.exit(1);
}
const duckW = duckMats.map(inv4);
const duckIndex = new Map(DUCK_BONES.map((n, i) => [n, i]));
const srcW = glb.ibm.map(inv4);

// 映射：模型的关节 → 鸭子骨
const map = glb.names.map((n) => duckIndex.get(TABLE[n] ?? "") ?? -1);
const unmapped = glb.names.filter((n, i) => map[i] < 0);

// 整体对齐（与 CharacterSwap.TryBodyAlignment 同一套：骨盆到头的方向为上，左右髋的方向为左）
function landmark(get, keys) {
  const pelvis = get(keys.pelvis);
  const head = get(keys.head);
  const lh = get(keys.leftHip);
  const rh = get(keys.rightHip);
  if (!pelvis || !head || !lh || !rh) return null;
  const up = unit(sub(head, pelvis));
  let left = sub(lh, rh);
  left = unit(left.map((x, i) => x - up[i] * dot(left, up)));
  const fwd = cross(left, up);
  return { F: frame(left, up, fwd), pelvis };
}
const duckHas = (n) => duckIndex.has(n);
const duckPos = (n) => posOf(duckW[duckIndex.get(n)]);
const srcPos = (n) => {
  const i = glb.names.indexOf(n);
  return i >= 0 ? posOf(srcW[i]) : null;
};
const fd = landmark(duckPos, { pelvis: "Pelvis", head: "Head", leftHip: "Thigh.L", rightHip: "Thigh.R" });
const fs = landmark(srcPos, {
  pelvis: "mixamorig:Hips", head: "mixamorig:Head",
  leftHip: "mixamorig:LeftUpLeg", rightHip: "mixamorig:RightUpLeg",
});
if (!fd || !fs) {
  console.error("FAIL  landmarks missing (need pelvis, head, left hip, right hip on both sides)");
  process.exit(1);
}
const align = mul4(fd.F, inv4(fs.F));
const alignRot = rot3(align);

console.log(`  模型: ${basename(glbPath)}  关节 ${glb.names.length}  映射到角色骨 ${map.filter((x) => x >= 0).length}` +
  (unmapped.length ? `  未映射: ${unmapped.join(", ")}` : ""));
console.log("  骨                     位移(m)   朝向偏差(度)");
const rows = [];
for (let i = 0; i < glb.names.length; i += 1) {
  const di = map[i];
  if (di < 0) continue;
  const sPos = posOf(srcW[i]);
  const gPos = posOf(duckW[di]);
  const rigid = apply4(align, [...sPos, 1]).slice(0, 3);
  const displacement = len(sub(gPos, rigid));
  // 朝向偏差：模型骨朝向经过整体对齐后，与角色骨朝向差多少
  const rel = mul3(rot3(inv4(duckW[di])), mul3(alignRot, rot3(srcW[i])));
  const trace = rel[0][0] + rel[1][1] + rel[2][2];
  const angle = (Math.acos(Math.min(1, Math.max(-1, (trace - 1) / 2))) * 180) / Math.PI;
  rows.push([displacement, angle, glb.names[i], DUCK_BONES[di]]);
}
rows.sort((a, b) => b[0] - a[0]);
for (const [d, a, from, to] of rows) {
  console.log(`  ${`${from} -> ${to}`.padEnd(42)} ${d.toFixed(3).padStart(6)}  ${a.toFixed(1).padStart(9)}`);
}
const disp = rows.map((r) => r[0]).sort((a, b) => a - b);
const ang = rows.map((r) => r[1]);
console.log(`\n  位移：中位 ${disp[disp.length >> 1].toFixed(3)} m  最大 ${disp[disp.length - 1].toFixed(3)} m`);
console.log(`  朝向偏差：平均 ${(ang.reduce((s, x) => s + x, 0) / ang.length).toFixed(1)} 度  ` +
  `最大 ${Math.max(...ang).toFixed(1)} 度`);
// ── 尺寸建议：自动部分已按「网格高度」对齐，剩下的差额来自头的占比 ──
//   占比 =（头骨高度 − 网格最低点）/ 网格高度，两侧各算一次；建议倍数 = 角色占比 ÷ 模型占比。
const duckHead = posOf(duckW[duckIndex.get("Head")]);
const srcHeadIdx = glb.names.indexOf("mixamorig:Head");
const srcHead = srcHeadIdx >= 0 ? srcPos("mixamorig:Head") : null;
function frac(head, aabb) {
  if (!head || !aabb) return null;
  const span = aabb.max[2] - aabb.min[2];
  if (span < 1e-6) return null;
  return (head[2] - aabb.min[2]) / span;
}
const duckFrac = frac(duckHead, duckAabb);
const srcFrac = srcHead && glb.pos ? (() => {
  const span = glb.pos.max[1] - glb.pos.min[1];              // glTF 是 Y 向上
  return span > 1e-6 ? (srcHead[1] - glb.pos.min[1]) / span : null;
})() : null;
if (duckFrac && srcFrac) {
  console.log(`\n  头的占比（头骨高度 − 网格最低点）/ 网格高度：` +
    `角色 ${(duckFrac * 100).toFixed(1)}%  模型 ${(srcFrac * 100).toFixed(1)}%` +
    `  ⇒ 建议 scale = ${(duckFrac / srcFrac).toFixed(2)}`);
  console.log("  用法：写进 config.json 的 \"scale\"。自动部分只按网格高度对齐，这一项补头的占比差。");
  console.log("  不写也可以（默认 1.0），但矮胖的模型会看起来偏小。");
} else {
  console.log("\n  头的占比算不出来（缺网格包围盒）。尺寸只能按网格高度对齐，即 scale 保持 1.0。");
}

console.log("\n  读法：位移越大，「把部件搬到角色骨上」对形状破坏越大；朝向偏差大而整齐，说明两副骨架");
console.log("  的骨轴约定不同，搬之前要先按 K_b = R_tᵀ·R_A·R_s 求对应。当前实现只做刚性放置（形状保真优先）。");
