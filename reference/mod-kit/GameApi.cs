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
    }
}
