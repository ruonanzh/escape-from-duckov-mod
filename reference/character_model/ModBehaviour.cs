// CharacterModelSwap.csproj 的 ModBehaviour.cs —— **角色替换的参考实现**。
//
// 它把玩家角色的身体换成你给的 GLB 模型（要先在 Tripo 那边绑好骨）。
// 搬运、隐藏、重申的全部逻辑在 `libs/mod-kit/`（`CharacterSwap.cs`、`CharacterRig.cs`、`GltfLoader.cs`），
// 这份文件只做三件事：读 `config.json`、找到主角色、每帧喂 Tick。
//
// 四个机制（都是实测换来的，改之前先读设计仓 `docs/unity-3d-assets/10-character-replacement-research.md` §5.5）：
//   ① 地标对齐 + 刚性搬运：只用「骨盆到头的方向」与「左右髋的方向」建坐标架。
//      不得改回逐骨重定姿态：两副骨架的骨局部朝向约定差得远（实测偏差平均 120 度），
//      逐骨会把每个部件各转一个角度，模型拧成一团。
//   ② 尺寸：按**两侧身体高度**（真实顶点跨度）等比缩放，再乘 `scale`。
//      不得用包围盒尺寸投影到上轴：上轴一倾斜，投影偏大，模型反而被缩小。
//   ③ 隐藏：钩子（`CharacterModel` 的五个方法）立刻藏，`Tick` 的逐帧窗口与每秒重申兜底。
//      物品挂点（手、背、护甲）在保留名单里，不得藏 —— 枪和背包要留着。
//   ④ 手臂：右手三根源关节由两骨 IK 对准掌心握把；左手三根骨并到 `Spine.004`。
//      手掌不主动旋转，左臂不独立摆动。

using System;
using System.IO;
using ModelKit;
using UnityEngine;

namespace CharacterModelSwap
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        string _configPath = "";
        string _modelFile = "model.glb";
        float _extraScale = 1.2f;

        long _stamp = -1;
        GltfLoader.Loaded _loaded;
        CharacterSwap.Result _result;
        Component _character;
        bool _applied;
        float _nextTry;
        string _lastReport = "";

        void Start()
        {
            _configPath = Path.Combine(ModelCache.ModDir(), "config.json");
            Reload(true);
        }

        void Update()
        {
            try
            {
                Reload(false);
                if (_loaded == null) return;

                if (!_applied)
                {
                    if (Time.unscaledTime < _nextTry) return;
                    _nextTry = Time.unscaledTime + 2f;
                    var mc = MainCharacter();
                    if (mc == null) return;                       // 还没进关卡 ⇒ 下一帧再看
                    _character = mc;
                    CharacterSwap.ExtraScale = _extraScale;
                    _result = CharacterSwap.Apply(mc, _loaded);
                    _applied = _result.Applied;
                    if (_result.Report != _lastReport)
                    {
                        Debug.Log("[CharacterModel] " + (_applied ? "APPLIED: " : "FAILED: ") + _result.Report);
                        _lastReport = _result.Report;
                    }
                    if (!_applied) return;                        // 两秒后重试；相同错误不重复写
                }

                // 重申（藏新出现的部件）+ 角色被游戏重建时自动重来。
                // 每帧调，内部自己限流；换好后的 4 秒内逐帧扫，所以没有可见延迟。
                CharacterSwap.Tick(Time.unscaledTime);
            }
            catch (Exception ex) { Debug.LogError("[CharacterModel] update threw: " + ex); }
        }

        /// <summary>主角色由游戏自己维护，不得用 FindObjectsOfType 去扫场景。</summary>
        static Component MainCharacter()
        {
            try
            {
                var lm = LevelManager.Instance;
                return lm != null ? lm.MainCharacter : null;
            }
            catch { return null; }
        }

        /// <summary>读 config.json。存盘即生效（按 mtime 与长度判断是否变过）。</summary>
        void Reload(bool first)
        {
            if (!File.Exists(_configPath))
            {
                if (first) Debug.LogWarning("[CharacterModel] no config.json at " + _configPath);
                return;
            }
            long stamp;
            try
            {
                var fi = new FileInfo(_configPath);
                stamp = fi.LastWriteTimeUtc.Ticks ^ fi.Length;
            }
            catch { return; }
            if (stamp == _stamp) return;
            _stamp = stamp;

            try
            {
                var doc = Json.Parse(ModConfig.StripJsonComments(File.ReadAllText(_configPath)));
                if (doc == null) return;
                JsonValue e = doc;
                var arr = doc["entries"];
                if (arr != null && arr.Count > 0) e = arr[0];
                // 运行时自检：条目必须是 replace_character。写错 action 时不要静默按角色处理。
                var act = e.GetStr("action", ModConfig.ReplaceCharacter);
                if (!string.Equals(act, ModConfig.ReplaceCharacter, StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"[CharacterModel] config action is '{act}', expected '{ModConfig.ReplaceCharacter}'; continuing anyway");

                _modelFile = e.GetStr("model", _modelFile);
                var sc = e["scale"];
                if (sc != null) _extraScale = (float)sc.Number;

                var path = Path.Combine(ModelCache.ModDir(), _modelFile);
                if (!File.Exists(path))
                {
                    Debug.LogError("[CharacterModel] model file not found: " + path);
                    return;
                }
                // 角色模型必须用 front=auto、zeroGrip:false 加载；否则加载期的旋转或位移会让顶点与骨架错位。
                _loaded = GltfLoader.LoadFile(path, e.GetStr("front", "auto"), zeroGrip: false);
                Debug.Log($"[CharacterModel] config: model={_modelFile} scale={_extraScale} | {_loaded.Report}");

                if (_result != null) { _result.Restore(); _result = null; }   // 有借有还：换了配置先把旧的还回去
                _applied = false;
                _nextTry = 0f;
                _lastReport = "";
            }
            catch (Exception ex) { Debug.LogError("[CharacterModel] config failed: " + ex.Message); }
        }
    }
}
