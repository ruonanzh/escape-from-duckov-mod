// CubePerson —— 用 YSM 模型替换角色外观（玩家 / 指定 NPC）。
//
// 逻辑都在库里：ModelLoader（读模型 → 建几何）+ CharacterModelReplacer（挂到骨骼、替换本体、保留装备）。
// 改 config.json 就能换目标，不用重新编译：
//   target: "player" | "npc"    match: NPC 的模型名/对象名片段（target=npc 时用）    replaceBody: true=替换 / false=只增加

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

        readonly StringBuilder _sb = new StringBuilder();
        string _configPath;
        System.DateTime _configStamp;
        ModelKit.JsonValue _cfg;

        YsmModel _ysm;
        string _modelFile;
        CharacterModelReplacer _replacer;
        int _lastHidden = -1, _lastKept = -1;

        void Start()
        {
            _configPath = Path.Combine(ModelLoader.ModDir(), "config.json");
            Log("=== CubePerson start ===");
            ReloadIfChanged(force: true);
            Flush();
        }

        void Update()
        {
            ReloadIfChanged();
            if (_replacer == null) return;

            var who = PickTarget();
            if (who != null && (!_replacer.Attached || _replacer.HiddenBody == 0))
            {
                bool wasAttached = _replacer.Attached;
                bool ok = _replacer.Attach(who);
                if (ok && !wasAttached) Log($"挂上 '{who.name}'（模型={GameApi.GetModel(who)?.name}）；方块 {_replacer.CubeCount}，骨骼 {_replacer.AttachedBones.Count}，缺 {_replacer.MissingBones.Count}" +
                            $"；材质源={(_replacer.MaterialSource != null ? _replacer.MaterialSource.name + "/" + _replacer.MaterialSource.shader.name : "无(会用兜底白模)")}");
                Flush();
            }

            _replacer.Tick();
            if (_replacer.ReplaceBody && (_replacer.HiddenBody != _lastHidden || _replacer.KeptEquipment != _lastKept))
            {
                _lastHidden = _replacer.HiddenBody; _lastKept = _replacer.KeptEquipment;
                Log($"本体已关={_lastHidden} 装备保留={_lastKept}");
                Flush();
            }
        }

        bool _listed;

        void ListCharactersOnce()
        {
            if (_listed) return;
            var all = GameApi.AllCharacters();
            if (all.Length == 0) return;
            _listed = true;
            Log($"场上 {all.Length} 个角色（可用于 config.json 的 match）：");
            foreach (var c in all)
            {
                var m = GameApi.GetModel(c);
                Log($"   · name='{c.name}' model={(m != null ? m.name : "null")} 玩家={c.IsMainCharacter}");
            }
            Flush();
        }

        CharacterMainControl PickTarget()
        {
            string target = _cfg?["target"]?.AsString("player") ?? "player";
            if (target == "player") return GameApi.FindMainCharacter();

            string match = _cfg?["match"]?.AsString("") ?? "";
            ListCharactersOnce();
            return GameApi.AllCharacters().FirstOrDefault(c =>
            {
                if (c == null || c.IsMainCharacter) return false;
                if (string.IsNullOrEmpty(match)) return true;
                var m = GameApi.GetModel(c);
                return (m != null && m.name.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || c.name.IndexOf(match, System.StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        void ReloadIfChanged(bool force = false)
        {
            try
            {
                var stamp = File.Exists(_configPath) ? File.GetLastWriteTime(_configPath) : System.DateTime.MinValue;
                if (!force && stamp == _configStamp) return;
                _configStamp = stamp;

                _cfg = File.Exists(_configPath) ? Json.Parse(File.ReadAllText(_configPath)) : null;
                string mf = _cfg?["model"]?.AsString("models/npc_duck.json") ?? "models/npc_duck.json";
                bool replace = !(_cfg?["replaceBody"] != null && !_cfg["replaceBody"].Bool);

                if (_replacer != null) _replacer.Detach();
                _modelFile = Path.Combine(ModelLoader.ModDir(), mf);
                _ysm = ModelLoader.LoadYsm(_modelFile);
                _replacer = new CharacterModelReplacer(_ysm) { ReplaceBody = replace };

                Log($"配置：模型={mf}（{_ysm.Bones.Count} 骨骼）目标={_cfg?["target"]?.AsString("player")} match='{_cfg?["match"]?.AsString("")}' 替换本体={replace}");
                _lastHidden = -1; _lastKept = -1;
            }
            catch (System.Exception e) { Log("配置/模型载入失败: " + e.Message); _replacer = null; }
        }

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
