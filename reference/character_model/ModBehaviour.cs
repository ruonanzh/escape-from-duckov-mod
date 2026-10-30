// CharacterModelSwap.csproj 的 ModBehaviour.cs —— **角色替换的参考实现**。
//
// 它把玩家角色的身体换成你给的 GLB 模型（要先在 Tripo 那边绑好骨）。
// 搬运、隐藏、重申的全部逻辑在 `libs/mod-kit/`（`CharacterSwap.cs`、`CharacterRig.cs`、`GltfLoader.cs`），
// 这份文件只做三件事：读 `config.json`、找到主角色、每帧喂 Tick。
//
// 四个机制：
//   ① 地标对齐：只用「骨盆到头的方向」与「左右髋的方向」确定身体整体旋转与位置。
//   ② 尺寸：按两侧身体高度的真实顶点跨度等比缩放，再乘 `scale`。
//   ③ 隐藏：钩子立刻藏，`Tick` 兜底；手、背、护甲挂点下的物品保留。
//   ④ 动画：CharacterRetarget 保留源骨架与源权重，在 LateUpdate 重定向游戏骨骼的旋转增量。

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
                    var mc = MainCharacter();
                    if (mc == null) return;                       // 还没进关卡 ⇒ 下一帧再看
                    _character = mc;
                    CharacterSwap.ExtraScale = _extraScale;
                    _result = CharacterSwap.Apply(mc, _loaded);
                    _applied = _result.Applied;
                    Debug.Log("[CharacterModel] " + (_applied ? "APPLIED: " : "FAILED: ") + _result.Report);
                    if (!_applied) return;                        // 失败不要每帧重试刷日志
                }

                // 重申（藏新出现的部件）+ 角色被游戏重建时自动重来。
                // 每帧调，内部自己限流；换好后的十几秒内逐帧扫，所以没有可见延迟。
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
            }
            catch (Exception ex) { Debug.LogError("[CharacterModel] config failed: " + ex.Message); }
        }
    }
}
