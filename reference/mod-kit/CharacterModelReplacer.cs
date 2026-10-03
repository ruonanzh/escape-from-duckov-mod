// CharacterModelReplacer.cs —— 把 YSM 模型作用到某个角色上：**替换** 或 **只增加**。
//
// 之前这套逻辑是写在 demo 里的（找骨骼、建几何、关本体、留装备、模型重建后重挂）；
// 抽到这里，玩家与 NPC 共用一条路径。
//
// 两种用法（由 ReplaceBody 决定）：
//   ReplaceBody = true  → 替换：关掉角色本体渲染器（enabled=false），装备/武器保持可见  ← 换外观
//   ReplaceBody = false → 只增加：不动本体，只把几何挂上去                              ← 挂件/饰品
//
// 关键实测经验（都写进代码里了）：
//   · 改 layer 当隐藏没用 —— 游戏刷新会把层改回 Character；enabled=false 才拦得住
//   · 装备/武器挂在名字含 Socket 的挂点下（MeleeWeaponSocket / HelmatSocket / ArmorSocket / BackpackSocket…）
//   · 角色模型会被游戏重建（进关卡 / 换装备）→ 每帧 Tick() 检测并重挂
//   · 隐藏层 SpecialCamera=31、可见层 Character=9（备用；主手段是 enabled）

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModelKit
{
    public sealed class CharacterModelReplacer
    {
        /// <summary>true = 替换（关本体）；false = 只增加（挂件）。</summary>
        public bool ReplaceBody = true;

        /// <summary>替换时是否保留装备/武器（挂在 *Socket* 下的东西）。</summary>
        public bool KeepEquipment = true;

        readonly YsmModel _model;

        Transform _root;
        CharacterModel _cm;
        GameObject[] _objects = new GameObject[0];
        readonly Dictionary<string, Transform> _boneCache = new Dictionary<string, Transform>();
        Transform[] _ours;                       // 我们建的对象的 Transform（O(1) 判断归属）
        int _covered = -1;                       // 已经处理过的角色实例 ID（换实例 → 重挂）

        public List<string> AttachedBones { get; } = new List<string>();
        public List<string> MissingBones { get; } = new List<string>();
        public int CubeCount { get; private set; }
        public int HiddenBody { get; private set; }
        public int KeptEquipment { get; private set; }
        public Texture2D Texture { get; private set; }
        /// <summary>克隆用的材质源（null = 用了兜底材质 → 会是白模）。</summary>
        public Material MaterialSource { get; private set; }
        public bool Attached => _covered >= 0;

        public CharacterModelReplacer(YsmModel model) { _model = model; }

        /// <summary>给这个角色挂上模型。重复调用同一个角色会被忽略（除非模型被重建过）。</summary>
        public bool Attach(CharacterMainControl who)
        {
            var cm = GameApi.GetModel(who);
            if (cm == null) return false;
            if (_covered == cm.GetInstanceID() && _objects.Length > 0 && _objects[0] != null) return true;

            Detach();
            _cm = cm;
            _root = cm.transform;

            var body = cm.GetComponentInChildren<SkinnedMeshRenderer>();
            var srcMat = body != null ? body.sharedMaterial : null;
            // 有的模型（如 boss）身上没有蒙皮网格或有别的渲染器 → 退而取任意一个有材质的，避免变成白模
            if (srcMat == null)
            {
                var any = cm.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.sharedMaterial != null);
                if (any != null) srcMat = any.sharedMaterial;
            }
            MaterialSource = srcMat;
            int layer = body != null ? body.gameObject.layer : cm.gameObject.layer;

            var build = ModelLoader.BuildYsmObjects(_model, _root, srcMat, layer);
            _objects = build.Objects.ToArray();
            _ours = new Transform[_objects.Length];
            for (int i = 0; i < _objects.Length; i++) _ours[i] = _objects[i] != null ? _objects[i].transform : null;
            AttachedBones.Clear(); AttachedBones.AddRange(build.AttachedBones);
            MissingBones.Clear(); MissingBones.AddRange(build.MissingBones);
            CubeCount = build.CubeCount;
            Texture = build.Texture;
            // 本体渲染器会被我们禁用；Animator 默认 culling 会因此停止更新骨骼（表现为卡在 T-pose）
            foreach (var an in cm.GetComponentsInChildren<Animator>(true))
                if (an.cullingMode != AnimatorCullingMode.AlwaysAnimate) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _covered = cm.GetInstanceID();
            return true;
        }

        /// <summary>每帧调用：替换本体 / 保留装备 / 角色模型被重建就重挂。</summary>
        public void Tick()
        {
            if (_cm == null) return;

            // 角色模型被游戏重建（进关卡、换装备）→ 我们的方块被销毁 → 重挂
            if (_objects.Length == 0 || _objects[0] == null || !_cm) { _covered = -1; return; }
            if (_root != _cm.transform) { _covered = -1; return; }

            if (ReplaceBody) ApplyBodyState();
        }

        void ApplyBodyState()
        {
            int hidden = 0, kept = 0;
            foreach (var r in _cm.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || IsOurs(r.transform)) continue;

                if (KeepEquipment && IsEquipment(r))
                {
                    if (!r.enabled) r.enabled = true;
                    if (GameApi.VisibleLayer >= 0 && r.gameObject.layer != GameApi.VisibleLayer) r.gameObject.layer = GameApi.VisibleLayer;
                    kept++;
                }
                else
                {
                    if (r.enabled) r.enabled = false;                  // 硬关（改层没用）
                    if (GameApi.HiddenLayer >= 0 && r.gameObject.layer != GameApi.HiddenLayer) r.gameObject.layer = GameApi.HiddenLayer;
                    hidden++;
                }
            }
            HiddenBody = hidden; KeptEquipment = kept;
        }

        public void Detach()
        {
            foreach (var o in _objects) if (o != null) Object.Destroy(o);
            _objects = new GameObject[0];
            _ours = null;
            _covered = -1;
        }

        /// <summary>装备/武器都挂在名字含 Socket 的挂点下（MeleeWeaponSocket / HelmatSocket / Hand.Soket.L…）。</summary>
        public static bool IsEquipment(Renderer r)
        {
            if (r == null) return false;
            if (r.gameObject.name.StartsWith("IG_")) return true;
            var t = r.transform;
            while (t != null)
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
            if (_ours == null) return false;
            while (t != null && t != _root)
            {
                foreach (var o in _ours) if (o != null && o == t) return true;
                t = t.parent;
            }
            return false;
        }
    }
}
