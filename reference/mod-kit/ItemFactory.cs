// ItemFactory.cs —— 新增物品的"每次都必须做对"的四件事，写在一处。
//
// 为什么需要它（都是实机踩出来的）：
//   ① ⭐ **DontDestroyOnLoad**：我们造的对象是"运行时场景对象"（不是 AssetBundle 资产）→ 换场景（菜单→关卡）
//      时会被销毁 → 物品的 `itemGraphic` 变成"已销毁引用"（Unity 里 `== null` 判 true）→ 游戏回退成图标贴图。
//   ② 名字必须走"**键 + 本地化表**"：`DisplayNameRaw` 塞字面量会被显示成 `*字面量*`（缺键标记）。
//   ③ `useSpriteForPickup = false`：否则地面上用图标而不是 3D 图形。
//   ④ **枪要打 `IsGun` 标记**：游戏 `ItemExtensions.CreateHandheldAgent` 用它决定
//      "用 `item.ItemGraphic` 现造手持实体"还是"用通用手持 prefab"——不打标记 → 拿在手里是通用姿势。
//      （原版枪数据里没有这个键，是运行时打上的；所以克隆出来的新物品必须自己补。）
//
// 用法（数据层）：
//     var item = ItemFactory.CloneAsNewItem(源typeID, 新typeID, "显示名");
//     binder.WriteGraphicTo(item, binder.BuildGraphicClone(源typeID));   // 模型层提供图形
//     ItemAssetsCollection.AddDynamicEntry(item);                        // 注册

using UnityEngine;

namespace ModelKit
{
    public static class ItemFactory
    {
        /// <summary>从现有物品克隆出一件新物品（**数据层**）。上面四条内置，调用方不用记。</summary>
        /// <param name="sourceTypeID">克隆源（现有物品的 typeID）</param>
        /// <param name="newTypeID">新物品的 typeID（要避开游戏本体的 ID；同 ID 会覆盖原版并打警告）</param>
        /// <param name="displayName">显示名（走本地化覆盖，不会出现 `*名字*`）</param>
        /// <param name="isGun">是否枪械；不传就**按源物品的 tag 自动判断**（`Gun` / `GunType_*`）</param>
        public static ItemStatsSystem.Item CloneAsNewItem(int sourceTypeID, int newTypeID, string displayName, bool? isGun = null)
        {
            ItemStatsSystem.Item src = null;
            try { src = ItemStatsSystem.ItemAssetsCollection.GetPrefab(sourceTypeID); } catch { }
            if (src == null) return null;

            var go = Object.Instantiate(src.gameObject);
            go.name = "Item_" + displayName;
            Object.DontDestroyOnLoad(go);                 // ⭐ ① 跨场景必须常驻（否则被销毁 → itemGraphic 变 null）

            var item = go.GetComponent<ItemStatsSystem.Item>();
            if (item == null) { Object.Destroy(go); return null; }

            item.SetTypeID(newTypeID);

            // ② 名字 = 键 + 本地化表
            string key = "Item_ModelKit_" + newTypeID;
            try { SodaCraft.Localizations.LocalizationManager.SetOverrideText(key, displayName); } catch { }
            item.DisplayNameRaw = key;

            item.useSpriteForPickup = false;              // ③ 地面/手里都用 3D 图形
            item.SetBool("IsGun", isGun ?? LooksLikeGun(src), true);   // ④ 枪标记

            return item;
        }

        /// <summary>按 tag 判断是不是枪：`Gun` 标签（实测 123 件枪**全部**有它，且没有一个带 `Accessory`/`MeleeWeapon`）。
        /// ⚠️ 别用 `GunType_*` 判 —— 配件（弹匣/瞄具/枪口）也带 `GunType_*`。
        /// 只给本类内部用（决定要不要打 `IsGun`）；要看结果就读物品上的标记 `item.GetBool("IsGun", false)`。</summary>
        static bool LooksLikeGun(ItemStatsSystem.Item item)
        {
            var tags = item != null ? item.Tags : null;
            if (tags == null) return false;
            try { return tags.Contains("Gun") && !tags.Contains("Accessory"); }
            catch { return false; }
        }
    }
}
