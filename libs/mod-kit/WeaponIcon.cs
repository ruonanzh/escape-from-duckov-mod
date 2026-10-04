// WeaponIcon.cs —— 给被替换的武器换上"我们模型"的图标（背包/商店卡片上那个 Sprite）。
//
// 为什么需要：`Item.icon` 是**一个 Sprite**（卡片用它），而 `itemGraphic` 才是手里的 3D 模型 ✓
// 我们只换模型时，卡片上仍是原版图 ✗ —— 这里把 Tripo 渲染图（白底 ✓）抠成透明并设成图标 ✓。
//
// 做法不是我们发明的：优香MPX（工坊 3808796395）的 DLL 里就是这条路 ——
// `Sprite` + `LoadImage` + `set_Icon`（+ `useSpriteForPickup`）✓ 我们从 2026-10-04 起与之保持一致 ✓
//
// 图从哪来：mod 目录里的 `icon.png`（`generate_model` 会存 ✓ —— 就是 Tripo 的渲染图，
// **它本身就是 512² RGBA 透明背景 PNG** ✓ 格式与游戏图标一致 ✓。
// 下面的"白底→透明"只对**白底图**（例如用户自己给的图标）兜底 ✓ 对已经是透明的图不会动它 ✓。

using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    public static class WeaponIcon
    {
        /// <summary>把 pngPath 设为 item 的图标（白底→透明 ✓）。返回说明文本（失败也不抛，图标不是必须的）。</summary>
        public static string Apply(ItemStatsSystem.Item item, string pngPath)
        {
            if (item == null) return "没有物品，跳过";
            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath)) return "没有图标文件，跳过";

            Texture2D tex;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(pngPath))) return "图标读不出来（不是 PNG/JPG？）";
            }
            catch (Exception e) { return $"图标读取失败：{e.Message}"; }

            int n = tex.width * tex.height;
            var px = tex.GetPixels32();
            // Tripo 的图已是透明底 → 这段基本不触发；白底图才会被抠 ✓
            int keyed = 0;
            for (int i = 0; i < n; i++)
            {
                // Tripo 的渲染图是纯白底 → 近白即透明 ✓；用一点羽化让边缘不生硬 ✓
                int max = Math.Max(px[i].r, Math.Max(px[i].g, px[i].b));
                int min = Math.Min(px[i].r, Math.Min(px[i].g, px[i].b));
                bool nearWhite = min >= 235 && (max - min) <= 12;
                if (nearWhite) { px[i].a = 0; keyed++; }
                else if (min >= 200 && (max - min) <= 20) px[i].a = (byte)(255 * (235 - min) / 35);   // 半透过渡
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = "WeaponIcon";

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "WeaponIconSprite";

            // 实例 + **同 typeID 的模板**都设（卡片读哪个都能覆盖 ✓ 不用赌它读哪个 ✓）
            int set = SetIcon(item, sprite);
            int tpl = 0;
            try
            {
                var typeId = item.TypeID;
                foreach (var other in Resources.FindObjectsOfTypeAll<ItemStatsSystem.Item>())
                {
                    if (other == null || ReferenceEquals(other, item)) continue;
                    if (other.TypeID != typeId) continue;
                    if (SetIcon(other, sprite) > 0) tpl++;
                }
            }
            catch (Exception e) { return $"图标已设到实例（{tex.width}x{tex.height}，白底像素 {keyed}），模板扫描失败：{e.Message}"; }

            return $"图标已设置：{tex.width}x{tex.height}（白底像素 {keyed}）；实例写入 {(set > 0 ? "成功" : "失败")}，同 typeID 模板 {tpl} 个";
        }

        /// <summary>`Item.icon` 既是字段也是只读属性 → 两条路都试（反射，避免版本差异）</summary>
        static int SetIcon(ItemStatsSystem.Item item, Sprite sprite)
        {
            int ok = 0;
            var t = item.GetType();
            var f = t.GetField("icon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) { try { f.SetValue(item, sprite); ok++; } catch { } }
            var p = t.GetProperty("Icon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite) { try { p.SetValue(item, sprite); ok++; } catch { } }
            return ok;
        }
    }
}
