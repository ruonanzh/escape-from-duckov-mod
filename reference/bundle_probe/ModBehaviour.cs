// BundleProbe —— 验证「AssetBundle 能不能被我们的代码加载并渲染」。
//
// 目的：回答"我们自己（或工坊）打的 bundle，游戏能不能正常画出来"，
// 尤其是**随包带的 shader 在本游戏（URP）里能不能渲染**这个最大不确定性。
//
// config.json：
//   { "bundle": "mpxmodels", "asset": "", "attach": "hand", "scale": 1.0, "layer": 9 }
//   bundle  : 相对本 mod 目录（或绝对路径）
//   asset   : 要加载的资产名（留空 = 自动挑第一个 *.prefab / 第一个能 LoadAsset<GameObject> 的）
//   attach  : hand（挂到玩家手上）/ front（放在玩家面前 2m）
//   layer   : >0 时把整个实例递归设成这一层（9 = Character，相机渲染它）
//
// 日志：/tmp/bundle_probe.log（每次加载都会打出**材质名 + shader 名 + 是否 supported**）

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModelKit;
using UnityEngine;

namespace BundleProbe
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        const string LogPath = "/tmp/bundle_probe.log";

        readonly StringBuilder _sb = new StringBuilder();
        string _cfgPath;

        string _bundleRel = "mpxmodels";
        string _wantedAsset = "";
        string _attach = "hand";
        float _scale = 1f;
        int _layerOverride;

        AssetBundle _bundle;
        GameObject _instance;
        bool _tried;
        float _nextCheck;
        string _lastParent;
        bool _dumped;

        void Start()
        {
            _cfgPath = Path.Combine(ModelLoader.ModDir(), "config.json");
            Log("=== BundleProbe start ===");
            Log($"mod dir = {ModelLoader.ModDir()}");
            Log($"config  = {_cfgPath}  exists={File.Exists(_cfgPath)}");
            ReloadConfig();
            Flush();
        }

        void Update()
        {
            if (!_tried) { _tried = true; TryLoad(); Flush(); return; }

            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 2f;
            EnsureAttached();
            if (_sb.Length > 0) Flush();
        }

        // ── 配置 ────────────────────────────────────────────────────────────
        void ReloadConfig()
        {
            try
            {
                if (!File.Exists(_cfgPath)) return;
                var root = Json.Parse(File.ReadAllText(_cfgPath));
                _bundleRel   = root["bundle"].AsString(_bundleRel);
                _wantedAsset = root["asset"].AsString("");
                _attach      = root["attach"].AsString("hand");
                _scale       = root["scale"].AsFloat(1f);
                _layerOverride = root["layer"].AsInt(0);
            }
            catch (Exception e) { Log($"配置读取失败：{e.Message}"); }
        }

        // ── 加载 bundle ─────────────────────────────────────────────────────
        void TryLoad()
        {
            string path = _bundleRel;
            if (!Path.IsPathRooted(path)) path = Path.Combine(ModelLoader.ModDir(), _bundleRel);
            if (!File.Exists(path)) { Log($"✗ 找不到 bundle：{path}"); return; }
            Log($"加载 bundle：{path}  ({new FileInfo(path).Length / 1024} KB)");

            try { _bundle = AssetBundle.LoadFromFile(path); }
            catch (Exception e) { Log($"✗ LoadFromFile 抛异常：{e}"); return; }

            if (_bundle == null) { Log("✗ LoadFromFile 返回 null（格式/版本不匹配，或包损坏）"); return; }
            Log($"✓ bundle 已加载：name={_bundle.name}");

            string[] names = null;
            try { names = _bundle.GetAllAssetNames(); } catch (Exception e) { Log($"GetAllAssetNames 失败：{e.Message}"); }
            if (names == null || names.Length == 0) { Log("✗ 包里没有资产名（内容不合法？）"); return; }
            Log($"包里 {names.Length} 个资产名：");
            foreach (var n in names.Take(40)) Log("   " + n);

            GameObject prefab = LoadPrefab(names);
            if (prefab == null) { Log("✗ 没能从包里加载到任何 GameObject"); return; }
            Log($"✓ 加载到 prefab：{prefab.name}");

            _instance = Instantiate(prefab);
            _instance.name = "BundleProbe_" + prefab.name;
            UnityEngine.Object.DontDestroyOnLoad(_instance);       // 跨场景常驻（我们的老坑）
            _instance.transform.localScale = Vector3.one * _scale;
            if (_layerOverride > 0) SetLayerRecursive(_instance, _layerOverride);

            DumpInstance(_instance);
        }

        GameObject LoadPrefab(string[] names)
        {
            // 1) 指定名字  2) *.prefab  3) 逐个试 LoadAsset<GameObject>
            if (!string.IsNullOrEmpty(_wantedAsset))
            {
                var go = _bundle.LoadAsset<GameObject>(_wantedAsset);
                if (go != null) return go;
                Log($"指定的 asset='{_wantedAsset}' 加载不到，改为自动挑");
            }
            var prefabName = names.FirstOrDefault(n => n.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
            if (prefabName != null)
            {
                var go = _bundle.LoadAsset<GameObject>(prefabName);
                if (go != null) { Log($"按 *.prefab 挑中：{prefabName}"); return go; }
            }
            foreach (var n in names)
            {
                try { var go = _bundle.LoadAsset<GameObject>(n); if (go != null) { Log($"逐个试挑中：{n}"); return go; } }
                catch { }
            }
            return null;
        }

        // ── 诊断：把实例里的渲染器/材质/shader 全打出来 ────────────────────────
        void DumpInstance(GameObject go)
        {
            if (_dumped) return;
            _dumped = true;

            var rs = go.GetComponentsInChildren<Renderer>(true);
            Log($"实例：渲染器 {rs.Length} 个，子物体 {go.GetComponentsInChildren<Transform>(true).Length} 个，layer={go.layer}");

            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rs)
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            Log($"包围盒：中心 {b.center}  尺寸 {b.size}");

            int i = 0;
            foreach (var r in rs.Take(12))
            {
                var mats = r.sharedMaterials;
                Log($"  [{i++}] {r.GetType().Name} '{r.name}' enabled={r.enabled} layer={r.gameObject.layer} 材质 {mats.Length} 个");
                foreach (var m in mats)
                {
                    if (m == null) { Log("        (null 材质)"); continue; }
                    string sh = m.shader != null ? m.shader.name : "(no shader)";
                    bool sup = m.shader != null && m.shader.isSupported;
                    Log($"        mat='{m.name}' shader='{sh}' supported={sup}");
                }
            }

            var names = go.GetComponentsInChildren<Transform>(true).Select(t => t.name).Take(30);
            Log("  子物体名：" + string.Join(", ", names));
        }

        // ── 每 2 秒：保证它挂在手上 / 保持在玩家面前 ─────────────────────────
        void EnsureAttached()
        {
            if (_instance == null) return;
            var mc = GameApi.FindMainCharacter();
            if (mc == null) return;

            Transform target = null;
            string label = "";
            if (_attach == "hand")
            {
                target = FindHand(mc.transform);
                label = target != null ? $"手挂点 {target.name}" : "";
            }
            if (target == null)
            {
                // 退路：放在玩家面前 2m、眼睛高度
                var head = FindDeepByName(mc.transform, "Head") ?? mc.transform;
                target = head;
                label = $"面前（{head.name}）";
            }

            string key = label + "/" + target.GetInstanceID();
            if (key == _lastParent) return;
            _lastParent = key;

            _instance.transform.SetParent(target, false);
            _instance.transform.localPosition = _attach == "hand" && label.StartsWith("手") ? Vector3.zero : new Vector3(0f, 0.1f, 2f);
            _instance.transform.localRotation = Quaternion.identity;
            Log($"→ 已挂到 {label}（attach={_attach}）");
        }

        Transform FindHand(Transform root)
        {
            // 游戏里两种拼写：Hand.Soket.L / Hand.Soket.R，以及 Hand.L / Hand.R
            string[] keys = { "Hand.Soket.R", "Hand.Soket.L", "Hand.R", "Hand.L" };
            foreach (var k in keys)
            {
                var t = FindDeepByName(root, k);
                if (t != null) return t;
            }
            return null;
        }

        static Transform FindDeepByName(Transform root, string name)
        {
            if (root == null) return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeepByName(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        // ── 日志 ────────────────────────────────────────────────────────────
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
