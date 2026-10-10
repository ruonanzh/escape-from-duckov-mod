// GameApi.cs —— 与 Duckov 角色系统打交道的小工具（找角色 / 拿模型 / 隐藏原外观）。
//
// 这些是"换角色外观"时每次都要做对的事，写在一处，mod 直接调用：
//   · 找玩家：CharacterMainControl.IsMainCharacter（FindObjectsOfType 的第一个可能是 NPC）
//   · 找特定角色：按 GameObject 名 / 模型名过滤
//   · 隐藏原外观：只压"蒙皮网格"（角色本体），保留 MeshRenderer（装备、手里的枪）
//   · 层：可见 = Character(9)，隐藏 = SpecialCamera(31)（实测）

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModelKit
{
    public static class GameApi
    {
        /// <summary>可见层 / 隐藏层（实测：角色渲染器在 Character=9；隐藏用 SpecialCamera=31）。</summary>
        public static int VisibleLayer => LayerMask.NameToLayer("Character");
        public static int HiddenLayer => LayerMask.NameToLayer("SpecialCamera");

        /// <summary>全部角色（玩家 + NPC + 宠物）。在关卡里才有，主菜单是空的 → 需要等。</summary>
        public static CharacterMainControl[] AllCharacters() => Object.FindObjectsOfType<CharacterMainControl>();

        /// <summary>玩家。用 IsMainCharacter 判定 —— 不要拿 FindObjectsOfType 的第一个，那可能是 NPC。</summary>
        public static CharacterMainControl FindMainCharacter()
            => AllCharacters().FirstOrDefault(c => c != null && c.IsMainCharacter);

        /// <summary>按名字找角色（GameObject 名，例如 'Character(Clone)' 是玩家；NPC 用它们的名字/模型名筛）。</summary>
        public static CharacterMainControl FindCharacter(string name)
            => AllCharacters().FirstOrDefault(c => c != null && c.name == name);

        /// <summary>按"模型名"找角色（模型名如 0_CharacterModel_Custom_Template；比 GameObject 名稳定）。</summary>
        public static CharacterMainControl FindCharacterByModel(string modelName)
            => AllCharacters().FirstOrDefault(c => c != null && GetModel(c) != null && GetModel(c).name.StartsWith(modelName));

        public static CharacterModel GetModel(CharacterMainControl c) => c != null ? c.characterModel : null;

        /// <summary>给**新增物品**补"手持实体"（社区 mod 优香MPX 的做法）：用我们的图形 prefab 造一个
        /// `ItemAgent_Gun`，注入 `item.AgentUtilities` 的私有 `agents` 列表（key = `Handheld`），并清
        /// `hashedAgentsCache`。**没有它：拿在手里显示的是"世界图形"（地上的姿势）**。
        /// 已有手持实体（克隆源带来的）就沿用。</summary>
        public static bool EnsureHandheldAgent(ItemStatsSystem.Item item, GameObject graphicPrefab)
        {
            if (item == null || graphicPrefab == null || item.AgentUtilities == null) return false;
            try
            {
                if (item.HasHandHeldAgent) return true;
                var agent = ItemAgent_Gun.BuildAgent(graphicPrefab);
                if (agent == null) return false;
                agent.gameObject.name = "ModelKit_Hand_" + graphicPrefab.name;
                agent.gameObject.SetActive(false);
                Object.DontDestroyOnLoad(agent.gameObject);
                return SetAgentPrefab(item.AgentUtilities, "Handheld", agent);
            }
            catch { return false; }
        }

        /// <summary>往 `ItemAgentUtilities.agents` 里写一项（私有字段 + 嵌套类型 `AgentKeyPair`，都靠反射）。</summary>
        public static bool SetAgentPrefab(ItemStatsSystem.ItemAgentUtilities au, string key, ItemStatsSystem.ItemAgent prefab)
        {
            try
            {
                var t = typeof(ItemStatsSystem.ItemAgentUtilities);
                var fAgents = t.GetField("agents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fAgents == null) return false;
                if (!(fAgents.GetValue(au) is System.Collections.IList list)) return false;
                var tPair = t.GetNestedType("AgentKeyPair", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (tPair == null) return false;
                var fKey = tPair.GetField("key");
                var fPrefab = tPair.GetField("agentPrefab");
                if (fKey == null || fPrefab == null) return false;

                object slot = null;
                foreach (var e in list) if (e != null && (string)fKey.GetValue(e) == key) { slot = e; break; }
                if (slot == null) { slot = System.Activator.CreateInstance(tPair); fKey.SetValue(slot, key); list.Add(slot); }
                fPrefab.SetValue(slot, prefab);

                var fCache = t.GetField("hashedAgentsCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                fCache?.SetValue(au, null);
                return true;
            }
            catch { return false; }
        }

        /// <summary>把一件物品塞给玩家（作新增物品的演示/自测用）：`PickupItem` 失败就退到 `inventory.AddItem`。
        /// ⚠️ `CharacterMainControl.itemControl` 是**私有字段**（`CharacterItemControl`），只能反射拿。</summary>
        public static bool GiveItemToPlayer(ItemStatsSystem.Item item)
        {
            var player = FindMainCharacter();
            if (player == null || item == null) return false;
            try
            {
                var f = typeof(CharacterMainControl).GetField("itemControl",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var ic = f != null ? f.GetValue(player) : null;
                if (ic == null) return false;
                var m = ic.GetType().GetMethod("PickupItem",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (m != null)
                {
                    var r = m.Invoke(ic, new object[] { item });
                    if (r is bool ok && ok) return true;
                }
                var inv = ic.GetType().GetProperty("inventory")?.GetValue(ic) as ItemStatsSystem.Inventory;
                return inv != null && inv.AddItem(item);
            }
            catch { return false; }
        }

        /// <summary>角色模型根（骨骼都挂在这个 Transform 下）。</summary>
        public static Transform ModelRoot(CharacterMainControl c)
        {
            var m = GetModel(c);
            return m != null ? m.transform : null;
        }

        /// <summary>把"角色本体（蒙皮网格）"压到隐藏层；装备/手里武器的 MeshRenderer 保持可见。
        /// 返回被隐藏的数量。注意：游戏可能把它们又显示回来 → 需要周期性重申（见 <see cref="KeepSkinHidden"/>）。</summary>
        public static int HideCharacterSkin(CharacterModel cm, IEnumerable<GameObject> keepUnder = null)
        {
            if (cm == null) return 0;
            int hidden = 0;
            int layer = HiddenLayer;
            if (layer < 0) return 0;

            foreach (var smr in cm.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (keepUnder != null && keepUnder.Any(k => k != null && smr.transform.IsChildOf(k.transform))) continue;
                if (smr.gameObject.layer != layer) { smr.gameObject.layer = layer; hidden++; }
            }
            return hidden;
        }

        /// <summary>每秒调一次即可：保证原外观保持隐藏（游戏在换装备/刷新时会把层改回去）。</summary>
        public static void KeepSkinHidden(CharacterModel cm, IEnumerable<GameObject> keepUnder = null)
        {
            HideCharacterSkin(cm, keepUnder);   // 幂等
        }

        /// <summary>把"我们的基础色贴图"套到一份**从游戏材质克隆来的**材质上 ✓，并**清掉其它槽** ✗。
        /// 武器 ✓ 物品 ✓ **共用同一套** ✓（用户要求一套逻辑 ✓）。
        ///
        /// 为什么必须清 ✗：克隆来的材质带着**游戏原来的**发光/法线/遮蔽图 ✗，而我们的 UV 与它不同 ✗
        ///   → 会采样出一片**脏 / 发光斑** ✗（实测：头盔上出现紫粉色发光斑 ✓）
        /// ⚠️ 属性名随管线不同 ✓ → 逐个 `HasProperty` 探 ✓（URP `_BaseMap`/`_BaseColor` ✓ 旧 API `_MainTex`/`_Color` ✓）</summary>
        public static void ApplyOurTexture(Material mat, Texture2D tex)
        {
            if (mat == null) return;
            try
            {
                // ① 基础色贴图 ✓
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    else mat.mainTexture = tex;                       // 最后兜底 ✓
                }
                // ② 基础色 tint 清成白 ✓（两套属性名都试 ✓）
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                try { mat.color = Color.white; } catch { }

                // ③ **关发光** ✗（头盔紫斑的来源 ✓）
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", null);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");

                // ④ **清掉"和我们 UV 对不上"的图** ✗（留着只会脏 ✓）
                var slots = new string[] { "_BumpMap", "_NormalMap", "_OcclusionMap", "_MetallicGlossMap",
                                           "_SpecGlossMap", "_DetailMask", "_DetailAlbedoMap", "_ParallaxMap" };
                for (int i = 0; i < slots.Length; i++)
                    if (mat.HasProperty(slots[i])) mat.SetTexture(slots[i], null);
            }
            catch { /* 材质换不上不该炸 ✓ */ }
        }
        /// <summary>⭐ 把一份**借来的游戏材质**改成**哑光** ✓ —— 专治“看着不干净”✗。
        ///
        /// 为什么要 ✗：材质是从**目标物品自己身上**借来的 ✓（`new Material(src)` ✓），
        ///   带着它的 **`_Metallic` / `_Glossiness` 原值** ✗；高金属 + 高光滑在游戏的高对比环境里
        ///   ⇒ baseColor 被当作“金属反射色”→ 暗部大片**死黑 / 暗紫斑** ✗ + **硬高光** ✗
        ///   （实测：头盔在游戏里脏 ✓ 而同一份网格/贴图离线渲染干净 ✓ ⇒ 就是这两项 ✓）
        ///
        /// 只给**物品**用 ✓；**武器不调** ✗（用户实测武器看着没问题 ✓ 别动 ✓）。
        /// 调完把**改前/改后**的数值打一条日志 ✓ —— 万一不是这个原因 ✓ 也能马上看出来 ✓。</summary>
        public static void MakeMatte(Material mat, string tag = null)
        {
            if (mat == null) return;
            try
            {
                float oldM = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : -1f;
                float oldG = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness")
                           : (mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : -1f);

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                // ⭐ 高光/反射：Standard shader 里**真正的开关是关键字** ✗（设 float 没用 ✗ —— 实测“边缘怪光”就是它们 ✓）
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");

                string sh = mat.shader != null ? mat.shader.name : "?";
                UnityEngine.Debug.Log($"[MakeMatte] {tag}｜金属 {oldM:0.##}→0｜光滑 {oldG:0.##}→0.25｜shader={sh}");
            }
            catch { /* 数值改不上不该炸 ✓ */ }
        }
        /// <summary>⭐⭐ 把 Tripo 那两张图接上 ✓（用户要求：“用 tripo 发来的图试试吧”✓）。
        ///
        /// <para>`normal` ✓ → `_BumpMap` ✓（glTF 与 Unity 同为 OpenGL 约定 +Y ✓ ⇒ **不用翻转** ✓）</para>
        /// <para>`metalGloss` ✓ → `_MetallicGlossMap` ✓ + `_OcclusionMap`（后者只读 **G** ✓ 所以两张图**共用一张** ✓）；
        /// 同时把 `_Metallic`/`_Glossiness` 置 **1** ✓ 让**贴图接管** ✓（否则数值会把图盖掉 ✗）</para>
        /// <para>⚠️ 图必须是**线性**读出来的 ✗（加载器已处理 ✓）。</para></summary>
        public static void ApplyTripoMaps(Material mat, Texture2D normal, Texture2D metalGloss, string tag = null)
        {
            if (mat == null) return;
            try
            {
                string info = "";
                if (normal != null)
                {
                    if (mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); info += "法线✓"; }
                    else if (mat.HasProperty("_NormalMap")) { mat.SetTexture("_NormalMap", normal); mat.EnableKeyword("_NORMALMAP"); info += "法线✓(URP)"; }
                    else info += "法线✗（shader 没这个槽）";
                }
                else info += "法线（GLB 里没有）";

                if (metalGloss != null)
                {
                    if (mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", metalGloss); mat.EnableKeyword("_METALLICGLOSSMAP"); info += "｜ORM✓"; }
                    else info += "｜ORM✗（shader 没这个槽）";
                    if (mat.HasProperty("_OcclusionMap")) mat.SetTexture("_OcclusionMap", metalGloss);   // 它**只读 G** ✓ 同一张图 ✓
                    // ⚠️ 金属度/光滑度**不在这里置 1** ✗ —— 交给紧接着的 `MakeMatte` ✓（金属 0 ✓ 光滑 0.25 ✓）
                    //   ⇒ 两者**相乘** ✓：若该 shader 读这张图 ⇒ 0 × 0.25 × A(≈0.03) ⇒ **更虺光** ✓；
                    //     若该 shader 不读 ✗ ⇒ 0.25 ⇒ 也是虺光 ✓ —— 两种 shader 都安全 ✓
                }

                string sh = mat.shader != null ? mat.shader.name : "?";
                UnityEngine.Debug.Log($"[ApplyTripoMaps] {tag}｜{info}｜shader={sh}");
            }
            catch { /* 图接不上不该炸 ✓ */ }
        }

        /// <summary>⭐ 游戏**自己声明**的"这套外观用哪些渲染器" ✓ —— 图形根上的 `CharacterSubVisuals.renderers` ✓。
        /// 实测价值 ✓：运行时挂上去的**灯 / 特效不在这个清单里** ✗ → 用它找"本体"最准 ✓（比"取最大"稳 ✓）。
        /// 武器/物品两边都适用 ✓（物品清单通常只有 1 个 = 本体 ✓；武器清单含 枪身+配件 ✓ 需再筛 ✓）</summary>
        public static List<Renderer> DeclaredRenderers(Transform root)
        {
            var outp = new List<Renderer>();
            if (root == null) return outp;
            try
            {
                var sv = root.GetComponent<CharacterSubVisuals>();
                if (sv != null && sv.renderers != null)
                    foreach (var r in sv.renderers) if (r != null) outp.Add(r);
            }
            catch { /* 老版本/没有这个组件 → 返回空 ✓ 调用方会走回退逻辑 ✓ */ }
            return outp;
        }

        /// <summary>⭐ 枚举**所有已加载的物品对象**（含物品模板 prefab 与场上实例 ✓）。
        /// 用途：按名字/typeID 找齐"该换模型的物品" ✓（和 <see cref="WeaponIcon.ApplyToAllMatching"/> 同一个枚举方式 ✓）</summary>
        public static IEnumerable<ItemStatsSystem.Item> AllItems()
            => Resources.FindObjectsOfTypeAll<ItemStatsSystem.Item>().Where(i => i != null);

        /// <summary>名字匹配（大小写不敏感的子串 ✓）：`target` 就是拿它去匹配物品的**对象名**。
        /// 例：`"Backpack"` 会命中 `Item_Backpack_Lv_3` ✓</summary>
        public static bool NameMatches(ItemStatsSystem.Item item, string target)
            => !string.IsNullOrEmpty(target) && item != null && item.name != null
               && item.name.IndexOf(target, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>目标尺寸（米）✓：把模型**等比**缩放到"游戏里那一档" ✓
    /// <para>优先级：config 的 `size`（玩家显式 ✓）&gt; 游戏 tag 推断 ✓ &gt; null（**不缩放** ✓）。</para>
    /// <para>表里的数字全部来自**离线实测**（游戏自己那些模型的最长边 ✓）：
    /// 枪按 `GunType_*` 分档 ✓；装备按 `Helmat`/`Backpack`/`FaceMask`/`Headset`/`Equipment` ✓；
    /// 普通物品（糖果/医疗 ✓）与近战/弓弩 → **不缩放** ✓。</para>
    /// <para>⚠️ 缩放是在**加载期**对 mesh 顶点做一次 ✓ → 世界/手持/实例三条路自动一致 ✓。</para></summary>
    public static class ModelSize
    {
        static readonly string[] GunTags = { "GunType_PST", "GunType_SMG", "GunType_AR", "GunType_BR", "GunType_SHT", "GunType_SNP", "GunType_MAG" };
        static readonly float[] GunSizes = { 0.44f, 0.85f, 1.00f, 1.25f, 1.05f, 1.35f, 1.45f };
        // ⭐ 装备/物品 **一律不缩放** ✓（用户定稿：实测头盔按 0.65 缩完太小 ✗ → 除武器外 scale = 1 ✓）

        /// <summary>该物品的**目标最长边**（米 ✓）；null = 不缩放（scale 保持 1 ✓）。
        /// <para>⭐ 只有**武器**有档位 ✓（按游戏自己的 `GunType_*` tag ✓）；装备/物品一律 null ✓。</para>
        /// <paramref name="explicitSize"/> 是 config 的 `size`（≤0 = 没填 ✓）—— 任何物品都能用它手动指定 ✓</summary>
        public static float? For(int typeID, float? explicitSize)
        {
            if (explicitSize.HasValue && explicitSize.Value > 0f) return explicitSize;   // 玩家填了 → 听玩家的 ✓
            try
            {
                var item = GameApi.AllItems().FirstOrDefault(i => i != null && i.TypeID == typeID);
                if (item == null) return null;
                var tags = item.Tags;
                if (tags == null) return null;
                if (tags.Contains("Weapon"))
                {
                    for (int i = 0; i < GunTags.Length; i++) if (tags.Contains(GunTags[i])) return GunSizes[i];
                    // 有 `Gun` 但没细分枪种（例如弓弩 ✓）→ 1.00 m 兜底 ✓（= 与现状一致 ✓ 不缩放 ✓）
                    if (tags.Contains("Gun")) return 1.00f;
                    return null;                       // 近战等 ✓ 先不管 ✓
                }
                // ⭐ 装备（头盔/背包/耳机/面具 ✓）与普通物品（糖果/医疗 ✓）→ **不缩放** ✓（定稿 ✓）
            }
            catch { }
            return null;
        }

        /// <summary>一步到位（mod 侧用 ✓）：`For(typeID, size)` → `Factor(mesh, ·)` ✓（`size` ≤0 = 没填 ✓）</summary>
        public static float FactorFor(int typeID, float size, Mesh mesh)
            => Factor(mesh, For(typeID, size > 0f ? (float?)size : null));

        /// <summary>模型当前的最长边（米 ✓）——用来算"该乘多少 scale" ✓</summary>
        public static float Longest(Mesh mesh)
        {
            if (mesh == null) return 0f;
            var s = mesh.bounds.size;
            return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        /// <summary>该乘的 scale 系数 ✓（= target / 当前最长边 ✓；没目标或离谱 → 1 ✓）—— **不碰 mesh 顶点** ✓
        public static float Factor(Mesh mesh, float? target)
        {
            float longest = Longest(mesh);
            return (longest < 1e-4f || !target.HasValue) ? 1f : target.Value / longest;
        }
    }
}
