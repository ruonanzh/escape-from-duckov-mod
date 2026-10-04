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
        DateTime _cfgStamp;

        string _bundleRel = "mpxmodels";
        string _wantedAsset = "";
        string _attach = "hand";
        float _scale = 1f;
        int _layerOverride;
        string _materialMode = "keep";   // keep=用包里的材质 / game=换成游戏自己的 URP 材质（保留包里的贴图）
        bool _materialsApplied;

        AssetBundle _bundle;
        GameObject _instance;
        bool _tried;
        int _loadTries;
        float _nextLoadTry;
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
            _cfgStamp = File.Exists(_cfgPath) ? File.GetLastWriteTimeUtc(_cfgPath) : DateTime.MinValue;
            Flush();
        }

        void Update()
        {
            // 加载：失败就重试（实测：mod 启动最早期 LoadFromFile 会返回 null，
            // 工坊 mod 的日志里也有同样一行 —— 它们成功是因为稍后重试了）
            if (_bundle == null)
            {
                if (Time.unscaledTime >= _nextLoadTry)
                {
                    _nextLoadTry = Time.unscaledTime + 2f;
                    _loadTries++;
                    TryLoad();
                    Flush();
                }
                return;
            }
            if (!_tried) { _tried = true; Log($"✓ 第 {_loadTries} 次尝试时加载成功"); Flush(); }

            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 2f;
            ReloadIfChanged();
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
                _materialMode = root["material"].AsString("keep");
            }
            catch (Exception e) { Log($"配置读取失败：{e.Message}"); }
        }

        void ReloadIfChanged()
        {
            try
            {
                if (!File.Exists(_cfgPath)) return;
                var st = File.GetLastWriteTimeUtc(_cfgPath);
                if (st == _cfgStamp) return;
                _cfgStamp = st;
                ReloadConfig();
                _lastParent = null;                 // 配置变了 → 重新摆位
                _materialsApplied = false;          // 配置变了 → 重新处理材质
                Log($"配置热重载：attach={_attach} scale={_scale} layer={_layerOverride} asset='{_wantedAsset}' material={_materialMode}");
            }
            catch (Exception e) { Log($"配置热重载失败：{e.Message}"); }
        }

        void LogHeldItem()
        {
            var mc = GameApi.FindMainCharacter();
            if (mc == null) { Log("手里诊断：找不到玩家角色"); return; }
            var agent = mc.CurrentHoldItemAgent;
            var item = agent != null ? agent.Item : null;
            if (item == null) { Log("手里诊断：玩家当前没有手持物品（手上是空的）"); return; }
            var g = item.ItemGraphic;
            Log($"手里诊断：玩家手持 '{item.name}'（typeID={item.TypeID}），它的 ItemGraphic={(g != null ? g.gameObject.name : "null")}");
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

            if (_bundle == null) { Log($"✗ 第 {_loadTries} 次 LoadFromFile 返回 null（太早 / 格式不符）→ 2 秒后重试"); return; }
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
            TryApplyGameMaterials();
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
                    var tex = m.mainTexture;
                    string texName = tex != null ? tex.name + $"({tex.width}x{tex.height})" : "无贴图";
                    string props = m.HasProperty("_BaseMap") ? "有_BaseMap" : (m.HasProperty("_MainTex") ? "有_MainTex(内置管线风格)" : "无常见贴图属性");
                    Log($"        mat='{m.name}' shader='{sh}' supported={sup} 主贴图={texName} {props}");
                }
            }

            var names = go.GetComponentsInChildren<Transform>(true).Select(t => t.name).Take(30);
            Log("  子物体名：" + string.Join(", ", names));
        }

        void LogCameraCheck()
        {
            var cam = Camera.main;
            if (cam == null) { Log("相机诊断：Camera.main 为空"); return; }
            int need = _layerOverride > 0 ? _layerOverride : (_instance != null ? _instance.layer : 0);
            bool rendered = (cam.cullingMask & (1 << need)) != 0;
            Log($"相机诊断：{cam.name}  cullingMask 包含 layer {need}? {rendered}");
            if (_instance != null)
            {
                int vis = 0, tot = 0;
                foreach (var r in _instance.GetComponentsInChildren<Renderer>(true)) { tot++; if (r.isVisible) vis++; }
                Log($"相机诊断：实例渲染器 {vis}/{tot} 被相机判定为可见（isVisible）");
            }
        }

        /// <summary>把包里的材质换成"游戏自己的 URP 材质克隆"，但保留包里的贴图。
        /// 目的：区分"URP 不画内置管线 shader"与"其它问题"。</summary>
        void TryApplyGameMaterials()
        {
            if (_materialMode != "game" || _materialsApplied || _instance == null) return;
            _materialsApplied = true;

            var src = FindGameMaterial();
            if (src == null) { Log("材质替换：✗ 没找到可克隆的游戏材质"); return; }
            Log($"材质替换：源材质='{src.name}' shader='{src.shader?.name}'");

            int n = 0;
            foreach (var r in _instance.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var oldMat = mats[i];
                    var clone = new Material(src);
                    if (oldMat != null)
                    {
                        var tex = oldMat.mainTexture;
                        if (tex != null)
                        {
                            if (clone.HasProperty("_BaseMap")) clone.SetTexture("_BaseMap", tex);
                            else if (clone.HasProperty("_MainTex")) clone.SetTexture("_MainTex", tex);
                            if (clone.HasProperty("_BaseColor")) clone.SetColor("_BaseColor", Color.white);
                        }
                        clone.name = "BundleProbe_" + oldMat.name;
                    }
                    mats[i] = clone;
                    n++;
                }
                r.sharedMaterials = mats;
            }
            Log($"材质替换：已替换 {n} 个材质槽（改用游戏的 shader，贴图沿用包里的）");
        }

        Material FindGameMaterial()
        {
            var mc = GameApi.FindMainCharacter();

            // ① 手里武器的**实际模型**（agent 的 gameObject，不是模板）
            if (mc != null)
            {
                var agent = mc.CurrentHoldItemAgent;
                if (agent != null)
                {
                    var m = FirstMaterial(agent.gameObject, out string tag);
                    if (m != null) { Log($"材质源①：手持模型 {tag}"); return m; }
                }
                // ② 玩家模型
                if (mc.characterModel != null)
                {
                    var m = FirstMaterial(mc.characterModel.gameObject, out string tag);
                    if (m != null) { Log($"材质源②：玩家模型 {tag}"); return m; }
                }
            }

            // ③ 场景里任一带 Soda* shader 的渲染器（含未激活对象）
            try
            {
                foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
                {
                    var m = r != null ? r.sharedMaterial : null;
                    if (m != null && m.shader != null && m.shader.name.StartsWith("Soda", StringComparison.Ordinal))
                    { Log($"材质源③：{r.name} → shader={m.shader.name}"); return m; }
                }
            }
            catch (Exception e) { Log($"材质源③失败：{e.Message}"); }

            // ④ 兜底：直接按名字找游戏 shader
            foreach (var name in new[] { "SodaCraft/SodaLit", "SodaCraft/SodaCharacter", "Universal Render Pipeline/Lit", "Standard" })
            {
                var sh = Shader.Find(name);
                if (sh != null) { Log($"材质源④：Shader.Find('{name}') ✓"); return new Material(sh); }
            }
            Log("材质源⑤：全部失败（连 Shader.Find 都找不到）");
            return null;
        }

        static Material FirstMaterial(GameObject go, out string tag)
        {
            tag = "";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && m.shader != null)
                    {
                        tag = $"{go.name}/{r.name} → shader={m.shader.name}";
                        return m;
                    }
                }
            }
            return null;
        }

        // ── 每 2 秒：保证它挂在手上 / 保持在玩家面前 ─────────────────────────
        void EnsureAttached()
        {
            if (_instance == null) return;
            var mc = GameApi.FindMainCharacter();
            if (mc == null) return;

            // attach=camera：直接放在相机前方 2m（一定在视野里，用来排除"朝向/位置猜错"）
            if (_attach == "camera")
            {
                var cam = Camera.main;
                if (cam == null) return;
                _instance.transform.SetParent(null, false);
                _instance.transform.position = cam.transform.position + cam.transform.forward * 2f;
                _instance.transform.rotation = cam.transform.rotation;
                if (_lastParent != "camera")
                {
                    _lastParent = "camera";
                    Log($"→ 已放到相机前方 2m（attach=camera）  世界坐标={_instance.transform.position}");
                    LogHeldItem();
                    LogCameraCheck();
                }
                TryApplyGameMaterials();
                return;
            }

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
            Log($"→ 已挂到 {label}（attach={_attach}）  世界坐标={_instance.transform.position}");
            LogHeldItem();
            TryApplyGameMaterials();
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
