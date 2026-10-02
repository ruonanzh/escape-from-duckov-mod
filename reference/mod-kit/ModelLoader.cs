// ModelLoader.cs —— 运行时加载：把模型（零件清单 / YSM）变成挂在游戏对象上的 GameObject。
//
// 依赖 MeshKit / TextureKit（纯 C#）+ UnityAdapter（唯一碰 UnityEngine 的地方）。
// 用法见 reference/cube_person/（方块人 demo）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModelKit
{
    public static class ModelLoader
    {
        /// <summary>本 mod 的目录（模型文件就放这儿，例如 <mod>/models/*.json）。</summary>
        public static string ModDir()
        {
            try { return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
            catch { return Directory.GetCurrentDirectory(); }
        }

        public static ModelSpec LoadPartsSpec(string jsonPath)
        {
            var errors = new List<string>();
            var spec = ModelJson.ToModel(Json.Parse(File.ReadAllText(jsonPath)), errors);
            if (errors.Count > 0) throw new ModelKitException(string.Join("; ", errors));
            return spec;
        }

        public static YsmModel LoadYsm(string jsonPath)
        {
            var errors = new List<string>();
            var ysm = ModelJson.ToYsm(Json.Parse(File.ReadAllText(jsonPath)), errors);
            if (errors.Count > 0) throw new ModelKitException(string.Join("; ", errors));
            return ysm;
        }

        // ── 物品 / 武器 / 建筑：一张 mesh 挂到目标对象下 ────────────────────────

        public static GameObject BuildPartsObject(ModelSpec spec, Transform parent, Material materialSource, int layer, string name = null)
        {
            var mesh = MeshKit.Build(spec);
            var bitmap = TextureKit.Paint(spec, mesh, faceEdges: false);
            var tex = UnityAdapter.ToTexture(bitmap, (name ?? spec.Name) + "_tex");
            var mat = UnityAdapter.CloneWithTexture(materialSource, tex);
            return UnityAdapter.CreateMeshObject(name ?? spec.Name, UnityAdapter.ToMesh(mesh, name ?? spec.Name), mat, parent, layer);
        }

        // ── 角色（YSM）：每个骨骼的 cube 各建一个 box，挂在同名游戏骨骼下 ───────

        public sealed class YsmBuild
        {
            public List<GameObject> Objects = new List<GameObject>();
            public List<string> MissingBones = new List<string>();
            public List<string> AttachedBones = new List<string>();
            public Texture2D Texture;
            public int CubeCount;
        }

        /// <summary>把 YSM 模型挂到游戏角色骨架上。单位：1 像素 = 1/16 米（Bedrock 约定）。</summary>
        public static YsmBuild BuildYsmObjects(YsmModel ysm, Transform modelRoot, Material materialSource, int layer)
        {
            var result = new YsmBuild();
            var bitmap = TextureKit.PaintYsm(ysm);
            result.Texture = UnityAdapter.ToTexture(bitmap, "ysm_skin");
            var mat = UnityAdapter.CloneWithTexture(materialSource, result.Texture);

            const float px = 1f / 16f;   // 像素 → 米

            foreach (var bone in ysm.Bones)
            {
                var boneTf = FindDeep(modelRoot, bone.Name);
                if (boneTf == null) { if (bone.Cubes.Count > 0) result.MissingBones.Add(bone.Name); continue; }
                result.AttachedBones.Add(bone.Name);

                foreach (var cube in bone.Cubes)
                {
                    // cube 在模型空间：origin 是最小角；换算成"相对该骨骼 pivot 的盒子中心"
                    float sx = Math.Max(1, (float)Math.Round(cube.Size[0]));
                    float sy = Math.Max(1, (float)Math.Round(cube.Size[1]));
                    float sz = Math.Max(1, (float)Math.Round(cube.Size[2]));
                    var centerPx = new Vec3(cube.Origin[0] + sx / 2f - bone.Pivot[0],
                                            cube.Origin[1] + sy / 2f - bone.Pivot[1],
                                            cube.Origin[2] + sz / 2f - bone.Pivot[2]);

                    var part = new PartSpec
                    {
                        Role = bone.Name,
                        Shape = "box",
                        Size = new[] { sx * px, sy * px, sz * px },
                        At = new[] { centerPx.X * px, centerPx.Y * px, centerPx.Z * px },
                        Rot = new[] { bone.Rotation[0], bone.Rotation[1], bone.Rotation[2] }
                    };
                    var rect = new AtlasRect
                    {
                        Role = bone.Name,
                        X = cube.Uv[0], Y = cube.Uv[1],
                        W = 2 * (int)sz + 2 * (int)sx, H = (int)sz + (int)sy,
                        PxW = (int)sx, PxH = (int)sy, PxD = (int)sz
                    };

                    var data = MeshKit.BuildPart(part, rect, ysm.TextureWidth);
                    var go = UnityAdapter.CreateMeshObject($"{bone.Name}_cube", UnityAdapter.ToMesh(data, bone.Name), mat, boneTf, layer);
                    result.Objects.Add(go);
                    result.CubeCount++;
                }
            }
            return result;
        }

        /// <summary>按名字递归找骨骼（游戏骨骼名与 YSM 的 bones[].name 一致 —— 模板就是这么生成的）。</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
