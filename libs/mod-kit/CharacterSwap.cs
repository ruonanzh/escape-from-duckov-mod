// CharacterSwap.cs —— 角色替换的**运行时一半**：把外部带骨骼的网格搬到游戏角色的骨架上。
//
// 依据：设计仓 `docs/unity-3d-assets/10-character-replacement-research.md` §4 与 §4.5。
//
// 为什么这样做（实测结论）：
//   · 游戏的角色 Avatar **不是 Humanoid**（`avatar.isHuman = False`）⇒ Unity 的人形重定向用不上，
//     `HumanPoseHandler` 也不可用。所以要**自己把顶点搬到游戏的骨架空间**。
//   · 玩家的身体是**一个蒙皮网格** `DuckBody`（`SkinnedMeshRenderer`，26 个骨骼槽、bindposes 26）。
//   · 按玩家批注的简化方案：**换整个身体 + 把游戏原有的头/五官/翅膀/鞋/装备全部隐藏**，
//     不做「把我们的部件一个个挂到游戏骨骼上」那条路（§3.4）。
//
// 搬运公式（每个顶点）：
//     v' = Σ_b  w_b · ( 游戏绑定世界_b · 外部绑定世界_b⁻¹ · v )
//   其中「绑定世界」 = `bindposes[i].inverse`（glTF 侧是 `inverseBindMatrices[i].inverse`）。
//   搬完以后把 `bindposes` 换成**游戏的**、`bones` 也换成游戏的 ⇒ 之后完全由游戏的动画驱动
//   （零每帧开销；不需要每帧搬运）。
//
// 权重归属：外部关节先按 `CharacterRig` 映射到游戏骨骼下标；**没映射上的关节，它的权重被丢掉并按比例归一化**，
// 丢掉多少会在 `Result.Report` 里写清楚（不得静默 ✗）。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    public static class CharacterSwap
    {
        public sealed class Result
        {
            public bool Applied;
            public bool NoOp;
            public string Report = "";
            /// <summary>被换掉网格的那个蒙皮渲染器（身体）</summary>
            public SkinnedMeshRenderer Body;
            /// <summary>外部关节里**成功映射**到游戏骨骼的数量</summary>
            public int MappedJoints;
            /// <summary>外部关节里**没映射上**的数量（它们的权重会被丢掉）</summary>
            public int UnmappedJoints;
            /// <summary>因为权重全丢而保持原位的顶点数</summary>
            public int UnweightedVertices;

            /// <summary>我们换上去的网格与克隆出来的材质（重申与还原用）</summary>
            internal Mesh NewMesh;
            internal Material NewMaterial;
            /// <summary>隐藏过几次（游戏会把部件改回来 ⇒ 重申）。大于 0 说明游戏确实动过。</summary>
            public int Reasserted;
            // 还原用（热重载与卸载）
            internal Mesh OriginalMesh;
            internal Material OriginalMaterial;
            internal readonly List<Renderer> Disabled = new List<Renderer>();
            internal object CharacterModel;
            internal IList Renderers;
            internal float _nextReassert;

            /// <summary>游戏会**把被隐藏的部件改回来**（换装备、刷新模型、切场景），也会把网格换回去。
            /// <para>每帧调一次即可，内部自己限流（默认 1 秒一次）。物品侧同样问题已用周期重申解决。</para></summary>
            public void Reassert(float now, float interval = 1f)
            {
                if (!Applied || now < _nextReassert) return;
                _nextReassert = now + interval;
                try
                {
                    if (Body != null && NewMesh != null && Body.sharedMesh != NewMesh)
                    {
                        Body.sharedMesh = NewMesh;                 // 游戏把网格换回去了 ⇒ 再换回来
                        Reasserted++;
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] reassert mesh failed: " + ex.Message); }
                // 再扫一遍：既重新隐藏被打开的，也顺手隐藏新出现的部件（换了装备会有新的渲染器）
                if (CharacterModel != null && Renderers != null) HideAllBut(CharacterModel, Renderers, Body, this);
                else foreach (var r in Disabled)
                {
                    try { if (r != null && r.enabled) { r.enabled = false; Reasserted++; } } catch { }
                }
            }

            /// <summary>把身体网格与材质放回去，并把隐藏的部件重新显示（有借有还）。</summary>
            public void Restore()
            {
                try
                {
                    if (Body != null)
                    {
                        Body.sharedMesh = OriginalMesh;
                        Body.sharedMaterial = OriginalMaterial;
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] restore mesh failed: " + ex.Message); }
                if (NewMaterial != null)
                {
                    // 克隆那份是我们造的 ⇒ 必须自己销毁（不销毁就每次热重载漏一份）
                    try { UnityEngine.Object.Destroy(NewMaterial); } catch { }
                    NewMaterial = null;
                }
                foreach (var r in Disabled)
                {
                    try { if (r != null) r.enabled = true; } catch { }
                }
                Disabled.Clear();
            }
        }

        /// <summary>把 `skin`（`GltfLoader` 读出来的带骨骼网格）装到 `character`（`CharacterMainControl` 那个组件）身上。
        ///
        /// 要求：`skin.HasSkin` 为真（先用 `generate_model` 的 `action=rig` 绑过骨）。
        /// 只处理**人形**骨骼名（`mixamorig:*` 或标准名）—— 见 `CharacterRig`。
        ///
        /// <para>**限制**：只搬 `vertices`/`uv`/`triangles`/`boneWeights`。源模型若有第二套 UV（`uv2`）或
        /// 顶点色会丢（L5）；也只读第一个 skin 与第 0 组关节/权重（M4）。
        /// `CharacterRig.ExtraDuckBones` 是**离线核对用**的（整体换身体时用不到）—— 见 L3。</para>
        ///
        /// <para>调用后请每帧调 `Result.Reassert(Time.unscaledTime)`：游戏会把隐藏的部件改回来（M1）。</para></summary>
        public static Result Apply(Component character, GltfLoader.Loaded skin)
        {
            var res = new Result();
            if (character == null) { res.Report = "the character is null"; return res; }
            if (skin == null || skin.Mesh == null) { res.Report = "the model is null"; return res; }
            if (!skin.HasSkin)
            {
                res.Report = "this model has no skin (no bones or no weights) - rig it first: generate_model action=rig";
                return res;
            }

            var cm = Member(character, "characterModel");
            if (cm == null) { res.Report = "cannot read CharacterMainControl.characterModel"; return res; }

            // ① 找到身体那个蒙皮渲染器（bones 非空的那个）
            var renderers = Member(cm, "renderers") as IList;
            if (renderers == null) { res.Report = "cannot read CharacterModel.renderers"; return res; }
            SkinnedMeshRenderer body = null;
            foreach (var o in renderers)
            {
                var s = o as SkinnedMeshRenderer;
                if (s != null && s.bones != null && s.bones.Length > 0) { body = s; break; }
            }
            if (body == null) { res.Report = "this character has no skinned body renderer"; return res; }
            res.Body = body;

            var gameBones = body.bones;
            var gamePoses = body.sharedMesh != null ? body.sharedMesh.bindposes : null;
            if (gamePoses == null || gamePoses.Length != gameBones.Length)
            {
                res.Report = $"the body has {gameBones.Length} bones but {gamePoses?.Length ?? 0} bindposes";
                return res;
            }

            // ② 关节映射：外部关节下标 → 游戏骨骼下标（-1 = 没映射上）
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < gameBones.Length; i++)
                if (gameBones[i] != null && !byName.ContainsKey(gameBones[i].name)) byName[gameBones[i].name] = i;

            var map = new int[skin.JointNames.Length];
            var unmapped = new List<string>();
            for (int i = 0; i < map.Length; i++)
            {
                string duck;
                int gi;
                if (CharacterRig.TryMap(skin.JointNames[i], out duck) && byName.TryGetValue(duck, out gi))
                {
                    map[i] = gi;
                    res.MappedJoints++;
                }
                else
                {
                    map[i] = -1;
                    res.UnmappedJoints++;
                    unmapped.Add(skin.JointNames[i]);
                }
            }

            // ③ 两边的「绑定世界矩阵」= bindposes 的逆
            var gameBind = new Matrix4x4[gameBones.Length];
            for (int i = 0; i < gameBones.Length; i++) gameBind[i] = gamePoses[i].inverse;
            // H1：加载期顶点被**旋转/平移**过（`front` 朝向声明 + 握把归零）。骨架必须跟顶点同一坐标系，
            // 所以把同一个变换也左乘上去。漏了它：顶点与骨架错位 ⇒ 角色歪掉或飘走。
            var srcBind = new Matrix4x4[skin.InverseBind.Length];
            for (int i = 0; i < srcBind.Length; i++)
                srcBind[i] = skin.VertexTransform * skin.InverseBind[i].inverse;

            // M2：如果映射链上带**镜像**（行列式 < 0），搬完的三角形绕序会翻 ⇒ `RecalculateNormals` 得到朝内的法线。
            // 用第一根映射上的骨骼判断即可 —— 所有骨骼共享同一个整体手性差。
            bool flipWinding = false;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] < 0) continue;
                flipWinding = (gameBind[map[i]] * srcBind[i].inverse).determinant < 0f;
                break;
            }

            // ④ 顶点搬运 + 权重重映射
            var srcMesh = skin.Mesh;
            var sv = srcMesh.vertices;
            var nv = new Vector3[sv.Length];
            var nw = new BoneWeight[sv.Length];
            for (int i = 0; i < sv.Length; i++)
            {
                var bw = i < skin.Weights.Length ? skin.Weights[i] : new BoneWeight();
                Matrix4x4 acc = new Matrix4x4();
                float wsum = 0f;
                var keepIdx = new int[4];
                var keepW = new float[4];
                int keep = 0;
                for (int k = 0; k < 4; k++)
                {
                    int ji = BoneIndex(bw, k);
                    float w = BoneWeightAt(bw, k);
                    if (w <= 0.0001f || ji < 0 || ji >= map.Length) continue;
                    int gi = map[ji];
                    if (gi < 0) continue;                                  // 没映射上 ⇒ 这根骨的权重丢掉
                    acc = Add(acc, Scale(gameBind[gi] * srcBind[ji].inverse, w));
                    wsum += w;
                    if (keep < 4) { keepIdx[keep] = gi; keepW[keep] = w; keep++; }
                }

                if (wsum <= 0.0001f)
                {
                    // 这件模型的权重一根都没映射上：保持原位置，并记一笔（不得静默 ✗）
                    nv[i] = sv[i];
                    res.UnweightedVertices++;
                }
                else
                {
                    float inv = 1f / wsum;
                    nv[i] = Scale(acc, inv).MultiplyPoint3x4(sv[i]);
                }

                var outW = new BoneWeight();
                if (keep > 0)
                {
                    float inv = 1f / Mathf.Max(0.0001f, wsum);
                    outW.boneIndex0 = keepIdx[0]; outW.weight0 = keepW[0] * inv;
                    if (keep > 1) { outW.boneIndex1 = keepIdx[1]; outW.weight1 = keepW[1] * inv; }
                    if (keep > 2) { outW.boneIndex2 = keepIdx[2]; outW.weight2 = keepW[2] * inv; }
                    if (keep > 3) { outW.boneIndex3 = keepIdx[3]; outW.weight3 = keepW[3] * inv; }
                }
                nw[i] = outW;
            }

            // ⑤ 造新网格：顶点已搬进**游戏的绑定空间** ⇒ 绑定姿势与骨骼都换成游戏的
            var mesh = new Mesh { name = "char_swap_" + srcMesh.name };
            if (nv.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = nv;
            mesh.uv = srcMesh.uv;
            var srcTris = srcMesh.triangles;
            if (flipWinding)                                  // M2：镜像 ⇒ 绕序翻回来，否则法线朝内
                for (int t = 0; t + 2 < srcTris.Length; t += 3)
                {
                    var tmp = srcTris[t + 1]; srcTris[t + 1] = srcTris[t + 2]; srcTris[t + 2] = tmp;
                }
            mesh.triangles = srcTris;
            mesh.boneWeights = nw;
            mesh.bindposes = gamePoses;                     // **游戏的绑定姿势**（不得用模型的 ✗）
            // 法线必须重算：顶点被搬过，原来的法线不再成立（切线与法线贴图一起重算）
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // ⑥ 换上去 + 隐藏其余部件（玩家批注：换整个身体，其余全隐藏）
            res.OriginalMesh = body.sharedMesh;
            res.OriginalMaterial = body.sharedMaterial;
            var origBounds = res.OriginalMesh != null ? res.OriginalMesh.bounds.size : Vector3.zero;  // L1：必须在赋值前取
            res.NewMesh = mesh;
            res.CharacterModel = cm;
            res.Renderers = renderers;
            body.sharedMesh = mesh;
            try
            {
                if (body.sharedMaterial != null)
                {
                    var mat = new Material(body.sharedMaterial);      // **克隆**：不动共享材质（会连累别人 ✗）
                    if (skin.MainTexture != null) GameApi.ApplyOurTexture(mat, skin.MainTexture);
                    res.NewMaterial = mat;                            // L2：留给 Restore 销毁
                    body.sharedMaterial = mat;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[CharacterSwap] material swap failed: " + ex.Message); }

            HideAllBut(cm, renderers, body, res);

            res.Applied = true;
            res.Report = $"swapped the body mesh: {nv.Length} vertices, {mesh.triangles.Length / 3} triangles,"
                       + $" joints mapped {res.MappedJoints}/{skin.JointNames.Length}"
                       + (res.UnmappedJoints > 0 ? $" (unmapped: {string.Join(", ", unmapped.ToArray())})" : "")
                       + (res.UnweightedVertices > 0 ? $", {res.UnweightedVertices} vertices had no mapped weight (kept in place)" : "")
                       + $", hidden parts {res.Disabled.Count}"
                       + $", new mesh bounds {mesh.bounds.size} (body was {origBounds})"
                       + (skin.VertexTransform != Matrix4x4.identity ? ", WARN the model was loaded with front!=auto or zeroGrip:true (now compensated, but load it with front=auto, zeroGrip:false)" : "")
                       + (flipWinding ? ", mirrored (triangle winding flipped)" : "");
            return res;
        }

        /// <summary>把除身体以外的渲染器全部停掉：`renderers` 列表、`subVisuals[].renderers`、`customFace`。</summary>
        static void HideAllBut(object cm, IList renderers, SkinnedMeshRenderer body, Result res)
        {
            Action<object> hide = (o) =>
            {
                var r = o as Renderer;
                if (r == null || r == body || !r.enabled) return;
                r.enabled = false;
                res.Disabled.Add(r);
            };
            foreach (var o in renderers) hide(o);

            var subs = Member(cm, "subVisuals") as IList;
            if (subs != null)
                foreach (var sub in subs)
                {
                    var list = Member(sub, "renderers") as IList;
                    if (list != null) foreach (var o in list) hide(o);
                }

            var face = Member(cm, "customFace") as Component;
            if (face != null)
                foreach (var r in face.GetComponentsInChildren<Renderer>(true)) hide(r);
        }

        // ── 反射小工具（游戏字段的公开性不保证，用反射最稳）──
        static object Member(object o, string name)
        {
            if (o == null) return null;
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(o);
                var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanRead) return p.GetValue(o);
            }
            return null;
        }

        /// <summary>Unity 的 `Matrix4x4` **没有加法运算符** ⇒ 自己按 16 个元素加（矩阵有索引器 ✓）。
        /// 顶点搬运要把每根骨贡献的矩阵加权求和，所以必须自己加。</summary>
        static Matrix4x4 Add(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++) a[i] += b[i];
            return a;
        }

        /// <summary>`Matrix4x4 * float` 在游戏这版 UnityEngine 里也没有 ⇒ 自己按元素乘。</summary>
        static Matrix4x4 Scale(Matrix4x4 m, float s)
        {
            for (int i = 0; i < 16; i++) m[i] *= s;
            return m;
        }

        static int BoneIndex(BoneWeight w, int k)
        {
            switch (k)
            {
                case 0: return w.boneIndex0;
                case 1: return w.boneIndex1;
                case 2: return w.boneIndex2;
                default: return w.boneIndex3;
            }
        }

        static float BoneWeightAt(BoneWeight w, int k)
        {
            switch (k)
            {
                case 0: return w.weight0;
                case 1: return w.weight1;
                case 2: return w.weight2;
                default: return w.weight3;
            }
        }
    }
}
