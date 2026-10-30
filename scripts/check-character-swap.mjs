#!/usr/bin/env node
/**
 * check-character-swap.mjs —— 校验角色替换的**身体对齐**与运行时接线。
 *
 * 为什么要有这个脚本：
 *   角色替换踩过一串「静默错」，而且全都不抛异常，只在游戏里「看着不对」：
 *     ① `ReadAccessor` 漏了 `MAT4` ⇒ 逆绑定矩阵只读 1 个 float ⇒ 顶点被算飞（模型消失、空中剩碎片）；
 *     ② 加载期顶点被旋转/平移（朝向声明与握把归零）而地标没跟上 ⇒ 顶点与骨架错位；
 *     ③ **逐骨重定姿态**：实测两副骨架的骨局部朝向约定**完全不同**
 *        （逐骨变换相对全局对齐偏差平均 120 度、最大 177 度，比例全是 1.000）
 *        ⇒ 每个部件各自被转不同角度 ⇒ 模型拧成一团。
 *        改为「身体地标对齐 + 刚性搬运」后不再拧（离线渲染已验证）。
 *   本脚本做两件事：
 *     A. 用 JavaScript 复算**身体对齐**，对性质断言（正交、行列式 +1、上/左对应）；
 *     B. 对 C# 原文做关键字断言，防止那些坑回来。
 *
 * 用法：node scripts/check-character-swap.mjs
 * 退出码：0 = 符合预期；1 = 有问题（逐条打印）
 */

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const SWAP = join(root, "libs", "mod-kit", "CharacterSwap.cs");
const LOADER = join(root, "libs", "mod-kit", "GltfLoader.cs");
const RETARGET = join(root, "libs", "mod-kit", "CharacterRetarget.cs");

let failed = 0;
const check = (name, expected, got) => {
  const isVec = (x) => Array.isArray(x) && x.every((v) => typeof v === "number");
  const ok = isVec(expected) && isVec(got)
    ? expected.length === got.length && expected.every((v, i) => Math.abs(v - got[i]) < 1e-6)
    : JSON.stringify(expected) === JSON.stringify(got);
  if (!ok) failed += 1;
  console.log(`  ${ok ? "PASS" : "FAIL"}  ${name}`);
  if (!ok) console.log(`      期望 ${JSON.stringify(expected)}，得到 ${JSON.stringify(got)}`);
};
const checkTrue = (name, cond, detail = "") => {
  if (!cond) failed += 1;
  console.log(`  ${cond ? "PASS" : "FAIL"}  ${name}${cond ? "" : "  " + detail}`);
};

// ══ A. 复算身体对齐（与 CharacterSwap.BodyFrame / TryBodyAlignment 同一个构造）══
const vsub = (a, b) => a.map((x, i) => x - b[i]);
const vdot = (a, b) => a.reduce((s, x, i) => s + x * b[i], 0);
const vcross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const vlen = (v) => Math.sqrt(vdot(v, v));
const vnorm = (v) => { const L = vlen(v) || 1; return v.map((x) => x / L); };
/** 列 = 左 / 上 / 前；两边同构造 ⇒ 只会得到旋转。 */
const bodyFrame = (pelvis, head, leftHip, rightHip) => {
  const up = vnorm(vsub(head, pelvis));
  let left = vsub(leftHip, rightHip);
  left = left.map((x, i) => x - up[i] * vdot(left, up));
  left = vnorm(left);
  const fwd = vcross(left, up);
  return [[left[0], up[0], fwd[0]], [left[1], up[1], fwd[1]], [left[2], up[2], fwd[2]]];
};
const mT = (m) => m[0].map((_, c) => m.map((r) => r[c]));
const mMul = (a, b) => a.map((r) => b[0].map((_, j) => r.reduce((s, x, k) => s + x * b[k][j], 0)));
const mVec = (m, v) => m.map((r) => r.reduce((s, x, k) => s + x * v[k], 0));
const det3 = (m) => m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
  - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
  + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]);

