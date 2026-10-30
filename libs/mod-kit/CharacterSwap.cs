// CharacterSwap.cs —— 角色替换流程：身体对齐、源骨架创建、渲染器替换、隐藏与还原。
//
// 游戏 Avatar 是 Generic，不能使用 HumanPoseHandler。当前实现保留源网格、源权重与源骨架，
// 由 CharacterRetarget 在 LateUpdate 把游戏骨骼相对 bind pose 的旋转增量重定向到源关节。
// 原鸭子骨骼继续由 Animator 驱动；身体渲染器改用源骨骼。不复制骨骼位置，不改变源模型的骨长。
//
// 身体仍用地标做统一旋转、缩放和平移。手部、背部等挂点下的物品不隐藏，见 KeepVisible。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
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
            /// <summary>外部关节里没映射上的数量。它们保持源参考姿势，源权重仍保留。</summary>
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
            internal CharacterRetarget Retarget;
            internal Transform[] OriginalBones;
            internal Transform OriginalRootBone;
            internal bool OriginalUpdateWhenOffscreen;
            internal float _nextReassert;
            /// <summary>这段时间内**每帧**重申（换好之后的新部件会陆续出现，逐帧扫才没有可见延迟）。</summary>
            internal float _fastUntil;

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
                try { if (Retarget != null) Retarget.Reassert(Body); }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] reassert source bones failed: " + ex.Message); }
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
                        Body.bones = OriginalBones;
                        Body.rootBone = OriginalRootBone;
                        Body.updateWhenOffscreen = OriginalUpdateWhenOffscreen;
                    }
                    if (_current == this)
                    {
                        _current = null;
                        if (_hooked) { try { ItemHooks.OnGraphicReady -= OnGraphicReady; } catch { } _hooked = false; }
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] restore mesh failed: " + ex.Message); }
                if (Retarget != null) { Retarget.Dispose(); Retarget = null; }
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
        static bool _hooked;

        /// <summary>`ItemGraphicInfo.Setup` 的钩子：游戏给角色造出**新部件**（头发、眼睛、帽子、鞋、尾巴）时
        /// 立刻判断并隐藏，不必等一秒的循环。
        ///
        /// <para>本仓的定法：钩子优先，循环只做兜底（与 `ItemHooks` 那套一致）。</para>
        ///
        /// <para>为什么这些部件来得晚：它们由物品外观系统生成（名字都是 `IG_*`），
        /// 在关卡初始化之后才挂到角色的骨骼挂点上。</para></summary>
        /// <summary>角色模型自己的钩子。`ItemGraphicInfo.Setup` 那条实测**不覆盖**这些部件
        /// （跑了一次，钩子一次都没命中），所以改挂角色模型的方法。</summary>
        static class ModelHooks
        {
            static bool _installed;
            /// <summary>装钩子（幂等）。装不上不得抛出去。</summary>
            public static void Install()
            {
                if (_installed) return;
                _installed = true;
                try
                {
                    var h = new Harmony("gamer-agent-pi.mod-kit.character-swap");
                    bool a = Patch(h, "AddSubVisuals");
                    bool b = Patch(h, "SetFaceFromData");
                    bool c = Patch(h, "SetFaceFromPreset");
                    bool d = Patch(h, "OnHelmatSlotContentChange");
                    bool e = Patch(h, "OnFaceMaskSlotContentChange");
                    Debug.Log($"[CharacterSwap] character model hooks: AddSubVisuals={a} SetFaceFromData={b} "
                              + $"SetFaceFromPreset={c} HelmatSlot={d} FaceMaskSlot={e}");
                }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] model hooks failed: " + ex.Message); }
            }

            static bool Patch(Harmony h, string name)
            {
                try
                {
                    var m = AccessTools.Method(typeof(CharacterModel), name);
                    if (m == null) return false;
                    h.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ModelHooks), nameof(Post))));
                    return true;
                }
                catch { return false; }
            }

            /// <summary>任何一个 hook 命中，就立刻把属于我们的部件藏干净（判据仍然是 HideOne）。</summary>
            static void Post(CharacterModel __instance)
            {
                try
                {
                    var r = _current;
                    if (r == null || !r.Applied || __instance == null || r.Character == null) return;
                    if (__instance.transform != r.Character.transform && !IsUnder(r.Character.transform, __instance.transform)) return;
                    int before = r.Disabled.Count;
                    HideAllBut(r.Character, __instance, r.Renderers, r.Body, r);
                    int hid = r.Disabled.Count - before;
                    if (hid > 0) Debug.Log($"[CharacterSwap] model hook hid {hid} renderer(s) (total {r.Disabled.Count})");
                }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] model hook failed: " + ex.Message); }
            }

            static bool IsUnder(Transform root, Transform t)
            {
                for (var cur = t; cur != null; cur = cur.parent) if (cur == root) return true;
                return false;
            }
        }

        static void OnGraphicReady(ItemStatsSystem.Item item, ItemGraphicInfo graphic)
        {
            try
            {
                var r = _current;
                if (r == null || !r.Applied || graphic == null || r.Character == null) return;
                var root = r.Character.transform;
                var t = graphic.transform;
                bool mine = false;
                for (var cur = t; cur != null; cur = cur.parent) if (cur == root) { mine = true; break; }
                if (!mine) return;                                     // 别的角色（宠物、商人）不动
                int before = r.Disabled.Count;
                foreach (var rd in graphic.GetComponentsInChildren<Renderer>(true)) HideOne(root, rd, r);
                int hid = r.Disabled.Count - before;
                if (hid > 0) Debug.Log($"[CharacterSwap] graphic hook hid {hid} renderer(s) under {graphic.name} (total {r.Disabled.Count})");
            }
            catch (Exception ex) { Debug.LogWarning("[CharacterSwap] graphic hook failed: " + ex.Message); }
        }

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
            // 换好后的一小段逐帧扫（兜底）。主要靠 CharacterModel 的钩子，所以窗口不必长：
            // 15 秒 × 60 帧约 900 次全树扫描，装备多时会占帧。改成 4 秒。
            if (now < r._fastUntil) { r.Reassert(now, 0f); return; }
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
            int reasserted = r.Reasserted;
            r.Restore();                                      // 先归还旧材质、骨骼、渲染器与源骨架，再应用到新角色
            var nr = Apply(mc, r.Skin);
            if (nr.Applied)
            {
                nr.Reasserted = reasserted + 1;
                Debug.Log("[CharacterSwap] re-applied after the game rebuilt the character: " + nr.Report);
            }
            else Debug.LogWarning("[CharacterSwap] re-apply failed: " + nr.Report);
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
        /// <para>**限制**：保留 `vertices`/第一套 `uv`/`triangles`/`boneWeights` 与源节点层级。
        /// 源模型若有第二套 UV（`uv2`）或顶点色会丢（L5）；也只读第一个 skin 与第 0 组关节/权重（M4）。
        /// `CharacterRig.ExtraDuckBones` 是**离线核对用**的（整体换身体时用不到）—— 见 L3。</para>
        ///
        /// <para>调用后请每帧调 `Result.Reassert(Time.unscaledTime)`：游戏会把隐藏的部件改回来（M1）。</para></summary>
        /// <summary>额外倍数（默认 1.0）。
        ///
        /// <para>按「身体高度」对齐只能让**包围盒**一样高。实测这对模型：鸭子的头骨在 Z=0.800，
        /// 鸭猫的头关节在 Y=0.580（占自身高度的 58%）⇒ 比值 0.72。也就是**高瘦对矮胖**：
        /// 包围盒一样高，头却低 28%，看起来就小一截。</para>
        ///
        /// <para>想让**头顶**对齐到鸭子的高度，把这里设成 1.38（模型总高会到约 1.45 m）。
        /// 折中值 1.2 也不难看。</para></summary>
        public static float ExtraScale = 1f;

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

            // ③ 身体地标对齐：源骨架根乘同一个旋转、缩放和平移。
            // 地标对齐只决定身体的整体位置与大小；逐帧骨骼旋转由 CharacterRetarget 负责。
            Matrix4x4 align;
            string alignWhy;
            float scale;
            var duckMesh = body.sharedMesh;                        // 换网格**之前**拿到鸭子自己的网格
            if (!TryBodyAlignment(skin, gameBones, gamePoses, duckMesh, out align, out alignWhy, out scale))
            {
                res.Report = "cannot align the model to the character body: " + alignWhy;
                return res;
            }

            // ④ 保留源网格、源权重与源骨架。逆绑定矩阵和节点层级使用与顶点相同的坐标转换。
            var srcMesh = skin.Mesh;
            if (skin.Weights == null || skin.Weights.Length != srcMesh.vertexCount)
            {
                res.Report = "the model has no usable skin weights";
                return res;
            }
            srcMesh.boneWeights = skin.Weights;               // 下标仍是源 joints；SourceBones 也按同一顺序创建
            var convert = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)) * skin.VertexTransform;
            var inverseConvert = convert.inverse;
            var sourcePoses = new Matrix4x4[skin.InverseBind.Length];
            for (int i = 0; i < sourcePoses.Length; i++)
                sourcePoses[i] = convert * skin.InverseBind[i] * inverseConvert;
            srcMesh.bindposes = sourcePoses;

            string retargetError;
            var retarget = CharacterRetarget.Create(body.transform, skin, gameBones, gamePoses, map, align, out retargetError);
            if (retarget == null)
            {
                res.Report = "cannot create the source rig: " + retargetError;
                return res;
            }
            res.Retarget = retarget;
            var mesh = srcMesh;

            // ⑤ 原鸭子骨骼继续由 Animator 驱动；身体渲染器改用源骨骼。身体对齐放在源骨架根上。
            res.OriginalMesh = body.sharedMesh;
            res.OriginalMaterial = body.sharedMaterial;
            res.OriginalBones = body.bones;
            res.OriginalRootBone = body.rootBone;
            res.OriginalUpdateWhenOffscreen = body.updateWhenOffscreen;
            var origBounds = res.OriginalMesh != null ? res.OriginalMesh.bounds.size : Vector3.zero;  // L1：必须在赋值前取
            res.NewMesh = mesh;
            if (!_hooked)
            {
                _hooked = true;
                try
                {
                    ItemHooks.Install();                               // 幂等；本 mod 可能不是那个装钩子的 mod
                    ItemHooks.OnGraphicReady += OnGraphicReady;
                    ModelHooks.Install();
                }
                catch (Exception ex) { Debug.LogWarning("[CharacterSwap] hook install failed: " + ex.Message); }
            }
            res.CharacterModel = cm;
            res.Renderers = renderers;
            res.Character = character;
            res.Skin = skin;
            try { res._fastUntil = Time.unscaledTime + 4f; } catch { res._fastUntil = 0f; }
            _current = res;
            body.sharedMesh = mesh;
            body.bones = retarget.SourceBones;
            body.rootBone = retarget.RootBone;
            body.updateWhenOffscreen = true;                 // 蒙皮后的 bounds 随源骨架与整体对齐变化
            retarget.Apply();
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
            res.Report = $"swapped the body mesh with runtime source-rig retargeting: {srcMesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles,"
                       + $" joints mapped {res.MappedJoints}/{skin.JointNames.Length}"
                       + (res.UnmappedJoints > 0 ? $" (unmapped, kept at source rest: {string.Join(", ", unmapped.ToArray())})" : "")
                       + $", hidden parts {res.Disabled.Count}"
                       + $", new mesh bounds {mesh.bounds.size} (body was {origBounds})"
                       + (skin.VertexTransform != Matrix4x4.identity ? ", WARN front!=auto or zeroGrip:true is not compensated; reload with front=auto, zeroGrip:false" : "")
                       + $", scale {scale:0.###}"
                       + "";
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
            scale *= Mathf.Clamp(ExtraScale, 0.1f, 4f);

            var lin = Matrix4x4.Scale(new Vector3(scale, scale, scale)) * rot;
            var t = duckPelvis - lin.MultiplyPoint3x4(srcPelvis);
            align = lin;
            align.m03 = t.x; align.m13 = t.y; align.m23 = t.z;
            return true;
        }

        /// <summary>隐藏一个渲染器（保留名单里的、以及身体本身以外的都藏）。
        /// <para>循环与钩子共用这一个判据，不得写两份。</para></summary>
        static void HideOne(Transform root, Renderer r, Result res)
        {
            if (r == null || !r.enabled || res == null || r == res.Body) return;
            if (root != null && UnderKeepVisible(root, r.transform)) return;   // 手里/背上的物品不藏
            r.enabled = false;
            if (!res.Disabled.Contains(r)) res.Disabled.Add(r);   // 游戏重开某个部件时不得重复记录
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
                if (r == null || r == body) return;
                HideOne(character != null ? character.transform : null, r, res);
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

    }
}
