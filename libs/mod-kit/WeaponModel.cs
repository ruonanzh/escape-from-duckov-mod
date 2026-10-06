// WeaponModel.cs —— 把外部模型（GltfLoader 读出的 Mesh + 贴图）换到**手持武器**上。
//
// 调用方（mod）只需要：找到玩家手里那把枪 + 给出 Mesh/贴图 → 调 Apply()。
// 这里把"每次都必须做对的事"集中在一处（都是实机踩过才定下来的）：
//
//   ① 只换**旧模型零件**：`WPN_*`（枪身）与 `HideIf_*`（原枪自带的默认件）
//      —— `ShowIf_*`（配件本身的模型）与特效（MuzzleFlash/Particle）**不能动** ✗ 归游戏管
//   ② 挂到**原枪身零件**的变换帧下（不是根节点 ✗）：同原点/同朝向
//      并且把缩放补回世界尺度 1（prefab 内部有缩放链 —— 直接挂会缩到看不见 ✗ 踩过）
//   ③ **对齐**：以游戏那把枪自己的局部包围盒为基准（"原点的归一化位置"对齐）
//      —— 比"猜握把"可靠 ✓（游戏那把枪的原点就是手的位置 ✓）
//   ④ 材质：**克隆游戏材质**（URP shader 才渲染得出来 ✗）+ 只把贴图换成我们的
//
// 不做的事：不改数值/行为 ✓ 不碰背包里的模板（只动运行时手里那把活实体 ✓）

