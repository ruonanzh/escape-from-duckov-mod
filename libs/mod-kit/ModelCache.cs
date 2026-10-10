// ─────────────────────────────────────────────────────────────────────────────
//  ModelCache —— 每个"模型 mod"都要的那几件小事（原来两份 mod 各写一遍 ✗）
//    ① 本 mod 的目录（DLL 所在处）
//    ② 模型指纹（路径 + 朝向 + 文件 mtime/size）
//    ③ 缓存容器 `CachedModel`
//  ⭐ 纯搬运 ✓ 逻辑与原来**逐字等价** ✓（A1 步：行为不可能变 ✓）
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    /// <summary>一个已解析好的模型 ✓（指纹 = 路径 + 朝向 + 文件 mtime/size ✓）</summary>
    public sealed class CachedModel
    {
        public string Sig;
        public Mesh Mesh;
        public Texture2D Texture;
        public Texture2D NormalTexture;      // 法线（Tripo `normalTexture` ✓ 可能为 null ✓）
        public Texture2D MetalGlossMap;      // ⭐ 已重排成 Unity 格式：R=metal ✓ G=AO ✓ A=smoothness ✓
    }

    /// <summary>模型缓存相关的共用小工具 ✓</summary>
    public static class ModelCache
    {
        /// <summary>本 mod 的目录（DLL 所在处 ✓）</summary>
        public static string ModDir()
        {
            try { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "."; }
            catch { return "."; }
        }

        /// <summary>模型指纹 ✓ —— ⚠️ **不含 `size`** ✗：`size` 只影响挂载时的 scale ✓ 不改 mesh ✓
        /// 所以改 `size` 热重载**不必重读**几百 KB~几 MB 的 GLB ✓（实测踩过：白读一次 ✓）</summary>
        public static string Fingerprint(string path, string front)
        {
            try
            {
                var fi = new FileInfo(path);
                return path + "|" + front + "|" + fi.LastWriteTimeUtc.Ticks + ":" + fi.Length;
            }
            catch { return path + "|" + front; }
        }

        /// <summary>取模型 ✓：指纹命中就复用 ✓（此时 `loaded` 为 null ✓）；
        /// 否则读 GLB ✓（`loaded` 非空 ✓ 调用方可用它打日志 ✓）。
        /// 路径为空 / 文件不存在 → 返回 null ✓（调用方自己决定怎么报 ✗）</summary>
        public static CachedModel Get(Dictionary<string, CachedModel> cache, string path, string front, out GltfLoader.Loaded loaded)
        {
            loaded = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (cache != null)
            {
                string sig = Fingerprint(path, front);
                if (cache.TryGetValue(path, out var c) && c != null && c.Sig == sig && c.Mesh != null) return c;
            }
            loaded = GltfLoader.LoadFile(path, front);
            var n = new CachedModel { Sig = Fingerprint(path, front), Mesh = loaded.Mesh, Texture = loaded.MainTexture,
                                      NormalTexture = loaded.NormalTexture, MetalGlossMap = loaded.MetallicGlossMap };
            if (cache != null) cache[path] = n;
            return n;
        }
    }
}
