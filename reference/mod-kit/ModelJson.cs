// ModelJson.cs —— 把 JSON 映射成两种模型：零件清单（物品/武器/建筑）与 YSM（角色）。
//
// 与 Json.cs、MeshKit.cs、TextureKit.cs 一起构成"共享层"（不依赖 UnityEngine），
// 离线工具与运行时 mod 共用同一份 —— 解析/几何/贴图只有一个真相。

using System;
using System.Collections.Generic;

namespace ModelKit
{
    // ── YSM（角色）──────────────────────────────────────────────────────────────

    public sealed class YsmCube
    {
        public float[] Origin = { 0, 0, 0 };
        public float[] Size = { 1, 1, 1 };
        public int[] Uv = { 0, 0 };
    }

    public sealed class YsmBone
    {
        public string Name = "";
        public string Parent;                 // null = 根
        public float[] Pivot = { 0, 0, 0 };
        public float[] Rotation = { 0, 0, 0 };
        public List<YsmCube> Cubes = new List<YsmCube>();
        public Dictionary<string, float[]> Locators;   // 挂点（模型内名字 → 偏移）
    }

    public sealed class YsmModel
    {
        /// <summary>按骨骼名给的底色（可选，来自模型文件 `texture.fills`）；缺省时按骨骼名哈希取色。</summary>
        public Dictionary<string, string> Fills = new Dictionary<string, string>();

        public string Identifier = "";
        public int TextureWidth = 64, TextureHeight = 64;
        public List<YsmBone> Bones = new List<YsmBone>();
        public JsonValue Raw;                  // 原始 JSON（保留未识别字段，便于诊断）
    }

    public static class ModelJson
    {
        static float[] F3(JsonValue v, float[] fallback)
        {
            if (v == null || !v.IsArray || v.Count < 3) return fallback;
            return new[] { v[0].AsFloat(), v[1].AsFloat(), v[2].AsFloat() };
        }

        static float[][] Pairs(JsonValue v)
        {
            if (v == null || !v.IsArray) return null;
            var list = new List<float[]>();
            for (int i = 0; i < v.Count; i++)
            {
                var row = v[i];
                if (row == null || !row.IsArray) continue;
                var r = new float[row.Count];
                for (int k = 0; k < row.Count; k++) r[k] = row[k].AsFloat();
                list.Add(r);
            }
            return list.ToArray();
        }

        // ── 零件清单 ────────────────────────────────────────────────────────────

        public static ModelSpec ToModel(JsonValue root, List<string> errors)
        {
            var spec = new ModelSpec
            {
                Name = root["name"]?.AsString("") ?? "",
                Category = root["category"]?.AsString("") ?? "",
                Summary = root["summary"]?.AsString("") ?? "",
                PivotOffset = F3(root["pivotOffset"], new float[] { 0, 0, 0 })
            };

            // 配件槽位挂点（可选）：{ "Scope": [x,y,z], "Muzzle": [x,y,z,rx,ry,rz], ... }
            var slots = root["slots"];
            if (slots != null && slots.IsObject)
                foreach (var kv in slots.Object)
                {
                    var arr = kv.Value;
                    if (arr == null || arr.Kind == JsonKind.Null) { spec.Slots[kv.Key] = null; continue; }   // null = 这把枪没有该挂点
                    if (!arr.IsArray || arr.Count < 3) continue;
                    var vals = new float[arr.Count];
                    for (int i = 0; i < arr.Count; i++) vals[i] = arr[i].AsFloat();
                    spec.Slots[kv.Key] = vals;
                }

            var parts = root["parts"];
            if (parts == null || !parts.IsArray)
            {
                errors.Add("缺 parts[]（或不是数组）");
                return null;
            }
            for (int i = 0; i < parts.Count; i++) spec.Parts.Add(ToPart(parts[i], errors));

            var tex = root["texture"];
            if (tex != null && tex.IsObject)
            {
                var size = tex["size"];
                if (size != null && size.IsArray && size.Count >= 2) spec.Texture.Size = size[0].AsInt(spec.Texture.Size);
                var ppm = tex["pixels_per_meter"];
                if (ppm != null && ppm.Kind == JsonKind.Number) spec.Texture.PixelsPerMeter = ppm.AsFloat();
                var src = tex["source"];
                if (src != null)
                {
                    if (src.Kind == JsonKind.String) spec.Texture.Source = src.Str;
                    else if (src.IsObject && src["file"] != null) { spec.Texture.Source = "file"; spec.Texture.File = src["file"].AsString(); }
                }
                var fills = tex["fills"];
                if (fills != null && fills.IsObject)
                    foreach (var kv in fills.Object) spec.Texture.Fills[kv.Key] = kv.Value.AsString();
            }
            return spec;
        }