console.log("身体对齐（性质断言）：");
{
  // 源：Y 向上、左为 +X、脚在 -Z（glTF 常见约定）
  const src = { pelvis: [0, 1, 0], head: [0, 1.6, 0], leftHip: [0.15, 0.9, 0], rightHip: [-0.15, 0.9, 0] };
  // 鸭子：Z 向上、左为 -X（实测：头在 Z=0.99，Thigh.L 在 x=-0.162）
  const duck = { pelvis: [0, 0, 0.16], head: [0, 0, 0.8], leftHip: [-0.162, 0, 0.203], rightHip: [0.162, 0, 0.203] };
  const Fs = bodyFrame(src.pelvis, src.head, src.leftHip, src.rightHip);
  const Fd = bodyFrame(duck.pelvis, duck.head, duck.leftHip, duck.rightHip);

  const cols = (m) => [0, 1, 2].map((c) => m.map((r) => r[c]));
  checkTrue("坐标架是正交的（三列两两垂直、长度为 1）", (() => {
    const cs = cols(Fs);
    return cs.every((c) => Math.abs(vlen(c) - 1) < 1e-6)
      && cs.every((c, i) => cs.every((d, j) => i === j || Math.abs(vdot(c, d)) < 1e-6));
  })());
  check("坐标架行列式 = +1（右手的，不是镜像）", 1, Math.round(det3(Fs)));
  check("鸭子坐标架行列式 = +1", 1, Math.round(det3(Fd)));

  const R = mMul(Fd, mT(Fs));
  check("对齐是纯旋转：行列式 = +1", 1, Math.round(det3(R)));
  const upS = vnorm(vsub(src.head, src.pelvis));
  const leftS = vnorm(vsub(src.leftHip, src.rightHip));
  // 源的上(+Y) 应该落到鸭子的上(+Z)
  check("源的上方向映射到鸭子的上方向", [0, 0, 1], mVec(R, upS).map((x) => Math.round(x * 1e6) / 1e6));
  // 源的左(+X) 应该落到鸭子的左(-X)
  check("源的左方向映射到鸭子的左方向", [-1, 0, 0], mVec(R, leftS).map((x) => Math.round(x * 1e6) / 1e6));

  // 刚性：任意两点距离不变
  const a = [0.1, 1.2, 0.05], b = [-0.3, 0.8, -0.2];
  const ta = [0, 0, 0], tb = [0.4, 0.4, -0.25];   // 加上平移后再比
  const d0 = vlen(vsub(a, b));
  const d1 = vlen(vsub(vsub(ta, tb).map((x, i) => mVec(R, a)[i] + x), vsub(ta, tb).map((x, i) => mVec(R, b)[i] + x)));
  checkTrue("刚性：两点距离不变", Math.abs(d0 - d1) < 1e-6, `${d0} vs ${d1}`);
}

