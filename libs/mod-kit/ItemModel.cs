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
        static GameObject Attach(Transform root, Renderer mount, Mesh mesh, Texture2D texture, string itemName)
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
            go.transform.localPosition = Vector3.zero;
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

            // 幂等 ✓：**整个子树**里已经有我们的东西就不再挂一遍 ✗
            //   （模板改过之后，游戏新造出来的实例**天生**就带着它 ✓ → 不判子级会给每个实例重复挂 ✗ 实测）
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.gameObject.name.StartsWith("ModelKit_"))
                { res.Applied = true; res.Report = "已经换过了 ✓"; return res; }

            var mount = PickMount(root);
            res.AnchorName = mount != null ? mount.gameObject.name : "根";
            HideOld(root, res);
            res.Instance = Attach(root, mount, mesh, texture, itemName);
            res.Applied = true;
            res.Report = $"关旧外观 {res.Hidden.Count} 个｜挂到 '{res.AnchorName}'"
                       + $"｜mesh 顶点={mesh.vertexCount} 子网格={mesh.subMeshCount}"
                       + $"｜材质={(texture != null ? "已换贴图" : "游戏原材质")}";
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
                res.Report = "没有 ItemGraphic ✗（纯图标物品 ✓ 换不了外观）";
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
