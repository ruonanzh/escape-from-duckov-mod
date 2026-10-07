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

            public string AnchorName = "根";
            public bool CacheCleared;

            /// <summary>true = **什么都没做**（已经换过了 ✓ / 本来就换不了 ✓）→ 调用方**别打日志** ✗
            /// （之前靠比 `Report` 字符串 ✗ → 少一个字段就刷了 5000 行日志 ✓ 实测 ✓）</summary>
            public bool NoOp;
            public string Report = "";

            /// <summary>热重载/卸载：把关掉的开回来 + 抹掉我们挂的东西 ✓（幂等 ✓ 可重复调 ✓）</summary>
            public void Restore()
            {
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
                foreach (var g in UnityEngine.Object.FindObjectsByType<ItemGraphicInfo>(FindObjectsSortMode.None))
                {
                    if (g == null) continue;
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                        if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.sharedMaterial != null)
                        { _borrowed = r.sharedMaterial; return _borrowed; }
                }
            }
            catch { }
            return null;
        }

        /// <summary>只读诊断 ✓：这件物品身上有哪些 agent 键（`normal` / `pickUp` / `handheld` / `equipment`…）
        /// —— 用来判断"地上 / 手里 / 穿戴"各走哪条路 ✓（键名是游戏自己写进去的 ✓）
        /// ⚠️ 纯读 ✓ 不写任何东西 ✓</summary>
        public static string AgentKeys(ItemStatsSystem.Item item)
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                var au = item != null ? item.AgentUtilities : null;
                if (au == null) return "(没有 AgentUtilities ✗)";
                var f = typeof(ItemStatsSystem.ItemAgentUtilities)
                    .GetField("agents", BindingFlags.Instance | BindingFlags.NonPublic);
                var list = f != null ? f.GetValue(au) as System.Collections.IList : null;
                if (list == null) return "(读不到 agents 列表 ✗)";
                for (int i = 0; i < list.Count; i++)
                {
                    var pair = list[i];
                    if (pair == null) continue;
                    var kf = pair.GetType().GetField("key");
                    var af = pair.GetType().GetField("agentPrefab");
                    string k = kf != null ? (kf.GetValue(pair) as string) : null;
                    object a = af != null ? af.GetValue(pair) : null;
                    if (i > 0) sb.Append(", ");
                    sb.Append(k).Append(a != null ? "✓" : "✗");
                }
            }
            catch (Exception ex) { return "(读失败 ✗：" + ex.Message + ")"; }
            return sb.Length == 0 ? "(空 ✓ 没有自带 agent)" : "[" + sb + "]";
        }

        // ───────────────────────── 关旧外观 / 挂我们的 ─────────────────────────

        /// <summary>取"挂点"：优先**包围盒最大**的渲染器（**任何类型** ✓ 含 SpriteRenderer ✓）；
        /// 一个都没有（视觉靠 `Setup(item)` 运行时补的那种 ✓）→ 返回 null（挂到根 ✓）</summary>
        static Renderer PickMount(Transform root)
        {
            Renderer best = null;
            float bestSize = -1f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.gameObject.name.StartsWith("ModelKit_")) continue;      // 我们自己的不算 ✓
                float sz;
                try { sz = r.bounds.size.magnitude; } catch { continue; }
                if (sz > bestSize && sz < 5f) { bestSize = sz; best = r; }     // < 5m：避开天空盒那类怪东西 ✓
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
                                Vector3 oldCenterWorld, Material fallbackMat = null)
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
            go.transform.localScale = inv;

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
            var one = src != null ? new Material(src) : null;
            if (one != null)
            {
                if (texture != null) one.mainTexture = texture;
                one.color = Color.white;
            }
            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];                    // 每个 submesh 一个材质槽（别让 Unity 去猜 ✓）
            for (int i = 0; i < sub; i++) mats[i] = one;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>核心：**就地**把一个"外观物体"换成我们的（prefab 或场上实例都一样 ✓）</summary>
        public static Result ApplyToTransform(Transform root, string itemName, int typeID, Mesh mesh, Texture2D texture)
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
            res.Instance = Attach(root, mount, mesh, texture, itemName, oldCenter);
            if (root.GetComponent<Marked>() == null) root.gameObject.AddComponent<Marked>();   // 打标记 ✓
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
        public static Result ApplyHandheld(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture)
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

            res.Instance = Attach(agent.transform, null, mesh, texture, item.name, agent.transform.position, fallback);
            if (agent.gameObject.GetComponent<Marked>() == null) agent.gameObject.AddComponent<Marked>();

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
        static readonly Dictionary<int, ItemGraphicInfo> _made = new Dictionary<int, ItemGraphicInfo>();

        static ItemGraphicInfo MakeGraphicForItem(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, Result res,
                                                  bool cache = true)
        {
            if (cache)
            {
                // 已经造过 → 直接复用 ✓（Unity 的"已销毁"会当 null ✓ 所以这里能自动重造 ✓）
                if (_made.TryGetValue(item.TypeID, out var cached) && cached != null) return cached;
                // 物品当前指向的已经是我们的 → 也直接复用 ✓
                if (item.ItemGraphic != null && item.ItemGraphic.gameObject != null
                    && item.ItemGraphic.gameObject.name.StartsWith("ModelKit_graphic_")) return item.ItemGraphic;
            }

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
            if (g.gameObject.GetComponent<Marked>() == null) g.gameObject.AddComponent<Marked>();

            if (!WriteGraphic(item, g)) return null;            // 写回 item.ItemGraphic ✓
            if (cache) _made[item.TypeID] = g;                  // 记住它 ✓ 下次复用 ✓
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
        public static Result RegisterDynamicOverride(int typeID, Mesh mesh, Texture2D texture)
        {
            var res = new Result { TypeID = typeID };
            if (mesh == null) { res.Report = "网格为空 ✗"; return res; }

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
                g.transform.SetParent(item.transform, false);                     // 挂克隆下 ✓ → 跟着 DDOL ✓
            }
            else
            {
                g = MakeGraphicForItem(item, mesh, texture, res, cache: false);   // 造一个 ✓
                if (g == null) { res.Report = "造图形失败 ✗"; return res; }
                g.transform.SetParent(item.transform, false);
            }

            InitGraphicFields(g);                                                 // ④ 补字段 ✓（否则游戏一用就空引用 ✗）
            var mount = PickMount(g.transform);
            HideOld(g.transform, res);
            res.Instance = Attach(g.transform, mount, mesh, texture, src.name, g.transform.position,
                                  mount != null ? mount.sharedMaterial : BorrowMaterial());
            if (g.gameObject.GetComponent<Marked>() == null) g.gameObject.AddComponent<Marked>();

            if (!WriteGraphic(item, g)) { res.Report = "写回克隆的 itemGraphic 失败 ✗"; return res; }   // ⑤
            item.useSpriteForPickup = false;                                      // ⑥ 用模型不用精灵 ✓

            bool ok = false;
            try { ok = ItemStatsSystem.ItemAssetsCollection.AddDynamicEntry(item); }   // ⑦ **注册动态条目** ✓
            catch (Exception ex) { res.Report = "注册动态条目抛错 ✗：" + ex.Message; return res; }
            if (!ok) { res.Report = "AddDynamicEntry 返回 false ✗"; return res; }

            res.CacheCleared = ClearAgentCache(item);
            res.AnchorName = "动态条目（克隆物品）";
            res.Applied = true;
            res.Report = "没有 ItemGraphic → **克隆物品预制体 + 注册动态条目**（遮蔽原物品 ✓ 关卡重建冲不掉 ✓）"
                       + $"｜关旧外观 {res.Hidden.Count} 个｜挂到 '{res.AnchorName}'｜mesh 顶点={mesh.vertexCount}"
                       + $"｜清缓存={(res.CacheCleared ? "是" : "否")}";
            return res;
        }

        // ───────────────────────── 三个入口 ─────────────────────────

        /// <summary>① 按 **typeID** 换（**推荐主入口** ✓ 不用先拿到 Item ✓）：
        /// 改物品模板的图形 → 以后每个实例（掉地上/手里/装备 ✓）都是我们的 ✓</summary>
        public static Result ApplyByTypeID(int typeID, Mesh mesh, Texture2D texture)
        {
            var res = new Result { TypeID = typeID };
            ItemStatsSystem.Item prefab = null;
            try { prefab = ItemStatsSystem.ItemAssetsCollection.GetPrefab(typeID); }
            catch (Exception ex) { res.Report = "取物品模板失败 ✗：" + ex.Message; return res; }
            if (prefab == null) { res.Report = $"typeID={typeID} 找不到物品 ✗"; return res; }
            return Apply(prefab, mesh, texture);
        }

        /// <summary>② 换一件物品：**它的模板** + **场上已有的实例** 一起换 ✓
        /// ⚠️ 没有 `ItemGraphic` 的物品（纯图标那种 ✓）会被**跳过** ✓（换不了外观 ✗ 不是失败 ✓）</summary>
        public static Result Apply(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture)
        {
            var res = new Result { TypeID = item != null ? item.TypeID : 0, ItemName = item != null ? item.name : "" };
            if (item == null) { res.Report = "物品为空 ✗"; return res; }

            var graphic = item.ItemGraphic;
            if (graphic == null)
            {
                // ⭐ 这件物品本来没有世界图形（游戏会画一张 **2D 图片** 兜底 ✓）
                //   → 我们**造一份图形**写进去 ✓ 游戏下次就用我们的 ✓（= **替换**那个兜底 ✓ 不是删它 ✗）
                // 走"克隆 + 动态条目"那条 ✓（写实例引用会被关卡重建冲掉 ✗ 实测 ✓）
                var dyn = RegisterDynamicOverride(item.TypeID, mesh, texture);
                res.Report = dyn.Report;
                res.Applied = dyn.Applied;
                res.Instance = dyn.Instance;
                res.Hidden.AddRange(dyn.Hidden);
                res.CacheCleared = dyn.CacheCleared;
                res.AnchorName = dyn.AnchorName;
                return res;
            }

            // ① 就地改它自己那份图形 ✓（物品实例与模板都适用 ✓ 因为改的都是**同一个 prefab** ✓）
            res = ApplyToTransform(graphic.transform, item.name, item.TypeID, mesh, texture);
            res.TypeID = item.TypeID;
            res.ItemName = item.name ?? "";

            // ② 再确认一次**物品模板**（`GetPrefab` ✓）：实例可能不是模板本身 ✓
            try
            {
                var prefab = ItemStatsSystem.ItemAssetsCollection.GetPrefab(item.TypeID);
                if (prefab != null && prefab.ItemGraphic != null && !ReferenceEquals(prefab.ItemGraphic, graphic))
                    ApplyToTransform(prefab.ItemGraphic.transform, prefab.name, prefab.TypeID, mesh, texture);
                if (prefab != null) res.CacheCleared = ClearAgentCache(prefab);
            }
            catch { /* 模板这条失败不影响实例那条 ✓ */ }

            // ③ 场上已经拿在手里/装备着的那一个：就地换 ✓（不销毁任何东西 ✓）
            ApplyToInstance(item, mesh, texture);

            // ④ 清缓存 → 游戏下次建实体时会读**改过的 prefab** ✓
            if (!res.CacheCleared) res.CacheCleared = ClearAgentCache(item);
            return res;
        }

        /// <summary>③ 只改**场上这一个**实例（不动模板 ✓ 调试/局部用 ✓）：走它的 `ActiveAgent`（手持/装备 ✓）</summary>
        public static Result ApplyToInstance(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture)
        {
            var res = new Result { TypeID = item != null ? item.TypeID : 0, ItemName = item != null ? item.name : "" };
            if (item == null || mesh == null) { res.Report = "物品或网格为空 ✗"; return res; }
            var active = item.ActiveAgent;
            if (active == null) { res.Report = "没有 ActiveAgent ✗（没拿在手里/没装备 ✓）"; return res; }

            // 已经换过就不重复挂 ✓
            foreach (Transform child in active.transform)
                if (child.name.StartsWith("ModelKit_")) { res.Report = "这个实例已经换过了 ✓"; return res; }

            var r = ApplyToTransform(active.transform, item.name, item.TypeID, mesh, texture);
            r.CacheCleared = false;
            return r;
        }
    }
}
