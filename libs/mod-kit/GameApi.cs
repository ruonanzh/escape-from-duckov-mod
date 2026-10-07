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

        static int _displayLayer = int.MinValue;

        /// <summary>⭐ **世界显示层** ✓ —— 游戏在世界里显示物品/装备时用的那个层 ✓。
        ///
        /// 为什么需要它 ✗：我们**自己造**图形时（物品没有 `ItemGraphic` 的那种 ✓），没有"游戏零件"的层可抄 ✗。
        /// 而**物品预制体自己的层**不能用 ✗ —— 实测（用户报的 ✓）：拿它当层 → 模型**发白** ✗ +
        /// **渲染在最前面** ✗（那是 **UI 层**的行为 ✓ 因为 Item 的 GameObject 平时是给背包格子/UI 用的 ✓）。
        ///
        /// 取法（证据优先 ✓）：① 场上一件**游戏自己的**图形实例（`ItemGraphicInfo` ✓ 不是我们造的 `ModelKit_*` ✓）
        /// → 从它的网格渲染器读层 ✓；② 退而用 `VisibleLayer`（Character ✓）；③ 都不行 → -1（调用方保持原样 ✓）</summary>
        public static int DisplayLayer()
        {
            if (_displayLayer != int.MinValue) return _displayLayer;
            _displayLayer = -1;
            try
            {
                foreach (var g in UnityEngine.Object.FindObjectsByType<ItemGraphicInfo>(FindObjectsSortMode.None))
                {
                    if (g == null || g.gameObject == null) continue;
                    if (g.gameObject.name.StartsWith("ModelKit_")) continue;      // 我们造的跳过 ✗
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                        if ((r is MeshRenderer || r is SkinnedMeshRenderer) && r.gameObject != null)
                        { _displayLayer = r.gameObject.layer; break; }
                    if (_displayLayer >= 0) break;
                }
            }
            catch { }
            if (_displayLayer < 0)
            {
                int v = VisibleLayer;                                             // "Character" ✓
                if (v >= 0) _displayLayer = v;
            }
            return _displayLayer;
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
}
