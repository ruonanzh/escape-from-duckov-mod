// ItemSwap —— 物品换模型 demo：把手里/地上的物品模型换成我们用零件清单拼出来的模型。
//
// 逻辑在库里（ItemModelBinder）：替换（换掉已实例化模型的渲染器）/ 新增（造 ItemGraphicInfo 绑给物品）。
// config.json：
//   { "model": "models/pistol_compact.json", "bindNew": false }
//   bindNew=true 时还会走"新增"那条路（反射写 Item.itemGraphic 并回读校验）。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModelKit;
using UnityEngine;

namespace ItemSwap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        const string LogPath = "/tmp/item_swap.log";

        readonly StringBuilder _sb = new StringBuilder();
        string _configPath;
        System.DateTime _configStamp;
        JsonValue _cfg;

        ModelSpec _spec;
        ItemModelBinder _binder;
        bool _bindNew;
        readonly HashSet<int> _bound = new HashSet<int>();
        string _match = "";
        readonly HashSet<int> _typeIds = new HashSet<int>();
        string _lastHeld;
        readonly HashSet<int> _boundNew = new HashSet<int>();
        bool _loggedFirst;

        void Start()
        {
            _configPath = Path.Combine(ModelLoader.ModDir(), "config.json");
            Log("=== ItemSwap start ===");
            ReloadIfChanged(force: true);
            Flush();
        }

        void Update()
        {
            ReloadIfChanged();
            if (_binder == null) return;

            var player = GameApi.FindMainCharacter();
            if (player == null) return;

            var agent = player.CurrentHoldItemAgent;
            var item = agent != null ? agent.Item : null;
            if (item == null) return;

            if (item.name != _lastHeld)
            {
                _lastHeld = item.name;
                bool hit = Matches(item);
                Log($"手里的物品：'{item.name}'（typeID={item.TypeID}）→ 过滤 {(_match.Length > 0 ? "match=" + _match : "无")}{(_typeIds.Count > 0 ? " typeIDs=" + string.Join(",", _typeIds) : "")}：{(hit ? "命中 ✓ 会换" : "不命中 —— 不换")}");
                Flush();
            }
            if (!Matches(item)) return;

            _diagAcc += Time.deltaTime;
            if (_diagTicks < 2 && _diagAcc > 3f) { _diagAcc = 0; _diagTicks++; Diagnose(item); }

            if (_bound.Add(item.GetInstanceID()))
            {
                try
                {
                    bool pt = _binder.BindGraphicOnPrefab(item.TypeID);   // 先改模板 → 之后的实例天生就对
                    bool g = _binder.BindGraphic(item);
                    bool h = _binder.ReplaceHeld(item);
                    Log($"已换：模板(prefab)={pt}｜图形(实例)={g}｜手持实体={h}" +
                        $"｜锚点={_binder.AnchoredAt} 材质={_binder.MaterialInfo} 枪身渲染器关掉 {_binder.HiddenRenderers} 个（保留配件槽位 {_binder.KeptConditional} 个）｜贴合缩放 {_binder.FitScale:0.##}" +
                        $"｜场上的我们的图形 {ModelKit.ItemModelBinder.CountOurGraphics()} 个");
                }
                catch (System.Exception e) { Log("换模型异常: " + e); }
                Flush();
            }

            if (_bindNew && _boundNew.Add(item.GetInstanceID()))
            {
                bool ok = _binder.BindGraphic(item);
                Log($"新增/替换图形（反射写 Item.itemGraphic）：{(ok ? "写入并回读一致 ✓" : "失败 ✗")}");
                Flush();
            }
        }

        int _changed;

        bool Matches(ItemStatsSystem.Item item)
        {
            if (_typeIds.Count > 0 && !_typeIds.Contains(item.TypeID)) return false;
            if (_match.Length == 0) return true;
            return item.name.IndexOf(_match, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void ReloadIfChanged(bool force = false)
        {
            try
            {
                var stamp = File.Exists(_configPath) ? File.GetLastWriteTime(_configPath) : System.DateTime.MinValue;
                if (!force && stamp == _configStamp) return;
                _configStamp = stamp;

                _cfg = File.Exists(_configPath) ? Json.Parse(File.ReadAllText(_configPath)) : null;
                string mf = _cfg?["model"]?.AsString("models/pistol_compact.json") ?? "models/pistol_compact.json";
                _bindNew = _cfg?["bindNew"] != null && _cfg["bindNew"].Bool;
                _match = _cfg?["match"]?.AsString("") ?? "";
                _typeIds.Clear();
                var ids = _cfg?["typeIDs"];
                if (ids != null && ids.IsArray) for (int i = 0; i < ids.Count; i++) _typeIds.Add(ids[i].AsInt());
                _lastHeld = null;
                _spec = ModelLoader.LoadPartsSpec(Path.Combine(ModelLoader.ModDir(), mf));
                _binder = new ItemModelBinder(_spec);
                _boundNew.Clear();
                Log($"配置：模型={mf}（{_spec.Parts.Count} 零件）bindNew={_bindNew}｜建成 mesh {_binder.Mesh.vertexCount} 顶点");
            }
            catch (System.Exception e) { Log("配置/模型载入失败: " + e.Message); _binder = null; }
        }

        float _diagAcc; int _diagTicks; int _diagListed;

        /// <summary>精确诊断：只看"手里那个实体"和"它的渲染器"，把每个渲染器的真实状态打出来。</summary>
        void Diagnose(ItemStatsSystem.Item item)
        {
            var active = item != null ? item.ActiveAgent : null;
            if (active == null) { Log("── 诊断：没有 ActiveAgent"); Flush(); return; }

            var path = TPath(active.transform);
            bool ours = active.gameObject.name.StartsWith("ModelKit_");
            var rs = active.GetComponentsInChildren<MeshRenderer>(true);
            int enabledCount = rs.Count(r => r.enabled);
            Log($"── 手持实体 '{(ours ? "★我们的克隆" : "原版")}' {path}｜MeshRenderer {rs.Length} 个（启用 {enabledCount}）");
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                int vc = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0;
                var mat = r.sharedMaterial;
                string tex = "无";
                if (mat != null)
                {
                    if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null) tex = "_MainTex=" + mat.GetTexture("_MainTex").name;
                    else if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) tex = "_BaseMap=" + mat.GetTexture("_BaseMap").name;
                }
                Log($"   {(r.enabled ? "启用" : "关闭")} '{r.gameObject.name}' 顶点={vc} 世界尺寸={r.bounds.size.magnitude:0.###}m " +
                    $"层={r.gameObject.layer} 活动={r.gameObject.activeInHierarchy} 材质={(mat != null ? mat.name + "/" + mat.shader.name : "无")} 贴图={tex}");
            }
            Flush();
        }

        static string TPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null && parts.Count < 5) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts);
        }

        void Log(string s)
        {
            Debug.Log("[ItemSwap] " + s);
            _sb.AppendLine(s);
        }

        void Flush()
        {
            try { File.WriteAllText(LogPath, _sb.ToString()); } catch { }
        }
    }
}
