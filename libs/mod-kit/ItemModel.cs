// ItemModel.cs —— 把模型作用到**物品**上（枪、配件、背包、任意 Item）。
//
// 物品的模型有**两条独立的路径**（读游戏代码 + 实测得到的结论 ✓），两条都要换：
//   · **掉落/展示** → `ItemGraphicInfo`：游戏调 `ItemGraphicInfo.CreateAGraphic(item.ItemGraphic, …)` 实例化
//     ⇒ 所以正解是"改 item.ItemGraphic（或直接改物品模板的 prefab）+ 清掉实体缓存" → 让游戏自己去重建
//   · **拿在手上** → `ItemAgent`：手里那份是 `item.ActiveAgent`
//     ⇒ 正解是**就地**换几何（关原渲染器 + 挂我们的 mesh），**不销毁任何游戏持有的对象**
//
// ⚠️ 实测踩过的坑（照抄，别改）：
//   · **千万不要用 `ItemAgentUtilities.CreateAgent()` 去"替换"活实体** ✗：它内部会 `ReleaseActiveAgent()`
//     销毁旧实体，而游戏（ItemAgentHolder 等）还持有旧实体的引用 → 那件武器会"选不中/用不了"，
//     直到丢地上再捡起来。就地换几何不动游戏持有的对象，所以状态不受影响 ✓
//   · **模板（克隆出来的那个）千万不要 `SetActive(false)`** ✗：Unity 的 `Instantiate` 会继承激活状态
//     → 之后所有实例全都不可见（手里看不到、掉地上消失）。模板保持激活 ✓ 但挪到世界外（y = −5000）✓
//   · **必须 `DontDestroyOnLoad`** ✓：否则换场景（菜单 → 关卡）时模板被销毁
//     → 物品的 itemGraphic 变成"已销毁"引用（= 空）→ 又变回原样 ✗
//   · **别把 mesh 锚在"第一个"渲染器上** ✗（可能是个小管子）→ 取**包围盒最大**的那个当锚点 ✓
//   · **枪上的零件要挑着关** ✓：`WPN_*`（枪身）+ `HideIf_*`（原枪自带的默认件）要关；
//     `ShowIf_*`（配件自己的模型）与特效（`MuzzleFlash` / `Particle`）**留给游戏按状态开关** ✓
//     我们碰了就会出现"第一次看不到配件、切换一次才全显示" ✗（实测踩过两次）
//   · 挂在锚点上要**把缩放补回"世界尺度 = 1"** ✓（prefab 内部缩放链不一定为 1，否则会缩到看不见 ✗）
//
// 写回图形用**反射** ✓（先试可写属性 `ItemGraphic` ✓ 再退私有字段 `itemGraphic` ✓）—— 更能抗游戏更新 ✓
// （社区 mod 也是这么做的 ✓）

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    /// <summary>把"我们的 mesh + 贴图"接到某个**物品**上（改图形 prefab + 清实体缓存 + 就地换手持几何）。</summary>
    public static class ItemModel
    {
        /// <summary>一次替换的结果 ✓（带 `Restore()` ✓ 热重载/卸载时能还原 ✓）</summary>
        public sealed class Result
        {
            public bool Applied;
            public GameObject Instance;                       // 我们挂上去的那个物体
            public GameObject Template;                       // 我们克隆出来、藏到世界外的图形模板（可能为 null ✓）
            public readonly List<Renderer> Hidden = new List<Renderer>();
            public int KeptRenderers;
            public string AnchorName = "";
            public bool PrefabBound;                          // 是否写到了物品模板的 prefab 上
            public bool HeldSwapped;                          // 是否就地换了手里那个
            public bool CacheCleared;                         // 是否清了实体缓存
            public string Report = "";

            /// <summary>热重载/卸载：恢复被关掉的旧几何 + 抹掉我们挂上去的东西 ✓（幂等 ✓ 可重复调 ✓）</summary>
            public void Restore()
            {
                for (int i = 0; i < Hidden.Count; i++)
                    if (Hidden[i] != null) Hidden[i].enabled = true;
                Hidden.Clear();
                if (Instance != null) { UnityEngine.Object.Destroy(Instance); Instance = null; }
                if (Template != null) { UnityEngine.Object.Destroy(Template); Template = null; }
                Applied = false;
            }
        }

        // ───────────────────────── 反射：写回物品的图形 ─────────────────────────

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

        /// <summary>把图形写到物品上 ✓（属性优先 ✓ 私有字段兜底 ✓）；两条都不通返回 false（游戏会退化成"纸片" ✗）</summary>
        public static bool WriteGraphic(ItemStatsSystem.Item item, ItemGraphicInfo graphic)
        {
            if (item == null || graphic == null) return false;
            ProbeGraphic();
            if (_graphicProp != null) { _graphicProp.SetValue(item, graphic); return true; }
            if (_graphicField != null) { _graphicField.SetValue(item, graphic); return true; }
            return false;
        }

        /// <summary>清掉物品的"实体缓存"（社区做法 ✓）：让游戏下次创建实体时重新读 `item.ItemGraphic`（= 我们的图形 ✓）。
        /// ⚠️ 别自己去销毁/替换活着的实体 ✗（见文件头）</summary>
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

        // ───────────────────────── 找锚点 / 关旧几何 / 挂我们的 mesh ─────────────────────────

        /// <summary>属于**旧模型**、要替换掉的零件：枪身（`WPN_*`）+ 原枪自带的默认件（`HideIf_*`）。
        /// `ShowIf_*`（配件本身的模型）与特效（`MuzzleFlash` / `Particle`）留给游戏管，不能动 ✗</summary>
        static bool IsOldWeaponPart(Renderer r)
        {
            var n = r.gameObject.name;
            if (n.StartsWith("ShowIf_") || n.StartsWith("MuzzleFlash") || n.StartsWith("Particle")) return false;
            if (n.StartsWith("WPN_") || n.StartsWith("HideIf_")) return true;
            return false;   // 其余不认识的零件保守起见也不动 ✓
        }

        /// <summary>这个 prefab 是不是"武器式"的（有 `WPN_*` / `ShowIf_*` / `HideIf_*` 这类零件命名 ✓）</summary>
        static bool LooksLikeWeapon(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name;
                if (n.StartsWith("WPN_") || n.StartsWith("ShowIf_") || n.StartsWith("HideIf_")) return true;
            }
            return false;
        }

        /// <summary>取"主体"渲染器：`WPN_*` 优先，否则取**包围盒最大**的那个（< 5m，避开天空盒之类的怪东西 ✓）</summary>
        static Renderer PickAnchor(Transform root)
        {
            Renderer anchor = null;
            float best = -1f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var n = r.gameObject.name;
                if (n.StartsWith("ShowIf_") || n.StartsWith("HideIf_")) continue;
                if (n.StartsWith("WPN_")) return r;                       // 枪身优先 ✓
                float sz = r.bounds.size.magnitude;
                if (sz > best && sz < 5f) { best = sz; anchor = r; }
            }
            return anchor;
        }

        /// <summary>把 root 下的旧几何关掉（保留配件槽位/特效 ✓），把关掉的记进 res ✓（能还原 ✓）。
        /// 武器式命名 → 只关 `WPN_*` / `HideIf_*` ✓；普通物品 → 全关 ✓（它就是整个旧模型 ✓）</summary>
        static void HideOldGeometry(Transform root, Result res)
        {
            bool weapon = LooksLikeWeapon(root);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                bool ours = r.gameObject.name.StartsWith("ModelKit_");
                if (ours) continue;
                if (weapon && !IsOldWeaponPart(r)) { res.KeptRenderers++; continue; }
                if (r.enabled) { r.enabled = false; res.Hidden.Add(r); }
            }
        }

        /// <summary>把我们的 mesh 挂到锚点位置上（同一个变换帧、同一个原点、同一个朝向 ✓）
        /// + 克隆游戏材质并换上我们的贴图 ✓ + 缩放补回世界尺度 1 ✓</summary>
        static GameObject AttachOurMesh(Transform root, Renderer anchor, Mesh mesh, Texture2D texture, string name)
        {
            Vector3 lossy = anchor != null ? anchor.transform.lossyScale : root.lossyScale;
            var inv = new Vector3(
                Math.Abs(lossy.x) > 1e-6f ? 1f / lossy.x : 1f,
                Math.Abs(lossy.y) > 1e-6f ? 1f / lossy.y : 1f,
                Math.Abs(lossy.z) > 1e-6f ? 1f / lossy.z : 1f);

            var go = new GameObject("ModelKit_" + name);
            go.layer = anchor != null ? anchor.gameObject.layer : root.gameObject.layer;
            go.transform.SetParent(anchor != null ? anchor.transform : root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = inv;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            // 材质：克隆游戏自己的（shader/关键字/渲染状态都对 ✓）→ 只把贴图换成我们的 ✓
            var srcMat = anchor != null ? anchor.sharedMaterial : null;
            if (srcMat == null)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial != null) { srcMat = r.sharedMaterial; break; }
            var one = srcMat != null ? new Material(srcMat) : null;
            if (one != null)
            {
                if (texture != null) one.mainTexture = texture;
                one.color = Color.white;
            }
            int sub = Mathf.Max(1, mesh.subMeshCount);
            var mats = new Material[sub];                 // 每个 submesh 一个材质槽（别让 Unity 去猜 ✓）
            for (int i = 0; i < sub; i++) mats[i] = one;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        // ───────────────────────── 对外：替换 / 新增 ─────────────────────────

        /// <summary>⭐ 复制一个图形模板（源 = 某个物品自己的图形 ✓）→ 换成我们的几何 → 藏到世界外 → 返回它。
        /// 拿到的这个 `ItemGraphicInfo` 就是物品的 `itemGraphic`（`Item.itemGraphic` 是**数据层与模型层唯一的连接点** ✓）。
        /// ⚠️ 只造图形，不注册物品 —— 新物品（新 typeID / 名字 / 数值）属**数据层** ✗</summary>
        public static ItemGraphicInfo BuildGraphicClone(ItemGraphicInfo template, Mesh mesh, Texture2D texture,
                                                       out GameObject templateGo)
        {
            templateGo = null;
            if (template == null) return null;
            var clone = UnityEngine.Object.Instantiate(template);
            clone.gameObject.name = "ModelKit_graphic";
            clone.gameObject.SetActive(true);                          // ⚠️ 不能停用（见文件头）
            clone.transform.position = new Vector3(0f, -5000f, 0f);    // 模板藏到世界外
            UnityEngine.Object.DontDestroyOnLoad(clone.gameObject);    // ⚠️ 必须在（见文件头）
            templateGo = clone.gameObject;

            var res = new Result { Template = clone.gameObject };
            var anchor = PickAnchor(clone.transform);
            res.AnchorName = anchor != null ? anchor.gameObject.name : "根节点";
            HideOldGeometry(clone.transform, res);
            res.Instance = AttachOurMesh(clone.transform, anchor, mesh, texture, "graphic");
            return clone;
        }

        /// <summary>把（`BuildGraphicClone` 造出来的）图形写到任意 Item 上 —— 新增物品的最后一步 ✓。
        /// 失败返回 false（游戏里会退化成"纸片" ✗）</summary>
        public static bool WriteGraphicTo(ItemStatsSystem.Item item, ItemGraphicInfo graphic)
            => WriteGraphic(item, graphic);

        /// <summary>就地改**活着的**手持实体（不销毁任何东西 ✓）：
        /// 已经换过就不重复挂 ✓。返回挂上去的物体（没换则 null ✓）</summary>
        public static GameObject ReplaceHeld(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, Result res)
        {
            var active = item != null ? item.ActiveAgent : null;
            if (active == null) return null;
            var tf = active.transform;
            foreach (Transform child in tf)
                if (child.name.StartsWith("ModelKit_")) { res.HeldSwapped = true; return null; }   // 已换 ✓

            if (!LooksLikeWeapon(tf)) return null;   // 普通物品没有"手持实体"这条 ✓
            var anchor = PickAnchor(tf);
            HideOldGeometry(tf, res);
            var go = AttachOurMesh(tf, anchor, mesh, texture, "held");
            res.HeldSwapped = true;
            return go;
        }

        /// <summary>⭐ 主入口：把物品（掉落/展示 + 拿在手里）的模型换成我们的。
        ///
        /// 做四件事（缺哪件都会"只换一半" ✗）：
        ///   ① 克隆物品自己的图形 prefab（保留 sockets / groundPoint / 各种设置 ✓）→ 只换几何 → 写回 `item.ItemGraphic`
        ///   ② 如果给了 `typeID`：把图形也写到**物品模板**上（`ItemAssetsCollection.GetPrefab` ✓）→ 以后每个实例开局就是对的 ✓
        ///   ③ 清实体缓存（`ClearAgentCache` ✓）→ 游戏下次创建实体时读我们的图形 ✓
        ///   ④ 就地换**已经拿在手里**的那个（只影响以后生成的实例，手里那个不会自己变 ✗）
        /// </summary>
        public static Result Apply(ItemStatsSystem.Item item, Mesh mesh, Texture2D texture, bool bindPrefab = true)
        {
            var res = new Result();
            if (item == null || mesh == null) { res.Report = "物品或网格为空 ✗"; return res; }

            // ① 克隆它自己的图形
            var src = item.ItemGraphic;
            if (src == null) { res.Report = "这件物品没有 itemGraphic ✗（换不了外观）"; return res; }
            GameObject tmplGo;
            var clone = BuildGraphicClone(src, mesh, texture, out tmplGo);
            if (clone == null) { res.Report = "克隆图形 prefab 失败 ✗"; return res; }
            res.Template = tmplGo;
            res.Instance = tmplGo.GetComponentInChildren<MeshFilter>() != null
                ? FindOurChild(tmplGo.transform) : res.Instance;

            bool wrote = WriteGraphic(item, clone);
            if (!wrote) { res.Report = "写回 item.ItemGraphic 失败 ✗（属性/字段都找不到）"; return res; }

            // ② 写到物品模板（社区做法 ✓ 开局就对 ✓）
            if (bindPrefab)
            {
                try
                {
                    var prefab = ItemStatsSystem.ItemAssetsCollection.GetPrefab(item.TypeID);
                    if (prefab != null && !ReferenceEquals(prefab, item))
                        res.PrefabBound = WriteGraphic(prefab, clone);
                }
                catch { res.PrefabBound = false; }
            }

            // ④ 就地换手里那个
            ReplaceHeld(item, mesh, texture, res);

            // ③ 清缓存（放最后 ✓）
            res.CacheCleared = ClearAgentCache(item);

            res.Applied = true;
            res.Report = $"锚点='{res.AnchorName}'｜关旧零件 {res.Hidden.Count} 个（保留 {res.KeptRenderers} 个：配件/特效）"
                       + $"｜写回图形={(wrote ? "成功" : "失败")}｜模板 prefab={(res.PrefabBound ? "已写" : "未写")}"
                       + $"｜手持就地换={(res.HeldSwapped ? "是" : "否")}｜清实体缓存={(res.CacheCleared ? "是" : "否")}"
                       + $"｜mesh 顶点={mesh.vertexCount} 子网格={mesh.subMeshCount} 材质={(texture != null ? "已换贴图" : "游戏原材质")}";
            return res;
        }

        static GameObject FindOurChild(Transform t)
        {
            foreach (Transform c in t) if (c.name.StartsWith("ModelKit_")) return c.gameObject;
            return null;
        }
    }
}
