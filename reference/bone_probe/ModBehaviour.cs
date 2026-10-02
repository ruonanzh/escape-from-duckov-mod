using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BoneProbe
{
    /// <summary>
    /// 角色骨骼挂载的参考实现：把运行时新建的方块挂到角色骨骼上，并周期性打日志。
    /// 包含三件必须处理的事 —— 按骨骼名找骨骼、跟角色渲染器的层、克隆游戏现有材质。
    /// 只读游戏状态；运行时只给自己建一个方块；不改任何游戏资产。
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        const string LogPath = "/tmp/bone_probe.log";
        readonly StringBuilder _sb = new StringBuilder();
        GameObject _cube;
        Transform _parent;
        float _acc;
        int _tick;
        int _waitLogged;

        void Start()
        {
            Log($"=== BoneProbe start (t={Time.time:F1}) ===");
            TryAttach();
            Flush();
        }

        void Update()
        {
            _acc += Time.deltaTime;
            if (_acc < 0.5f) return;
            _acc = 0f;

            if (_cube == null)
            {
                TryAttach();                       // 主菜单时还没有角色——每秒再试
                if (_cube == null) _waitLogged++;
                if (_cube == null) { if (_waitLogged % 10 == 1) { Log($"waiting for character… (t={Time.time:F1})"); Flush(); } return; }
            }

            _tick++;
            Log($"#{_tick} t={Time.time:F1} parent={_parent.name} parentWorld={Vec(_parent.position)} parentRot={Vec(_parent.eulerAngles)} cubeWorld={Vec(_cube.transform.position)} cubeLocal={Vec(_cube.transform.localPosition)} active={_cube.activeInHierarchy} hasRenderer={_cube.GetComponent<Renderer>() != null}");
            if (_tick % 4 == 0) Flush();
        }

        void TryAttach()
        {
            var chars = FindObjectsOfType<CharacterMainControl>();
            if (chars == null || chars.Length == 0)
            {
                var all = Resources.FindObjectsOfTypeAll<CharacterMainControl>();
                if (all != null && all.Length > 0) chars = all;
            }
            if (chars == null || chars.Length == 0) return;

            var cmc = chars[0];
            var cm = GetModel(cmc);
            Log($"found {chars.Length} CharacterMainControl；first='{cmc.name}' model={(cm != null ? cm.name : "null")}");
            if (cm == null) return;
            Log($"model root = {Path(cm.transform)}  children = {Children(cm.transform)}");
            Log($"model descendants (first 60) = {Descendants(cm.transform, 60)}");
            var bodySmr = cm.GetComponentInChildren<SkinnedMeshRenderer>();
            Log($"body SkinnedMeshRenderer = {(bodySmr != null ? Path(bodySmr.transform) : "null")}  layer={(bodySmr != null ? bodySmr.gameObject.layer : -1)}  mat={(bodySmr != null && bodySmr.sharedMaterial != null ? bodySmr.sharedMaterial.name + "/" + bodySmr.sharedMaterial.shader.name : "null")}");
            var allRenderers = cm.GetComponentsInChildren<Renderer>();
            Log($"renderers in model = {allRenderers.Length}; layers = {RendererLayers(allRenderers)}");

            var socket = Reflect(cm, "rightHandSocket") as Transform;
            Log($"private rightHandSocket (reflection) = {(socket != null ? Path(socket) : "null")}");
            var bone = FindDeep(cm.transform, "Hand.R");
            Log($"bone 'Hand.R' = {(bone != null ? Path(bone) : "null")}");

            _parent = bone != null ? bone : socket;
            if (_parent == null) return;

            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.name = "BoneProbeCube";
            _cube.transform.SetParent(_parent, false);
            _cube.transform.localPosition = Vector3.zero;
            _cube.transform.localRotation = Quaternion.identity;
            _cube.transform.localScale = Vector3.one * 0.25f;

            // 层：跟“身体渲染器”一致（游戏是给 renderer 设专层，不是给根节点）
            var smr = cm.GetComponentInChildren<SkinnedMeshRenderer>();
            int renderLayer = smr != null ? smr.gameObject.layer : cm.gameObject.layer;
            SetLayerRecursive(_cube, renderLayer);

            // 材质：克隆身体渲染器的材质（URP 才不粉紫）
            string matInfo = "none";
            if (smr != null && smr.sharedMaterial != null)
            {
                var m = new Material(smr.sharedMaterial);
                var orange = new Color(1f, 0.25f, 0.1f, 1f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", orange);
                if (m.HasProperty("_Color")) m.SetColor("_Color", orange);
                _cube.GetComponent<Renderer>().material = m;
                matInfo = $"{smr.sharedMaterial.name} / shader={smr.sharedMaterial.shader.name} / bodyLayer={smr.gameObject.layer}";
            }

            var cam = Camera.main;
            string camInfo = "no main camera";
            if (cam != null)
            {
                bool visible = (cam.cullingMask & (1 << renderLayer)) != 0;
                camInfo = $"main cam '{cam.name}' cullingMask={cam.cullingMask} rendersLayer{renderLayer}={visible}";
            }
            Log($"cube created; parent = {Path(_parent)}; cubeLayer={renderLayer}; scale 0.25; body = {matInfo}; {camInfo}");
        }

        static CharacterModel GetModel(CharacterMainControl c)
        {
            var f = typeof(CharacterMainControl).GetField("characterModel",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return f?.GetValue(c) as CharacterModel;
        }

        static object Reflect(object o, string field)
        {
            try
            {
                var f = o.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return f?.GetValue(o);
            }
            catch { return null; }
        }

        static string RendererLayers(Renderer[] rs)
        {
            var d = new Dictionary<int, int>();
            foreach (var r in rs)
            {
                d.TryGetValue(r.gameObject.layer, out var n);
                d[r.gameObject.layer] = n + 1;
            }
            var parts = new List<string>();
            foreach (var kv in d) parts.Add($"layer{kv.Key}x{kv.Value}");
            return string.Join(" ", parts);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        static string Children(Transform t)
        {
            var names = new List<string>();
            for (int i = 0; i < t.childCount && i < 20; i++) names.Add(t.GetChild(i).name);
            return string.Join(", ", names);
        }

        static string Descendants(Transform t, int max)
        {
            var all = new List<string>();
            Collect(t, all, max);
            return string.Join(", ", all);
        }

        static void Collect(Transform t, List<string> into, int max)
        {
            if (into.Count >= max) return;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (into.Count >= max) return;
                into.Add(c.name);
                Collect(c, into, max);
            }
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            while (t != null && parts.Count < 8) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts);
        }

        static string Vec(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";

        void Log(string line)
        {
            Debug.Log("[BoneProbe] " + line);
            _sb.AppendLine(line);
        }

        void Flush()
        {
            try { File.WriteAllText(LogPath, _sb.ToString()); } catch { }
        }
    }
}
