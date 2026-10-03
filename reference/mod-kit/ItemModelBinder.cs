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
        ModelKit.MeshData _data;      // 生成时的顶点数据（含每个零件的盒 → 按语义算槽位）
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
            _data = MeshKit.Build(_spec);
            var data = _data;
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
            Object.DontDestroyOnLoad(clone.gameObject);              // ⚠️ 必须在：否则换场景（菜单→关卡）时被销毁
                                                                     //    → 物品的 itemGraphic 变成"已销毁"引用（= null）

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

        /// <summary>⭐ 给**新增物品**造图形：复制"源物品"的 `itemGraphic` prefab → 换成我们的几何 → 返回它。
        /// 拿到的这个 `ItemGraphicInfo` 就是新物品的 `itemGraphic`（`Item.itemGraphic` 是数据层与模型层
        /// **唯一的连接点**；工坊 mod 也是这么接的）。
        /// ⚠️ 只造图形，不注册物品 —— 新物品（新 typeID / 名字 / 数值 / `AddDynamicEntry`）属**数据层**。</summary>
        public ItemGraphicInfo BuildGraphicClone(ItemGraphicInfo template)
        {
            if (template == null) return null;
            EnsureBuilt();
            var clone = Object.Instantiate(template);
            clone.gameObject.name = "ModelKit_" + _spec.Name;
            clone.gameObject.SetActive(true);                        // ⚠️ 不能停用（见文件头）
            clone.transform.position = new Vector3(0f, -5000f, 0f);  // 模板藏到世界外
            Object.DontDestroyOnLoad(clone.gameObject);              // ⚠️ 同 BindGraphic：跨场景必须存活
            PrepareGeometry(clone.transform);
            return clone;
        }

        /// <summary>同上，但源取自某个现有物品（`ItemAssetsCollection.GetPrefab(typeID)`）的图形。</summary>
        public ItemGraphicInfo BuildGraphicClone(int sourceTypeID)
        {
            try
            {
                var src = ItemStatsSystem.ItemAssetsCollection.GetPrefab(sourceTypeID);
                return src != null ? BuildGraphicClone(src.ItemGraphic) : null;
            }
            catch { return null; }
        }

        /// <summary>把（`BuildGraphicClone` 造出来的）图形写到任意 Item 上 —— 新增物品的最后一步。
        /// 失败返回 false（游戏里会退化成"纸片"）。</summary>
        public bool WriteGraphicTo(ItemStatsSystem.Item item, ItemGraphicInfo graphic)
            => item != null && graphic != null && WriteGraphic(item, graphic);

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

        /// <summary>锚点与我们的 mesh 的世界变换（位置/旋转/缩放），供离线核对。</summary>
        public string AnchorsDebug { get; private set; } = "";

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
            var oneMat = UnityAdapter.CloneWithTexture(anchor != null ? anchor.sharedMaterial : null, _texture);
            int subCount = Mathf.Max(1, _mesh.subMeshCount);
            var mats = new Material[subCount];                 // 每个 submesh 一个材质槽（别让 Unity 去猜）
            for (int i = 0; i < subCount; i++) mats[i] = oneMat;
            mr.sharedMaterials = mats;
            MaterialInfo += $"｜子网格={subCount} 材质槽={mats.Length} shader={(oneMat != null && oneMat.shader != null ? oneMat.shader.name : "?")}"
                           + "｜双面=" + (UnityAdapter.MakeDoubleSided(oneMat) ? "是" : "否");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            AnchorsDebug = "锚点 '" + (anchor != null ? anchor.name : "根") + "' 世界位置=" + Fmt(anchor != null ? anchor.transform.position : root.position)
                + " 旋转=" + (anchor != null ? anchor.transform.rotation.eulerAngles.ToString("0.#") : "-")
                + " 缩放=" + Fmt(anchor != null ? anchor.transform.lossyScale : root.lossyScale)
                + "；我们的 mesh 世界位置=" + Fmt(go.transform.position) + " 旋转=" + go.transform.rotation.eulerAngles.ToString("0.#")
                + " 缩放=" + Fmt(go.transform.lossyScale) + "；mesh 局部盒 min=" + Fmt(_mesh.bounds.min) + " max=" + Fmt(_mesh.bounds.max);
            PlaceSlots(root, go, mr, anchor);
        }

        /// <summary>把配件槽位摆到新模型上。**权威对象是 `Sockets/&lt;槽位&gt;` 这个 Transform**（实测 + 反编译）：
        /// 装上的配件由 `ItemGraphicInfo` 实例化后 `SetParent(socketPoint)`（局部位置/旋转归零、缩放 1）
        /// → **挂点在哪，配件就出现在哪**。`ShowIf_&lt;槽位&gt;` 只是"占位模型"，跟着一起平移保持一致。
        ///
        /// 位置来源：① 模型文件 `slots` 声明（米、模型自身坐标系）= **槽位应在我们模型上的位置**；
        /// ② 默认自动：原槽位在"原枪身包围盒"里的相对位置 → 映射到"我们 mesh 的包围盒"的同一相对位置。
        /// 声明 `null` = 我们这把枪没有这个挂点（槽位与占位件都关掉）。</summary>
        void PlaceSlots(Transform root, GameObject meshGo, Renderer ourRenderer, Renderer anchor)
        {
            SlotReport = ""; SlotDebug = "";

            var sockets = new Dictionary<string, Transform>();
            var socketsRoot = FindByName(root, "Sockets");
            if (socketsRoot != null)
                for (int i = 0; i < socketsRoot.childCount; i++)
                    sockets[socketsRoot.GetChild(i).name] = socketsRoot.GetChild(i);
            if (_spec.Slots != null)                       // 声明的槽位即使 prefab 里没有也要能报出来
                foreach (var k in _spec.Slots.Keys) if (!sockets.ContainsKey(k)) sockets[k] = null;
            if (sockets.Count == 0) { SlotReport = "(prefab 无 Sockets 槽位、模型也未声明)"; return; }

            // ⚠️ 用**锚点局部坐标系**里的盒子（不是世界轴 AABB）—— 手里的枪是斜的，世界 AABB 会被转歪（实测踩过）
            var frame = anchor != null ? anchor.transform : meshGo.transform;
            Bounds body = anchor != null ? anchor.localBounds : ourRenderer.localBounds;   // 原枪身（局部）
            Bounds mine = _mesh.bounds;                                                    // 我们 mesh（模型坐标）

            foreach (var kv in sockets)
            {
                string slot = kv.Key;
                var socket = kv.Value;
                var placeholder = FindByName(root, "ShowIf_" + slot);   // 占位模型（非权威，仅保持一致）

                float[] v = null; bool declared = false, hide = false;
                if (_spec.Slots != null && _spec.Slots.ContainsKey(slot))
                { declared = true; v = _spec.Slots[slot]; hide = v == null; }

                if (hide)                       // 我们这把枪没有这个挂点
                {
                    if (socket != null) socket.gameObject.SetActive(false);
                    if (placeholder != null) placeholder.gameObject.SetActive(false);
                    SlotReport += slot + "✗;"; continue;
                }
                if (socket == null) { SlotReport += slot + "(无挂点);"; continue; }

                Vector3 from = socket.position;                                         // 原挂点（世界）
                Vector3 fromLocal = frame.InverseTransformPoint(from);                  // 同上，锚点局部系
                Vector3 autoLocal = MapBox(fromLocal, body, mine);
                // 优先"按零件语义"算（枪口=barrel 前端…）；零件 role 不认识时才退回包围盒归一化映射
                Vec3 guess;
                if (_data != null && _data.TryGuessSlot(slot, out guess))
                    autoLocal = new Vector3(guess.X, guess.Y, guess.Z);
                Vector3 auto = frame.TransformPoint(autoLocal);
                Vector3 target = declared ? meshGo.transform.TransformPoint(new Vector3(v[0], v[1], v[2])) : auto;
                Vector3 delta = target - from;
                socket.position += delta;                                   // ← 挂点移动：配件就落在这
                if (placeholder != null) placeholder.position += delta;     // 占位件跟着动，保持一致
                if (DebugMarkers) MakeMarker(socket, target);
                SlotReport += slot + (declared ? "✓(声明);" : "✓(自动);");
                SlotDebug += slot + ": 挂点" + Fmt(from) + " -> " + Fmt(target) + " 位移" + Fmt(delta)
                    + "（自动=" + Fmt(auto) + "）"
                    + " | 原枪身盒 min=" + Fmt(body.min) + " size=" + Fmt(body.size)
                    + " | 我方盒 min=" + Fmt(mine.min) + " size=" + Fmt(mine.size) + " || ";
            }
        }

        /// <summary>把世界坐标 from 在 inBox 里的相对位置(0..1)，映射到 outBox 里的同一相对位置。</summary>
        static Vector3 MapBox(Vector3 from, Bounds inBox, Bounds outBox)
        {
            float nx = inBox.size.x > 1e-6f ? Mathf.Clamp01((from.x - inBox.min.x) / inBox.size.x) : 0.5f;
            float ny = inBox.size.y > 1e-6f ? Mathf.Clamp01((from.y - inBox.min.y) / inBox.size.y) : 0.5f;
            float nz = inBox.size.z > 1e-6f ? Mathf.Clamp01((from.z - inBox.min.z) / inBox.size.z) : 0.5f;
            return new Vector3(outBox.min.x + nx * outBox.size.x, outBox.min.y + ny * outBox.size.y,
                               outBox.min.z + nz * outBox.size.z);
        }

        /// <summary>一个 Transform 子树里所有渲染器的合并包围盒（它的 GameObject 可以是未激活的，仍能算）。</summary>
        static Bounds BoundsOf(Transform t)
        {
            bool any = false; Bounds b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) b = new Bounds(t.position, Vector3.zero);
            return b;
        }

        /// <summary>诊断：每个挂点 + 挂在上面的配件（它的图形）几何，全部换算到**挂点局部坐标**打印。
        /// 缝隙就写在数字里：配件几何的 min.z 不在 0 附近 = 离枪有距离。</summary>
        public string DescribeSlots(Transform root)
        {
            var sb = new System.Text.StringBuilder();
            var socketsRoot = FindByName(root, "Sockets");
            if (socketsRoot == null) return "(没有 Sockets 容器)";
            for (int i = 0; i < socketsRoot.childCount; i++)
            {
                var s = socketsRoot.GetChild(i);
                sb.Append(s.name).Append(": 世界=").Append(Fmt(s.position))
                  .Append(" 缩放=").Append(Fmt(s.lossyScale));
                bool any = false;
                foreach (Transform c in s)
                {
                    Bounds b; if (!LocalBoundsIn(s, c, out b)) continue;
                    any = true;
                    sb.Append(" | 装着 '").Append(c.name).Append("' 几何(挂点局部) min=")
                      .Append(Fmt(b.min)).Append(" max=").Append(Fmt(b.max));
                }
                if (!any) sb.Append(" | (没装东西)");
                sb.Append(" || ");
            }
            return sb.ToString();
        }

        /// <summary>把 c 的渲染器几何精确换算到 frame 坐标系里的包围盒。
        /// ⚠️ 不能拿渲染器的**世界轴 AABB** 去 TransformPoint —— 枪是斜的，世界 AABB 被放大（实测踩过：
        /// 枪口件真长 0.247m，却量出"往枪身包了 3cm"）。这里用 localBounds × 矩阵。</summary>
        static bool LocalBoundsIn(Transform frame, Transform c, out Bounds result)
        {
            result = new Bounds();
            bool any = false;
            foreach (var r in c.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var lb = r.localBounds;
                var m = frame.worldToLocalMatrix * r.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? lb.min.x : lb.max.x,
                        (i & 2) == 0 ? lb.min.y : lb.max.y,
                        (i & 4) == 0 ? lb.min.z : lb.max.z);
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; } else result.Encapsulate(p);
                }
            }
            return any;
        }

        static string Fmt(Vector3 v) => "(" + v.x.ToString("0.###") + "," + v.y.ToString("0.###") + "," + v.z.ToString("0.###") + ")";

        /// <summary>调试用：在配件"会出现在哪"放一个小球（默认材质，最不容易受 shader 影响）。</summary>
        static void MakeMarker(Transform part, Vector3 at)
        {
            foreach (Transform c in part) if (c.name.StartsWith("ModelKitMarker")) return;
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m.name = "ModelKitMarker_" + part.name;
            var col = m.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
            m.transform.SetParent(part, true);
            m.transform.position = at;                 // 打在"配件会出现在哪"（= 配件渲染中心的目标位置）
            float s = part.lossyScale.x;
            float k = Mathf.Abs(s) > 1e-6f ? 0.02f / s : 0.02f;
            m.transform.localScale = new Vector3(k, k, k);
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