console.log("\n运行时旋转重定向（世界增量与父子坐标转换）：");
{
  const qMul = (a, b) => [
    a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
    a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
    a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
    a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
  ];
  const qInv = (q) => [-q[0], -q[1], -q[2], q[3]];
  const qRotate = (q, v) => {
    const u = [q[0], q[1], q[2]];
    const uv = vcross(u, v), uuv = vcross(u, uv);
    return v.map((x, i) => x + 2 * (q[3] * uv[i] + uuv[i]));
  };
  const qAxis = (axis, degrees) => {
    const a = vnorm(axis), h = degrees * Math.PI / 360;
    return [...a.map((x) => x * Math.sin(h)), Math.cos(h)];
  };
  const closeVec = (a, b) => a.every((x, i) => Math.abs(x - b[i]) < 1e-6);
  const driverRest = qAxis([0, 1, 0], 37);
  const sourceRest = qAxis([1, 0, 1], -62);
  const delta = qAxis([0, 0, 1], 83);
  const currentDriver = qMul(delta, driverRest);
  const recoveredDelta = qMul(currentDriver, qInv(driverRest));
  const desired = qMul(recoveredDelta, sourceRest);
  checkTrue("参考姿势不变时，源关节保持自己的参考旋转",
    closeVec(qRotate(qMul(qMul(driverRest, qInv(driverRest)), sourceRest), [0.2, 0.7, -0.1]), qRotate(sourceRest, [0.2, 0.7, -0.1])));
  checkTrue("不同骨轴的源关节仍接收同一个世界旋转增量",
    closeVec(qRotate(desired, [0.2, 0.7, -0.1]), qRotate(delta, qRotate(sourceRest, [0.2, 0.7, -0.1]))));
  const parent = qAxis([1, 2, -1], 29);
  const local = qMul(qInv(parent), desired);
  checkTrue("世界目标转成局部旋转后，父骨 × 局部旋转能还原目标",
    closeVec(qRotate(qMul(parent, local), [-0.4, 0.3, 0.8]), qRotate(desired, [-0.4, 0.3, 0.8])));
}

// ══ B. C# 原文断言（那些坑还在不在）══════════════════════════════════════════
const swap = readFileSync(SWAP, "utf8");
const loader = readFileSync(LOADER, "utf8");
const retarget = readFileSync(RETARGET, "utf8");
const applyBody = retarget.slice(retarget.indexOf("public void Apply()"), retarget.indexOf("internal void Reassert"));

console.log("\nC# 原文（防回退）：");
checkTrue("ReadAccessor 认得 MAT4 = 16 个分量（漏了 ⇒ 逆绑定矩阵成垃圾）",
  /type == "MAT4" \? 16/.test(loader), "找不到 `MAT4 ? 16`");
checkTrue("ReadAccessor 处理 accessor.normalized（漏了 ⇒ ubyte 权重变 0..255）",
  /acc\.Has\("normalized"\)/.test(loader) && /norm \? u \/ 65535f/.test(loader) && /norm \? bin\[q\] \/ 255f/.test(loader),
  "找不到 normalized 处理");
