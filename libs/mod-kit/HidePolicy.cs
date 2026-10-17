// HidePolicy.cs —— 「关掉旧外观」的唯一判据（武器与物品共用）。
//
// 为什么要有这一个文件：同一套判断在 `WeaponModel`（替换枪）与 `ItemModel`（替换物品 / 新建）
// 各写过一遍，于是分歧过两次：
//   · 一边按名字挑（`WPN_`），另一边全关 —— 13 把枪关不掉、配件却被全关
//   · 12 处 `ShowIf_*` / `HideIf_*` 到底是“旧枪自带的外观”还是“配件本身”被搞混
// 判据统一到这里，改一次两边都变。
//
// 保留三类（其余全关）：
//   1. 我们自己挂的（`ModelKit_*`；旧版武器实例叫 `WeaponModel`，一并认）
//   2. 灯与特效（`MuzzleFlash` / `Particle` / `Light` / …）
//   3. **槽位子树**：配件的模型是 `ItemGraphicInfo.OnSlotContentChanged` 里
//      `CreateAGraphic(content, socketPoint)` 现造并挂到 socket 下的，属运行时产物
// `ShowIf_*` / `HideIf_*` 是 prefab 自带的“装了/没装”两块外观（`showIfPluged` / `hideIfPluged`），
// 替换时**一并关**。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    public static class HidePolicy
    {
        static readonly string[] SlotNames = { "Scope", "Tec", "Muzzle", "Stock", "Grip" };

        static readonly string[] EffectWords =
        {
            "MuzzleFlash", "Particle", "Light", "Glow", "Point", "Fx", "VFX", "Flash", "Trail",
        };

        /// <summary>我们自己挂上去的实例（`ModelKit_*`；旧版武器实例的固定名字是 `WeaponModel`）。</summary>
        public static bool IsOurs(string name)
            => !string.IsNullOrEmpty(name)
               && (name.StartsWith("ModelKit_", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "WeaponModel", StringComparison.OrdinalIgnoreCase));

        /// <summary>灯/特效：它们不属于旧外观，是运行时挂的。</summary>
        public static bool IsEffectLike(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < EffectWords.Length; i++)
                if (name.IndexOf(EffectWords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool IsSlotName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < SlotNames.Length; i++)
                if (string.Equals(name, SlotNames[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>找槽位点：优先 `Sockets/<槽位>`；没有 `Sockets` 容器时退化成“名字正好等于槽位名”。
        /// 只用于**排除**（这些点下的渲染器是配件实例），不用于挑锚点。</summary>
        public static HashSet<Transform> Sockets(Transform root)
        {
            var set = new HashSet<Transform>();
            if (root == null) return set;
            var underContainer = new HashSet<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsSlotName(t.gameObject.name)) continue;
                var p = t.parent;
                if (p != null && string.Equals(p.gameObject.name, "Sockets", StringComparison.OrdinalIgnoreCase))
                {
                    underContainer.Add(t);
                    set.Add(t);
                }
            }
            if (underContainer.Count > 0) return set;                    // 有容器就只认容器里的
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (IsSlotName(t.gameObject.name) && !set.Contains(t)) set.Add(t);
            return set;
        }

        /// <summary>`t` 自己或某个祖先在 `roots` 里。</summary>
        public static bool IsUnderAny(Transform t, HashSet<Transform> roots)
        {
            if (roots == null || roots.Count == 0) return false;
            for (var c = t; c != null; c = c.parent) if (roots.Contains(c)) return true;
            return false;
        }

        /// <summary>关掉旧外观：把被关掉的渲染器追加到 `hiddenOut`（调用方拿来 `Restore()`）。
        /// 返回被关掉的个数；`keptUnderSocket` / `keptEffect` 是保留的分类计数（日志与验收用）。</summary>
        public static int Hide(Transform root, List<Renderer> hiddenOut, out int keptUnderSocket, out int keptEffect)
        {
            keptUnderSocket = 0; keptEffect = 0;
            int hidden = 0;
            if (root == null) return 0;
            var sockets = Sockets(root);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var n = r.gameObject.name;
                if (IsOurs(n)) continue;
                if (IsUnderAny(r.transform, sockets)) { keptUnderSocket++; continue; }
                if (IsEffectLike(n)) { keptEffect++; continue; }
                if (r.enabled)
                {
                    r.enabled = false;
                    hidden++;
                    if (hiddenOut != null) hiddenOut.Add(r);
                }
            }
            return hidden;
        }
    }
}
