// CharacterRig.cs —— 角色替换的**骨骼映射**：Tripo/Mixamo 的人形 23 关节 → 游戏鸭子的 26 个骨骼槽。
//
// 依据（都是运行时实测，不是猜的）：
//   设计仓 `docs/unity-3d-assets/10-character-replacement-research.md` §4.5。
//   · 玩家 `DuckBody` 的 `SkinnedMeshRenderer`：`bones` 26 个槽、`bindposes` 26、`rootBone = Root`。
//     其中 **24 根实骨 + 2 个 null 槽**（最后两槽没有骨骼，对应顶点绑在根上）。
//   · Tripo 的 `/animations/rig` 用 `spec=mixamo`（**只对 biped 生效**）会给标准 Mixamo 名：
//     `Root` + 22 个 `mixamorig:*`（实测：`Hips` `Spine` `Spine1` `Spine2` `Neck` `Head`
//     `Left/RightShoulder` `Left/RightArm` `Left/RightForeArm` `Left/RightHand`
//     `Left/RightUpLeg` `Left/RightLeg` `Left/RightFoot` `Left/RightToeBase`）。
//
// ⚠️ **映射按名字查，不得按位置猜。** 运行时 `bones[]` 的顺序是
// `Root, Pelvis, Spine.001-004, Head, HeadTip, 手臂 10 根, Thigh.R, Foot.R, Tail, Tail.001, Thigh.L, Foot.L`
// —— 腿与尾巴排在**手臂之后**，不是从根往下顺着排。
//
// 这个文件**只放表与查表**（纯数据 + 纯函数，不动游戏对象）。顶点搬运与挂载是下一步。

using System;
using System.Collections.Generic;

namespace ModelKit
{
    public static class CharacterRig
    {
        /// <summary>游戏鸭子骨骼，**按运行时 `bones[]` 的顺序**（顺序本身就是数据）。
        /// 末尾两个槽运行时是 `null`，这里不放名字 —— 见 <see cref="DuckSlotCount"/>。</summary>
        public static readonly string[] DuckBones =
        {
            "Root",                                                                        // 0
            "Pelvis",                                                                       // 1
            "Spine.001", "Spine.002", "Spine.003", "Spine.004",                             // 2-5
            "Head", "HeadTip",                                                              // 6-7
            "UpperArm.L", "Elbow.L", "ForeArm.L", "Hand.L", "Hand.Soket.L",                 // 8-12
            "UpperArm.R", "Elbow.R", "ForeArm.R", "Hand.R", "Hand.Soket.R",                 // 13-17
            "Thigh.R", "Foot.R",                                                            // 18-19
            "Tail", "Tail.001",                                                             // 20-21
            "Thigh.L", "Foot.L",                                                            // 22-23
        };

        /// <summary>骨骼槽总数（含末尾两个 `null` 槽）。和 `SkinnedMeshRenderer.bindposes` 的长度一致。</summary>
        public const int DuckSlotCount = 26;

