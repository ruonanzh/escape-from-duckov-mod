#!/usr/bin/env node
/**
 * check-character-swap.mjs —— 校验角色替换的**搬运算法**与运行时接线。
 *
 * 为什么要有这个脚本：
 *   角色替换这条链踩过三次「静默错」：
 *     ① `ReadAccessor` 漏了 `MAT4` ⇒ 逆绑定矩阵只读了 1 个 float ⇒ 顶点被算飞（游戏里模型消失、空中剩碎片）；
 *     ② 加载期顶点被旋转/平移（朝向声明 + 握把归零）而骨架没跟上 ⇒ 顶点与骨架错位（H1）；
 *     ③ 镜像带负行列式 ⇒ 三角形绕序翻 ⇒ 法线朝内（M2）。
 *   三种都不会抛异常，只会在游戏里「看起来不对」。所以这里做两件事：
 *     A. 用 JavaScript 复算一遍搬运公式，对**手算得出的期望值**断言（算法对不对）；
 *     B. 对 C# 原文做关键字断言（那三个坑还在不在）。
 *
 * 算法（与 `libs/mod-kit/CharacterSwap.cs` 一致）：
 *     v' = Σ_b w_b · ( G_b · ( V · S_b⁻¹ ) ) · v
 *     G_b = 目标骨骼绑定世界矩阵（`bindposes[b]⁻¹`）
 *     S_b = 源骨骼绑定世界矩阵（`inverseBindMatrices[b]⁻¹`）
 *     V   = 加载期顶点净变换（`GltfLoader.Loaded.VertexTransform`）
 *
 * 用法：node scripts/check-character-swap.mjs
 * 退出码：0 = 全部符合预期；1 = 有问题（逐条打印）
 */

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const SWAP = join(root, "libs", "mod-kit", "CharacterSwap.cs");
const LOADER = join(root, "libs", "mod-kit", "GltfLoader.cs");

