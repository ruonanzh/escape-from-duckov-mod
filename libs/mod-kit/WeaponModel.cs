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
        }

        /// <summary>把手持武器的模型换成给进来的 Mesh+贴图。root 一般是 `itemGraphic.gameObject` 或手持实体的 GameObject。</summary>
        public static Result Apply(Transform root, Mesh mesh, Texture2D texture, Vector3 extraOffset = default)
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
                if (n.StartsWith("WPN_") || n.StartsWith("HideIf_")) { if (r.enabled) { r.enabled = false; hidden++; } continue; }
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

            // ④ 对齐：以游戏那把枪的局部包围盒为基准（原点在它包围盒里的归一化位置 → 映射到我们包围盒同一位置）
            Vector3 target = Vector3.zero;
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
            go.transform.localPosition = extraOffset - target;

            res.Applied = true; res.Instance = go;
            res.Report = $"已换模型：隐藏旧零件 {hidden} 个（保留 {kept} 个：配件/特效）；锚点={(anchor != null ? anchor.name : "根节点")}；"
                       + $"材质={(one != null ? one.name + "/" + (one.shader != null ? one.shader.name : "?") : "无")}；"
                       + $"我们的包围盒={mesh.bounds.size}；原枪身包围盒={(anchor != null ? anchor.localBounds.size.ToString() : "-")}";
            return res;
        }
    }
}
