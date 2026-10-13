// PatchRegistry.cs —— **共用登记表**：记下"哪些层我们打过补丁"。
//
// 为什么单独一个文件：`WeaponModel`（枪 / 近战）与 `ItemModel`（装备 / 物品）是**平级**的两条路，
// 但它们**必须共用同一张表** —— 一件东西可能两边都碰，谁先挂上另一个就得看得见，
// 否则扫描会把同一个实例**再挂一次**（实测：配件消失）。
//
// 原先这张表住在 `ItemModel` 里，于是 `WeaponModel` 必须依赖 `ItemModel` ✗ —— 平级互相依赖，
// 三个后果：
//   ① 只用武器那条路的 mod 也必须编译 `ItemModel.cs`（实测：ak103 那个 mod 编不过）
//   ② 将来 `ItemModel` 要反过来用 `WeaponModel` 的一点东西就成循环依赖
//   ③ 依赖方向是任意的（谁先写就先挂在谁身上）
// ⇒ 抽到这里，两边都依赖它，互相不依赖 ✓（2026-10-13 重构）

using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    /// <summary>共用登记表：我们 patch 过的层 + 打在这些层上的标记。</summary>
    public static class PatchRegistry
    {
        /// <summary>标记"这一层已经换成我们的了"。
        /// 必须挂在**被处理的那一层自己身上** —— 这样才能区分：
        /// · 图形层（我们改过 它的**克隆**也会带着这个标记，于是 不会重复挂）
        /// · agent 层（手里那层 独立处理 它自己的图标精灵才会被关掉 以前漏了）</summary>
        public sealed class Marked : MonoBehaviour { }

        /// <summary> 已经挂过的**层**（按该层自己的 `instanceID`）。
        /// <para>为什么不看 `Marked`：我们 patch 过的 **prefab** 会把标记**遗传给所有实例**
        /// 于是 看标记会把"带遗传标记、但没挂过"的副本误跳过 （掉落物 / 穿戴实例）。</para>
        /// <para>为什么不看"这一层下的 `ModelKit_*`"：我们的 mesh 是挂在**挂点**（`PickMount` 的结果）
        /// 下面，**不是**这一层的直接子物体，于是 子物体扫描同样会漏，于是 变成**每轮重复挂**
        /// （诊断日志实锤：同名 mesh 每 0.5 秒多一个）。</para>
        /// <para>⇒ 唯一可靠做法 = **登记表**：挂上时记下这一层，`Restore()` 时注销 </para></summary>
        static readonly HashSet<GameObject> _patchedLayers = new HashSet<GameObject>();
        static int _patchedPruneAt;

        /// <summary> 为什么存 **GameObject 引用**而不是 `instanceID`：
        /// Unity 对象销毁后，它的 instanceID **可能被新对象复用**，于是 按 id 存会"认错人"
        /// （新对象被当成"已挂过"，于是 永远不挂）。按引用存：新对象是**另一个引用** 不受影响。</summary>
        /// <summary> 这一层我们挂过没有 （给"场上实例补挂"用 判据唯一）</summary>
        public static bool IsPatched(GameObject layer) => layer != null && _patchedLayers.Contains(layer);

        /// <summary> 登记"我们在这层挂过" （**别的能力**（如武器侧）挂完也要调它
        /// —— 否则共用登记表里没有它，于是 扫描会把同一个实例**再挂一次** 实测：配件消失）</summary>
        public static void Mark(GameObject layer) { if (layer != null) _patchedLayers.Add(layer); }

        /// <summary>注销 （`Restore()` 时用 有借有还）</summary>
        public static void Unmark(GameObject layer) { if (layer != null) _patchedLayers.Remove(layer); }

        /// <summary> 这一层（含**整棵子树**）里有没有我们挂的 mesh。
        /// <para>用途：跳过"**我们自己造的/已经带我们 mesh 的**图形"
        /// （糖果那种"我们造的图形" 它的实例天生带我们的 mesh，于是 不得再补一次
        /// 否则 `PickMount` 会把**我们的 mesh** 当成"旧外观"关掉，于是 物品直接消失 实测）
        /// 只对"图形实例"用：agent 身上我们挂的 mesh 叫 `WeaponModel` 不是 `ModelKit_*`
        /// 于是 所以 agent 不会被这条误跳过 （它靠登记表）</para></summary>
        public static bool HasOurMeshUnder(Transform t)
        {
            if (t == null) return false;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name.StartsWith("ModelKit_")) return true;
                if (HasOurMeshUnder(c)) return true;
            }
            return false;
        }

        /// <summary>顺手清掉被游戏销毁的（掉落物被捡走）。不得每帧跑。</summary>
        public static void PruneIfNeeded()
        {
            if (_patchedLayers.Count < 256 || Time.frameCount < _patchedPruneAt) return;
            _patchedPruneAt = Time.frameCount + 300;                 // 不得每帧数
            _patchedLayers.RemoveWhere(g => g == null);              // 被游戏销毁的（掉落物被捡走）清掉
        }
    }
}
