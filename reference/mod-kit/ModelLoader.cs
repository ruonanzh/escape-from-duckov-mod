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

            // ⚠️ 关键：游戏骨骼的**绑定旋转不是单位**（Root 就转了 90°，四肢各有 30~75°）。
            // YSM 方块是"模型空间"里摆的（像 Minecraft），而方块会成为骨骼的子物体 → 会被骨骼的旋转带着转。
            // 所以必须按骨骼的**绑定旋转**把"模型空间偏移"换算进骨骼局部坐标系（否则四肢会歪/散开）。
            var bindRot = CollectBindRotations(modelRoot);

            foreach (var bone in ysm.Bones)
            {
                var boneTf = FindDeep(modelRoot, bone.Name);
                if (boneTf == null) { if (bone.Cubes.Count > 0) result.MissingBones.Add(bone.Name); continue; }
                result.AttachedBones.Add(bone.Name);

                // 该骨骼的所有方块**合并成一个 mesh**（一个骨骼一个 GameObject）——
                // 否则细体素会有上千个 GameObject（上千次绘制调用），性能撑不住。
                var merged = new MeshData
                {
                    AtlasSize = ysm.TextureWidth, PixelsPerMeter = 1f,
                    Atlas = new List<AtlasRect>(), Emitted = new List<PartSpec>(),
                };
                int cubeIndex = 0;
                foreach (var cube in bone.Cubes)
                {
                    float sx = Math.Max(0.05f, cube.Size[0]);
                    float sy = Math.Max(0.05f, cube.Size[1]);
                    float sz = Math.Max(0.05f, cube.Size[2]);
                    var centerPx = new Vec3(cube.Origin[0] + sx / 2f - bone.Pivot[0],
                                            cube.Origin[1] + sy / 2f - bone.Pivot[1],
                                            cube.Origin[2] + sz / 2f - bone.Pivot[2]);
                    // 网格建在"以原点为中心"，再按骨骼绑定旋转换算：位置 = R⁻¹·offset，朝向 = R⁻¹
                    var part = new PartSpec
                    {
                        Role = bone.Name,
                        Shape = "box",
                        Size = new[] { sx * px, sy * px, sz * px },
                        At = new[] { 0f, 0f, 0f },
                    };
                    var rect = new AtlasRect { Role = bone.Name, X = cube.Uv[0], Y = cube.Uv[1], W = 4, H = 4, PxW = 4, PxH = 4, PxD = 4 };
                    var one = MeshKit.BuildPart(part, rect, ysm.TextureWidth);

                    Quaternion rInv = Quaternion.identity;
                    if (bindRot.TryGetValue(bone.Name, out var rq)) rInv = Quaternion.Inverse(rq);
                    var offset = rInv * new Vector3(centerPx.X * px, centerPx.Y * px, centerPx.Z * px);

                    int v0 = merged.Positions.Count, i0 = merged.Indices.Count;
                    for (int v = 0; v < one.Positions.Count; v++)
                    {
                        var pv = one.Positions[v];
                        var world = offset + rInv * new Vector3(pv.X, pv.Y, pv.Z);
                        merged.Positions.Add(new Vec3(world.x, world.y, world.z));
                        var nv = one.Normals[v];
                        var nworld = rInv * new Vector3(nv.X, nv.Y, nv.Z);
                        merged.Normals.Add(new Vec3(nworld.x, nworld.y, nworld.z));
                        merged.Uvs.Add(one.Uvs[v]);
                    }
                    foreach (var idx in one.Indices) merged.Indices.Add(idx + v0);
                    merged.Atlas.Add(rect);
                    merged.Emitted.Add(part);
                    merged.EmittedBounds.Add(new Box3(new Vec3(0, 0, 0), new Vec3(0, 0, 0)));
                    cubeIndex++;
                }
                if (merged.Positions.Count == 0) continue;

                var go = UnityAdapter.CreateMeshObject($"{bone.Name}_cubes", UnityAdapter.ToMesh(merged, bone.Name), mat, boneTf, layer);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                result.Objects.Add(go);
                result.CubeCount += cubeIndex;
            }
            return result;
        }

        /// <summary>取每根骨骼在**模型空间**的绑定旋转（来自模型的 `SkinnedMeshRenderer.bindposes`）。
        /// 没有蒙皮网格（或找不到该骨骼）时退化成单位旋转（= 老行为，只适合"方块放在 pivot 上"的模型）。</summary>
        public static Dictionary<string, Quaternion> CollectBindRotations(Transform modelRoot)
        {
            var map = new Dictionary<string, Quaternion>();
            if (modelRoot == null) return map;
            foreach (var smr in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || smr.bones == null) continue;
                for (int i = 0; i < smr.bones.Length && i < mesh.bindposes.Length; i++)
                {
                    var b = smr.bones[i];
                    if (b == null || map.ContainsKey(b.name)) continue;
                    var localToWorldAtBind = mesh.bindposes[i].inverse;      // bindpose = 世界→骨骼（绑定姿势下）
                    map[b.name] = localToWorldAtBind.rotation;               // 它在模型空间的旋转
                }
            }
            return map;
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