        /// <summary>游戏鸭子有、Mixamo 没有的骨：挂到**最近的已映射父骨**上（玩家批注的简化方案）。
        /// 键 = 多出来的骨，值 = 挂到哪根。`null` 槽不在这里。</summary>
        public static readonly Dictionary<string, string> ExtraDuckBones = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "HeadTip",      "Head" },
            { "Elbow.L",      "UpperArm.L" },
            { "Elbow.R",      "UpperArm.R" },
            { "Hand.Soket.L", "Hand.L" },
            { "Hand.Soket.R", "Hand.R" },
            { "Tail",         "Pelvis" },
            { "Tail.001",     "Pelvis" },
        };

        /// <summary>Mixamo（可带或不带 `mixamorig:` 前缀）→ 游戏鸭子骨骼。
        /// 一个鸭子骨可以被多个 Mixamo 关节指向（例如 `LeftLeg` / `LeftFoot` / `LeftToeBase` 都落到 `Foot.L`）——
        /// 鸭子小腿很短，没有单独的脚骨与脚趾骨。</summary>
        static readonly Dictionary<string, string> Table = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Root",                 "Root" },
            { "mixamorig:Hips",       "Pelvis" },
            { "mixamorig:Spine",      "Spine.001" },
            { "mixamorig:Spine1",     "Spine.002" },
            { "mixamorig:Spine2",     "Spine.003" },
            { "mixamorig:Neck",       "Spine.004" },
            { "mixamorig:Head",       "Head" },
            { "mixamorig:LeftShoulder",  "Spine.004" },
            { "mixamorig:LeftArm",       "UpperArm.L" },
            { "mixamorig:LeftForeArm",   "ForeArm.L" },
            { "mixamorig:LeftHand",      "Hand.L" },
            { "mixamorig:RightShoulder", "Spine.004" },
            { "mixamorig:RightArm",      "UpperArm.R" },
            { "mixamorig:RightForeArm",  "ForeArm.R" },
            { "mixamorig:RightHand",     "Hand.R" },
            { "mixamorig:LeftUpLeg",     "Thigh.L" },
            { "mixamorig:LeftLeg",       "Foot.L" },
            { "mixamorig:LeftFoot",      "Foot.L" },
            { "mixamorig:LeftToeBase",   "Foot.L" },
            { "mixamorig:RightUpLeg",    "Thigh.R" },
            { "mixamorig:RightLeg",      "Foot.R" },
            { "mixamorig:RightFoot",     "Foot.R" },
            { "mixamorig:RightToeBase",  "Foot.R" },
        };

        /// <summary>去掉 `mixamorig:` 前缀（Tripo 的 `spec=tripo` 不给它，`spec=mixamo` 给）。</summary>
        public static string StripPrefix(string boneName)
        {
            if (string.IsNullOrEmpty(boneName)) return "";
            int i = boneName.IndexOf(':');
            return i >= 0 ? boneName.Substring(i + 1) : boneName;
        }

        /// <summary>把外部骨架的骨骼名映射到游戏鸭子的骨骼名。查不到返回 false（调用方要报出来，不得静默跳过）。</summary>
        public static bool TryMap(string mixamoBoneName, out string duckBone)
        {
            duckBone = null;
            if (string.IsNullOrEmpty(mixamoBoneName)) return false;
            if (Table.TryGetValue(mixamoBoneName, out duckBone)) return true;
            // 前缀可有可无：Tripo 的 `spec=tripo` 名字没有 `mixamorig:`，`spec=mixamo` 有
            return Table.TryGetValue("mixamorig:" + StripPrefix(mixamoBoneName), out duckBone);
        }

        /// <summary>自检：表自己有没有矛盾。返回问题列表（空 = 没问题）。
        ///
        /// 查四件：
        ///   ① 每个映射的**目标**都在 <see cref="DuckBones"/> 里；
        ///   ② Mixamo 的 22 个关节一个不漏；
        ///   ③ <see cref="ExtraDuckBones"/> 的每个多出骨与目标都在名单里，且**不重复**映射；
        ///   ④ 两个名单加起来覆盖 <see cref="DuckBones"/> 全部 24 根。
        /// </summary>
        public static List<string> Verify()
        {
            var problems = new List<string>();
            var known = new HashSet<string>(DuckBones, StringComparer.Ordinal);

            // ①
            foreach (var kv in Table)
                if (!known.Contains(kv.Value))
                    problems.Add($"map target '{kv.Value}' (from '{kv.Key}') is not in DuckBones");

            // ②
            string[] mixamo =
            {
                "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
                "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                "RightShoulder", "RightArm", "RightForeArm", "RightHand",
                "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
                "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
            };
            foreach (var m in mixamo)
                if (!Table.ContainsKey("mixamorig:" + m) && !Table.ContainsKey(m))
                    problems.Add($"Mixamo bone '{m}' has no mapping");

            // ③
            foreach (var kv in ExtraDuckBones)
            {
                if (!known.Contains(kv.Key)) problems.Add($"extra bone '{kv.Key}' is not in DuckBones");
                if (!known.Contains(kv.Value)) problems.Add($"extra bone '{kv.Key}' points at unknown '{kv.Value}'");
                foreach (var t in Table)
                    if (string.Equals(t.Value, kv.Key, StringComparison.Ordinal) && !string.Equals(t.Key, "Root", StringComparison.Ordinal))
                        problems.Add($"bone '{kv.Key}' is both mapped from '{t.Key}' and listed as an extra bone");
            }

            // ④
            var covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in Table) covered.Add(kv.Value);
            foreach (var kv in ExtraDuckBones) covered.Add(kv.Key);
            foreach (var b in DuckBones)
                if (!covered.Contains(b)) problems.Add($"duck bone '{b}' is neither mapped nor listed as an extra");

            return problems;
        }
    }
}
