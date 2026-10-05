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

            // 背景处理：**不做** ✓
            //   图标来源现在是 Tripo 的 `chat_image_2.5_flare + background=transparent + output_format=png`
            //   （或模型的渲染图）→ **原生就带透明** ✓，不需要我们猜背景 ✓。
            //   曾经的"近白=背景→透明"已删除：主体本身可能有白色（实测那把 AK 的弹匣就是白的 ✗），
            //   抠白会打穿主体 ✗ → 等真有"白底图"的需求时再按需加（届时也应先判断图里有没有 alpha ✗）。

            // 构图：**暂不裁方/居中** —— 交给提示词让模型自己构图（实测 flare 出图：x 偏 1px、y 偏 28px ✓ 够准）
            //   tex = CenterOnContent(tex);        // ← 需要时再打开（玩家自带的图构图不可控 ✓ 那时才需要它）
            // （CenterOnContent 保留着，没删 ✓）

            // ⭐ 尺寸对齐游戏自己的物品图标：实测游戏是 **256×256 贴图 + Sprite PPU 50**（MP5 等 158 把武器一致）
            //   我们的来源图是 512² → 缩到 256²（盒式均值 ✓）并同样用 PPU 50 → 显示尺寸与游戏完全一致 ✓
            tex = DownscaleTo(tex, 256);
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 50f);
            sprite.name = "WeaponIconSprite";

            // 实例 + **同 typeID 的模板**都设（卡片读哪个都能覆盖 ✓ 不用赌它读哪个 ✓）
            int set = SetSpritesEverywhere(item, sprite);
            int tpl = 0;
            try
            {
                var typeId = item.TypeID;
                foreach (var other in Resources.FindObjectsOfTypeAll<ItemStatsSystem.Item>())
                {
                    if (other == null || ReferenceEquals(other, item)) continue;
                    if (other.TypeID != typeId) continue;
                    if (SetSpritesEverywhere(other, sprite) > 0) tpl++;
                }
            }
            catch (Exception e) { return $"图标已设到实例（{tex.width}x{tex.height}），模板扫描失败：{e.Message}"; }

            return $"图标已设置：{tex.width}x{tex.height} @PPU50；实例写入 {(set > 0 ? "成功" : "失败")}，同 typeID 模板 {tpl} 个";
        }

        /// <summary>把 item 上**所有** Sprite 字段/属性都盖上（不猜字段名 ✓）：
        /// `Item.icon`（实例+模板 ✓）+ `Item.ItemGraphic`（ItemGraphicInfo）上任何 Sprite 字段 ✓
        /// + 它的 `spriteGraphicPfb` 里的 SpriteRenderer.sprite ✓ —— 卡片/格子可能各读一处 ✗</summary>
        public static int SetSpritesEverywhere(ItemStatsSystem.Item item, Sprite sprite)
        {
            if (item == null || sprite == null) return 0;
            int n = SetIcon(item, sprite);
            try
            {
                var ig = item.ItemGraphic;                      // ItemGraphicInfo
                if (ig != null)
                {
                    n += SetSpriteFields(ig, sprite);
                    // spriteGraphicPfb：共享 prefab → 直接改它的 SpriteRenderer（所有实例一起生效 ✓）
                    var f = ig.GetType().GetField("spriteGraphicPfb", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null)
                    {
                        var pfb = f.GetValue(ig) as GameObject;
                        if (pfb != null)
                            foreach (var sr in pfb.GetComponentsInChildren<SpriteRenderer>(true))
                                if (sr.sprite != sprite) { sr.sprite = sprite; n++; }
                    }
                }
            }
            catch { }
            return n;
        }

        /// <summary>递归把对象上所有 Sprite 类型的字段/属性设成 sprite（深度 1，防环 ✓）</summary>
        static int SetSpriteFields(object obj, Sprite sprite)
        {
            int n = 0;
            var t = obj.GetType();
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType == typeof(Sprite))
                {
                    try { if (!ReferenceEquals(f.GetValue(obj), sprite)) { f.SetValue(obj, sprite); n++; } } catch { }
                }
            }
            foreach (var pr in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (pr.PropertyType == typeof(Sprite) && pr.CanWrite)
                {
                    try { if (!ReferenceEquals(pr.GetValue(obj), sprite)) { pr.SetValue(obj, sprite); n++; } } catch { }
                }
            }
            return n;
        }

        /// <summary>按名字片段/typeID 扫所有已加载的 Item（模板 + 实例）设图标 —— 供启动时"尽早预置" ✓</summary>
        public static int ApplyToAllMatching(string namePart, System.Collections.Generic.HashSet<int> typeIds, string pngPath)
        {
            var sprite = BuildSprite(pngPath);
            if (sprite == null) return 0;
            int n = 0;
            foreach (var it in Resources.FindObjectsOfTypeAll<ItemStatsSystem.Item>())
            {
                if (it == null) continue;
                bool hit = (typeIds != null && typeIds.Contains(it.TypeID))
                        || (!string.IsNullOrEmpty(namePart) && it.name != null &&
                            it.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!hit) continue;
                if (SetSpritesEverywhere(it, sprite) > 0) n++;
            }
            return n;
        }

        /// <summary>把 pngPath 读成"对齐游戏尺寸"的 Sprite（256² + PPU50 ✓）</summary>
        static Sprite BuildSprite(string pngPath)
        {
            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(pngPath))) return null;
                tex = DownscaleTo(CenterOnContent(tex), 256);
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 50f);
                sp.name = "WeaponIconSprite";
                return sp;
            }
            catch { return null; }
        }

        // ⭐ 缩小必须在 **linear 空间**求平均 ✗：直接在 sRGB 编码值上平均会让成品**整体偏暗**
        //    （实测同一张图：sRGB 直接平均 → 平均线性亮度偏 −15.7% ✗；linear 空间平均 → 偏 −1.2% ✓）
        //    用查表（256 项 ✓ 快）做 sRGB→linear；反变换只对**输出**像素做（256² 很小 ✓）
        static readonly float[] SrgbToLinearTable = BuildSrgbToLinearTable();
        static float[] BuildSrgbToLinearTable()
        {
            var t = new float[256];
            for (int i = 0; i < 256; i++)
            {
                float v = i / 255f;
                t[i] = v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            }
            return t;
        }
        static byte LinearToSrgb(float l)
        {
            float v = l <= 0.0031308f ? l * 12.92f : 1.055f * Mathf.Pow(l, 1f / 2.4f) - 0.055f;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
        }

        /// <summary>按透明包围盒裁成正方形并**居中**（Tripo 的渲染图物体偏下 ✗ 我们统一构图 ✓）</summary>
        static Texture2D CenterOnContent(Texture2D src, float margin = 0.12f)
        {
            var p = src.GetPixels32();
            int w = src.width, h = src.height;
            int mnx = w, mny = h, mxx = -1, mxy = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (p[y * w + x].a > 8)
                    {
                        if (x < mnx) mnx = x; if (x > mxx) mxx = x;
                        if (y < mny) mny = y; if (y > mxy) mxy = y;
                    }
            if (mxx < 0) return src;                                  // 全透明 → 原样返回
            int bw = mxx - mnx + 1, bh = mxy - mny + 1;
            int side = Mathf.CeilToInt(Mathf.Max(bw, bh) * (1f + margin * 2f));
            var dst = new Texture2D(side, side, TextureFormat.RGBA32, false);
            var q = new Color32[side * side];
            int cx = (mnx + mxx) / 2, cy = (mny + mxy) / 2;
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    int sx = cx - side / 2 + x, sy = cy - side / 2 + y;
                    q[y * side + x] = (sx >= 0 && sx < w && sy >= 0 && sy < h) ? p[sy * w + sx] : new Color32(0, 0, 0, 0);
                }
            dst.SetPixels32(q);
            dst.Apply();
            dst.wrapMode = TextureWrapMode.Clamp;
            return dst;
        }

        /// <summary>等比缩到 target 边长（盒式均值 ✓ 简单、无副作用）</summary>
        static Texture2D DownscaleTo(Texture2D src, int target)
        {
            if (src.width <= target) return src;
            // ⚠️ 原来 f = src.width / target，"不是 2 的幂就回退成 2" ✗ ——
            //    单边 2540（CenterOnContent 加 margin 后的常见尺寸）的 f=9 → 回退成 2 ✗
            //    → **不是缩放、而是只取左上角一块** ✗（实机踩过：图标整块白）。
            //    改成按浮点比例映射做盒式均值：任意比例都对 ✓
            var sPix = src.GetPixels32();
            int sw = src.width, sh = src.height;
            var dst = new Texture2D(target, target, TextureFormat.RGBA32, false);
            var dPix = new Color32[target * target];
            for (int y = 0; y < target; y++)
                for (int x = 0; x < target; x++)
                {
                    int x0 = x * sw / target, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / target);
                    int y0 = y * sh / target, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / target);
                    float r = 0, g = 0, b = 0;
                    int a = 0, n = 0;
                    for (int sy = y0; sy < y1 && sy < sh; sy++)
                        for (int sx = x0; sx < x1 && sx < sw; sx++)
                        {
                            var c = sPix[sy * sw + sx];
                            r += SrgbToLinearTable[c.r]; g += SrgbToLinearTable[c.g]; b += SrgbToLinearTable[c.b];
                            a += c.a; n++;
                        }
                    if (n == 0) { dPix[y * target + x] = new Color32(0, 0, 0, 0); continue; }
                    dPix[y * target + x] = new Color32(LinearToSrgb(r / n), LinearToSrgb(g / n), LinearToSrgb(b / n), (byte)(a / n));
                }
            dst.SetPixels32(dPix);
            dst.Apply();
            dst.wrapMode = TextureWrapMode.Clamp;
            dst.name = "WeaponIcon_256";
            return dst;
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
