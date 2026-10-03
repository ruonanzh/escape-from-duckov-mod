// ItemModelBinder.cs —— 把模型作用到**物品**上（枪、配件、背包…）。
//
// 物品的模型有两条独立的路径（读游戏代码得到的结论）：
//   · **掉落/展示** → `ItemGraphicInfo`：游戏调 `ItemGraphicInfo.CreateAGraphic(item.ItemGraphic, …)` 实例化
//   · **拿在手上** → `ItemAgent`：由 `ItemAgentUtilities` 管，手里那份就是 `item.ActiveAgent`
//     （实测：ActiveAgent=IG_Gun_Mp5(Clone)(ItemAgent_Gun)，而 agents 列表是空的 → 实体是从图形 prefab 派生的）
//
// 两条都要换：
//   1. BindGraphic  —— 克隆物品自己的图形 prefab（保留 sockets/groundPoint/设置），只把几何换成我们的，
//                      再反射写回私有字段 Item.itemGraphic → 之后游戏实例化的（掉落/展示）就是我们的
//   2. ReplaceHeld   —— **就地**在现有 ActiveAgent 上换几何（关原渲染器 + 挂我们的 mesh）。
//                      ⚠️ 别用 CreateAgent 替换实体：它会销毁旧实体，而游戏还持有引用 → 武器"选不中/用不了"（实测踩过）
//
// ⚠️ 踩过的坑：
//   · **千万不要把"模板" SetActive(false)**：Unity 的 Instantiate 会继承激活状态 → 实例全都不可见
//     （手里看不到、掉地上消失）。模板保持激活，但挪到世界外（y=-5000）。
//   · 别把 mesh 锚在"第一个"渲染器上（可能是小管子）→ 取**包围盒最大**的那个当锚点。

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    public sealed class ItemModelBinder
    {
        /// <summary>只换"枪身"这一个零件（名字以 WPN_ 开头，或最大的那个非配件零件），其余零件一概不碰。
        /// 枪上的其他零件（ShowIf_*/HideIf_* 配件槽位、弹匣、枪机…）**由游戏按状态开关**，
        /// 我们碰了就会出现"第一次看不到配件、切换一次才全显示"（实测踩过两次）。</summary>
        public bool ReplaceBodyOnly = true;

        readonly ModelSpec _spec;
        Mesh _mesh;
        Texture2D _texture;

        // 反射：社区 mod 的做法是"先试可写属性 ItemGraphic，再退到私有字段 itemGraphic"（更能抗游戏更新）
        static PropertyInfo _graphicProp;
        static FieldInfo _graphicField;
        static bool _probed;

        static void ProbeGraphic()
        {
            if (_probed) return;
            _probed = true;
            var prop = typeof(ItemStatsSystem.Item).GetProperty("ItemGraphic",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite) _graphicProp = prop;
            if (_graphicProp == null)
            {
                var f = typeof(ItemStatsSystem.Item).GetField("itemGraphic",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(ItemGraphicInfo)) _graphicField = f;
            }
        }

        static bool WriteGraphic(ItemStatsSystem.Item item, ItemGraphicInfo graphic)
        {
            ProbeGraphic();
            if (_graphicProp != null) { _graphicProp.SetValue(item, graphic); return true; }
            if (_graphicField != null) { _graphicField.SetValue(item, graphic); return true; }
            return false;
        }

        public ItemModelBinder(ModelSpec spec) { _spec = spec; }

        public Mesh Mesh { get { EnsureBuilt(); return _mesh; } }
        public Texture2D Texture { get { EnsureBuilt(); return _texture; } }
        public int HiddenRenderers { get; private set; }
        public string AnchoredAt { get; private set; } = "";
        public string MaterialInfo { get; private set; } = "";
        public bool HeldReplaced { get; private set; }
        public float OurWorldSize { get; private set; }
        public int KeptConditional { get; private set; }
        public string PartsDebug { get; private set; } = "";
        public string SlotReport { get; private set; } = "";

        void EnsureBuilt()
        {
            if (_mesh != null) return;
            var data = MeshKit.Build(_spec);
            _mesh = UnityAdapter.ToMesh(data, _spec.Name);
            _texture = UnityAdapter.ToTexture(TextureKit.Paint(_spec, data), _spec.Name + "_tex");
        }

        /// <summary>掉落/展示那条：把物品的图形换成"克隆它自己 + 我们的几何"。</summary>
        public bool BindGraphic(ItemStatsSystem.Item item)
        {
            if (item == null) return false;
            var src = item.ItemGraphic;
            if (src == null) return false;
            EnsureBuilt();

            var clone = Object.Instantiate(src);
            clone.gameObject.name = "ModelKit_" + _spec.Name;
            clone.gameObject.SetActive(true);                        // ⚠️ 不能停用（见文件头）
            clone.transform.position = new Vector3(0f, -5000f, 0f);  // 模板藏到世界外

            PrepareGeometry(clone.transform);
            return WriteGraphic(item, clone);
        }

        /// <summary>把图形写到**物品模板（prefab）**上：`ItemAssetsCollection.GetPrefab(typeID)`。
        /// 社区 mod 走的就是这条 —— 以后游戏每次实例化这个物品都直接用我们的图形（开局就对，不用等重申）。</summary>
        public bool BindGraphicOnPrefab(int typeID)
        {
            try
            {
                var prefab = ItemStatsSystem.ItemAssetsCollection.GetPrefab(typeID);
                if (prefab == null) return false;
                return BindGraphic(prefab);
            }
            catch { return false; }
        }

        /// <summary>清掉物品的"实体缓存"（社区做法 `ClearAgentCache`）：让游戏下次创建实体时重新读
        /// `item.ItemGraphic`（= 我们的图形）。**不要自己去销毁/替换活着的实体** —— 游戏还持有引用，
        /// 那样会让武器"选不中/用不了"（实测踩过）。</summary>
        public bool ClearAgentCache(ItemStatsSystem.Item item)
        {
            var au = item != null ? item.AgentUtilities : null;
            if (au == null) return false;
            try
            {
                var f = typeof(ItemStatsSystem.ItemAgentUtilities).GetField("hashedAgentsCache",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) return false;
                f.SetValue(au, null);
                return true;
            }
            catch { return false; }
        }

        /// <summary>手持那条（**备选**）：就地改活实体。默认不走这条 —— 正解是"改 prefab + 清缓存"让游戏重建。
        ///
        /// ⚠️ 不要用 `ItemAgentUtilities.CreateAgent()` 去替换实体：它内部会 `ReleaseActiveAgent()` 销毁旧实体，
        /// 而游戏（ItemAgentHolder 等）还持有旧实体的引用 → 那件武器会"选不中/用不了"，直到丢地上再捡起来。
        /// 就地换几何不动游戏持有的对象，所以状态不受影响。</summary>
        public bool ReplaceHeld(ItemStatsSystem.Item item)
        {
            if (item == null || !InPlaceHeldSwap) return false;
            EnsureBuilt();

            var active = item.ActiveAgent;
            if (active == null) return false;

            var tf = active.transform;
            // 已经换过就不再动（避免每帧重复挂）
            foreach (Transform child in tf)
                if (child.name.StartsWith("ModelKit_")) { HeldReplaced = true; return false; }

            PrepareGeometry(tf);
            HeldReplaced = true;
            return true;
        }

        /// <summary>就地改活实体（默认 **true**）：改 prefab + 清缓存只影响**以后**生成的实例，
        /// 已经拿在手里的那个不会自己变 —— 必须就地换几何（不销毁任何东西，所以安全）。
        /// ⚠️ 反例：用 `CreateAgent` 去"替换"活实体会销毁它，游戏引用失效 → 武器选不中/用不了。</summary>
        public bool InPlaceHeldSwap = true;

        /// <summary>没在模型文件里声明的槽位，按"原枪身包围盒里的相对位置"自动映射到新模型上（不用手填）。</summary>
        public bool AutoSlots = true;

        /// <summary>在每个挂点放一个小球，便于在游戏里肉眼确认挂点落哪（调试用）。</summary>
        public bool DebugMarkers = false;

        /// <summary>每个槽位的"原位置 -> 新位置"与两边包围盒，供核对。</summary>
        public string SlotDebug { get; private set; } = "";

        /// <summary>属于**旧模型**、要替换掉的零件：枪身（`WPN_*`）+ 原枪自带的默认件（`HideIf_*`）。
        /// `ShowIf_*`（配件本身的模型）与特效（`MuzzleFlash` / `Particle`）留给游戏管，不能动。</summary>
        static bool IsOldModelPart(Renderer r)
        {
            var n = r.gameObject.name;
            if (n.StartsWith("ShowIf_") || n.StartsWith("MuzzleFlash") || n.StartsWith("Particle")) return false;
            if (n.StartsWith("WPN_") || n.StartsWith("HideIf_")) return true;
            return false;   // 其余不认识的零件保守起见也不动
        }

        /// <summary>把 root 下的几何换成我们的：关掉枪身渲染器（保留配件槽位），我们的 mesh 挂在最大枪身零件的位置上。</summary>
        void PrepareGeometry(Transform root)
        {
            // 选"枪身"：名字 WPN_* 优先，否则取最大的非配件零件
            Renderer anchor = null;
            float best = -1f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.gameObject.name.StartsWith("ShowIf_") || r.gameObject.name.StartsWith("HideIf_")) continue;
                if (r.gameObject.name.StartsWith("WPN_")) { anchor = r; best = r.bounds.size.magnitude; break; }
                float sz = r.bounds.size.magnitude;
                if (sz > best && sz < 5f) { best = sz; anchor = r; }
            }

            int hidden = 0, kept = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                // "属于旧模型"的零件 = 枪身（WPN_*）+ 原枪自带的默认件（HideIf_*：枪口/枪托/镜座/握把）
                // 配件本身的模型（ShowIf_*）与特效留给游戏管。
                bool body = IsOldModelPart(r);
                if (!body) { kept++; continue; }
                if (r.enabled) { r.enabled = false; hidden++; }
            }
            HiddenRenderers = hidden;
            KeptConditional = kept;
            var names = new System.Collections.Generic.List<string>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (names.Count < 12) names.Add(r.gameObject.name + (ReferenceEquals(r, anchor) ? "(枪身)" : ""));
            PartsDebug = string.Join(",", names);
            AnchoredAt = anchor != null ? $"{anchor.gameObject.name}({best:0.##}m)" : "根节点";
            MaterialInfo = anchor != null && anchor.sharedMaterial != null
                ? $"{anchor.sharedMaterial.name}/{anchor.sharedMaterial.shader.name}" : "无（兜底材质）";

            // 我们的 mesh：挂在**原枪身零件**的位置上（同一个变换帧、同一个原点、同一个朝向），
            // 并把缩放补回"世界尺度 = 1"—— 因为 prefab 内部缩放链不一定为 1（实测踩过：挂在根上会缩到看不见）。
            Vector3 lossy = anchor != null ? anchor.transform.lossyScale : root.lossyScale;
            var inv = new Vector3(
                Mathf.Abs(lossy.x) > 1e-6f ? 1f / lossy.x : 1f,
                Mathf.Abs(lossy.y) > 1e-6f ? 1f / lossy.y : 1f,
                Mathf.Abs(lossy.z) > 1e-6f ? 1f / lossy.z : 1f);

            var go = new GameObject("ModelKit_" + _spec.Name + "_mesh");
            go.layer = anchor != null ? anchor.gameObject.layer : root.gameObject.layer;
            go.transform.SetParent(anchor != null ? anchor.transform : root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = inv;
            OurWorldSize = _mesh.bounds.size.magnitude;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = UnityAdapter.CloneWithTexture(anchor != null ? anchor.sharedMaterial : null, _texture);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            PlaceSlots(root, go, mr, anchor);
        }

        /// <summary>把游戏里 `ShowIf_&lt;槽位&gt;`（= 配件模型）的挂点摆到新模型上。两种来源：
        /// ① **模型文件 `slots` 声明**（米、模型自身坐标系）—— 声明了就用它；
        /// ② **自动**（默认）：把原位置在"原枪身包围盒"里的相对位置映射到"我们 mesh 的包围盒"。
        /// 只改位置、保留原件旋转/缩放（配件的世界朝向不动，只是换个安装点）。</summary>
        void PlaceSlots(Transform root, GameObject meshGo, Renderer ourRenderer, Renderer anchor)
        {
            SlotReport = ""; SlotDebug = "";
            var parts = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("ShowIf_"))
                    parts[t.name.Substring("ShowIf_".Length)] = t;
            if (parts.Count == 0) { SlotReport = "(prefab 无 ShowIf_ 槽位)"; return; }

            Bounds body = anchor != null ? anchor.bounds : ourRenderer.bounds;   // 原枪身的世界包围盒
            Bounds mine = ourRenderer.bounds;                                    // 我们 mesh 的世界包围盒

            foreach (var kv in parts)
            {
                string slot = kv.Key;
                var part = kv.Value;

                float[] v = null; bool declared = false, hide = false;
                if (_spec.Slots != null && _spec.Slots.ContainsKey(slot))
                { declared = true; v = _spec.Slots[slot]; hide = v == null; }

                if (hide)                          // 声明 null：我们这把枪没有这个挂点 → 藏掉（免得配件飘在旧位置）
                {
                    part.gameObject.SetActive(false);
                    SlotReport += slot + "✗(无挂点);";
                    continue;
                }

                Vector3 before = part.position;    // 原位置（世界）
                // 自动方案（仅供参考/对照）：按包围盒相对位置映射
                Vector3 auto = new Vector3(
                    body.size.x > 1e-6f ? mine.min.x + Mathf.Clamp01((before.x - body.min.x) / body.size.x) * mine.size.x : mine.center.x,
                    body.size.y > 1e-6f ? mine.min.y + Mathf.Clamp01((before.y - body.min.y) / body.size.y) * mine.size.y : mine.center.y,
                    body.size.z > 1e-6f ? mine.min.z + Mathf.Clamp01((before.z - body.min.z) / body.size.z) * mine.size.z : mine.center.z);
                if (declared)
                {
                    // 挂到**我们的 mesh** 下：它的世界缩放=1 → 声明值（米）就是相对模型原点的偏移（模型原点=握把）
                    part.SetParent(meshGo.transform, true);
                    if (v.Length >= 6) part.localRotation = Quaternion.Euler(v[3], v[4], v[5]);
                    part.localPosition = new Vector3(v[0], v[1], v[2]);
                    SlotReport += slot + "✓(声明);";
                }
                else if (AutoSlots)
                {
                    // 原位置在"原枪身包围盒"中的相对坐标(0..1) → 映射到"我们 mesh 的包围盒"的同一相对位置
                    part.position = auto;
                    SlotReport += slot + "✓(自动);";
                }
                else { SlotReport += slot + "-(未处理);"; continue; }

                if (DebugMarkers) MakeMarker(part);
                SlotDebug += slot + ": " + Fmt(before) + " -> " + Fmt(part.position)
                    + (declared ? "（自动会放 " + Fmt(auto) + "）" : "")
                    + " | 原枪身盒 min=" + Fmt(body.min) + " size=" + Fmt(body.size)
                    + " | 我方盒 min=" + Fmt(mine.min) + " size=" + Fmt(mine.size) + " || ";
            }
        }

        static string Fmt(Vector3 v) => "(" + v.x.ToString("0.###") + "," + v.y.ToString("0.###") + "," + v.z.ToString("0.###") + ")";

        /// <summary>调试用：在挂点处放一个小球（默认材质，最不容易受 shader 影响）。</summary>
        static void MakeMarker(Transform slot)
        {
            foreach (Transform c in slot) if (c.name.StartsWith("ModelKitMarker")) return;
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m.name = "ModelKitMarker_" + slot.name;
            var col = m.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            m.transform.SetParent(slot, false);
            m.transform.localPosition = Vector3.zero;
            float s = slot.lossyScale.x; m.transform.localScale = new Vector3(
                Mathf.Abs(s) > 1e-6f ? 0.02f / s : 0.02f, Mathf.Abs(s) > 1e-6f ? 0.02f / s : 0.02f, Mathf.Abs(s) > 1e-6f ? 0.02f / s : 0.02f);
        }

        static Transform FindByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindByName(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>每帧：图形被游戏换回去就再绑一次（手持实体只在需要时才重建，避免每帧刷新）。</summary>
        public int Tick(ItemStatsSystem.Item item, HashSet<int> bound)
        {
            if (item == null) return 0;
            int id = item.GetInstanceID();
            int done = 0;

            // ① 物品模板（社区做法）：改一次，之后每次实例化都用我们的图形
            if (!bound.Contains(id) && BindGraphicOnPrefab(item.TypeID)) done++;
            // ② 活动实例的图形（如果那份还是旧的）
            if (!bound.Contains(id) && item.ItemGraphic != null && !item.ItemGraphic.name.StartsWith("ModelKit_"))
            {
                if (BindGraphic(item)) done++;
            }
            // ③ 已经拿在手里的那个实例：就地换几何（只改渲染器 + 挂我们的 mesh，不销毁任何东西）
            if (ReplaceHeld(item)) done++;
            // ④ 清实体缓存 → 以后游戏创建实体时会读我们的图形
            if (done > 0) ClearAgentCache(item);
            if (done > 0) bound.Add(id);
            return done;
        }

        /// <summary>场上属于这个物品的图形实例数 / 我们克隆的实例数（诊断用）。</summary>
        public static int CountOurGraphics()
        {
            int n = 0;
            foreach (var g in Object.FindObjectsOfType<ItemGraphicInfo>())
                if (g != null && g.name.StartsWith("ModelKit_")) n++;
            return n;
        }
    }
}
