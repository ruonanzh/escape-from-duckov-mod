// BundleSwap —— 用**已经打包好的 bundle**（工坊 MPX）替换**手里武器的模型**。
//
// 目的：验证"打包好的资产能不能套到游戏物品上"这条链路 ——
// 唯一的新变量是"模型来自 bundle"，其余全用已验证的路径（改图形 / 隐藏旧零件 / 克隆游戏材质）。
//
// config.json：
//   { "bundle": "mpxmodels", "asset": "", "match": "MP5", "typeIDs": [655],
//     "material": "game", "scale": 1.0, "offset": [0,0,0], "rotate": [0,0,0] }
//   match="" 且 typeIDs 为空 = 对**任意手持物品**生效（测试用）
//
// 做法（与 reference/mod-kit/ItemModelBinder 同规则）：
//   ① 关掉旧模型零件（`WPN_*` / `HideIf_*`；`ShowIf_*` 与特效留着给游戏用）
//   ② 把 bundle 的 prefab 实例挂到"原枪身"零件的变换帧下（localScale 补 1/lossyScale）
//   ③ 材质：克隆原枪身材质（游戏自己的 URP shader ✓），贴图沿用 bundle 里的
//
// 日志：/tmp/bundle_swap.log

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModelKit;
using UnityEngine;