        static PartSpec ToPart(JsonValue e, List<string> errors)
        {
            if (e == null || !e.IsObject) { errors.Add("parts[] 里有一项不是对象"); return new PartSpec { Shape = "(invalid)" }; }
            var p = new PartSpec
            {
                Role = e["role"]?.AsString("") ?? "",
                Shape = e["shape"]?.AsString("") ?? "",
                Size = F3(e["size"], null),
                R = e["r"]?.AsFloat() ?? 0f,
                R1 = e["r1"]?.AsFloat() ?? 0f,
                R2 = e["r2"]?.AsFloat() ?? 0f,
                H = e["h"]?.AsFloat() ?? 0f,
                Depth = e["depth"]?.AsFloat() ?? 0f,
                Segments = e["segments"]?.AsInt(12) ?? 12,
                Rings = e["rings"]?.AsInt(0) ?? 0,
                Profile = Pairs(e["profile"]),
                Outline = Pairs(e["outline"]),
                At = F3(e["at"], new float[] { 0, 0, 0 }),
                Rot = F3(e["rot"], new float[] { 0, 0, 0 }),
                Scale = F3(e["scale"], new float[] { 1, 1, 1 }),
                Mirror = e["mirror"]?.AsString()
            };

            switch (p.Shape)
            {
                case "box":
                    if (p.Size == null || p.Size[0] <= 0 || p.Size[1] <= 0 || p.Size[2] <= 0)
                        errors.Add($"零件 '{p.Role}': box 需要 size[3] 且都 > 0");
                    break;
                case "cylinder":
                    if (p.R <= 0 || p.H <= 0) errors.Add($"零件 '{p.Role}': cylinder 需要 r > 0 且 h > 0");
                    break;
                case "cone":
                    if (p.H <= 0 || (p.R1 <= 0 && p.R2 <= 0)) errors.Add($"零件 '{p.Role}': cone 需要 h > 0 且 r1/r2 至少一个 > 0");
                    break;
                case "sphere":
                    if (p.R <= 0) errors.Add($"零件 '{p.Role}': sphere 需要 r > 0");
                    break;
                case "lathe":
                    if (p.Profile == null || p.Profile.Length < 2) errors.Add($"零件 '{p.Role}': lathe 需要 profile（≥2 个 [半径,高度]）");
                    break;
                case "extrude":
                    if (p.Outline == null || p.Outline.Length < 3) errors.Add($"零件 '{p.Role}': extrude 需要 outline（≥3 个 [x,y]）");
                    if (p.Depth <= 0) errors.Add($"零件 '{p.Role}': extrude 需要 depth > 0");
                    break;
                default:
                    errors.Add($"零件 '{p.Role}': 不支持的 shape '{p.Shape}'（box|cylinder|cone|sphere|lathe|extrude）");
                    break;
            }
            if (p.Mirror != null && p.Mirror != "x" && p.Mirror != "y" && p.Mirror != "z")
                errors.Add($"零件 '{p.Role}': mirror 只能是 x/y/z");
            return p;
        }

        // ── YSM（角色）──────────────────────────────────────────────────────────

        public static YsmModel ToYsm(JsonValue root, List<string> errors)
        {
            var model = new YsmModel { Raw = root };
            var geo = root["minecraft:geometry"];
            JsonValue g = geo != null && geo.IsArray && geo.Count > 0 ? geo[0] : null;
            if (g == null || !g.IsObject) { errors.Add("缺 minecraft:geometry[0]"); return model; }

            var desc = g["description"];
            if (desc != null && desc.IsObject)
            {
                model.Identifier = desc["identifier"]?.AsString("") ?? "";
                model.TextureWidth = desc["texture_width"]?.AsInt(64) ?? 64;
                model.TextureHeight = desc["texture_height"]?.AsInt(64) ?? 64;
            }

            var fills = root["texture"]?["fills"];      // 我们的扩展：{"Hip":"#E0913C", ...} 按骨骼名上色
            if (fills != null && fills.IsObject)
                foreach (var kv in fills.Object)
                {
                    var c = kv.Value.AsString("");
                    if (!string.IsNullOrEmpty(c)) model.Fills[kv.Key] = c;
                }

            var bones = g["bones"];
            if (bones == null || !bones.IsArray || bones.Count == 0) { errors.Add("bones 为空"); return model; }

            for (int i = 0; i < bones.Count; i++)
            {
                var b = bones[i];
                if (b == null || !b.IsObject) continue;
                var bone = new YsmBone
                {
                    Name = b["name"]?.AsString("") ?? "",
                    Parent = b["parent"]?.AsString(),
                    Pivot = F3(b["pivot"], new float[] { 0, 0, 0 }),
                    Rotation = F3(b["rotation"], new float[] { 0, 0, 0 })
                };
                var cubes = b["cubes"];
                if (cubes != null && cubes.IsArray)
                {
                    for (int c = 0; c < cubes.Count; c++)
                    {
                        var cu = cubes[c];
                        if (cu == null || !cu.IsObject) continue;
                        var cube = new YsmCube
                        {
                            Origin = F3(cu["origin"], new float[] { 0, 0, 0 }),
                            Size = F3(cu["size"], new float[] { 1, 1, 1 })
                        };
                        var uv = cu["uv"];
                        if (uv != null && uv.IsArray && uv.Count >= 2) cube.Uv = new[] { uv[0].AsInt(), uv[1].AsInt() };
                        bone.Cubes.Add(cube);
                    }
                }
                var locs = b["locators"];
                if (locs != null && locs.IsObject)
                {
                    bone.Locators = new Dictionary<string, float[]>();
                    foreach (var kv in locs.Object) bone.Locators[kv.Key] = F3(kv.Value, new float[] { 0, 0, 0 });
                }
                model.Bones.Add(bone);
            }
            return model;
        }
    }
}