using System;   // StringComparison / StringComparer ✓
using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    public static class WeaponModel
    {
        public sealed class Result
        {
            public bool Applied;
            public GameObject Instance;
            public string Report = "";
            /// <summary>config 里声明的槽位 → 我们模型局部坐标（米）的换算结果 ✓（供 step 2 把配件挂到我们形状上用 ✓）</summary>
            public readonly Dictionary<string, Vector3> SlotPoints = new Dictionary<string, Vector3>();
            /// <summary>对齐用到了什么（日志用 ✓）</summary>
            public string AlignSource = "";
            /// <summary>被我们**禁用掉的旧零件**（热重载/卸载时用来恢复 ✗ 否则枪会整个不见 ✗）</summary>
            public readonly List<Renderer> Hidden = new List<Renderer>();
            /// <summary>被我们**搬过位置**的游戏挂点（`Sockets/<槽位>`）→ 原 localPosition ✓（恢复用 ✓）</summary>
            public readonly Dictionary<Transform, Vector3> MovedSockets = new Dictionary<Transform, Vector3>();
            /// <summary>把我们禁用过的旧零件**恢复显示** ✓（幂等 ✓）</summary>
            public void RestoreHidden()
            {
                foreach (var r in Hidden) if (r != null) r.enabled = true;
                Hidden.Clear();
            }
            /// <summary>把改过的东西**全部还原** ✓（幂等 ✓）：旧零件显示 ✓ + 搬过的挂点位置 ✓</summary>
            public void Restore()
            {
                RestoreHidden();
                foreach (var kv in MovedSockets) if (kv.Key != null) kv.Key.localPosition = kv.Value;
                MovedSockets.Clear();
            }
        }

        /// <summary>游戏定的 5 个挂点名 ✓（旧文档实测：prefab 的 `Sockets` 容器下就这 5 个子节点 ✓）</summary>
        static readonly string[] SlotNames = { "Scope", "Tec", "Muzzle", "Stock", "Grip" };
        static bool IsSlotName(string n)
        {
            foreach (var s in SlotNames) if (string.Equals(s, n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>找游戏自己的挂点：优先 `Sockets/<槽位>` ✓；没有 `Sockets` 容器就退化成“名字正好等于槽位名的 Transform”✓
        /// （**不会**误抓 `ShowIf_Scope` / `HideIf_Scope` ✓ —— 那些名字不全等 ✓）</summary>
        static Dictionary<string, Transform> FindSockets(Transform root)
        {
            var map = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsSlotName(t.gameObject.name)) continue;
                var p = t.parent;
                if (p != null && string.Equals(p.gameObject.name, "Sockets", StringComparison.OrdinalIgnoreCase)) map[t.gameObject.name] = t;
            }
            if (map.Count == 0)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var n = t.gameObject.name;
                    if (IsSlotName(n) && !map.ContainsKey(n)) map[n] = t;
                }
            return map;
        }

        /// <summary>把手持武器的模型换成给进来的 Mesh+贴图。root 一般是 `itemGraphic.gameObject` 或手持实体的 GameObject。</summary>
        /// <param name="slots">可选：config.json 的槽位（**比例** ✓ 键如 Muzzle/Stock/Scope/Tec/Grip，以及可选的 pivot）；
        /// 每个值 [L,H,D] 均为 0~1：**L 沿 Z（枪口 +Z ✓）· H 沿 Y · D 沿 X**（= 模型自身包围盒的比例 ✓）。
        /// 作用：① 换算成我们模型局部坐标（记入 Result.SlotPoints ✓）② 有 `pivot` 就用它当对齐基准（比包围盒映射准 ✓）。</param>
        public static Result Apply(Transform root, Mesh mesh, Texture2D texture, Vector3 extraOffset = default, Dictionary<string, Vector3> slots = null)
        {
            var res = new Result();
            if (root == null || mesh == null) { res.Report = "缺少 root 或 mesh"; return res; }

            // ① 选"枪身"锚点：WPN_* 优先，否则取最大的非配件零件（<5m，避免选中特效）
            Renderer anchor = null; float best = -1f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var n = r.gameObject.name;
                if (n.StartsWith("ShowIf_") || n.StartsWith("HideIf_")) continue;
                if (n.StartsWith("WPN_")) { anchor = r; best = r.bounds.size.magnitude; break; }
                float sz = r.bounds.size.magnitude;
                if (sz > best && sz < 5f) { best = sz; anchor = r; }
            }
            var parent = anchor != null ? anchor.transform : root;

            // ② 关掉旧模型零件（保留配件与特效）
            int hidden = 0, kept = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name;
                if (n.StartsWith("ShowIf_") || n.StartsWith("MuzzleFlash") || n.StartsWith("Particle")) { kept++; continue; }
                if (n.StartsWith("WPN_") || n.StartsWith("HideIf_")) { if (r.enabled) { r.enabled = false; hidden++; res.Hidden.Add(r); } continue; }
                kept++;
            }

            // ③ 我们的实例挂在"枪身零件"的变换帧下（同原点/朝向），缩放补回世界尺度 1
            var go = new GameObject("WeaponModel");
            go.layer = anchor != null ? anchor.gameObject.layer : root.gameObject.layer;
            go.transform.SetParent(parent, false);
            var lossy = parent.lossyScale;
            var inv = new Vector3(
                Mathf.Abs(lossy.x) > 1e-6f ? 1f / lossy.x : 1f,
                Mathf.Abs(lossy.y) > 1e-6f ? 1f / lossy.y : 1f,
                Mathf.Abs(lossy.z) > 1e-6f ? 1f / lossy.z : 1f);
            go.transform.localScale = inv;
            go.transform.localRotation = Quaternion.identity;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // 材质：克隆游戏材质（URP ✓）+ 换我们的贴图
            var srcMat = anchor != null ? anchor.sharedMaterial : null;
            if (srcMat == null)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial != null) { srcMat = r.sharedMaterial; break; }
            Material one = srcMat != null ? new Material(srcMat) : null;
            if (one != null)
            {
                one.name = "WeaponModel_mat";
                if (texture != null)
                {
                    if (one.HasProperty("_BaseMap")) one.SetTexture("_BaseMap", texture);
                    else if (one.HasProperty("_MainTex")) one.SetTexture("_MainTex", texture);
                    if (one.HasProperty("_BaseColor")) one.SetColor("_BaseColor", Color.white);
                }
            }
            int subCount = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[subCount];
            for (int i = 0; i < subCount; i++) mats[i] = one;
            mr.sharedMaterials = mats;

            // ④ 对齐：默认以游戏那把枪的局部包围盒为基准（原点在它包围盒里的归一化位置 → 映射到我们包围盒同一位置）
            Vector3 target = Vector3.zero;
            res.AlignSource = "按原枪包围盒归一化映射";
            if (anchor != null)
            {
                var refB = anchor.localBounds;
                var myB = mr.localBounds;
                var frac = new Vector3(
                    Mathf.Approximately(refB.size.x, 0f) ? 0.5f : (0f - refB.min.x) / refB.size.x,
                    Mathf.Approximately(refB.size.y, 0f) ? 0.5f : (0f - refB.min.y) / refB.size.y,
                    Mathf.Approximately(refB.size.z, 0f) ? 0.5f : (0f - refB.min.z) / refB.size.z);
                target = new Vector3(myB.min.x + frac.x * myB.size.x,
                                     myB.min.y + frac.y * myB.size.y,
                                     myB.min.z + frac.z * myB.size.z);
            }

            // ⭐ slots：先把每个 [L,H,D] 比例换算成“我们模型局部坐标里的点” ✓；
            //    然后有 pivot → 用它当对齐基准（比包围盒映射准 ✓）；否则 Muzzle+Stock 都给 → 用它们的中点 ✓
            if (slots != null && slots.Count > 0)
            {
                var b = mr.localBounds;
                foreach (var kv in slots)
                {
                    var k = kv.Value;
                    res.SlotPoints[kv.Key] = new Vector3(
                        b.min.x + Mathf.Clamp01(k.z) * b.size.x,   // D → X ✓
                        b.min.y + Mathf.Clamp01(k.y) * b.size.y,   // H → Y ✓
                        b.min.z + Mathf.Clamp01(k.x) * b.size.z);  // L → Z ✓（枪口 +Z）
                }
                if (res.SlotPoints.TryGetValue("pivot", out var pv))
                { target = pv; res.AlignSource = "slots.pivot"; }
                else if (res.SlotPoints.TryGetValue("Muzzle", out var mz) && res.SlotPoints.TryGetValue("Stock", out var st))
                { target = (mz + st) * 0.5f; res.AlignSource = "slots.Muzzle+Stock 中点"; }
            }
            go.transform.localPosition = extraOffset - target;

            // ⑤ slots → 把**游戏自己的挂点**（`Sockets/<槽位>`）搬到我模型上的对应位置 ✓
            //    游戏随后会把配件 instance 挂到那个 socket 上 ✓ → 配件就落在**我们模型的形状**上 ✓✓
            //    ⭐ 只动“挂点” ✗ 不动配件 ✓（不去跟游戏的装配逻辑打架 ✓）
            int moved = 0;
            if (res.SlotPoints.Count > 0)
            {
                var sockets = FindSockets(root);
                foreach (var kv in res.SlotPoints)
                {
                    if (string.Equals(kv.Key, "pivot", StringComparison.OrdinalIgnoreCase)) continue;   // pivot 不是挂点 ✓（它是手抓位置 ✓）
                    if (!sockets.TryGetValue(kv.Key, out var sk) || sk == null) continue;
                    if (!res.MovedSockets.ContainsKey(sk)) res.MovedSockets[sk] = sk.localPosition;      // 记原值 ✓ 便于恢复 ✓
                    sk.position = go.transform.TransformPoint(kv.Value);   // 用**世界坐标**赋值 → 自动处理缩放/父子 ✓✓
                    moved++;
                }
            }

            res.Applied = true; res.Instance = go;
            res.Report = $"已换模型：隐藏旧零件 {hidden} 个（保留 {kept} 个：配件/特效）；锚点={(anchor != null ? anchor.name : "根节点")}；"
                       + $"材质={(one != null ? one.name + "/" + (one.shader != null ? one.shader.name : "?") : "无")}；"
                       + $"对齐={res.AlignSource}；"
                       + (moved > 0 ? $"挂点已搬 {moved} 个；" : "")
                       + (res.SlotPoints.Count > 0
                           ? $"槽位(局部米)={string.Join(" ", System.Linq.Enumerable.Select(res.SlotPoints, kv => kv.Key + "=" + kv.Value.ToString("F3")))}；"
                           : "")
                       + $"我们的包围盒={mesh.bounds.size}；原枪身包围盒={(anchor != null ? anchor.localBounds.size.ToString() : "-")}";
            return res;
        }
    }
}