let failed = 0;
const check = (name, expected, got) => {
  // 向量按容差比（浮点会给出 −0.9999999999999997 这种结果）
  const isVec = (x) => Array.isArray(x) && x.every((v) => typeof v === "number");
  const ok = isVec(expected) && isVec(got)
    ? expected.length === got.length && near(expected, got)
    : JSON.stringify(expected) === JSON.stringify(got);
  if (!ok) failed += 1;
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}`);
  if (!ok) console.log(`        期望 ${JSON.stringify(expected)}，得到 ${JSON.stringify(got)}`);
};
const checkTrue = (name, cond, detail = "") => {
  if (!cond) failed += 1;
  console.log(`  ${cond ? "PASS" : "FAIL"}  ${name}${cond ? "" : "  " + detail}`);
};

// ══ A. 复算搬运公式 ══════════════════════════════════════════════════════════
// 仿射矩阵用 [R|t] 表示（R 是 3x3 行主序，t 是 3 维平移）。这些骨骼绑定姿势都是仿射的。
const rotY = (deg) => {
  const r = (deg * Math.PI) / 180;
  const c = Math.cos(r);
  const s = Math.sin(r);
  return [[c, 0, s], [0, 1, 0], [-s, 0, c]];                    // 绕 Y 的旋转
};
const ID = [[1, 0, 0], [0, 1, 0], [0, 0, 1]];
const affine = (R = ID, t = [0, 0, 0]) => ({ R, t });           // 绑定世界矩阵 = [R|t]
const inv = (m) => {                                            // 仿射矩阵求逆：[R⁻¹|−R⁻¹t]
  const R = m.R;
  const d = R[0][0] * (R[1][1] * R[2][2] - R[1][2] * R[2][1]) - R[0][1] * (R[1][0] * R[2][2] - R[1][2] * R[2][0]) + R[0][2] * (R[1][0] * R[2][1] - R[1][1] * R[2][0]);
  if (d === 0) throw new Error("singular matrix in test fixture");
  const cof = (a, b) => R[(a + 1) % 3][(b + 1) % 3] * R[(a + 2) % 3][(b + 2) % 3] - R[(a + 1) % 3][(b + 2) % 3] * R[(a + 2) % 3][(b + 1) % 3];
  const Ri = [[cof(0, 0), cof(1, 0), cof(2, 0)], [cof(0, 1), cof(1, 1), cof(2, 1)], [cof(0, 2), cof(1, 2), cof(2, 2)]].map((row) => row.map((v) => v / d));
  const t = m.t;
  return affine(Ri, Ri.map((row) => -(row[0] * t[0] + row[1] * t[1] + row[2] * t[2])));
};
const mul = (a, b) => affine(a.R.map((row, i) => [0, 1, 2].map((j) => row[0] * b.R[0][j] + row[1] * b.R[1][j] + row[2] * b.R[2][j])),
  a.R.map((row, i) => row[0] * b.t[0] + row[1] * b.t[1] + row[2] * b.t[2] + a.t[i]));
const apply = (m, v) => m.R.map((row, i) => row[0] * v[0] + row[1] * v[1] + row[2] * v[2] + m.t[i]);
const det3 = (m) => m.R[0][0] * (m.R[1][1] * m.R[2][2] - m.R[1][2] * m.R[2][1]) - m.R[0][1] * (m.R[1][0] * m.R[2][2] - m.R[1][2] * m.R[2][0]) + m.R[0][2] * (m.R[1][0] * m.R[2][1] - m.R[1][1] * m.R[2][0]);
const near = (a, b) => a.every((v, i) => Math.abs(v - b[i]) < 1e-6);

/** 复算一个顶点：`skin` = { joints, srcBind, vertexTransform }；`map` = 关节下标到游戏骨下标（-1 = 没映射上）。 */
const retarget = (skin, gameBind, map, v, weights) => {
  let acc = null;
  let wsum = 0;
  const keep = [];
  for (let k = 0; k < 4; k += 1) {
    const w = weights.bw[k];
    const ji = weights.bi[k];
    if (!(w > 1e-4) || ji < 0 || ji >= map.length) continue;
    const gi = map[ji];
    if (gi < 0) continue;                                     // 没映射上的骨：权重丢掉
    const m = mul(gameBind[gi], mul(inv(skin.srcBind[ji]), skin.vertexTransform));
    acc = acc === null
      ? { R: m.R.map((row) => row.map((x) => x * w)), t: m.t.map((x) => x * w) }
      : { R: acc.R.map((row, i) => row.map((x, j) => x + m.R[i][j] * w)), t: acc.t.map((x, i) => x + m.t[i] * w) };
    wsum += w;
    keep.push(w);
  }
  if (wsum <= 1e-4) return { v: v.slice(), wsum, mapped: 0 };
  const scaled = { R: acc.R.map((row) => row.map((x) => x / wsum)), t: acc.t.map((x) => x / wsum) };
  const normalized = keep.map((w) => w / wsum);
  return { v: apply(scaled, v), wsum, mapped: keep.length, weights: normalized };
};

console.log("搬运公式（对用纸笔算出的期望值）：");
{
  // 源骨架：骨 0 在原点、骨 1 在 (0,1,0)。顶点 (1,0,0) 全权重在骨 0 上。
  const srcBind = [affine(ID, [0, 0, 0]), affine(ID, [0, 1, 0])];
  // 目标骨架：骨 0 挪到 (0,0,5)，骨 1 挪到 (0,0,6)。同一根骨、不同位置。
  const gameBind = [affine(ID, [0, 0, 5]), affine(ID, [0, 0, 6])];
  const map = [0, 1];
  const skin = { srcBind, vertexTransform: affine() };
  const all0 = { bi: [0, 0, 0, 0], bw: [1, 0, 0, 0] };
  check("全权重 1 根骨：顶点随目标骨平移", [1, 0, 5], retarget(skin, gameBind, map, [1, 0, 0], all0).v);

  // 同一根骨，但目标骨绕 Y 转 90 度（平移也随之变成 (5,0,0)）⇒ 骨局部偏移 (1,0,0) 跟着转成 (0,0,-1)。
  // 手算：R·(1,0,0) + t = (0,0,-1) + (5,0,0) = (5,0,-1)
  const gameBindR = [mul(affine(rotY(90)), affine(ID, [0, 0, 5])), gameBind[1]];
  check("目标骨带旋转：骨局部偏移跟着转", [5, 0, -1], retarget(skin, gameBindR, map, [1, 0, 0], all0).v);

  // 半半权重 ⇒ 两根骨各自结果的加权平均。
  // 手算：骨 0 给 (1,0,5)；骨 1 在 (0,0,6)，顶点相对它是 (1,-1,0) ⇒ (1,-1,6)。平均 = (1,-0.5,5.5)
  const half = { bi: [0, 1, 0, 0], bw: [0.5, 0.5, 0, 0] };
  check("两根骨各 0.5：结果是两个位置的中点", [1, -0.5, 5.5], retarget(skin, gameBind, map, [1, 0, 0], half).v);

  // 骨 1 **没映射上** ⇒ 它的 0.4 权重丢掉，剩下的 0.6 归一化成 1.0 ⇒ 结果等于纯骨 0
  const partial = { bi: [0, 1, 0, 0], bw: [0.6, 0.4, 0, 0] };
  const r = retarget(skin, gameBind, [0, -1], [1, 0, 0], { ...partial });
  check("没映射上的骨权重丢掉后归一化", [1, 0, 5], r.v);
  check("归一化后的权重和为 1", true, Math.abs(r.weights.reduce((a, b) => a + b, 0) - 1) < 1e-9);

  // 完全没映射上 ⇒ 顶点保持原位（并被记一笔，不得静默）
  const orphan = { bi: [1, 0, 0, 0], bw: [1, 0, 0, 0] };
  check("权重全没映射上：顶点保持原位", [1.5, 2.5, 3.5], retarget(skin, gameBind, [-1, -1], [1.5, 2.5, 3.5], orphan).v);

  // 加载期顶点变换必须参与：否则顶点与骨架错位（H1）
  const shifted = { srcBind, vertexTransform: affine(ID, [0, 2, 0]) };
  check("顶点净变换参与：结果跟着移", [1, 2, 5], retarget(shifted, gameBind, map, [1, 0, 0], all0).v);

  // 镜像：行列式为负 ⇒ 必须翻转绕序（M2）
  checkTrue("镜像（det < 0）能被判别出来", det3(mul(gameBind[0], inv(srcBind[0]))) > 0 && det3(affine([[-1, 0, 0], [0, 1, 0], [0, 0, 1]])) < 0);
}

// ══ B. C# 原文断言（三个坑还在不在）══════════════════════════════════════════
const swap = readFileSync(SWAP, "utf8");
const loader = readFileSync(LOADER, "utf8");

console.log("\nC# 原文（防回退）：");
checkTrue("ReadAccessor 认得 MAT4 = 16 个分量（漏了 ⇒ 逆绑定矩阵成垃圾）",
  /type == "MAT4" \? 16/.test(loader), "GltfLoader 里找不到 `MAT4 ? 16`");
checkTrue("ReadAccessor 处理 accessor.normalized（漏了 ⇒ ubyte 权重变 0..255）",
  /acc\.Has\("normalized"\)/.test(loader) && /norm \? u \/ 65535f/.test(loader) && /norm \? bin\[q\] \/ 255f/.test(loader), "找不到 normalized 的处理");
checkTrue("加载期顶点净变换被记下来（H1）",
  /public Matrix4x4 VertexTransform/.test(loader) && /r\.VertexTransform = Matrix4x4\.TRS\(/.test(loader), "GltfLoader 里找不到 VertexTransform");
checkTrue("搬运时骨架也带上顶点净变换（H1）",
  /srcBind\[i\] = skin\.VertexTransform \*/.test(swap), "CharacterSwap 里骨架没跟上顶点变换");
checkTrue("新网格用**游戏的**绑定姿势（用模型的就是歪的）",
  /mesh\.bindposes = gamePoses;/.test(swap) && !/mesh\.bindposes = skin\./.test(swap), "bindposes 赋错");
checkTrue("镜像时翻转三角形绕序（M2）",
  /\.determinant < 0f/.test(swap) && /if \(flipWinding\)/.test(swap), "找不到行列式判断");
checkTrue("低频重申隐藏的部件（M1：游戏会改回来）",
  /public void Reassert\(float now/.test(swap) && /Body\.sharedMesh != NewMesh/.test(swap), "找不到 Reassert");
checkTrue("角色被游戏重建后会重新搬运（关卡初始化在搬运之后跑）",
  /public static void Tick\(float now/.test(swap) && /if \(r\.Body == null \|\| r\.Character == null\) \{ Reapply\(\); return; \}/.test(swap), "找不到 Tick/Reapply");
checkTrue("还原时销毁克隆材质（L2：否则每次热重载漏一份）",
  /UnityEngine\.Object\.Destroy\(NewMaterial\)/.test(swap), "找不到材质销毁");
checkTrue("原网格包围盒在赋值前取（L1）",
  /var origBounds = res\.OriginalMesh/.test(swap) && !/body was \{mesh\.bounds/.test(swap), "origBounds 取得太晚");
checkTrue("限制写进了注释（M4 只读 skins[0]/JOINTS_0；L5 不搬 uv2 与顶点色）",
  /只读第一个 skin 与第 0 组关节/.test(swap) && /uv2/.test(swap), "限制没写");

console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
