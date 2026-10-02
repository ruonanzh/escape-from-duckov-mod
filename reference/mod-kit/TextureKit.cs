// TextureKit —— 按模型规范生成贴图位图（RGBA32）与 UV 模板。
//
// 约束同 MeshKit：**不依赖 UnityEngine**，只用 System —— 同一份源码在离线工具里生成 PNG，
// 在运行时由薄适配器塞进 UnityEngine.Texture2D（SetPixels32）。
//
// 画法：每个零件用它展开矩形里对应的那块（fills 的底色）；可选画出面分割线（看清 box-UV 布局）。
// UV 模板：给"手绘 / 外部图"用 —— 每个面的矩形描边 + 零件分块灰度，照着涂。

using System;
using System.Collections.Generic;

namespace ModelKit
{
    public sealed class Bitmap
    {
        public int W, H;
        public byte[] Rgba;     // RGBA32；行 0 = 图像顶部
        public Bitmap(int w, int h) { W = w; H = h; Rgba = new byte[w * h * 4]; }

        public void Fill(int x0, int y0, int w, int h, byte r, byte g, byte b, byte a = 255)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(H, y0 + h); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(W, x0 + w); x++)
                {
                    int i = (y * W + x) * 4;
                    Rgba[i] = r; Rgba[i + 1] = g; Rgba[i + 2] = b; Rgba[i + 3] = a;
                }
        }

        public void Border(int x0, int y0, int w, int h, byte r, byte g, byte b)
        {
            Fill(x0, y0, w, 1, r, g, b); Fill(x0, y0 + h - 1, w, 1, r, g, b);
            Fill(x0, y0, 1, h, r, g, b); Fill(x0 + w - 1, y0, 1, h, r, g, b);
        }
    }

    public static class TextureKit
    {
        public static (byte r, byte g, byte b) ParseColor(string hex, (byte, byte, byte) fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            var s = hex.TrimStart('#');
            if (s.Length != 6) return fallback;
            try
            {
                return (Convert.ToByte(s.Substring(0, 2), 16),
                        Convert.ToByte(s.Substring(2, 2), 16),
                        Convert.ToByte(s.Substring(4, 2), 16));
            }
            catch { return fallback; }
        }

        /// <summary>按 fills 给每个零件的展开矩形填色（可选画出面分割线）。</summary>
        public static Bitmap Paint(ModelSpec model, MeshData mesh, bool faceEdges = false)
        {
            var bmp = new Bitmap(mesh.AtlasSize, mesh.AtlasSize);
            var defaultCol = ParseColor(model.Texture.DefaultFill, (0x6A, 0x70, 0x76));

            foreach (var r in mesh.Atlas)
            {
                model.Texture.Fills.TryGetValue(r.Role ?? "", out var hex);
                var c = ParseColor(hex, defaultCol);
                bmp.Fill(r.X, r.Y, r.W, r.H, c.r, c.g, c.b);

                if (!faceEdges) continue;
                byte eR = (byte)Math.Max(0, c.r - 40), eG = (byte)Math.Max(0, c.g - 40), eB = (byte)Math.Max(0, c.b - 40);
                foreach (var f in new[] { "top", "bottom", "right", "front", "left", "back" })
                {
                    var fr = MeshKit.FaceRect(r, f);
                    bmp.Border(fr.X, fr.Y, fr.W, fr.H, eR, eG, eB);
                }
            }
            return bmp;
        }

        /// <summary>给 YSM 模型生成一张"皮肤"位图：每个 cube 的展开矩形按骨骼名分配颜色，
        /// 并画出 6 个面的分隔线 —— UV 有错时一眼就能看出来（颜色会串到别的部位）。</summary>
        public static Bitmap PaintYsm(YsmModel model, int size = 0)
        {
            int w = size > 0 ? size : model.TextureWidth;
            int h = size > 0 ? size : model.TextureHeight;
            var bmp = new Bitmap(w, h);
            bmp.Fill(0, 0, w, h, 34, 36, 40);

            foreach (var bone in model.Bones)
            {
                var c = ColorFor(bone.Name);
                foreach (var cube in bone.Cubes)
                {
                    int pw = Math.Max(1, (int)Math.Round(cube.Size[0]));
                    int ph = Math.Max(1, (int)Math.Round(cube.Size[1]));
                    int pd = Math.Max(1, (int)Math.Round(cube.Size[2]));
                    var rect = new AtlasRect { Role = bone.Name, X = cube.Uv[0], Y = cube.Uv[1],
                                               W = 2 * pd + 2 * pw, H = pd + ph, PxW = pw, PxH = ph, PxD = pd };
                    bmp.Fill(rect.X, rect.Y, rect.W, rect.H, c.r, c.g, c.b);
                    byte eR = (byte)Math.Max(0, c.r - 45), eG = (byte)Math.Max(0, c.g - 45), eB = (byte)Math.Max(0, c.b - 45);
                    foreach (var f in new[] { "top", "bottom", "right", "front", "left", "back" })
                    {
                        var fr = MeshKit.FaceRect(rect, f);
                        bmp.Border(fr.X, fr.Y, fr.W, fr.H, eR, eG, eB);
                    }
                }
            }
            return bmp;
        }

        /// <summary>名字 → 颜色（稳定哈希，保证同一骨骼每次同色）。</summary>
        public static (byte r, byte g, byte b) ColorFor(string name)
        {
            int hash = 17;
            foreach (var ch in name ?? "") hash = hash * 31 + ch;
            hash = Math.Abs(hash);
            // 用 HSV：色相散开，明度/饱和固定，避免太暗看不清
            float hue = (hash % 360) / 360f;
            float sat = 0.45f + (hash / 360 % 30) / 100f;
            float val = 0.75f;
            float c = val * sat, x = c * (1 - Math.Abs((hue * 6) % 2 - 1)), m = val - c;
            float r, g, b;
            if (hue < 1f / 6) { r = c; g = x; b = 0; }
            else if (hue < 2f / 6) { r = x; g = c; b = 0; }
            else if (hue < 3f / 6) { r = 0; g = c; b = x; }
            else if (hue < 4f / 6) { r = 0; g = x; b = c; }
            else if (hue < 5f / 6) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return ((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        /// <summary>UV 模板：灰底 + 每个面矩形描边 + 零件按序不同灰度（给手绘用）。</summary>
        public static Bitmap Template(MeshData mesh)
        {
            var bmp = new Bitmap(mesh.AtlasSize, mesh.AtlasSize);
            bmp.Fill(0, 0, mesh.AtlasSize, mesh.AtlasSize, 40, 42, 46);

            for (int i = 0; i < mesh.Atlas.Count; i++)
            {
                var r = mesh.Atlas[i];
                byte shade = (byte)(70 + (i * 23) % 120);
                bmp.Fill(r.X, r.Y, r.W, r.H, shade, shade, shade);
                foreach (var f in new[] { "top", "bottom", "right", "front", "left", "back" })
                {
                    var fr = MeshKit.FaceRect(r, f);
                    bmp.Border(fr.X, fr.Y, fr.W, fr.H, 240, 240, 240);
                }
                // 前面（front）右下角点一个小标记，方便认方向
                var front = MeshKit.FaceRect(r, "front");
                bmp.Fill(front.X + front.W - 3, front.Y + front.H - 3, 2, 2, 255, 120, 60);
            }
            return bmp;
        }
    }

    /// <summary>最小 PNG 编码器（RGBA8，无滤波）：zlib(Deflate) + CRC32 —— 只依赖 System，Unity 里也能用。</summary>
    public static class PngWriter
    {
        public static void Save(Bitmap bmp, string path)
        {
            var bytes = Encode(bmp);
            System.IO.File.WriteAllBytes(path, bytes);
        }

        public static byte[] Encode(Bitmap bmp)
        {
            // 原始扫描线：每行前加一个 filter 字节 0
            var raw = new byte[(bmp.W * 4 + 1) * bmp.H];
            int p = 0;
            for (int y = 0; y < bmp.H; y++)
            {
                raw[p++] = 0;
                Buffer.BlockCopy(bmp.Rgba, y * bmp.W * 4, raw, p, bmp.W * 4);
                p += bmp.W * 4;
            }

            var idat = Zlib(raw);

            using var ms = new System.IO.MemoryStream();
            ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

            var ihdr = new byte[13];
            WriteBE(ihdr, 0, bmp.W);
            WriteBE(ihdr, 4, bmp.H);
            ihdr[8] = 8;    // bit depth
            ihdr[9] = 6;    // color type RGBA
            WriteChunk(ms, "IHDR", ihdr);
            WriteChunk(ms, "IDAT", idat);
            WriteChunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        static void WriteBE(byte[] b, int off, int v)
        {
            b[off] = (byte)(v >> 24); b[off + 1] = (byte)(v >> 16); b[off + 2] = (byte)(v >> 8); b[off + 3] = (byte)v;
        }

        static void WriteChunk(System.IO.Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length);
            s.Write(len, 0, 4);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4);
            s.Write(data, 0, data.Length);
            uint crc = 0xFFFFFFFF;
            crc = Crc32Update(crc, t);
            crc = Crc32Update(crc, data);
            crc ^= 0xFFFFFFFF;
            var c = new byte[4]; WriteBE(c, 0, (int)crc);
            s.Write(c, 0, 4);
        }

        static uint Crc32Update(uint crc, byte[] data)
        {
            foreach (var b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        static readonly uint[] Table = MakeTable();
        static uint[] MakeTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            return t;
        }

        /// <summary>zlib 容器：2 字节头 + Deflate 数据 + Adler32。</summary>
        static byte[] Zlib(byte[] data)
        {
            using var ms = new System.IO.MemoryStream();
            ms.WriteByte(0x78); ms.WriteByte(0x9C);      // CM=8, CINFO=7, 默认压缩级别
            using (var deflate = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                deflate.Write(data, 0, data.Length);
            uint a = 1, b = 0;
            foreach (var x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
            uint adler = (b << 16) | a;
            ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16));
            ms.WriteByte((byte)(adler >> 8)); ms.WriteByte((byte)adler);
            return ms.ToArray();
        }
    }
}