namespace BundleSwap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        const string LogPath = "/tmp/bundle_swap.log";

        readonly StringBuilder _sb = new StringBuilder();
        string _cfgPath;

        string _bundleRel = "mpxmodels";
        string _wantedAsset = "";
        string _match = "MP5";
        readonly HashSet<int> _typeIds = new HashSet<int>();
        string _materialMode = "game";
        float _scale = 1f;
        Vector3 _offset = Vector3.zero;
        Vector3 _rotate = Vector3.zero;

        AssetBundle _bundle;
        GameObject _template;              // 已实例化、未激活的模版（每次换的时候 Clone 一份）
        int _loadTries;
        float _nextTry;
        string _lastHeld;
        readonly Dictionary<int, GameObject> _applied = new Dictionary<int, GameObject>();   // root InstanceID → 我们挂的实例
        bool _loggedFirst;

        void Start()
        {
            _cfgPath = Path.Combine(ModelLoader.ModDir(), "config.json");
            Log("=== BundleSwap start ===");
            ReloadConfig();
            Flush();
        }

        void Update()
        {
            // ① 加载 bundle（实测：mod 启动最早期会失败 → 每 2 秒重试）
            if (_template == null)
            {
                if (Time.unscaledTime >= _nextTry)
                {
                    _nextTry = Time.unscaledTime + 2f;
                    _loadTries++;
                    TryLoad();
                    Flush();
                }
                return;
            }

            // ② 找到手持武器 → 需要就换
            var mc = GameApi.FindMainCharacter();
            if (mc == null) return;
            var agent = mc.CurrentHoldItemAgent;
            var item = agent != null ? agent.Item : null;
            if (item == null || agent == null) return;

            if (item.name != _lastHeld)
            {
                _lastHeld = item.name;
                Log($"手持：'{item.name}'（typeID={item.TypeID}）→ 过滤 match='{_match}' typeIDs=[{string.Join(",", _typeIds)}]：{(Matches(item) ? "命中 ✓ 会换" : "不命中 —— 不换")}");
            }
            if (!Matches(item)) { Flush(); return; }

            var root = agent.gameObject != null ? agent.gameObject.transform : null;
            if (root == null) return;

            int id = root.GetInstanceID();
            if (_applied.TryGetValue(id, out var go) && go != null) return;   // 已经换过、还在

            Apply(root, item);
            Flush();
        }

        bool Matches(ItemStatsSystem.Item item)
        {
            bool any = string.IsNullOrEmpty(_match) && _typeIds.Count == 0;
            if (any) return true;
            if (_typeIds.Count > 0 && _typeIds.Contains(item.TypeID)) return true;
            if (!string.IsNullOrEmpty(_match) && item.name != null &&
                item.name.IndexOf(_match, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ── 加载 ────────────────────────────────────────────────────────────
        void TryLoad()
        {
            string path = _bundleRel;
            if (!Path.IsPathRooted(path)) path = Path.Combine(ModelLoader.ModDir(), _bundleRel);
            if (!File.Exists(path)) { Log($"✗ 找不到 bundle：{path}"); return; }

            try { _bundle = AssetBundle.LoadFromFile(path); }
            catch (Exception e) { Log($"✗ LoadFromFile 抛异常：{e}"); return; }
            if (_bundle == null) { Log($"✗ 第 {_loadTries} 次 LoadFromFile 返回 null → 2 秒后重试"); return; }

            string[] names = _bundle.GetAllAssetNames();
            Log($"✓ 第 {_loadTries} 次加载成功：{_bundle.name}，{names.Length} 个资产");

            GameObject prefab = null;
            if (!string.IsNullOrEmpty(_wantedAsset)) prefab = _bundle.LoadAsset<GameObject>(_wantedAsset);
            if (prefab == null)
            {
                var pn = names.FirstOrDefault(n => n.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
                if (pn != null) prefab = _bundle.LoadAsset<GameObject>(pn);
            }
            if (prefab == null)
                foreach (var n in names) { try { prefab = _bundle.LoadAsset<GameObject>(n); if (prefab != null) break; } catch { } }
            if (prefab == null) { Log("✗ 包里没有可用的 GameObject"); return; }

            _template = Instantiate(prefab);
            _template.name = "BundleSwap_template_" + prefab.name;
            _template.SetActive(false);         // 模版本身不显示
            UnityEngine.Object.DontDestroyOnLoad(_template);
            Log($"✓ 模版已建：{_template.name}（渲染器 {_template.GetComponentsInChildren<Renderer>(true).Length} 个）");
        }

        // ── 换模型 ──────────────────────────────────────────────────────────
        void Apply(Transform root, ItemStatsSystem.Item item)
        {
            // 选"枪身"锚点（与 ItemModelBinder 同规则：WPN_* 优先，否则最大且 <5m 的非配件零件）
            Renderer anchor = null; float best = -1f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (r.gameObject.name.StartsWith("ShowIf_") || r.gameObject.name.StartsWith("HideIf_")) continue;
                if (r.gameObject.name.StartsWith("WPN_")) { anchor = r; best = r.bounds.size.magnitude; break; }
                float sz = r.bounds.size.magnitude;
                if (sz > best && sz < 5f) { best = sz; anchor = r; }
            }

            int hidden = 0, kept = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name;
                if (n.StartsWith("ShowIf_") || n.StartsWith("MuzzleFlash") || n.StartsWith("Particle")) { kept++; continue; }
                if (n.StartsWith("WPN_") || n.StartsWith("HideIf_"))
                {
                    if (r.enabled) { r.enabled = false; hidden++; }
                    continue;
                }
                kept++;
            }
            Log($"隐藏旧零件 {hidden} 个（保留 {kept} 个：配件/特效）  锚点={(anchor != null ? anchor.name : "根节点")}");

            // 挂我们的实例
            var inst = Instantiate(_template);
            inst.name = "BundleSwap_" + item.name;
            inst.SetActive(true);
            var parent = anchor != null ? anchor.transform : root;
            inst.transform.SetParent(parent, false);

            Vector3 lossy = parent.lossyScale;
            var inv = new Vector3(Mathf.Abs(lossy.x) > 1e-6f ? 1f / lossy.x : 1f,
                                  Mathf.Abs(lossy.y) > 1e-6f ? 1f / lossy.y : 1f,
                                  Mathf.Abs(lossy.z) > 1e-6f ? 1f / lossy.z : 1f);
            inst.transform.localScale = Vector3.Scale(inv, Vector3.one * _scale);
            inst.transform.localPosition = _offset;
            inst.transform.localRotation = Quaternion.Euler(_rotate);
            SetLayerRecursive(inst, parent.gameObject.layer);

            // 材质：克隆游戏材质（URP ✓），贴图沿用 bundle 里的
            int slots = 0;
            if (_materialMode == "game")
            {
                var srcMat = anchor != null ? anchor.sharedMaterial : null;
                if (srcMat == null)
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { if (r.sharedMaterial != null) { srcMat = r.sharedMaterial; break; } }
                Log($"材质源：{(srcMat != null ? srcMat.name + "/" + (srcMat.shader != null ? srcMat.shader.name : "?") : "无")}");
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var old = mats[i];
                        var clone = srcMat != null ? new Material(srcMat) : (old != null ? new Material(old) : null);
                        if (clone == null) continue;
                        var tex = old != null ? old.mainTexture : null;
                        if (tex != null)
                        {
                            if (clone.HasProperty("_BaseMap")) clone.SetTexture("_BaseMap", tex);
                            else if (clone.HasProperty("_MainTex")) clone.SetTexture("_MainTex", tex);
                            if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", Color.white);
                        }
                        clone.name = "BundleSwap_" + (old != null ? old.name : i.ToString());
                        mats[i] = clone; slots++;
                    }
                    r.sharedMaterials = mats;
                }
            }
            Log($"材质已处理 {slots} 个槽（mode={_materialMode}）");

            // 诊断（这里的 1m = 游戏世界米；对比原枪身尺寸判断缩放对不对）
            var rs = inst.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(inst.transform.position, Vector3.zero); bool first = true;
            foreach (var r in rs) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            Log($"我们的实例：渲染器 {rs.Length} 个  世界包围盒 尺寸={b.size}  中心={b.center}");
            if (anchor != null) Log($"原枪身：'{anchor.name}' 世界包围盒 尺寸={anchor.bounds.size}（对比一下大小是否接近）");
            int i2 = 0;
            foreach (var r in rs.Take(10))
                Log($"   [{i2++}] {r.name} enabled={r.enabled} layer={r.gameObject.layer} mat={(r.sharedMaterial != null ? r.sharedMaterial.name + "/" + (r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?") : "null")}");

            _applied[root.GetInstanceID()] = inst;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        void ReloadConfig()
        {
            try
            {
                if (!File.Exists(_cfgPath)) return;
                var root = Json.Parse(File.ReadAllText(_cfgPath));
                _bundleRel = root["bundle"].AsString(_bundleRel);
                _wantedAsset = root["asset"].AsString("");
                _match = root["match"].AsString(_match);
                _materialMode = root["material"].AsString("game");
                _scale = root["scale"].AsFloat(1f);
                _typeIds.Clear();
                var ids = root["typeIDs"];
                if (ids != null && ids.IsArray) for (int i = 0; i < ids.Count; i++) _typeIds.Add(ids[i].AsInt(0));
                _offset = ReadVec(root["offset"]); _rotate = ReadVec(root["rotate"]);
                Log($"配置：bundle={_bundleRel} match='{_match}' typeIDs=[{string.Join(",", _typeIds)}] material={_materialMode} scale={_scale}");
            }
            catch (Exception e) { Log($"配置读取失败：{e.Message}"); }
        }

        static Vector3 ReadVec(JsonValue v)
        {
            if (v == null || !v.IsArray || v.Count < 3) return Vector3.zero;
            return new Vector3(v[0].AsFloat(0f), v[1].AsFloat(0f), v[2].AsFloat(0f));
        }

        void Log(string s) => _sb.AppendLine(s);
        void Flush()
        {
            if (_sb.Length == 0) return;
            try { File.AppendAllText(LogPath, _sb.ToString()); } catch { }
            _sb.Clear();
        }
        void OnDestroy() => Flush();
    }
}
