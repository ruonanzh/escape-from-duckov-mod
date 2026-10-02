// CubePerson —— 方块人 demo：用 YSM 模型文件替换玩家角色外观。
//
// 走的就是"我们自己的运行时库"：ModelLoader 读 models/npc_duck.json（YSM: 骨骼 + 方块），
// 按骨骼名把方块挂到游戏角色骨骼上（动画由游戏驱动），贴图由 TextureKit 生成。
// 角色的模型会被游戏重建（换装备、进关卡）→ 这里会检测并重新挂上。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModelKit;
using UnityEngine;

namespace CubePerson
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        const string LogPath = "/tmp/cube_person.log";
        const string ModelFile = "models/npc_duck.json";

        readonly StringBuilder _sb = new StringBuilder();

        YsmModel _ysm;
        readonly List<Renderer> _hidden = new List<Renderer>();
        int _hiddenLayer = -1;
        Transform _modelRoot;
        ModelLoader.YsmBuild _build;
        float _acc;
        int _builds;
        int _tick;

        void Start()
        {
            Log("=== CubePerson start ===");
            Log($"mod dir = {ModelLoader.ModDir()}");
        }

        void Update()
        {
            _acc += Time.deltaTime;
            if (_acc < 1f) return;
            _acc = 0;

            var all = GameApi.AllCharacters();
            var cmc = GameApi.FindMainCharacter() ?? all.FirstOrDefault();
            if (_build == null && all.Length > 0)
            {
                Log($"角色数={all.Length}；选中 '{cmc?.name}' IsMainCharacter={(cmc != null && cmc.IsMainCharacter)}");
                foreach (var c in all.Take(12))
                {
                    var m = GameApi.GetModel(c);
                    Log($"   · '{c.name}' main={c.IsMainCharacter} 模型={(m != null ? m.name : "null")}");
                }
            }
            var cm = GameApi.GetModel(cmc);
            if (cm == null) return;

            // 需要（重新）挂载的情况：还没挂过 / 游戏换了模型 / 我们建的方块被销毁了
            bool needBuild = _build == null
                             || _modelRoot != cm.transform
                             || _build.Objects.Count == 0
                             || _build.Objects[0] == null;
            if (needBuild)
            {
                try { Build(cm); }
                catch (System.Exception e) { Log("FAIL: " + e); }
                Flush();
                return;
            }

            if (_tick++ < 6 && _build.Objects.Count > 0 && _build.Objects[0] != null)
            {
                var probe = _build.Objects[0];
                var mr = probe.GetComponent<MeshRenderer>();
                var mf = probe.GetComponent<MeshFilter>();
                string info = "无渲染器";
                if (mr != null)
                    info = $"layer={probe.layer} 可见={mr.isVisible} 世界包围盒={mr.bounds.size} 实体={mr.enabled}";
                string meshInfo = mf != null && mf.sharedMesh != null ? $"顶点={mf.sharedMesh.vertexCount} 本地包围盒={mf.sharedMesh.bounds.size}" : "无 mesh";
                Log($"t={Time.time:F1} 探针 {probe.name} 父={probe.transform.parent?.name} 世界={probe.transform.position} " +
                    $"本地缩放={probe.transform.localScale} 世界缩放={probe.transform.lossyScale} 父世界缩放={probe.transform.parent?.lossyScale} " +
                    $"| {info} | {meshInfo} | 相机={Camera.main?.name} cull={Camera.main?.cullingMask}");
                Flush();
            }
        }

        void Build(CharacterModel cm)
        {
            if (_ysm == null)
            {
                var path = Path.Combine(ModelLoader.ModDir(), ModelFile);
                Log($"载入 {path} 存在={File.Exists(path)}");
                _ysm = ModelLoader.LoadYsm(path);
                Log($"YSM: {_ysm.Bones.Count} 骨骼 / {_ysm.Bones.Sum(b => b.Cubes.Count)} 方块 / 贴图 {_ysm.TextureWidth}×{_ysm.TextureHeight}");
            }

            // 换模型时先把上一次建的清掉
            if (_build != null)
                foreach (var o in _build.Objects) if (o != null) Destroy(o);

            _modelRoot = cm.transform;
            var body = cm.GetComponentInChildren<SkinnedMeshRenderer>();
            var srcMat = body != null ? body.sharedMaterial : null;
            int layer = body != null ? body.gameObject.layer : cm.gameObject.layer;
            if (_builds == 0) Log($"材质源={(srcMat != null ? srcMat.name + " / " + srcMat.shader.name : "无")} 层={layer}");

            _build = ModelLoader.BuildYsmObjects(_ysm, _modelRoot, srcMat, layer);
            _builds++;
            Log($"[第 {_builds} 次挂载] 建成 {_build.CubeCount} 个方块；挂上 {_build.AttachedBones.Count} 骨骼；缺 {_build.MissingBones.Count}" +
                (_build.MissingBones.Count > 0 ? "（" + string.Join(",", _build.MissingBones) + "）" : ""));

        }

        /// <summary>替换语义：把"角色本体"渲染器直接关掉（enabled=false，与相机剔除无关），
        /// 装备/武器（挂在 *Socket* 下的）保持打开。每帧重申一次 —— 游戏刷新时只改层，不会改 enabled。</summary>
        void LateUpdate()
        {
            if (_modelRoot == null || _build == null) return;
            var cm = _modelRoot.GetComponent<CharacterModel>();
            if (cm == null) return;
            ApplyVisualReplacement(cm);
        }

        int _hiddenBody, _keptEquip;

        void ApplyVisualReplacement(CharacterModel cm)
        {
            int hiddenLayer = GameApi.HiddenLayer, visibleLayer = GameApi.VisibleLayer;
            int hiddenBody = 0, keptEquip = 0;
            var kept = new List<string>();

            foreach (var r in cm.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || IsOurs(r.transform)) continue;
                if (IsEquipment(r))
                {
                    if (!r.enabled) r.enabled = true;
                    if (visibleLayer >= 0 && r.gameObject.layer != visibleLayer) r.gameObject.layer = visibleLayer;
                    keptEquip++;
                    if (kept.Count < 6) kept.Add(r.gameObject.name);
                }
                else
                {
                    if (r.enabled) r.enabled = false;                        // ← 硬关（替换）
                    if (hiddenLayer >= 0 && r.gameObject.layer != hiddenLayer) r.gameObject.layer = hiddenLayer;
                    hiddenBody++;
                }
            }

            if (hiddenBody != _hiddenBody || keptEquip != _keptEquip)
            {
                _hiddenBody = hiddenBody; _keptEquip = keptEquip;
                Log($"本体已关={hiddenBody} 装备保留={keptEquip}" + (kept.Count > 0 ? "（如：" + string.Join(",", kept) + "）" : ""));
                Flush();
            }
        }

        /// <summary>装备/武器都挂在名字带 Socket 的挂点下（MeleeWeaponSocket / HelmatSocket / ArmorSocket /
        /// BackpackSocket / Hand.Soket.L…），本体不在挂点下 —— 比看名字前缀可靠。</summary>
        bool IsEquipment(Renderer r)
        {
            if (r.gameObject.name.StartsWith("IG_")) return true;
            var t = r.transform;
            while (t != null && t != _modelRoot)
            {
                var n = t.name;
                if (n.IndexOf("Socket", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("Soket", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                t = t.parent;
            }
            return false;
        }

        bool IsOurs(Transform t)
        {
            var root = _modelRoot;
            while (t != null && t != root)
            {
                foreach (var o in _build.Objects) if (o != null && o.transform == t) return true;
                t = t.parent;
            }
            return false;
        }

        CharacterSubVisuals _visuals;

        void Log(string s)
        {
            Debug.Log("[CubePerson] " + s);
            _sb.AppendLine(s);
        }

        void Flush()
        {
            try { File.WriteAllText(LogPath, _sb.ToString()); } catch { }
        }
    }
}
