// UnityAdapter.cs —— 把共享层的纯数据变成 Unity 对象（Mesh / Texture2D / Material）。
//
// 这一层**是唯一**碰 UnityEngine 的地方：共享层（Json / ModelJson / MeshKit / TextureKit）保持纯 C#，
// 于是同一份源码既能在离线工具里跑（校验、生成 PNG），也能在 mod 里跑（生成游戏里的网格与贴图）。

using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    public static class UnityAdapter
    {
        /// <summary>MeshData → UnityEngine.Mesh（顶点/UV/法线/submesh）。</summary>
        public static Mesh ToMesh(MeshData d, string name)
        {
            var mesh = new Mesh { name = name };

            var verts = new Vector3[d.Positions.Count];
            var norms = new Vector3[d.Normals.Count];
            var uvs = new Vector2[d.Uvs.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = d.Positions[i];
                verts[i] = new Vector3(p.X, p.Y, p.Z);
                var n = i < norms.Length ? d.Normals[i] : new Vec3(0, 1, 0);
                norms[i] = new Vector3(n.X, n.Y, n.Z);
                var t = i < uvs.Length ? d.Uvs[i] : new Vec2(0, 0);
                uvs[i] = new Vector2(t.X, t.Y);
            }
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;

            // 每个 submesh（role）一段索引
            var tris = new List<int>[d.SubMeshes.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();
            for (int s = 0; s < d.SubMeshes.Count; s++)
            {
                var sm = d.SubMeshes[s];
                for (int k = sm.Start; k < sm.Start + sm.Count; k++) tris[s].Add(d.Indices[k]);
            }
            mesh.subMeshCount = Mathf.Max(1, tris.Length);
            for (int s = 0; s < tris.Length; s++) mesh.SetTriangles(tris[s], s);

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Bitmap → Texture2D。注意 Bitmap 的行 0 在**上**，而 Unity 的 SetPixels32 行 0 在**下** —— 这里翻一次。</summary>
        public static Texture2D ToTexture(Bitmap bmp, string name)
        {
            var tex = new Texture2D(bmp.W, bmp.H, TextureFormat.RGBA32, false) { name = name };
            var colors = new Color32[bmp.W * bmp.H];
            for (int y = 0; y < bmp.H; y++)
            {
                int src = y * bmp.W * 4;
                int dstRow = (bmp.H - 1 - y) * bmp.W;
                for (int x = 0; x < bmp.W; x++)
                {
                    int s = src + x * 4;
                    colors[dstRow + x] = new Color32(bmp.Rgba[s], bmp.Rgba[s + 1], bmp.Rgba[s + 2], bmp.Rgba[s + 3]);
                }
            }
            tex.SetPixels32(colors);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>克隆游戏里现成的材质（URP 自定义 shader → 必须克隆），把贴图塞进它的主贴图槽。</summary>
        public static Material CloneWithTexture(Material source, Texture2D tex, Color? tint = null)
        {
            var mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (source == null && mat.shader == null) mat = new Material(Shader.Find("Standard"));

            // 主贴图槽的名字各 shader 不同，挨个试
            foreach (var prop in new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo" })
            {
                if (mat.HasProperty(prop))
                {
                    mat.SetTexture(prop, tex);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint ?? Color.white);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint ?? Color.white);
                    return mat;
                }
            }
            mat.mainTexture = tex;
            return mat;
        }

        /// <summary>把材质改成**双面渲染**（`_Cull = 0` / `Cull Off`）—— 用于诊断"模型看不见是不是背面剔除"。
        /// 返回是否改成功（不同 shader 属性名不同：URP 用 `_Cull`，内置/标准用 `_Cull`；找不到就返回 false）。</summary>
        public static bool MakeDoubleSided(Material mat)
        {
            if (mat == null) return false;
            if (mat.HasProperty("_Cull")) { mat.SetFloat("_Cull", 0f); return true; }
            if (mat.HasProperty("_CullMode")) { mat.SetFloat("_CullMode", 0f); return true; }
            return false;
        }

        /// <summary>建一个带 MeshFilter/MeshRenderer 的 GameObject（层与材质源渲染器一致 —— 否则可能被相机剔除）。</summary>
        public static GameObject CreateMeshObject(string name, Mesh mesh, Material mat, Transform parent, int layer, bool localSpace = true)
        {
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            go.layer = layer;
            go.transform.SetParent(parent, localSpace);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }
    }
}