checkTrue("加载期顶点净变换被记下来（朝向声明与握把归零）",
  /public Matrix4x4 VertexTransform/.test(loader) && /r\.VertexTransform = Matrix4x4\.TRS\(/.test(loader), "找不到 VertexTransform");
checkTrue("源地标也带上顶点净变换（否则地标与顶点不同坐标系）",
  /skin\.VertexTransform\.MultiplyPoint3x4\(new Vector3\(world\.m03/.test(swap), "SourceLandmark 没带净变换");
checkTrue("源地标取反 X（加载器已取反过顶点，地标不跟着取反就左右翻）",
  /p = new Vector3\(-v\.x, v\.y, v\.z\);/.test(swap), "找不到取反 X");
checkTrue("GLB 保留源节点层级与局部矩阵",
  /public RigNode\[\] Nodes/.test(loader) && /rigNodes\[child\]\.Parent = i/.test(loader)
  && /r\.JointNodes = jointNodes/.test(loader) && /r\.LocalMatrix = Matrix4x4\.TRS/.test(loader),
  "源骨架层级或局部矩阵没有读入");
checkTrue("用身体地标对齐（同一个构造建两边的坐标架）",
  /static bool BodyFrame\(/.test(swap) && /var rot = fd \* fs\.transpose;/.test(swap), "找不到 BodyFrame 或对齐乘法");
checkTrue("源逆绑定矩阵与顶点使用同一个坐标转换",
  /sourcePoses\[i\] = convert \* skin\.InverseBind\[i\] \* inverseConvert/.test(swap)
  && /srcMesh\.bindposes = sourcePoses/.test(swap), "源 bindposes 的坐标转换不一致");
checkTrue("按身体高度等比缩放（不缩放会“稍微小一些”）",
  /const float MinScale = 0\.25f, MaxScale = 4f;/.test(swap) && /Mathf\.Clamp\(duckH \/ srcH, MinScale, MaxScale\)/.test(swap),
  "找不到尺寸对齐");
checkTrue("有额外倍数旋钮（包围盒对齐不了「高瘦对矮胖」的视觉差）",
  /public static float ExtraScale = 1f;/.test(swap) && /scale \*= Mathf\.Clamp\(ExtraScale, 0\.1f, 4f\);/.test(swap), "找不到 ExtraScale");
checkTrue("高度用真实顶点跨度，不用包围盒投影（投影会在上轴倾斜时放大，导致模型变小）",
  /static float ExtentAlong\(Mesh mesh, Vector3 axis\)/.test(swap)
  && /float srcH = ExtentAlong\(skin\.Mesh, col1\(fs\)\)/.test(swap)
  && !/Vector3\.Dot\(skin\.Mesh\.bounds\.size, col1\(fs\)\)/.test(swap), "还在用包围盒投影");
checkTrue("源权重按源 joints 下标写回网格，不改绑到游戏骨骼",
  /srcMesh\.boneWeights = skin\.Weights/.test(swap)
  && !/RedirectToTorso|BindArmBones|mesh\.boneWeights = nw/.test(swap), "源权重缺失或仍有旧改绑路径");
checkTrue("世界旋转增量 = 当前游戏旋转 × 游戏参考旋转的逆",
  /var delta = RotationOf\(currentDriver\) \* Quaternion\.Inverse\(e\.DriverRest\)/.test(retarget)
  && /var desiredInSpace = delta \* e\.SourceRest/.test(retarget), "世界增量公式被改错");
checkTrue("目标世界旋转先转回父骨局部空间，且按父到子更新",
  /Quaternion\.Inverse\(RotationOf\(parentInSpace\)\) \* desiredInSpace/.test(retarget)
  && /entries\.Sort\(\(a, b\) => Depth\(skin\.Nodes, a\.Node\)/.test(retarget), "父子旋转顺序或转换被改错");
checkTrue("Animator 更新后的 LateUpdate 驱动源骨架",
  /void LateUpdate\(\)/.test(retarget) && /Target\.Apply\(\)/.test(retarget), "没有 LateUpdate 驱动");
checkTrue("运行时每帧只写旋转，不写源骨骼位置或缩放",
  applyBody.includes("localRotation") && !/localPosition|localScale/.test(applyBody),
  "Apply 写入了位置或缩放");
checkTrue("不引入 IK 或 HumanPoseHandler",
  !/new\s+HumanPoseHandler|TwoBoneIKConstraint|\bIK\s*\(/.test(retarget), "出现了超出方案 A 的机制");
checkTrue("对齐是纯旋转 ⇒ 不需要翻绕序",
  !/flipWinding/.test(swap), "还留着 flipWinding");
checkTrue("换好之后有一段时间逐帧重申（否则新部件会晚一秒才消失）",
  /res\._fastUntil = Time\.unscaledTime \+ 4f;/.test(swap) && /if \(now < r\._fastUntil\) \{ r\.Reassert\(now, 0f\); return; \}/.test(swap),
  "找不到逐帧重申窗口");
checkTrue("角色模型钩子挂的是 CharacterModel 的方法（ItemGraphicInfo.Setup 实测不覆盖这些部件）",
  /typeof\(CharacterModel\), name\)/.test(swap) && /"AddSubVisuals"/.test(swap) && /"SetFaceFromData"/.test(swap),
  "找不到角色模型钩子");
checkTrue("新部件用钩子处理（钩子优先，循环只做兜底）",
  /ItemHooks\.OnGraphicReady \+= OnGraphicReady;/.test(swap) && /static void OnGraphicReady\(ItemStatsSystem\.Item item, ItemGraphicInfo graphic\)/.test(swap),
  "找不到外观部件的钩子");
checkTrue("隐藏判据只有一份（循环与钩子共用 HideOne）",
  /static void HideOne\(Transform root, Renderer r, Result res\)/.test(swap)
  && (swap.match(/UnderKeepVisible\(/g) || []).length === 2, "HideOne 或保留名单判据被写了两份");
checkTrue("重申没有被双重限流挡掉（以前 Tick 先设限额再调 Reassert ⇒ 永远不执行）",
  /r\.Reassert\(now, interval\);/.test(swap) && !/_nextReassert = now \+ interval;[\s\S]{0,200}?if \(r\.Body == null/.test(swap),
  "Tick 里还是先设限额再调 Reassert");
checkTrue("低频重申隐藏的部件（M1：游戏会改回来）",
  /public void Reassert\(float now/.test(swap) && /Body\.sharedMesh != NewMesh/.test(swap), "找不到 Reassert");
checkTrue("角色被游戏重建后会重新搬运（关卡初始化在搬运之后跑）",
  /public static void Tick\(float now/.test(swap) && /\n\s*Reapply\(\);/.test(swap), "找不到 Tick/Reapply");
checkTrue("还原时销毁克隆材质（L2：否则每次热重载漏一份）",
  /UnityEngine\.Object\.Destroy\(NewMaterial\)/.test(swap), "找不到材质销毁");
checkTrue("还原时恢复原骨骼、离屏状态并销毁源骨架",
  /Body\.bones = OriginalBones/.test(swap) && /Body\.rootBone = OriginalRootBone/.test(swap)
  && /Body\.updateWhenOffscreen = OriginalUpdateWhenOffscreen/.test(swap)
  && /Retarget\.Dispose\(\)/.test(swap), "骨骼、离屏状态或源骨架没有还原");
checkTrue("重申会恢复被游戏重置的源骨骼",
  /Retarget\.Reassert\(Body\)/.test(swap) && /body\.bones = _sourceBones/.test(retarget), "源骨骼没有重申");
checkTrue("原网格包围盒在赋值前取（L1）",
  /var origBounds = res\.OriginalMesh/.test(swap) && !/body was \{mesh\.bounds/.test(swap), "origBounds 取得太晚");
checkTrue("源骨架与整体对齐后的蒙皮 bounds 每帧更新，避免边缘误剔除",
  /body\.updateWhenOffscreen = true/.test(swap), "没有更新蒙皮 bounds");
checkTrue("隐藏会扫角色整棵层级（五官/头发不在 CharacterModel.renderers 里）",
  /character\.GetComponentsInChildren<Renderer>\(true\)/.test(swap) && /UnderKeepVisible/.test(swap), "找不到整棵层级扫描");
checkTrue("每一遍隐藏都走同一个判据（只在最后一遍查会把枪藏掉）",
  /Action<object> hide = \(o\) =>[\s\S]{0,300}?HideOne\(/.test(swap)
  && /static void HideOne\(Transform root, Renderer r, Result res\)[\s\S]{0,300}?UnderKeepVisible\(root, r\.transform\)/.test(swap),
  "hide 没走 HideOne，或 HideOne 没查保留名单");
checkTrue("装备与非角色几何在保留名单里（枪/背包/护甲/粒子）",
  ["Hand.Soket.L", "HandObj.L", "BackpackSocket", "ArmorSocket", "RunParticle"].every((n) => swap.includes(`"${n}"`)),
  "保留名单缺项");
checkTrue("限制写进了注释（M4 只读 skins[0]/JOINTS_0；L5 不搬 uv2 与顶点色）",
  /只读第一个 skin 与第 0 组关节/.test(swap) && /uv2/.test(swap), "限制没写");

console.log(failed ? `\n  ${failed} 条不符合预期` : "\n  全部符合预期");
process.exit(failed ? 1 : 0);
