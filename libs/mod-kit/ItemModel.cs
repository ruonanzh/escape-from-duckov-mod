// ItemModel.cs —— **非武器物品**的外观替换（武器走 WeaponModel ✓ 两套互不掺和 ✗）。
//
// 事实（反编译 + 实测 ✓）：
//   · 物品的世界外观 = 它自己的 `ItemGraphicInfo`（`item.ItemGraphic`）实例化出来的；
//     没有 `ItemGraphic` 的物品退化成"图标精灵"（`CreateAGraphic`: 用 `spriteGraphicPfb` +
//     `fallbackSprite.sprite = item.Icon`）→ 那种我们**不动** ✗（换不了外观 ✓ 直接跳过 ✓）
//   · **非武器物品的图形里常常一个网格都没有** ✗（只有精灵，或者靠 `Setup(item)` 运行时补 ✓）
//     —— 所以"关旧几何/找锚点"**不能**只认 `MeshRenderer`/`SkinnedMeshRenderer` ✗
//   · `ItemAgent.AgentTypes = { normal, pickUp, handheld, equipment }` ✓ 与"武器/物品"无关 ✓
//   · 游戏每次要显示时都 `Instantiate(item.ItemGraphic)` ✓
//     → **就地改那个 prefab** = 以后每个实例都自动是对的 ✓
//
// ⚠️ 血泪教训（归档那份从没测过的代码留下的 ✗）
//   · **绝不把"克隆出来的模板"写回 `item.ItemGraphic`** ✗：
//     模板被塞进 `DontDestroyOnLoad`，而 Unity 里 `Instantiate` 出来的副本**落在原物体所在的场景**
//     → 之后每个实例都生在 DDOL 场景里 → **全都看不见** ✗
//     （实测现象：整件背包连它原来的外观一起消失 ✗）
//   · 所以这里**不克隆** ✓：直接改游戏自己的 prefab（只在运行时内存里 ✓ 不落盘 ✓ 可 `Restore()` ✓）
//
// 它不管什么：挂点/槽位/枪械零件命名（`WPN_*` / `ShowIf_*` / `HideIf_*` 那套是**武器专用** ✗）。
// 物品这里只有三件事：**关掉原外观 → 挂上我们的模型 → 让游戏下次重建时也用它** ✓

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    /// <summary>把"我们的 mesh + 贴图"接到**非武器物品**上（改它自己的图形 ✓ 不克隆不 DDOL ✗）。</summary>
    public static class ItemModel
    {
        /// <summary>一次替换的结果 ✓（带 `Restore()` ✓ 热重载/卸载时能还原 ✓）</summary>
        public sealed class Result
        {
            public bool Applied;
            public int TypeID;
            public string ItemName = "";
            public GameObject Instance;                       // 我们挂上去的子物体

            /// <summary>被我们关掉的旧外观（**任何**渲染器都可能 ✓ 含 SpriteRenderer ✓）</summary>
            public readonly List<Renderer> Hidden = new List<Renderer>();

            /// <summary>我们在这层打过 `Marked` 的对象 ✓（`Restore()` 时**精确摘掉这一层** ✓）。
            /// ⭐ 绝不用"全局扫描摘标记" ✗ —— 那会把**游戏对象上的标记**也摘掉 ✓
            ///   → 下一轮 `Apply` 会去重处理**原物品模板** ✗ → 走成另一条分支 ✗（实测报过 ✓）</summary>
            public GameObject MarkedOn;

            public string AnchorName = "根";
            public bool CacheCleared;

            /// <summary>true = **什么都没做**（已经换过了 ✓ / 本来就换不了 ✓）→ 调用方**别打日志** ✗
            /// （之前靠比 `Report` 字符串 ✗ → 少一个字段就刷了 5000 行日志 ✓ 实测 ✓）</summary>
            public bool NoOp;
            public string Report = "";

            /// <summary>热重载/卸载：把关掉的开回来 + 抹掉我们挂的东西 ✓（幂等 ✓ 可重复调 ✓）</summary>
            public void Restore()
            {
                if (MarkedOn != null)
                {
                    var mk = MarkedOn.GetComponent<Marked>();
                    if (mk != null) UnityEngine.Object.DestroyImmediate(mk);   // 摘掉我们打的标记 ✓ → 下次 Apply 才能重挂 ✓
                    MarkedOn = null;
                }
                for (int i = 0; i < Hidden.Count; i++)
                    if (Hidden[i] != null) Hidden[i].enabled = true;
                Hidden.Clear();
                if (Instance != null) { UnityEngine.Object.Destroy(Instance); Instance = null; }
                Applied = false;
            }
        }

        /// <summary>标记"这一层已经换成我们的了" ✓。
        /// ⚠️ 必须挂在**被处理的那一层自己身上** ✓ —— 这样才能区分：
        ///   · 图形层（我们改过 ✓ 它的**克隆**也会带着这个标记 ✓ → 不会重复挂 ✓）
        ///   · agent 层（手里那层 ✓ 独立处理 ✓ 它自己的图标精灵才会被关掉 ✗ 以前漏了 ✓）</summary>
        public sealed class Marked : MonoBehaviour { }


        // ───────────────────────── 清实体缓存（让游戏下次重建 ✓）─────────────────────────

        /// <summary>清掉"实体缓存"（社区做法 ✓）：游戏下次创建实体时会重新看 `item.ItemGraphic` ✓
        /// ⚠️ 别自己去销毁/替换活着的实体 ✗（游戏还持有引用 → 那件物品会"选不中/用不了" ✗ 实测踩过）</summary>
        public static bool ClearAgentCache(ItemStatsSystem.Item item)
        {
            var au = item != null ? item.AgentUtilities : null;
            if (au == null) return false;
            try
            {
                var f = typeof(ItemStatsSystem.ItemAgentUtilities)
                    .GetField("hashedAgentsCache", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) return false;
                f.SetValue(au, null);
                return true;
            }
            catch { return false; }
        }

        static PropertyInfo _graphicProp;
        static FieldInfo _graphicField;
        static bool _probed;

        static void ProbeGraphic()
        {
            if (_probed) return;
            _probed = true;
            var t = typeof(ItemStatsSystem.Item);
            var prop = t.GetProperty("ItemGraphic", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite) _graphicProp = prop;
            if (_graphicProp == null)
            {
                var f = t.GetField("itemGraphic", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(ItemGraphicInfo)) _graphicField = f;
            }
        }

        /// <summary>把图形写到物品上 ✓（属性优先 ✓ 私有字段兜底 ✓ —— 工坊 mod 也是这两条 ✓）</summary>
        public static bool WriteGraphic(ItemStatsSystem.Item item, ItemGraphicInfo graphic)
        {
            if (item == null || graphic == null) return false;
            ProbeGraphic();
            if (_graphicProp != null) { _graphicProp.SetValue(item, graphic); return true; }
            if (_graphicField != null) { _graphicField.SetValue(item, graphic); return true; }
            return false;
        }

        /// <summary>借一份游戏材质（给"本来没有图形"的物品用 ✓）：从**任意**一个游戏网格材质上取 ✓
        /// —— shader/关键字都是游戏自己的 ✓ 不会像上次那样退回默认材质（粉色 ✗）</summary>
        static Material _borrowed;

        static Material BorrowMaterial()
        {
            if (_borrowed != null) return _borrowed;
            try
            {
                // ① 先在**全场景的网格渲染器**里找 ✓（原来只找 ItemGraphicInfo 底下的 ✗ ——
                //    载入阶段可能一个都还没加载 ✓ → 借不到 → 材质 null → 粉色 ✗ 实测）
                foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    var sh = r.sharedMaterial.shader;
                    if (sh == null) continue;
                    if (sh.name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // 精灵材质不要 ✗
                    if (sh.name.IndexOf("UI/", StringComparison.OrdinalIgnoreCase) >= 0) continue;        // UI 材质不要 ✗
                    _borrowed = r.sharedMaterial; return _borrowed;
                }
                // ② 退一步：SkinnedMeshRenderer（角色身上的）
                foreach (var r in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                    if (r != null && r.sharedMaterial != null) { _borrowed = r.sharedMaterial; return _borrowed; }
            }
            catch { }
            return null;
        }

        // ───────────────────────── 关旧外观 / 挂我们的 ─────────────────────────

        /// <summary>取"挂点"：优先**包围盒最大**的渲染器（**任何类型** ✓ 含 SpriteRenderer ✓）；
        /// 一个都没有（视觉靠 `Setup(item)` 运行时补的那种 ✓）→ 返回 null（挂到根 ✓）</summary>
        static Renderer PickMount(Transform root)
        {
            // ⭐ ① **先在"游戏声明的渲染器清单"里挑** ✓（`CharacterSubVisuals.renderers` ✓）
            //    物品这边**不看名字** ✗（`WPN_*` 是武器语义 ✓）→ 清单里取"包围盒最大的网格" = 本体 ✓
            //    关键 ✓：运行时挂的**灯/特效不在清单里** ✗✓（实测那盏 `SodaPointLight` 就是这么被排除的 ✓）
            var declared = GameApi.DeclaredRenderers(root);
            if (declared.Count > 0)
            {
                Renderer pick = null;
                float pickSize = -1f;
                for (int i = 0; i < declared.Count; i++)
                {
                    var r = declared[i];
                    if (r == null) continue;
                    if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                    if (r.gameObject.name.StartsWith("ModelKit_")) continue;
                    float sz;
                    try { sz = r.bounds.size.magnitude; } catch { continue; }
                    if (sz > pickSize && sz < 5f) { pickSize = sz; pick = r; }
                }
                if (pick != null) return pick;
            }

            // ⭐ ② 回退：清单为空 / 没有那个组件 → 遍历 + 排灯/特效 + 比大小 ✓（不比以前差 ✓）
            Renderer best = null;
            float bestSize = -1f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // ⭐ **只认真网格** ✓（跟武器那份 `WeaponModel` 同一句 ✓ 用户定的规矩 ✓）
                //     ✗ 别把 Sprite / 灯光光晕 / 粒子 / 拖尾 之类算进来 ——
                //     实测踩过：头盔图形上运行时挂了一盏 `SodaPointLight`，它的光晕 Sprite 比头盔还大 ✗
                //     → "取最大的"就选中了灯 ✗ → 真模型被关、我们的模型挂到灯上 → **头盔消失** ✗
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r == null) continue;
                if (r.gameObject.name.StartsWith("ModelKit_")) continue;      // 我们自己的不算 ✓

                // ⭐ 排除"灯 / 特效"那类渲染器 ✗ —— 实测踩过：头盔(921) 运行时挂了盏 `SodaPointLight`，
                //    它的渲染器**也是 MeshRenderer** ✗ → 光靠"只认 mesh"拦不住 ✓ → 它会以"更大"胜出 ✗
                //    → 真模型被关 + 我们的模型挂到灯上 → **头盔消失** ✗
                Transform t = r.transform;
                bool lightish = false;
                for (int up = 0; up < 6 && t != null && t != root.parent; up++, t = t.parent)
                {
                    if (t.GetComponent<Light>() != null) { lightish = true; break; }     // 祖先里有灯 → 排除 ✓
                    var n2 = t.gameObject.name;
                    if (n2.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n2.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n2.IndexOf("Point", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n2.IndexOf("Fx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n2.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n2.IndexOf("Flash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n2.IndexOf("Trail", StringComparison.OrdinalIgnoreCase) >= 0) { lightish = true; break; }
                }
                if (lightish) continue;

                // ⭐ 顶点太少的也不像本体 ✗（那些光晕/贴片常常只有几个顶点 ✓）
                try
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount < 20) continue;
                }
                catch { }

                float sz;
                try { sz = r.bounds.size.magnitude; } catch { continue; }
                if (sz > bestSize && sz < 5f) { bestSize = sz; best = r; }     // 只在"真网格"里比大小 ✓
            }
            return best;
        }

        /// <summary>关掉 root 下**所有**旧外观（不限渲染器类型 ✗ 精灵也要关 ✓），记进 res ✓ 可还原 ✓</summary>
        static void HideOld(Transform root, Result res)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.gameObject.name.StartsWith("ModelKit_")) continue;       // 我们自己的不动 ✓
                if (r.enabled) { r.enabled = false; res.Hidden.Add(r); }
            }
        }

        /// <summary>把我们的 mesh 挂上去（挂在挂点上 ✓ 没有挂点就挂根 ✓）
        /// 缩放补回"世界尺度 = 1" ✓（prefab 的缩放链不一定是 1 ✗）；材质**克隆游戏自己的**再换贴图 ✓</summary>
        static GameObject Attach(Transform root, Renderer mount, Mesh mesh, Texture2D texture, string itemName,
                                Vector3 oldCenterWorld, Material fallbackMat = null, float scale = 1f)
        {
            var parent = mount != null ? mount.transform : root;
            Vector3 lossy = parent.lossyScale;
            var inv = new Vector3(
                Math.Abs(lossy.x) > 1e-6f ? 1f / lossy.x : 1f,
                Math.Abs(lossy.y) > 1e-6f ? 1f / lossy.y : 1f,
                Math.Abs(lossy.z) > 1e-6f ? 1f / lossy.z : 1f);

            var go = new GameObject("ModelKit_" + (string.IsNullOrEmpty(itemName) ? "item" : itemName));
            go.layer = mount != null ? mount.gameObject.layer : root.gameObject.layer;
            go.transform.SetParent(parent, false);
            // ⭐ 让"我们 mesh 的**包围盒中心**"落到"**被替掉的那个外观**的包围盒中心" ✓
            //   旧零件的原点常常不在自己身上 ✗（比如原点在角色脚下、几何靠自身变换挪上去 ✓）
            //   → 直接 localPosition = zero 就会摆错 ✗（实测：模型出现在角色脚下 ✗）
            //   尺度仍保持**真实米制** ✓（不随旧模型大小缩放 ✗）
            var want = parent.InverseTransformPoint(oldCenterWorld);
            go.transform.localPosition = want - Vector3.Scale(mesh.bounds.center, inv);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = inv * scale;   // 抵消挂点缩放 ✓ × 适配系数（⭐ 由 localScale 承担 ✓ 不改 mesh 顶点 ✗）

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            // 材质：优先用**网格**渲染器的（武器/装备类 ✓）；只有精灵的话它的材质是 sprite 专用的 ✗
            // → 那就退而找 root 下任意一个 mesh 材质 ✓ 都没有才用默认 ✓
            Material src = mount is MeshRenderer || mount is SkinnedMeshRenderer
                ? (mount != null ? mount.sharedMaterial : null) : null;
            if (src == null)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is MeshRenderer || r is SkinnedMeshRenderer) { src = r.sharedMaterial; break; }
                }
            if (src == null) src = fallbackMat;            // ⭐ 兜底：调用方给的（例如"世界图形上我们刚挂的那份材质" ✓）
            if (src == null) UnityEngine.Debug.LogWarning("[ItemModel] ⚠️ 借不到游戏材质 ✗ → 会用 Unity 默认材质（**粉色** ✗）");
            var one = src != null ? new Material(src) : null;
            if (one != null) GameApi.ApplyOurTexture(one, texture);      // ⭐ 换贴图 + **清其它槽** ✓（与武器共用一套 ✓）
            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];                    // 每个 submesh 一个材质槽（别让 Unity 去猜 ✓）
            for (int i = 0; i < sub; i++) mats[i] = one;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>核心：**就地**把一个"外观物体"换成我们的（prefab 或场上实例都一样 ✓）</summary>
        /// <summary>⭐ 只对"**场上的图形克隆**"补挂 ✓（掉落/展示那条 ✗ 不能只改共享 prefab ✗）。
        /// <para>原理 ✓：游戏 `ItemGraphicInfo.CreateAGraphic` = `Instantiate(item.ItemGraphic)` ✓
        /// —— **已经在场上的副本**不会跟着 prefab 变 ✗（掉落物 / 换场景遗留 ✓）→ 逐个补 ✓。</para>
        /// <para>⚠️ 只传**克隆**进来 ✓（调用方筛过 ✓）；共享 prefab **绝不要**走这里 ✗</para></summary>
        public static Result ApplyToGraphicClone(ItemGraphicInfo clone, string itemName, int typeID, Mesh mesh, Texture2D texture, float scale = 1f)
            => clone == null ? new Result { Report = "克隆为空 ✗" } : ApplyToTransform(clone.transform, itemName, typeID, mesh, texture, scale);

        public static Result ApplyToTransform(Transform root, string itemName, int typeID, Mesh mesh, Texture2D texture, float scale = 1f)
        {
            var res = new Result { TypeID = typeID, ItemName = itemName ?? "" };
            if (root == null || mesh == null) { res.Report = "外观为空或网格为空 ✗"; return res; }

            // 幂等 ✓：**只看这一层自己**有没有标记 ✓（不能扫子树 ✗ —— agent 的子树里含着图形 ✓
            //   扫子树会把"图形已经换过"误判成"agent 也换过" ✗ → agent 自己那层图标精灵永远关不掉 ✗ 实测）
            if (root.GetComponent<Marked>() != null)
            { res.Applied = false; res.NoOp = true; res.Report = "这一层已经换过了 ✓"; return res; }

            var mount = PickMount(root);
            res.AnchorName = mount != null ? mount.gameObject.name : "根";
            // 旧外观的中心要在**关掉之前**取 ✓（用 localBounds × 矩阵 ✓ 禁用后 AABB 可能不更新 ✗）
            Vector3 oldCenter = mount != null
                ? mount.transform.TransformPoint(mount.localBounds.center)
                : root.position;
            HideOld(root, res);
            res.Instance = Attach(root, mount, mesh, texture, itemName, oldCenter, null, scale);
            if (root.GetComponent<Marked>() == null) root.gameObject.AddComponent<Marked>();   // 打标记 ✓
            res.MarkedOn = root.gameObject;
            res.Applied = true;
            res.Report = $"关旧外观 {res.Hidden.Count} 个｜挂到 '{res.AnchorName}'"
                       + (mount != null
                          ? $"(世界 {Fmt(mount.transform.position)} 缩放 {Fmt(mount.transform.lossyScale)} 旧外观中心 {Fmt(oldCenter)})"
                          : "(没有可用的挂点 → 挂根 ✓)")
                       + $"｜mesh 顶点={mesh.vertexCount} 子网格={mesh.subMeshCount}"
                       + $"｜材质={(texture != null ? "已换贴图" : "游戏原材质")}";
            return res;
        }

        static string Fmt(Vector3 v)
            => "(" + v.x.ToString("0.###") + "," + v.y.ToString("0.###") + "," + v.z.ToString("0.###") + ")";

        // ───────────────────────── 手持 / 装备（agent 那条 ✓ 工坊验证过）─────────────────────────

        /// <summary>拿在手里用哪个模板：① 物品自带的 `agents["Handheld"]` ✓ ② 游戏的通用手持 agent ✓
        /// （工坊 mod `三角洲模型合集` 就是按这个顺序取的 ✓ r9 验证）</summary>
        static ItemStatsSystem.ItemAgent HandheldTemplate(ItemStatsSystem.Item item)
        {
            try
            {
                var t = item.AgentUtilities != null ? item.AgentUtilities.GetPrefab("Handheld") : null;
                if (t != null) return t;
            }
            catch { /* 物品没有就退到通用模板 ✓ */ }
            try
            {
                var p = Duckov.Utilities.GameplayDataSettings.Prefabs;
                if (p != null) return p.HandheldAgentPrefab;
            }
            catch { }
            return null;
        }

        /// <summary>⭐ 把"拿在手里/装备"那条也换成我们的模型 ✓
        ///
        /// 做法（工坊验证 ✓）：造一个 `ItemAgent` 模板 → **清掉它自带的视觉**（原来那张图标精灵 ✓ 就是你看到的"图片" ✗）
        /// → 挂上我们的 mesh → 用 `agents["Handheld"]` 写回去 → 清实体缓存 ✓
        ///
        /// ⚠️ 这里用 `DontDestroyOnLoad` 是**对的** ✓（工坊也这么做 ✓）：agent 是**模板** ✓，
        ///    游戏会把实例 `SetParent` 到持有者身上 ✓ → 换场景不受影响 ✓
        ///    （我上次踩的坑是反的 ✗：DDOL 的是**图形**实例 ✗，游戏只是 `Instantiate` 它 ✗ → 副本留在 DDOL 场景 → 看不见 ✗）</summary>
        public static Result ApplyHandheld(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, float scale = 1f)
        {
            var res = new Result { TypeID = item != null ? item.TypeID : 0, ItemName = item != null ? item.name : "" };
            if (item == null || mesh == null) { res.Report = "物品或网格为空 ✗"; return res; }

            // 已经换过就不重复造（我们造的 agent 名字带前缀 ✓）
            try
            {
                var cur = item.AgentUtilities != null ? item.AgentUtilities.GetPrefab("Handheld") : null;
                if (cur != null && cur.gameObject != null && cur.gameObject.name.StartsWith("ModelKit_"))
                { res.Applied = false; res.NoOp = true; res.Report = "手持已经换过了 ✓"; return res; }
            }
            catch { }

            var tmpl = HandheldTemplate(item);
            if (tmpl == null || tmpl.gameObject == null)
            { res.Report = "找不到手持 agent 模板 ✗（这件物品不能拿在手里 ✓）"; return res; }

            ItemStatsSystem.ItemAgent agent;
            try { agent = UnityEngine.Object.Instantiate(tmpl); }
            catch (Exception ex) { res.Report = "克隆手持模板失败 ✗：" + ex.Message; return res; }

            agent.gameObject.name = "ModelKit_Hand_" + (string.IsNullOrEmpty(item.name) ? "item" : item.name);
            UnityEngine.Object.DontDestroyOnLoad(agent.gameObject);        // ⚠️ 模板要活着 ✓（见上面注释 ✓）

            var kids = new List<Transform>();                              // 先收集再删 ✓（边遍历边删会错 ✗）
            foreach (Transform c in agent.transform) kids.Add(c);
            foreach (var c in kids) if (c != null) UnityEngine.Object.Destroy(c.gameObject);

            // ⭐ 材质兜底：agent 模板自己的视觉被我们清掉了 ✗ → 借**世界图形上我们刚挂的那份材质** ✓
            //   （同一套 shader/贴图 ✓ 不然 Unity 会用默认材质 = **粉色** ✗ 就是刚才那个现象 ✓）
            Material fallback = null;
            var g = item.ItemGraphic;
            if (g != null)
            {
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.gameObject.name.StartsWith("ModelKit_") && r.sharedMaterial != null) { fallback = r.sharedMaterial; break; }
                if (fallback == null)
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                        if (r is MeshRenderer || r is SkinnedMeshRenderer) { fallback = r.sharedMaterial; break; }
            }

            res.Instance = Attach(agent.transform, null, mesh, texture, item.name, agent.transform.position, fallback, scale);
            if (agent.gameObject.GetComponent<Marked>() == null) agent.gameObject.AddComponent<Marked>();
            res.MarkedOn = agent.gameObject;

            // ⚠️ **agent 模板也要挪到世界外** ✗（跟图形那条同理 ✓ 实测：载入画面里又看到一把枪 ✗）
            //   游戏拿它时会 `Instantiate` 副本 ✓ → `ChangeHoldItem` 里 `SetParent(手部 socket)` + `localPosition = 0` ✓
            //   → 所以挪到 -5000 只影响**模板本身** ✓ 不影响它生成的副本 ✓
            agent.transform.SetParent(null);
            agent.transform.position = new Vector3(0f, -5000f, 0f);

            try
            {
                var au = item.AgentUtilities;
                if (au == null || !GameApi.SetAgentPrefab(au, "Handheld", agent))
                { res.Report = "写回 agents[Handheld] 失败 ✗"; return res; }
                res.CacheCleared = ClearAgentCache(item);
            }
            catch (Exception ex) { res.Report = "写回手持 agent 失败 ✗：" + ex.Message; return res; }

            res.Applied = true;
            res.AnchorName = "手持 agent";
            res.Report = $"（拿在手里那条 ✓）清掉模板自带视觉 {kids.Count} 个｜挂上我们的 mesh 顶点={mesh.vertexCount}"
                       + $"｜清缓存={(res.CacheCleared ? "是" : "否")}";
            return res;
        }

        /// <summary>⚠️ 把 `AddComponent` 出来的 `ItemGraphicInfo` 的**必需字段初始化好** ✗
        ///
        /// 反编译依据 ✓：游戏 `CreateAGraphic` → `Setup(item)` → `RefreshSubGraphics()`，里面**必然**执行
        ///   `foreach (ItemGraphicSocket socket in sockets)` ✓ + 用 `subGraphics` / `socketsDictionary` ✓
        /// 而 `AddComponent` 造出来的这些字段**全是 null** ✗ → 必抛空引用 ✗ → 游戏就用不了我们的图形 ✗
        /// （工坊 mod 用 AssetBundle 里的**正经 prefab** ✓ 所以它碰不到这个坑 ✓；我们只能自己补 ✓）</summary>
        static void InitGraphicFields(ItemGraphicInfo g)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var t = typeof(ItemGraphicInfo);
            try
            {
                var fSockets = t.GetField("sockets", F);
                if (fSockets != null && fSockets.GetValue(g) == null)
                    fSockets.SetValue(g, new List<ItemGraphicInfo.ItemGraphicSocket>());

                var fSub = t.GetField("subGraphics", F);
                if (fSub != null && fSub.GetValue(g) == null)
                    fSub.SetValue(g, new List<ItemGraphicInfo>());

                var fDic = t.GetField("socketsDictionary", F);
                if (fDic != null && fDic.GetValue(g) == null)
                    fDic.SetValue(g, new Dictionary<string, ItemGraphicInfo.ItemGraphicSocket>());

                var fBuilt = t.GetField("dicBuilt", F);
                if (fBuilt != null) fBuilt.SetValue(g, true);       // 别让它再去建一次 ✗

                // groundPoint：给一个空子物体 ✓（SnapGroundPointToParent 会用到 ✓）
                var fGp = t.GetField("groundPoint", F);
                if (fGp != null && fGp.GetValue(g) == null)
                {
                    var gp = new GameObject("groundPoint");
                    gp.transform.SetParent(g.transform, false);
                    gp.transform.localPosition = Vector3.zero;
                    fGp.SetValue(g, gp.transform);
                }
            }
            catch { /* 补不上也别炸 ✓ 大不了这件换不成 ✓ */ }
        }

        /// <summary>给"本来没有图形"的物品**造一份世界图形**（用它替换 2D 图片兜底 ✓）：
        /// 运行时新建一个带 `ItemGraphicInfo` 的物体 ✓ 挂上我们的 mesh ✓ 写回 `item.ItemGraphic` ✓
        ///
        /// ⚠️ **不 `DontDestroyOnLoad`** ✗（上次"整件消失"就是踩了 DDOL ✗）：
        ///    它活在**当前场景** ✓ 换场景后自然失效 → 游戏回退到 2D 图片 ✓（优雅降级 ✓）
        ///    → mod 每秒的复查会**再造一份** ✓</summary>
        /// <summary>我们**造出来的**图形（按 typeID 缓存 ✓）—— 复用同一个 ✓ 别每次造新的 ✗
        /// ⚠️ 游戏会**重建物品预制体** ✗ → 我们写进去的引用会被冲掉 ✓ → 那就把这个**同一个**对象再写回去 ✓</summary>
        static ItemGraphicInfo MakeGraphicForItem(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, Result res)
        {

            ItemGraphicInfo g;
            try
            {
                var go = new GameObject("ModelKit_graphic_" + (string.IsNullOrEmpty(item.name) ? "item" : item.name));
                g = go.AddComponent<ItemGraphicInfo>();
            }
            catch (Exception) { return null; }

            InitGraphicFields(g);                               // ⭐ 先把它缺的字段补齐 ✓（否则游戏一用就空引用 ✗）

            var fallback = BorrowMaterial();                    // 借一份游戏材质 ✓（免得上默认材质变粉色 ✗）
            res.Instance = Attach(g.transform, null, mesh, texture, item.name, g.transform.position, fallback);
            // ⭐ **层要跟着物品走** ✗ —— `Attach` 在 mount==null 时用的是 root 的层 ✓
            //   而 root 是我们 `new GameObject()` 出来的 → **layer = 0（Default）** ✗
            //   游戏显示这个世界模型时按层过滤 → Default 层的 mesh **看不见** ✗（实测：糖果消失 ✓）
            //   ⚠️ **层：一律不设** ✗（实测证据 ✓）：游戏**掉落物**的图形 prefab 层就是 **0（Default）** ✓
            //      而且游戏里这些掉落物**看得见** ✓（`InteractablePickup.CreateGraphic` 里**没有任何设层代码** ✓）
            //      → 我们造的图形就保持 `new GameObject()` 的默认层（0 ✓）= 和游戏资产完全一致 ✓
            //   我先后猜过"Default 看不见"✗ / "用物品自己的层"✗（→ 发白+渲染在最前 ✓）/ "用 Character"✗（→ 看不见 ✓）
            //   —— 三次都错 ✓ 根因是**猜层值** ✗ 而不是照游戏的做法（**不动层** ✓）
            //   ⇒ **层一律不动** ✓（游戏自己的掉落物图形就是 0 层 ✓ 那个值可见 ✓ 不用我们设 ✗）
            if (g.gameObject.GetComponent<Marked>() == null) g.gameObject.AddComponent<Marked>();
            res.MarkedOn = g.gameObject;

            if (!WriteGraphic(item, g)) return null;            // 写回 item.ItemGraphic ✓
            return g;
        }

        // ───────────────── 用"动态条目"覆盖原物品（工坊那条路 ✓ 稳 ✓）─────────────────

        /// <summary>⭐ 给**没有 ItemGraphic** 的物品换成 3D：**克隆它自己的物品预制体** ✓
        /// → 克隆里的图形换成我们的 ✓ → `useSpriteForPickup=false` ✓ → **`AddDynamicEntry`** 注册 ✓
        ///
        /// 为什么必须这样 ✓（反编译实证 ✓）：
        ///   · 直接往 **实例** 的 `itemGraphic` 写引用 ✗ → 游戏**关卡加载时重建物品预制体**会把引用冲掉 ✗（实测 ✓）
        ///   · `GetPrefab` / `InstantiateAsync_Local` 都是**先查 `dynamicDic`** ✓ → 注册进去就**遮蔽原物品** ✓
        ///     而 `dynamicDic` 是游戏自己持有的**运行时字典** ✓ → **重建冲不掉** ✓✓（工坊 mod 就靠这个 ✓）
        ///   · 游戏对"占用已有 typeID"会 log 一句警告 ✓（"This will override the main game's item" ✓）→ 是**允许**的用法 ✓
        ///
        /// ⚠️ 只克隆、只改图形 ✓：数值/插槽/图标/变量全部继承原件 ✓（`Object.Instantiate` 是完整拷贝 ✓）</summary>
        /// <summary>已经注册过的 typeID ✓ —— **同一个只注册一次** ✗ 否则会自增克隆（实测：主界面越来越卡直到卡死 ✗）</summary>
        static readonly HashSet<int> _dynDone = new HashSet<int>();
        /// <summary>已注册动态条目时用的那个图形 ✓ —— 后续给"别的实例"补写时要复用它 ✓</summary>
        static readonly Dictionary<int, ItemGraphicInfo> _dynGraphic = new Dictionary<int, ItemGraphicInfo>();

        public static Result RegisterDynamicOverride(int typeID, Mesh mesh, Texture2D texture,
                                                     ItemStatsSystem.Item alsoFixThisInstance = null,
                                                     bool alsoHandheld = false)
        {
            var res = new Result { TypeID = typeID };
            if (mesh == null) { res.Report = "网格为空 ✗"; return res; }
            if (_dynDone.Contains(typeID))
            {
                // ⭐ 注册只做一次 ✓（断自增循环 ✓）—— 但**实例补写每次都要做** ✗
                //   （玩家后来才拿到的每一颗都是"老实例" ✗ 不补写就永远是贴图 ✗ 实测踩到 ✓）
                if (alsoFixThisInstance != null && _dynGraphic.TryGetValue(typeID, out var g0) && g0 != null)
                {
                    if (WriteGraphic(alsoFixThisInstance, g0))
                    {
                        alsoFixThisInstance.useSpriteForPickup = false;
                        ClearAgentCache(alsoFixThisInstance);
                        res.Applied = true;
                        res.Report = "已注册过 ✓（这次只补写当前实例 ✓ + 关掉它的 useSpriteForPickup ✓）";
                        return res;
                    }
                }
                res.NoOp = true; res.Report = "已经注册过动态条目 ✓"; return res;
            }

            ItemStatsSystem.Item src = null;
            try { src = ItemStatsSystem.ItemAssetsCollection.GetPrefab(typeID); }
            catch (Exception ex) { res.Report = "取原物品失败 ✗：" + ex.Message; return res; }
            if (src == null) { res.Report = $"typeID={typeID} 找不到原物品 ✗"; return res; }

            ItemStatsSystem.Item item;
            try { item = UnityEngine.Object.Instantiate(src); }                  // ① 克隆物品 ✓（完整拷贝 ✓）
            catch (Exception ex) { res.Report = "克隆物品失败 ✗：" + ex.Message; return res; }

            item.gameObject.name = "ModelKit_item_" + src.name;
            UnityEngine.Object.DontDestroyOnLoad(item.gameObject);                // ② 它不在场景里 ✓ 工坊也这么做 ✓

            // ③ 图形：原件有就**克隆它**（原件保持干净 ✓ 完全可逆 ✓），没有就造一个 ✓
            ItemGraphicInfo g = null;
            if (item.ItemGraphic != null)
            {
                var gsrc = item.ItemGraphic;
                g = UnityEngine.Object.Instantiate(gsrc);                         // ⚠️ 只克隆 ✓ **绝不动原件** ✗
                g.gameObject.name = "ModelKit_graphic_" + src.name;
            }
            else
            {
                g = MakeGraphicForItem(item, mesh, texture, res);                 // 造一个 ✓
                if (g == null) { res.Report = "造图形失败 ✗"; return res; }
            }

            // ⚠️⚠️ **绝不能把图形挂成物品的子物体** ✗✗（实测 ✓ 用户一眼指出 ✓）
            //   物品预制体的 GameObject 是**活的** ✓ → 我们的 mesh 会**跟着那件物品渲染** ✗
            //     · 玩家身上那颗 → "一把枪跟着玩家" ✗
            //     · 扔到地上 → "枪跟着糖果到地上" ✗
            //   游戏自己的图形是**独立资源** ✓ 从不挂在物品下面 ✓ → 所以"就地改"那条没事 ✓
            //   正解 = **独立**（SetParent(null) ✓）+ **挪到世界外**（y=-5000 ✓）+ DDOL ✓
            //   （游戏 `CreateAGraphic` 实例化副本之后会 `SetParent(显示位置)` + `localPosition = 0` ✓
            //     所以 -5000 只影响**模板本身** ✓ 不影响它生成的副本 ✓）
            g.transform.SetParent(null);
            g.transform.position = new Vector3(0f, -5000f, 0f);
            UnityEngine.Object.DontDestroyOnLoad(g.gameObject);

            InitGraphicFields(g);                                                 // ④ 补字段 ✓（否则游戏一用就空引用 ✗）
            var mount = PickMount(g.transform);
            HideOld(g.transform, res);
            res.Instance = Attach(g.transform, mount, mesh, texture, src.name, g.transform.position,
                                  mount != null ? mount.sharedMaterial : BorrowMaterial());
            // 层同上：**一律不动** ✓（游戏掉落物图形就是 0 层 ✓ 可见 ✓）
            if (g.gameObject.GetComponent<Marked>() == null) g.gameObject.AddComponent<Marked>();
            res.MarkedOn = g.gameObject;

            if (!WriteGraphic(item, g)) { res.Report = "写回克隆的 itemGraphic 失败 ✗"; return res; }   // ⑤
            item.useSpriteForPickup = false;                                      // ⑥ 用模型不用精灵 ✓

            // ⑦ ⭐ **也写到"当前这颗实例"上** ✓ —— 动态条目只影响"以后新实例化的" ✗，
            //    而玩家手里那颗是**我们换之前就存在的** ✗（`ItemGraphic=null` ✓）→ 丢地上照样是贴图 ✗（实测 ✓）
            bool instanceFixed = false;
            if (alsoFixThisInstance != null && !ReferenceEquals(alsoFixThisInstance, item))
            {
                try
                {
                    instanceFixed = WriteGraphic(alsoFixThisInstance, g);
                    if (instanceFixed)
                    {
                        alsoFixThisInstance.useSpriteForPickup = false;   // 实例上也要设 ✗ 不然 pickup 还走精灵 ✓
                        ClearAgentCache(alsoFixThisInstance);
                    }
                }
                catch { instanceFixed = false; }
            }

            bool ok = false;
            try { ok = ItemStatsSystem.ItemAssetsCollection.AddDynamicEntry(item); }   // ⑦ **注册动态条目** ✓
            catch (Exception ex) { res.Report = "注册动态条目抛错 ✗：" + ex.Message; return res; }
            if (!ok) { res.Report = "AddDynamicEntry 返回 false ✗"; return res; }

            _dynDone.Add(typeID);                                  // 记下 ✓ 只注册一次 ✓
            _dynGraphic[typeID] = g;                               // 存起来 ✓ 后续实例补写要用它 ✓

            // ⭐⭐ 关键 ✓：**也要把"手持 agent"写到克隆上** ✗
            //   `ItemExtensions.CreateHandheldAgent` 是先查 `GetPrefab("Handheld"的hash)` ✓ 拿不到才用游戏通用模板 ✓
            //     · 大急救箱 ✓ 玩家身上那件被"逐实例补写"过 ✓ → 手里是我们的枪 ✓
            //     · 糖果 ✗ maxStack>1 → 每用一次会**从预制体造新实例** ✓ → 而克隆上**没有** Handheld ✗
            //        → 新实例查到的是**游戏通用模板** = 那张图 ✗（这就是"糖果手里还是图片"的根因 ✓）
            if (alsoHandheld)
            {
                try { ApplyHandheld(item, mesh, texture, ModelSize.FactorFor(typeID, 0f, mesh)); }
                catch { /* 写不上不影响世界那条 ✓ */ }
            }
            res.CacheCleared = ClearAgentCache(item);
            res.AnchorName = "动态条目（克隆物品）";
            res.Applied = true;
            res.Report = "没有 ItemGraphic → **克隆物品预制体 + 注册动态条目**（遮蔽原物品 ✓ 关卡重建冲不掉 ✓）"
                       + $"｜当前实例也补写了={(instanceFixed ? "是 ✓" : "否 ✗")}"
                       + $"｜关旧外观 {res.Hidden.Count} 个｜mesh 顶点={mesh.vertexCount}"
                       + $"｜清缓存={(res.CacheCleared ? "是" : "否")}";
            return res;
        }

        // ───────────────────────── 对外入口 ─────────────────────────

        /// <summary>**主入口**：换一件物品：**它的模板** + **场上已有的实例** 一起换 ✓
        /// ⚠️ 没有 `ItemGraphic` 的物品（纯图标那种 ✓）会被**跳过** ✓（换不了外观 ✗ 不是失败 ✓）</summary>
        public static Result Apply(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, bool handheld = false, float size = 0f)
        {
            var res = new Result { TypeID = item != null ? item.TypeID : 0, ItemName = item != null ? item.name : "" };
            if (item == null) { res.Report = "物品为空 ✗"; return res; }
            // ⭐ 适配系数（**只武器有** ✓：按 `GunType_*` tag ✓；config 的 `size` 可覆盖 ✓）
            float scale = ModelSize.Factor(mesh, ModelSize.For(item.TypeID, size > 0f ? (float?)size : null));

            var graphic = item.ItemGraphic;
            if (graphic == null)
            {
                // ⭐ 这件物品本来没有世界图形（游戏会画一张 **2D 图片** 兜底 ✓）
                //   → 我们**造一份图形**写进去 ✓ 游戏下次就用我们的 ✓（= **替换**那个兜底 ✓ 不是删它 ✗）
                // 走"克隆 + 动态条目"那条 ✓（写实例引用会被关卡重建冲掉 ✗ 实测 ✓）
                var dyn = RegisterDynamicOverride(item.TypeID, mesh, texture, alsoFixThisInstance: item,
                                                  alsoHandheld: handheld);
                res.Report = dyn.Report;
                res.Applied = dyn.Applied;
                res.Instance = dyn.Instance;
                res.Hidden.AddRange(dyn.Hidden);
                res.CacheCleared = dyn.CacheCleared;
                res.AnchorName = dyn.AnchorName;
                return res;
            }

            // ① 就地改它自己那份图形 ✓（物品实例与模板都适用 ✓ 因为改的都是**同一个 prefab** ✓）
            res = ApplyToTransform(graphic.transform, item.name, item.TypeID, mesh, texture, scale);
            res.TypeID = item.TypeID;
            res.ItemName = item.name ?? "";

            // ② 再确认一次**物品模板**（`GetPrefab` ✓）：实例可能不是模板本身 ✓
            try
            {
                var prefab = ItemStatsSystem.ItemAssetsCollection.GetPrefab(item.TypeID);
                if (prefab != null && prefab.ItemGraphic != null && !ReferenceEquals(prefab.ItemGraphic, graphic))
                    ApplyToTransform(prefab.ItemGraphic.transform, prefab.name, prefab.TypeID, mesh, texture,
                                     ModelSize.Factor(mesh, ModelSize.For(prefab.TypeID, null)));
                if (prefab != null) res.CacheCleared = ClearAgentCache(prefab);
            }
            catch { /* 模板这条失败不影响实例那条 ✓ */ }

            // ③ 场上已经拿在手里/装备着的那一个：就地换 ✓（不销毁任何东西 ✓）
            ApplyToInstance(item, mesh, texture, scale);

            // ④ 清缓存 → 游戏下次建实体时会读**改过的 prefab** ✓
            if (!res.CacheCleared) res.CacheCleared = ClearAgentCache(item);
            return res;
        }

        /// <summary>③ 只改**场上这一个**实例（不动模板 ✓ 调试/局部用 ✓）：走它的 `ActiveAgent`（手持/装备 ✓）</summary>
        public static Result ApplyToInstance(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, float scale = 1f)
        {
            var res = new Result { TypeID = item != null ? item.TypeID : 0, ItemName = item != null ? item.name : "" };
            if (item == null || mesh == null) { res.Report = "物品或网格为空 ✗"; return res; }
            var active = item.ActiveAgent;
            if (active == null) { res.Report = "没有 ActiveAgent ✗（没拿在手里/没装备 ✓）"; return res; }

            // 已经换过就不重复挂 ✓
            foreach (Transform child in active.transform)
                if (child.name.StartsWith("ModelKit_")) { res.Report = "这个实例已经换过了 ✓"; return res; }

            var r = ApplyToTransform(active.transform, item.name, item.TypeID, mesh, texture, scale);
            r.CacheCleared = false;
            return r;
        }

        // ───────────── 只读诊断（config 顶层 `"debug": true` 打开 ✓ 默认关 ✓）─────────────
        /// <summary>调试开关 ✓（mod 从 config 读 ✓）</summary>
        public static bool DebugOn;
        static readonly HashSet<int> _dumpSeen = new HashSet<int>();

        /// <summary>把场上"我们造的物体"逐个打一行 ✓（名字/层/激活/场景/世界位置/世界缩放/父节点 ✓
        /// + 每个渲染器的层/开关/材质/世界包围盒 ✓）。
        /// 用途：排查"某条路看不见"（比如掉在地上没了 ✓）—— **先量 ✓ 别猜 ✗**</summary>
        public static void DumpNewObjects()
        {
            if (!DebugOn) return;
            if (Time.frameCount % 30 != 0) return;      // ⚠️ 节流：全场景扫描很贵 ✗ → 每 ~0.5 秒一次
            try
            {
                foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go == null || !go.name.StartsWith("ModelKit_")) continue;
                    if (!_dumpSeen.Add(go.GetInstanceID())) continue;      // 只打"新出现"的 ✓ 不刷屏 ✓
                    var t = go.transform;
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"[ItemModel][诊断] {go.name}｜层={go.layer}｜激活={go.activeInHierarchy}｜场景='{go.scene.name}'")
                      .Append($"｜世界位置={t.position}｜世界缩放={t.lossyScale}｜父='{(t.parent != null ? t.parent.name : "null")}'")
                      .Append($"｜子物体={t.childCount}");
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        sb.Append($"｜{r.GetType().Name}'{r.name}':层={r.gameObject.layer},开={r.enabled},"
                                + $"材质='{(r.sharedMaterial != null ? r.sharedMaterial.name : "null")}',世界包围盒={r.bounds.size}");
                    Debug.Log(sb.ToString());
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ItemModel] 诊断抛错：" + ex.Message); }
        }
    }
}
