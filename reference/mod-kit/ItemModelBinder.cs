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

        /// <summary>手持那条：**就地**在现有实体上换几何。
        ///
        /// ⚠️ 不要用 `ItemAgentUtilities.CreateAgent()` 去替换实体：它内部会 `ReleaseActiveAgent()` 销毁旧实体，
        /// 而游戏（ItemAgentHolder 等）还持有旧实体的引用 → 那件武器会"选不中/用不了"，直到丢地上再捡起来。
        /// 就地换几何不动游戏持有的对象，所以状态不受影响。</summary>
        public bool ReplaceHeld(ItemStatsSystem.Item item)
        {
            if (item == null) return false;
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

            // **按真实尺寸放（scale = 1，永不缩放）**：原游戏物品图形 99.5% 是 scale=1、社区 mod 包 100% 是 1；
            // 尺寸由模型自己定义（枪 0.5–0.9 m…），原点由模型文件声明（pivotOffset）——运行时不做任何缩放/对齐。
            var go = new GameObject("ModelKit_" + _spec.Name + "_mesh");
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            OurWorldSize = _mesh.bounds.size.magnitude * (root.lossyScale.magnitude / Mathf.Sqrt(3f));

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = UnityAdapter.CloneWithTexture(anchor != null ? anchor.sharedMaterial : null, _texture);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>每帧：图形被游戏换回去就再绑一次（手持实体只在需要时才重建，避免每帧刷新）。</summary>
        public int Tick(ItemStatsSystem.Item item, HashSet<int> bound)
        {
            if (item == null) return 0;
            int id = item.GetInstanceID();
            int done = 0;
            if (!bound.Contains(id) || item.ItemGraphic == null || !item.ItemGraphic.name.StartsWith("ModelKit_"))
            {
                if (BindGraphic(item)) { bound.Add(id); done++; }
            }
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
