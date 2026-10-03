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
        System.DateTime _modelStamp;
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

        // ── "新增物品"（数据层演示；模型由库里 BuildGraphicClone 提供）──
        JsonValue _newCfg;            // config.json 的 "newItem" 块
        bool _newItemDone;
        int  _newItemRetry;           // 物品库没就绪时的重试计数（只打前几次日志）
        bool _pendingGive;            // 注册已完成、还没发给玩家（等进关卡）
        string _pendingGiveMode = "drop";
        int  _pendingGiveTypeID;      // 要发的新物品 typeID
        int  _newItemTypeID;          // 已注册的新物品 typeID（用于诊断）
        ItemStatsSystem.Item _newItemTemplate;
        ItemGraphicInfo _newGraphic;
        int  _pendingGiveTries;

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
            if (!_newItemDone) TryRegisterNewItem();
            if (_pendingGive) TryGiveNewItem();

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
                // 新增物品的图形是我们注册时换好的 → 也 dump 一次它的渲染器，确认手里到底显示了什么
                if (item.TypeID == _pendingGiveTypeID || item.TypeID == _newItemTypeID)
                {
                    var g = item.ItemGraphic;
                    Log($"新物品手持诊断：ItemGraphic={(g != null ? g.gameObject.name : "null")} " +
                        $"子渲染器={(g != null ? g.GetComponentsInChildren<Renderer>(true).Length : 0)}");
                    Diagnose(item);
                }
                Flush();
            }
            if (!Matches(item)) return;

            foreach (var mr in item.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.gameObject.name.StartsWith("ModelKit_") && mr.gameObject.name.EndsWith("_mesh"))
                {
                    var m = mr.GetComponent<MeshFilter>()?.sharedMesh;
                    var sb = new StringBuilder();
                    for (int i = 0; i < (m != null ? m.subMeshCount : 0); i++) sb.Append(m.GetTriangles(i).Length / 3).Append(" ");
                    Log($"  mesh 诊断：{mr.gameObject.name} 子网格={m?.subMeshCount} 各子网格三角数=[{sb}] " +
                        $"材质槽={mr.sharedMaterials.Length} 材质={(mr.sharedMaterial != null ? mr.sharedMaterial.name : "null")} " +
                        $"贴图={(mr.sharedMaterial != null && mr.sharedMaterial.mainTexture != null ? mr.sharedMaterial.mainTexture.name : "无")} " +
                        $"顶点={m?.vertexCount} 启用={mr.enabled} 层={mr.gameObject.layer}");
                }

            _diagAcc += Time.deltaTime;
            if (_diagTicks < 2 && _diagAcc > 3f) { _diagAcc = 0; _diagTicks++; Diagnose(item); }

            if (_bound.Add(item.GetInstanceID()))
            {
                try
                {
                    bool pt = _binder.BindGraphicOnPrefab(item.TypeID);   // ① 模板（社区做法）
                    bool g = _binder.BindGraphic(item);                   // ② 活动实例的图形
                    bool cc = _binder.ClearAgentCache(item);              // ③ 清实体缓存 → 让游戏重建实体
                    bool h = _binder.InPlaceHeldSwap;                     // 可选：就地改活实体（默认关）
                    if (h) _binder.ReplaceHeld(item);
                    Log($"已换：模板={pt}｜图形={g}｜清实体缓存={cc}｜就地改活实体={h}" +
                        $"｜锚点={_binder.AnchoredAt} 材质={_binder.MaterialInfo} 枪身渲染器关掉 {_binder.HiddenRenderers} 个（保留配件槽位 {_binder.KeptConditional} 个）" +
                        $"｜场上的我们的图形 {ModelKit.ItemModelBinder.CountOurGraphics()} 个");
                    Log("锚点：" + _binder.AnchorsDebug);
                    Log("槽位：" + _binder.SlotReport);
                    if (_binder.SlotDebug.Length > 0) Log("槽位坐标：" + _binder.SlotDebug);
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

        /// <summary>新增物品（**数据层**：新 typeID/名字/数值/注册）+ 模型层（`BuildGraphicClone` 提供 itemGraphic）。
        /// 步骤照社区 mod（优香MPX）的 `RegisterNewItem`：克隆源物品 → 改身份 → 写图形 → 注册 → 发给玩家。</summary>
        void TryRegisterNewItem()
        {
            int cloneFrom = _newCfg["cloneFrom"]?.AsInt(0) ?? 0;
            int typeID    = _newCfg["typeID"]?.AsInt(0) ?? 0;
            string name   = _newCfg["displayName"]?.AsString("NewItem") ?? "NewItem";
            string giveMode = _newCfg["give"]?.AsString("drop") ?? "drop";   // drop | pickup | false

            if (cloneFrom <= 0 || typeID <= 0) { Log("新增物品：cloneFrom/typeID 必须 > 0"); _newItemDone = true; return; }

            ItemStatsSystem.Item src = null;
            try { src = ItemStatsSystem.ItemAssetsCollection.GetPrefab(cloneFrom); } catch { }
            if (src == null)
            {
                _newItemRetry++;
                if (_newItemRetry <= 3 || _newItemRetry % 600 == 0) Log($"新增物品：源物品 {cloneFrom} 还没就绪，重试中（第 {_newItemRetry} 次）");
                return;
            }

            try
            {
                // 数据层：库里的 ItemFactory 内置了"四条每次都必须做对的事"
                // （DontDestroyOnLoad / 名字走本地化 / useSpriteForPickup=false / 枪自动打 IsGun）
                var item = ItemFactory.CloneAsNewItem(cloneFrom, typeID, name);
                if (item == null) { Log("新增物品：ItemFactory.CloneAsNewItem 返回 null"); _newItemDone = true; return; }
                Log($"新增物品：克隆自 {cloneFrom}（{src.DisplayName}）→ typeID={item.TypeID} 名字={item.DisplayName} " +
                    $"图标={(item.Icon != null ? "有" : "无")} IsGun={ItemFactory.LooksLikeGun(src)} 常驻=True");

                var g = _binder.BuildGraphicClone(cloneFrom);
                _newGraphic = g;
                _newItemTemplate = item;
                if (g == null) { Log("新增物品：BuildGraphicClone 返回 null（源物品没有 itemGraphic？）"); _newItemDone = true; return; }
                if (!_binder.WriteGraphicTo(item, g)) { Log("新增物品：反射写 itemGraphic 失败（游戏里会退化成纸片）"); _newItemDone = true; return; }
                Log($"新增物品：图形已挂上（{g.gameObject.name}，渲染器 {g.GetComponentsInChildren<Renderer>(true).Length} 个）");

                bool ok;
                try { ok = ItemStatsSystem.ItemAssetsCollection.AddDynamicEntry(item); }
                catch (System.Exception e) { Log("新增物品：AddDynamicEntry 异常 " + e.Message); _newItemDone = true; return; }
                if (!ok) { Log("新增物品：AddDynamicEntry 返回 false（typeID 冲突？）"); _newItemDone = true; return; }
                Log($"已注册新物品：{name}  typeID={typeID}");

                // 自检：新物品在库里能拿到、且图形指针是我们挂上去的
                var back = ItemStatsSystem.ItemAssetsCollection.GetPrefab(typeID);
                Log($"自检：GetPrefab({typeID})={(back != null)}  图形={(back != null && back.ItemGraphic != null)}  可手持={(back != null && back.HasHandHeldAgent)}");

                _newItemTypeID = typeID;
                if (giveMode != "false" && giveMode.Length > 0) { _pendingGive = true; _pendingGiveTypeID = typeID; _pendingGiveMode = giveMode; _pendingGiveTries = 0; }
                _newItemDone = true;
            }
            catch (System.Exception e) { Log("新增物品异常：" + e); _newItemDone = true; }
            Flush();
        }

        /// <summary>把新物品发给玩家：注册发生在**进关卡之前**，那时还没有玩家 → 每帧重试到玩家出现。
        /// `PickupItem` 成功=进背包/插槽；失败=掉在脚下（所以拿到 false 时看看地上）。</summary>
        void TryGiveNewItem()
        {
            if (GameApi.FindMainCharacter() == null)
            {
                _pendingGiveTries++;
                if (_pendingGiveTries <= 3 || _pendingGiveTries % 600 == 0)
                    Log($"发新物品：玩家还没进关卡，等待中（第 {_pendingGiveTries} 帧）");
                Flush();
                return;
            }
            var player = GameApi.FindMainCharacter();
            var inst = ItemStatsSystem.ItemAssetsCollection.InstantiateSync(_pendingGiveTypeID);
            if (inst == null) { Log("发新物品：InstantiateSync 返回 null"); _pendingGive = false; Flush(); return; }
            Log($"发新物品：实例 typeID={inst.TypeID} name={inst.name} 模板同一对象={ReferenceEquals(inst, _newItemTemplate)} " +
                $"图形=({(inst.ItemGraphic != null ? inst.ItemGraphic.gameObject.name : "null")}) 我们要的图形=({(_newGraphic != null ? _newGraphic.gameObject.name : "null")})");
            // 手持实体：新增物品没有它 → 拿在手里是"世界姿势"
            bool hand = GameApi.EnsureHandheldAgent(inst, _newGraphic != null ? _newGraphic.gameObject : null);
            Log($"发新物品：手持实体={hand}");
            if (inst.ItemGraphic == null && _newGraphic != null)
            {
                bool w = _binder.WriteGraphicTo(inst, _newGraphic);
                Log($"发新物品：实例图形为空 → 兜底再写一次 = {w}（现在 {((inst.ItemGraphic != null) ? inst.ItemGraphic.gameObject.name : "仍为 null")}）");
            }
            if (_pendingGiveMode == "pickup")
            {
                bool got = GameApi.GiveItemToPlayer(inst);
                Log($"新物品：已尝试放进背包/插槽 → {got}（false 时会被掉在脚下）");
            }
            else
            {
                var agent = ItemExtensions.Drop(inst, player.transform.position, true,
                                                player.transform.forward, 45f);   // 掉在脚边（顺便验证地面模型）
                Log($"新物品：已掉在脚边 → agent={(agent != null ? agent.name : "null")} 位置={(agent != null ? agent.transform.position.ToString("0.##") : "-")}");
            }
            _pendingGive = false;
            Flush();
        }

        void ReloadIfChanged(bool force = false)
        {
            try
            {
                var stamp = File.Exists(_configPath) ? File.GetLastWriteTime(_configPath) : System.DateTime.MinValue;
                string mfNow = (_cfg?["model"]?.AsString("") ?? "models/smg_compact.json");
                var mp = Path.Combine(ModelLoader.ModDir(), mfNow);
                var mstamp = File.Exists(mp) ? File.GetLastWriteTime(mp) : System.DateTime.MinValue;
                if (!force && stamp == _configStamp && mstamp == _modelStamp) return;
                _configStamp = stamp; _modelStamp = mstamp;

                _cfg = File.Exists(_configPath) ? Json.Parse(File.ReadAllText(_configPath)) : null;
                _newCfg = _cfg?["newItem"];
                if (_newCfg == null || _newCfg.Kind == JsonKind.Null) _newItemDone = true;   // 没配 = 不做
                else { _newItemDone = false; _newItemRetry = 0; }
                string mf = _cfg?["model"]?.AsString("models/pistol_compact.json") ?? "models/pistol_compact.json";
                _bindNew = _cfg?["bindNew"] != null && _cfg["bindNew"].Bool;
                _match = _cfg?["match"]?.AsString("") ?? "";
                _typeIds.Clear();
                var ids = _cfg?["typeIDs"];
                if (ids != null && ids.IsArray) for (int i = 0; i < ids.Count; i++) _typeIds.Add(ids[i].AsInt());
                _lastHeld = null;
                _spec = ModelLoader.LoadPartsSpec(Path.Combine(ModelLoader.ModDir(), mf));
                _binder = new ItemModelBinder(_spec);
                // 只有 config 里显式写了才覆盖（否则用库的默认 true —— 开局那份必须就地换，游戏不会自己重建）
                if (_cfg?["inPlaceHeldSwap"] != null) _binder.InPlaceHeldSwap = _cfg["inPlaceHeldSwap"].Bool;
                if (_cfg?["debugMarkers"] != null) _binder.DebugMarkers = _cfg["debugMarkers"].Bool;
                _boundNew.Clear();
                Log($"配置：模型={mf}（{_spec.Parts.Count} 零件）bindNew={_bindNew} pivot={_spec.PivotOffset[0]},{_spec.PivotOffset[1]},{_spec.PivotOffset[2]}｜mesh {_binder.Mesh.vertexCount} 顶点");
            }
            catch (System.Exception e) { Log("配置/模型载入失败: " + e.Message); _binder = null; }
        }

        float _diagAcc; int _diagTicks; int _diagListed;

        /// <summary>精确诊断：只看"手里那个实体"和"它的渲染器"，把每个渲染器的真实状态打出来。</summary>
        void Diagnose(ItemStatsSystem.Item item)
        {
            DumpOurs();
            var active = item != null ? item.ActiveAgent : null;
            if (active == null) { Log("── 诊断：没有 ActiveAgent"); Flush(); return; }
            if (_binder != null) Log("挂点诊断：" + _binder.DescribeSlots(active.transform));

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

        void DumpOurs()
        {
            int n = 0;
            foreach (var g in FindObjectsOfType<ItemGraphicInfo>())
            {
                if (g == null || !g.name.StartsWith("ModelKit_")) continue;
                n++;
                var rs = g.GetComponentsInChildren<Renderer>(true);
                Log($"   ★我们的图形 '{TPath(g.transform)}'｜渲染器 {rs.Length} 个");
                foreach (var r in rs)
                {
                    if (rs.Length > 8 && !r.enabled) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    Log($"       {(r.enabled ? "启用" : "关闭")} '{r.gameObject.name}' 顶点={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0)} " +
                        $"世界尺寸={r.bounds.size.magnitude:0.####}m 层={r.gameObject.layer} 活动={r.gameObject.activeInHierarchy} " +
                        $"缩放={r.transform.lossyScale} 材质={(r.sharedMaterial != null ? r.sharedMaterial.name : "无")}");
                }
            }
            // 手持实体若是我们的克隆，也打一遍
            var mc = Object.FindObjectsOfType<ItemStatsSystem.ItemAgent>();
            foreach (var a in mc)
                if (a != null && a.gameObject.name.StartsWith("ModelKit_")) Log($"   ★我们的实体 '{TPath(a.transform)}' 渲染器 {a.GetComponentsInChildren<Renderer>(true).Length} 个");
            Log($"   （ModelKit_ 图形共 {n} 个；ITemAgent 里我们的 {mc.Count(a => a != null && a.gameObject.name.StartsWith("ModelKit_"))} 个）");
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
