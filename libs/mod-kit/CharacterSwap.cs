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
            internal Component Character;
            internal GltfLoader.Loaded Skin;
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
                if (CharacterModel != null && Renderers != null) HideAllBut(Character, CharacterModel, Renderers, Body, this);
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
                    if (_current == this) _current = null;
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

        static Result _current;

        /// <summary>每帧调一次（推荐入口）。做两件事：重申隐藏与网格；角色被游戏重建时**整个重新搬运一遍**。
        /// <para>实测：搬运会发生在 `[Level Initialization] Done!` **之前**，关卡初始化可能重建角色 ⇒
        /// 不重来的话换上去的网格会消失。</para></summary>
        public static void Tick(float now, float interval = 1f)
        {
            var r = _current;
            if (r == null || !r.Applied) return;
            // 角色或身体渲染器被销毁（Unity 的假 null 判定）⇒ 关卡重建了角色 ⇒ 重新搬运
            if (r.Body == null || r.Character == null)
            {
                if (now < r._nextReassert) return;
                r._nextReassert = now + interval;
                Reapply();
                return;
            }
            // ⚠️ 限流只在这里做、只做一次：以前 Tick 先设 `_nextReassert` 再调 Reassert，
            // 而 Reassert 自己也会查同一个字段 ⇒ 它**永远**提前返回 ⇒ 重申从来没执行过
            // （实测：头发/眼睛/眉毛/翅膀/鞋/尾巴都在搬运之后才生成，因此一直露着）。
            r.Reassert(now, interval);
        }

        static void Reapply()
        {
            var r = _current;
            if (r == null || r.Skin == null) return;
            var mc = r.Character != null ? r.Character : MainCharacter();
            if (mc == null) return;                       // 关卡还没起来 ⇒ 下一 tick 再试
            var nr = Apply(mc, r.Skin);
            if (nr.Applied)
            {
                nr.Reasserted = r.Reasserted + 1;
                Debug.Log("[CharacterSwap] re-applied after the game rebuilt the character: " + nr.Report);
            }
        }

        /// <summary>隐藏时要**保留**的名字。分两类：
        /// <para>① 玩家装备的挂点。这些子树里的渲染器是**物品**（枪、背包、护甲），不是角色本体。</para>
        /// <para>② 非角色几何。飘字挂点、跑步粒子、草地碰撞体。</para>
        /// <para>名字都来自运行时层级实测（设计仓 `10-character-replacement-research.md` §4.5）。
        /// 头部的挂点（`HairSocket`、`HelmatSocket`、`FaceMaskSocket`、`MouthSocket`）**故意不在**名单里：
        /// 脸上的东西一律隐藏，否则会和我们的头穿插。</para></summary>
        static readonly HashSet<string> KeepVisible = new HashSet<string>(StringComparer.Ordinal)
        {
            "Hand.Soket.L", "Hand.Soket.R", "HandObj.L", "HandObj.R",
            "LeftHandSocket", "RightHandSocket", "MeleeWeaponSocket", "TestBowSocket",
            "BackpackSocket", "ArmorSocket",
            "PopTextSocket", "RunParticle", "GrassCollider",
        };

        /// <summary>当前关卡的主角色（游戏自己维护的引用，不得用 FindObjectsOfType）。</summary>
        static Component MainCharacter()
        {
            try
            {
                var lm = LevelManager.Instance;
                return lm != null ? lm.MainCharacter : null;
            }
            catch { return null; }
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
        /// <summary>把手的权重并到前臂上（「不绑定手」）。实测我们的模型是刚性放置的，
        /// 而手骨绕鸭子自己的手腕转，转轴落在模型的前臂里面 ⇒ 手部看起来会扭。
        /// 取 false 时手部只跟前臂走，不再自己转。</summary>
        public static bool BindHandBones = true;

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

            // ③ 身体地标对齐：把整个模型**刚性**地放进鸭子的网格空间。
            //
            // 为什么不再逐骨重定姿态（实测结论，见设计仓 10-character-replacement-research.md §6）：
            // 逐骨用 `G_b · S_b⁻¹` 保留「骨局部坐标」，等于假定两副骨架的**骨局部朝向约定相同**。
            // 实测这对骨架完全不同：逐骨变换相对全局对齐的偏差平均 120 度、最大 177 度
            // （比例全是 1.000 ⇒ 纯旋转问题）。于是每个部件各自被转了不同角度 ⇒ 模型拧成一团。
            // 地标对齐只用「上」与「左」两个身体方向，不可能拧。
            Matrix4x4 align;
            string alignWhy;
            float scale;
            var duckMesh = body.sharedMesh;                        // 换网格**之前**拿到鸭子自己的网格
            if (!TryBodyAlignment(skin, gameBones, gamePoses, duckMesh, out align, out alignWhy, out scale))
            {
                res.Report = "cannot align the model to the character body: " + alignWhy;
                return res;
            }

            // ④ 顶点按同一个矩阵刚性搬运；权重只换骨骼下标并归一化（位置不再逐骨分化）
            var srcMesh = skin.Mesh;
            var sv = srcMesh.vertices;
            var nv = new Vector3[sv.Length];
            var nw = new BoneWeight[sv.Length];
            for (int i = 0; i < sv.Length; i++)
            {
                nv[i] = align.MultiplyPoint3x4(sv[i]);
                var bw = i < skin.Weights.Length ? skin.Weights[i] : new BoneWeight();
                var keepIdx = new int[4];
                var keepW = new float[4];
                int keep = 0;
                float wsum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int ji = BoneIndex(bw, k);
                    float w = BoneWeightAt(bw, k);
                    if (w <= 0.0001f || ji < 0 || ji >= map.Length) continue;
                    int gi = map[ji];
                    if (gi < 0) continue;                                  // 没映射上 ⇒ 这根骨的权重丢掉
                    if (!BindHandBones) gi = RedirectHand(gameBones, gi);   // 实验：手不单独绑
                    wsum += w;
                    if (keep < 4) { keepIdx[keep] = gi; keepW[keep] = w; keep++; }
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
                else
                {
                    res.UnweightedVertices++;                              // 不得静默 ✗
                }
                nw[i] = outW;
            }

            // ⑤ 造新网格：顶点已搬进**游戏的绑定空间** ⇒ 绑定姿势与骨骼都换成游戏的
            var mesh = new Mesh { name = "char_swap_" + srcMesh.name };
            if (nv.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = nv;
            mesh.uv = srcMesh.uv;
            // 对齐是纯旋转（行列式 +1）⇒ 绕序不用翻；镜像在加载期已经处理过
            mesh.triangles = srcMesh.triangles;
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
            res.Character = character;
            res.Skin = skin;
            _current = res;
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

            HideAllBut(character, cm, renderers, body, res);

            res.Applied = true;
            res.Report = $"swapped the body mesh: {nv.Length} vertices, {mesh.triangles.Length / 3} triangles,"
                       + $" joints mapped {res.MappedJoints}/{skin.JointNames.Length}"
                       + (res.UnmappedJoints > 0 ? $" (unmapped: {string.Join(", ", unmapped.ToArray())})" : "")
                       + (res.UnweightedVertices > 0 ? $", {res.UnweightedVertices} vertices had no mapped weight (kept in place)" : "")
                       + $", hidden parts {res.Disabled.Count}"
                       + $", new mesh bounds {mesh.bounds.size} (body was {origBounds})"
                       + (skin.VertexTransform != Matrix4x4.identity ? ", WARN the model was loaded with front!=auto or zeroGrip:true (now compensated, but load it with front=auto, zeroGrip:false)" : "")
                       + $", scale {scale:0.###}"
                       + (res.UnweightedVertices > 0 ? $", {res.UnweightedVertices} vertices had no mapped weight" : "");
            return res;
        }

        /// <summary>用身体地标把源模型刚性对齐到鸭子：骨盆到头的方向为「上」，左腿到右腿的方向为「左」，
        /// 第三个轴由叉乘得出。两边用**同一个构造**，所以只能得到旋转，不会得到镜像或拧。
        ///
        /// <para>源侧地标取自 `inverseBindMatrices` 的平移列，并做两件事：乘以加载期的顶点净变换
        /// （`front` 旋转与握把归零）；再取反 X —— 因为加载器已经取反过顶点的 X，地标不跟着取反就会左右翻。</para></summary>
        static bool TryBodyAlignment(GltfLoader.Loaded skin, Transform[] gameBones, Matrix4x4[] gamePoses,
                                     Mesh duckMesh, out Matrix4x4 align, out string why, out float scale)
        {
            align = Matrix4x4.identity;
            why = "";
            scale = 1f;
            Vector3 srcPelvis, srcHead, srcLeft, srcRight;
            if (!SourceLandmark(skin, "Pelvis", out srcPelvis) || !SourceLandmark(skin, "Head", out srcHead)
                || !SourceLandmark(skin, "Thigh.L", out srcLeft) || !SourceLandmark(skin, "Thigh.R", out srcRight))
            { why = "the model is missing hips, head or leg joints"; return false; }

            Vector3 duckPelvis, duckHead, duckLeft, duckRight;
            if (!DuckLandmark(gameBones, gamePoses, "Pelvis", out duckPelvis)
                || !DuckLandmark(gameBones, gamePoses, "Head", out duckHead)
                || !DuckLandmark(gameBones, gamePoses, "Thigh.L", out duckLeft)
                || !DuckLandmark(gameBones, gamePoses, "Thigh.R", out duckRight))
            { why = "the character is missing hips, head or leg bones"; return false; }

            Matrix4x4 fs, fd;
            if (!BodyFrame(srcPelvis, srcHead, srcLeft, srcRight, out fs)
                || !BodyFrame(duckPelvis, duckHead, duckLeft, duckRight, out fd))
            { why = "the body landmarks are degenerate"; return false; }

            var rot = fd * fs.transpose;                       // 正交基 ⇒ 转置就是逆
            rot.m03 = 0f; rot.m13 = 0f; rot.m23 = 0f; rot.m33 = 1f;

            // 尺寸：按「两侧身体的**包围盒高度**」等比缩放，让模型与角色同高。
            // 实测（鸭猫）：模型 1.00 m、鸭子 1.05 m ⇒ 缩放 1.05。不缩放就会“稍微小一些”。
            const float MinScale = 0.25f, MaxScale = 4f;
            scale = 1f;
            // ⚠️ 用**真实顶点跨度**，不要用「包围盒尺寸投影到上轴」：上轴一旦倾斜，
            // 投影会把尺寸放大（实测：源模型上轴偏 6 度 ⇒ 算出 1.11 而不是 1.00
            // ⇒ 缩放被算成 0.97，模型反而变小）。
            float srcH = ExtentAlong(skin.Mesh, col1(fs));
            float duckH = ExtentAlong(duckMesh, col1(fd));
            if (srcH > 1e-4f && duckH > 1e-4f) scale = Mathf.Clamp(duckH / srcH, MinScale, MaxScale);

            var lin = Matrix4x4.Scale(new Vector3(scale, scale, scale)) * rot;
            var t = duckPelvis - lin.MultiplyPoint3x4(srcPelvis);
            align = lin;
            align.m03 = t.x; align.m13 = t.y; align.m23 = t.z;
            return true;
        }

        /// <summary>坐标架的第二列 = 「上」。</summary>
        static Vector3 col1(Matrix4x4 m) { return new Vector3(m.m01, m.m11, m.m21); }

        /// <summary>网格顶点沿指定方向的**真实跨度**（最大投影减最小投影）。</summary>
        static float ExtentAlong(Mesh mesh, Vector3 axis)
        {
            if (mesh == null) return 0f;
            var vs = mesh.vertices;
            if (vs == null || vs.Length == 0) return 0f;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < vs.Length; i++)
            {
                float d = Vector3.Dot(vs[i], axis);
                if (d < lo) lo = d;
                if (d > hi) hi = d;
            }
            return hi - lo;
        }

        /// <summary>源关节的绑定位置（先做顶点净变换，再取反 X）。用「映射到哪根鸭骨」反查关节。</summary>
        static bool SourceLandmark(GltfLoader.Loaded skin, string duckBone, out Vector3 p)
        {
            p = Vector3.zero;
            for (int i = 0; i < skin.JointNames.Length; i++)
            {
                string duck;
                if (!CharacterRig.TryMap(skin.JointNames[i], out duck)
                    || !string.Equals(duck, duckBone, StringComparison.Ordinal)) continue;
                if (i >= skin.InverseBind.Length) return false;
                var world = skin.InverseBind[i].inverse;
                var v = skin.VertexTransform.MultiplyPoint3x4(new Vector3(world.m03, world.m13, world.m23));
                p = new Vector3(-v.x, v.y, v.z);
                return true;
            }
            return false;
        }

        /// <summary>游戏骨骼的绑定位置（`bindposes[骨]` 的逆的平移列）。</summary>
        static bool DuckLandmark(Transform[] gameBones, Matrix4x4[] gamePoses, string name, out Vector3 p)
        {
            p = Vector3.zero;
            for (int j = 0; j < gameBones.Length; j++)
            {
                if (gameBones[j] == null || !string.Equals(gameBones[j].name, name, StringComparison.Ordinal)) continue;
                if (j >= gamePoses.Length) return false;
                var w = gamePoses[j].inverse;
                p = new Vector3(w.m03, w.m13, w.m23);
                return true;
            }
            return false;
        }

        /// <summary>由「上」与「左」建正交基（列 = 左 / 上 / 前）。两边同构造 ⇒ 只能得到旋转。</summary>
        static bool BodyFrame(Vector3 pelvis, Vector3 head, Vector3 leftHip, Vector3 rightHip, out Matrix4x4 m)
        {
            m = Matrix4x4.identity;
            var up = head - pelvis;
            if (up.sqrMagnitude < 1e-8f) return false;
            up = up.normalized;
            var left = leftHip - rightHip;
            left -= up * Vector3.Dot(left, up);
            if (left.sqrMagnitude < 1e-8f) return false;
            left = left.normalized;
            var fwd = Vector3.Cross(left, up);
            m.m00 = left.x; m.m10 = left.y; m.m20 = left.z;    // 第 0 列 = 左
            m.m01 = up.x;   m.m11 = up.y;   m.m21 = up.z;      // 第 1 列 = 上
            m.m02 = fwd.x;  m.m12 = fwd.y;  m.m22 = fwd.z;     // 第 2 列 = 前
            m.m33 = 1f;
            return true;
        }

        /// <summary>把除身体以外的渲染器全部停掉：`renderers` 列表、`subVisuals[].renderers`、`customFace`。</summary>
        static void HideAllBut(Component character, object cm, IList renderers, SkinnedMeshRenderer body, Result res)
        {
            // ⚠️ **每一遍**都要查保留名单，不能只在最后一遍查。
            // 实测踩过：`customFace` 指的是 `CustomFaceInstance`，而它是**整个模型的根**
            // （下面挂着 `Armature`，手里的枪也在里面）⇒ 那一遍把枪一起藏了 ⇒ 游戏里枪消失。
            Action<object> hide = (o) =>
            {
                var r = o as Renderer;
                if (r == null || r == body || !r.enabled) return;
                if (character != null && UnderKeepVisible(character.transform, r.transform)) return;
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

            // ③ 兜底：沿**角色整棵层级**再扫一遍，凡不在保留名单里的渲染器一律隐藏。
            // 实测：五官（`Duck_Beak`、`Duck_Eye.L/R`）、头发（`HairSocket` 下）、脚（`Sphere`、`Duck_Foot_L/R`）、
            // 翅膀（`Wings.L/R`）、手臂网格（`Arms`）都不在 `CharacterModel.renderers` 里。
            // 只靠那个列表会「五官头发还露着」（实测踩过）。
            if (character != null)
                foreach (var r in character.GetComponentsInChildren<Renderer>(true)) hide(r);
        }

        /// <summary>`Hand.L/R` 换成 `ForeArm.L/R`（「不绑定手」实验用）。找不到就原样返回。</summary>
        static int RedirectHand(Transform[] bones, int gi)
        {
            if (bones == null || gi < 0 || gi >= bones.Length || bones[gi] == null) return gi;
            string target;
            switch (bones[gi].name)
            {
                case "Hand.L": target = "ForeArm.L"; break;
                case "Hand.R": target = "ForeArm.R"; break;
                default: return gi;
            }
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && string.Equals(bones[i].name, target, StringComparison.Ordinal)) return i;
            return gi;
        }

        /// <summary>从 `t` 往上走到 `root`，路上碰到保留名单里的名字就返回 true（那个渲染器是物品或特效）。</summary>
        static bool UnderKeepVisible(Transform root, Transform t)
        {
            for (var cur = t; cur != null && cur != root; cur = cur.parent)
                if (KeepVisible.Contains(cur.name)) return true;
            return false;
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
