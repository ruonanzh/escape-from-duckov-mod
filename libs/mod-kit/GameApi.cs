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
                                           "_MetallicSmoothness",   // ⭐ 游戏自定义名 ✓（两顶头盔的 shader 都用它 ✓）
                                           "_SpecGlossMap", "_DetailMask", "_DetailAlbedoMap", "_ParallaxMap" };
                for (int i = 0; i < slots.Length; i++)
                    if (mat.HasProperty(slots[i])) mat.SetTexture(slots[i], null);
            }
            catch { /* 材质换不上不该炸 ✓ */ }
        }
        /// <summary>⭐ **实验开关** ✓：要不要把 Tripo 的 **ORM（金属/光滑）** 接上 ✓。
        /// <para>实测印象 ✓（用户 ✓）：“加上 ORM **增加了真实感** ✓ 但和游戏原本风格**有些冲突**” ✗</para>
        /// <para>⇒ 默认 **关** ✗ = 只换 baseColor + 法线 ✓，**金属/光滑保持游戏原值** ✓（= 风格一致 ✓）。
        /// 想再比一次就改成 `true` ✓ 重编即可 ✓（法线**始终**接 ✓ 不受这个开关影响 ✓）。</para></summary>
        public static bool ApplyOrm = false;

        /// <summary>⭐⭐ **通用策略**：把 Tripo 的两张贴图按目标 shader 的**能力**接上 ✓。
        /// <para>用户口径 ✓：“应该 generic 地从 tripo 的结果设置这两个数值” ✓ /
        /// “没有 ORM 的情况就**不变**原有材质设置” ✗。</para>
        ///
        /// <para>① `normal` ✓：有 `_BumpMap` / `_NormalMap` 就接 ✓（glTF 与 Unity 同为 OpenGL 约定 +Y ✓ **不翻转** ✓）；
        /// 没有就**什么都不做** ✗。</para>
        /// <para>② `metalGloss`（ORM ✓，加载器已重排为 Unity 版：R=metallic ✓ G=AO ✓ A=smoothness ✓）：</para>
        /// <para>   · shader **有** `_MetallicGlossMap` 槽 ⇒ 接图 ✓ + 数值置 1 ✓（让贴图接管 ✓）</para>
        /// <para>   · **没有**槽 ✗ ⇒ ⭐ **把 ORM 折算成两个常数** ✓（这就是“从 Tripo 结果通用地得出这两个值”✓）</para>
        /// <para>③ **没有 ORM** ✗ ⇒ ⭐ **一处都不碰** ✓（保持借来材质的原值 ✓ —— 没依据就不该改 ✓）</para></summary>
        public static void ApplyTripoMaps(Material mat, Texture2D normal, Texture2D metalGloss, string tag = null)
        {
            if (mat == null) return;
            try
            {
                string info = "";
                // ── ① 法线 ──
                if (normal != null)
                {
                    if (mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); info += "法线✓"; }
                    else if (mat.HasProperty("_NormalMap")) { mat.SetTexture("_NormalMap", normal); mat.EnableKeyword("_NORMALMAP"); info += "法线✓(URP)"; }
                    else info += "法线✗（shader 没这个槽）";
                }
                else info += "法线（GLB 里没有）";

                // ── ②/③ 金属 + 光滑 ──
                if (metalGloss == null)
                {
                    info += "｜ORM（GLB 里没有 ✓ **材质原值不动** ✗）";      // ⭐ 没有依据 ⇒ 一个值都不改 ✓
                }
                else if (!ApplyOrm)
                {
                    // ⭐ 实验开关关着 ✓ ⇒ **ORM 完全不用** ✗ ⇒ 金属/光滑保持游戏原值 ✓（风格一致 ✓）
                    info += "｜ORM→**本次不接**（开关关 ✗ 保持游戏原值 ✓）";
                }
                else if (mat.HasProperty("_MetallicGlossMap") || mat.HasProperty("_MetallicSmoothness"))
                {
                    // ① 有贴图槽 ⇒ **用图** ✓（细节最全 ✓）
                    //   · `_MetallicGlossMap` = Unity 通用名 ✓；**`_MetallicSmoothness`** = 游戏自定义名 ✓
                    //     （实测两个头盔的 shader 都用后者 ✗ —— 之前只认通用名 ✗ 所以误判成“没槽”✗）
                    string slot = mat.HasProperty("_MetallicGlossMap") ? "_MetallicGlossMap" : "_MetallicSmoothness";
                    mat.SetTexture(slot, metalGloss);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    if (mat.HasProperty("_OcclusionMap")) mat.SetTexture("_OcclusionMap", metalGloss);   // 它**只读 G** ✓ 同一张图 ✓
                    // ⚠️ 游戏把 `_GlossMapScale` 设成 **0** ✗（= 把光滑贴图关了 ✗）⇒ 要接图就得开回来 ✓
                    if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 1f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 1f);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 1f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1f);
                    info += $"｜ORM→{slot}✓（数值置 1 ✓）";
                }
                else
                {
                    // ② ⭐ 真没有贴图槽 ⇒ **什么都不动** ✓（用户口径 ✓：“如果失败 ✓ 不要用均值 ✗ 就保留原有就好了” ✓）
                    info += "｜ORM→**不动** ✗（shader 没有金属/光滑贴图槽 ✓ 保留原有数值 ✓）";
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
