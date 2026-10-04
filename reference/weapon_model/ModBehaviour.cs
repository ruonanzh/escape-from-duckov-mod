// WeaponModel（示例 mod）—— 把某把武器的模型换成你给的 GLB。
//
// 这就是"替换武器模型"的**完整正确做法**：整个 mod 只有这一份配置 + 一句调用。
//
// config.json：
//   { "target": "MP5",  "model": "gun.glb" }
//     target = 武器名的一段（大小写不敏感；例 MP5 会匹配 SMG_MP5_Normal）；也可用 "typeIDs": [655]
//     model  = 放在本 mod 目录里的 GLB 文件（相对路径或绝对路径）
//
// 运行时会做（都在 libs/mod-kit 里，见 WeaponModel.cs / GltfLoader.cs / GameApi.cs）：
//   读 GLB → 建 Mesh+贴图（坐标系/绕序/UV 转换 + 握把归零）
//   → 找到你手里那把枪 → 关掉旧模型零件 → 挂上我们的模型 → 对齐 → 克隆游戏材质 + 换贴图

using System;
using System.Collections.Generic;
using System.IO;
using ModelKit;
using UnityEngine;

namespace WeaponModelSwap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        string _configPath;
        string _target = "";
        string _modelFile = "";
        readonly HashSet<int> _typeIds = new HashSet<int>();

        Mesh _mesh;
        Texture2D _texture;
        CharacterMainControl _player;
        float _nextFindPlayer;
        readonly Dictionary<int, GameObject> _applied = new Dictionary<int, GameObject>();
        string _lastHeld;

        void Start()
        {
            _configPath = Path.Combine(ModelLoaderDir(), "config.json");
            ReadConfig();
            Debug.Log($"[WeaponModel] 目标='{_target}' typeIDs=[{string.Join(",", _typeIds)}] 模型='{_modelFile}'");
            LoadModel();          // 启动就读好模型 → 玩家掏出枪时能**立刻**换（不然会先看到原模型 ✗）
        }

        void Update()
        {
            if (_mesh == null) return;

            // 找玩家较重（FindObjectsOfType）→ 只在没有/每 2 秒找一次；**每帧**只看"手里拿的是什么"（廉价 ✓）
            if (_player == null && Time.unscaledTime >= _nextFindPlayer)
            {
                _nextFindPlayer = Time.unscaledTime + 2f;
                _player = GameApi.FindMainCharacter();
            }
            if (_player == null) return;
            var agent = _player.CurrentHoldItemAgent;
            var item = agent != null ? agent.Item : null;
            if (item == null || agent == null || agent.gameObject == null) return;

            if (item.name != _lastHeld)
            {
                _lastHeld = item.name;
                Debug.Log($"[WeaponModel] 手里的物品：'{item.name}'（typeID={item.TypeID}）→ {(Matches(item) ? "命中 ✓ 会换" : "不命中 —— 不换")}");
            }
            if (!Matches(item)) return;

            var root = agent.gameObject.transform;
            int id = root.GetInstanceID();
            if (_applied.TryGetValue(id, out var go) && go != null) return;      // 换过、还在

            var r = ModelKit.WeaponModel.Apply(root, _mesh, _texture);
            if (r.Applied) { _applied[id] = r.Instance; Debug.Log("[WeaponModel] " + r.Report); }
            else Debug.LogWarning("[WeaponModel] 没换成：" + r.Report);
        }

        bool Matches(ItemStatsSystem.Item item)
        {
            if (string.IsNullOrEmpty(_target) && _typeIds.Count == 0) return false;
            if (_typeIds.Count > 0 && _typeIds.Contains(item.TypeID)) return true;
            if (!string.IsNullOrEmpty(_target) && item.name != null &&
                item.name.IndexOf(_target, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        void LoadModel()
        {
            string path = _modelFile;
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[WeaponModel] config 里没写 model"); return; }
            if (!Path.IsPathRooted(path)) path = Path.Combine(ModelLoaderDir(), path);
            if (!File.Exists(path)) { Debug.LogWarning($"[WeaponModel] 找不到模型文件：{path}"); return; }
            try
            {
                var g = GltfLoader.LoadFile(path);
                _mesh = g.Mesh; _texture = g.MainTexture;
                Debug.Log("[WeaponModel] " + g.Report);
            }
            catch (Exception e) { Debug.LogError($"[WeaponModel] 读模型失败：{e.Message}"); }
        }

        void ReadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) { Debug.LogWarning($"[WeaponModel] 没有 config.json：{_configPath}"); return; }
                var cfg = Json.Parse(File.ReadAllText(_configPath));
                _target = cfg["target"].AsString("");
                _modelFile = cfg["model"].AsString("");
                var ids = cfg["typeIDs"];
                if (ids != null && ids.IsArray) for (int i = 0; i < ids.Count; i++) _typeIds.Add(ids[i].AsInt(0));
            }
            catch (Exception e) { Debug.LogError($"[WeaponModel] 读 config 失败：{e.Message}"); }
        }

        static string ModelLoaderDir()
        {
            try { return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location); }
            catch { return "."; }
        }
    }
}
