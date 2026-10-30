// CharacterArmIk.cs —— 只处理右手握把的两骨 IK；左手由 CharacterSwap 并回躯干。
//
// 游戏肩、肘提供自然姿势，Hand.Soket.R 是掌心握持目标。
// 每帧只重定位源肩骨并旋转源上臂与前臂；不改变骨长，不移动躯干，不主动旋转手掌。
// 目标不可达时夹紧求解距离，保留实际误差并写日志，不拉长手臂。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModelKit
{
    internal sealed class CharacterArmIk : IDisposable
    {
        sealed class Arm
        {
            public string Side;
            public Transform Upper;
            public Transform Fore;
            public Transform Hand;
            public Transform DriverUpper;
            public Transform DriverElbow;
            public Transform DriverGrip;
            public Vector3 PalmOffset;
            public float UpperLength;
            public float ForeLength;
            public Quaternion UpperRestRotation;
            public Quaternion ForeRestRotation;
            public Quaternion UpperRestBasisInverse;
            public Quaternion ForeRestBasisInverse;
            public Quaternion LastUpperRotation;
            public Quaternion LastForeRotation;
            public bool HasReachableState;
            public bool LastReachable;
            public float NextLog;
        }

        Transform _root;
        Arm[] _arms = new Arm[0];
        bool _disposed;

        internal Transform[] RendererBones { get; private set; } = new Transform[0];
        internal Matrix4x4[] RendererBindposes { get; private set; } = new Matrix4x4[0];
        internal int[] JointToRenderer { get; private set; } = new int[0];

        CharacterArmIk() { }

        internal static CharacterArmIk Create(Transform space, GltfLoader.Loaded skin,
            Transform[] driverBones, Matrix4x4 alignment, out string error)
        {
            error = "";
            if (space == null || skin == null || driverBones == null)
            { error = "the arm IK input is null"; return null; }
            if (skin.JointNames.Length == 0 || skin.JointNodes.Length != skin.JointNames.Length
                || skin.InverseBind.Length != skin.JointNames.Length || skin.Nodes.Length == 0)
            { error = "the GLB has no complete source node hierarchy"; return null; }

            var result = new CharacterArmIk();
            try
            {
                var convert = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)) * skin.VertexTransform;
                var inverseConvert = convert.inverse;
                var root = new GameObject("ModelKit_CharacterArmIk");
                result._root = root.transform;
                result._root.SetParent(space, false);
                SetLocalMatrix(result._root, alignment);

                var nodes = new Transform[skin.Nodes.Length];
                for (int i = 0; i < nodes.Length; i++)
                {
                    nodes[i] = new GameObject("Source_" + skin.Nodes[i].Name).transform;
                    nodes[i].SetParent(result._root, false);       // 失败时由根节点统一销毁
                }
                for (int i = 0; i < nodes.Length; i++)
                {
                    int parent = skin.Nodes[i].Parent;
                    if (parent >= nodes.Length || parent == i || parent < -1)
                        throw new Exception("invalid source node parent at " + skin.Nodes[i].Name);
                    nodes[i].SetParent(parent >= 0 ? nodes[parent] : result._root, false);
                    SetLocalMatrix(nodes[i], convert * skin.Nodes[i].LocalMatrix * inverseConvert);
                }

                var jointTransforms = new Transform[skin.JointNames.Length];
                var byDuckName = new Dictionary<string, int>(StringComparer.Ordinal);
                result.JointToRenderer = new int[skin.JointNames.Length];
                for (int j = 0; j < skin.JointNames.Length; j++)
                {
                    int node = skin.JointNodes[j];
                    if (node < 0 || node >= nodes.Length) throw new Exception("invalid source joint node: " + skin.JointNames[j]);
                    jointTransforms[j] = nodes[node];
                    result.JointToRenderer[j] = -1;
                    string duck;
                    if (CharacterRig.TryMap(skin.JointNames[j], out duck)) byDuckName[duck] = j;
                }

                string[] armNames = { "UpperArm.R", "ForeArm.R", "Hand.R" };
                result.RendererBones = new Transform[armNames.Length];
                result.RendererBindposes = new Matrix4x4[armNames.Length];
                for (int i = 0; i < armNames.Length; i++)
                {
                    int joint;
                    if (!byDuckName.TryGetValue(armNames[i], out joint)) throw new Exception("missing source arm joint: " + armNames[i]);
                    result.JointToRenderer[joint] = i;
                    result.RendererBones[i] = jointTransforms[joint];
                    result.RendererBindposes[i] = convert * skin.InverseBind[joint] * inverseConvert * alignment.inverse;
                }
                // glTF 节点当前 TRS 不保证等于 inverseBindMatrices。先校准整副源骨架的 bind pose。
                var ordered = new List<int>(jointTransforms.Length);
                for (int j = 0; j < jointTransforms.Length; j++) ordered.Add(j);
                ordered.Sort((a, b) => Depth(skin.Nodes, skin.JointNodes[a]).CompareTo(Depth(skin.Nodes, skin.JointNodes[b])));
                foreach (int j in ordered)
                {
                    var t = jointTransforms[j];
                    var restWorld = result._root.localToWorldMatrix * convert * skin.InverseBind[j].inverse * inverseConvert;
                    SetLocalMatrix(t, t.parent.worldToLocalMatrix * restWorld);
                }

                const string suffix = ".R";
                int ui = byDuckName["UpperArm" + suffix];
                int fi = byDuckName["ForeArm" + suffix];
                int hi = byDuckName["Hand" + suffix];
                var driverUpper = FindBone(driverBones, "UpperArm" + suffix);
                var driverElbow = FindBone(driverBones, "Elbow" + suffix);
                var driverHand = FindBone(driverBones, "Hand" + suffix);
                if (driverUpper == null || driverElbow == null || driverHand == null)
                    throw new Exception("missing game arm bones for " + suffix);
                var driverGrip = driverHand.Find("Hand.Soket" + suffix);
                if (driverGrip == null) throw new Exception("missing game grip socket for " + suffix);
                var upper = jointTransforms[ui];
                var fore = jointTransforms[fi];
                var hand = jointTransforms[hi];
                Vector3 upperDirection = (fore.position - upper.position).normalized;
                Vector3 foreDirection = (hand.position - fore.position).normalized;
                Vector3 restNormal = Vector3.Cross(upperDirection, foreDirection);
                if (restNormal.sqrMagnitude < 1e-8f) restNormal = FallbackNormal(upperDirection);
                restNormal.Normalize();
                Vector3 palmOffset;
                if (!TryFindPalmOffset(skin, hi, result._root, hand, out palmOffset))
                    throw new Exception("the source right hand has no dominant hand-weighted vertices");
                result._arms = new[]
                {
                    new Arm
                    {
                        Side = suffix,
                        Upper = upper,
                        Fore = fore,
                        Hand = hand,
                        DriverUpper = driverUpper,
                        DriverElbow = driverElbow,
                        DriverGrip = driverGrip,
                        PalmOffset = palmOffset,
                        UpperLength = Vector3.Distance(upper.position, fore.position),
                        ForeLength = Vector3.Distance(fore.position, hand.position),
                        UpperRestRotation = upper.rotation,
                        ForeRestRotation = fore.rotation,
                        LastUpperRotation = upper.rotation,
                        LastForeRotation = fore.rotation,
                        UpperRestBasisInverse = Quaternion.Inverse(Quaternion.LookRotation(upperDirection, restNormal)),
                        ForeRestBasisInverse = Quaternion.Inverse(Quaternion.LookRotation(foreDirection, restNormal)),
                    }
                };
                result.Apply();
                var driver = result._root.gameObject.AddComponent<Driver>();
                driver.Target = result;
                return result;
            }
            catch (Exception ex)
            {
                result.Dispose();
                error = ex.Message;
                return null;
            }
        }

        internal void Apply()
        {
            if (_disposed || _root == null) return;
            foreach (var arm in _arms) Solve(arm);
        }

        static void Solve(Arm arm)
        {
            if (arm.Upper == null || arm.Fore == null || arm.Hand == null || arm.DriverUpper == null
                || arm.DriverElbow == null || arm.DriverGrip == null) return;
            // 只重定位源肩骨；不移动源骨架或躯干。手掌不设置世界旋转。
            arm.Upper.position = arm.DriverUpper.position;
            Vector3 shoulder = arm.Upper.position;
            float distance = 0f;
            bool reachable = false;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                // 手掌保持源局部旋转，掌心偏移随前臂自然变化。第二轮使用更新后的偏移。
                Vector3 palmWorldOffset = arm.Hand.TransformVector(arm.PalmOffset);
                Vector3 target = arm.DriverGrip.position - palmWorldOffset;
                Vector3 delta = target - shoulder;
                distance = delta.magnitude;
                float min = Mathf.Abs(arm.UpperLength - arm.ForeLength) + 0.0001f;
                float max = arm.UpperLength + arm.ForeLength - 0.0001f;
                reachable = distance >= min && distance <= max;
                float solvedDistance = Mathf.Clamp(distance, min, max);
                Vector3 direction = distance > 1e-6f ? delta / distance : (arm.Hand.position - shoulder).normalized;
                if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
                Vector3 solvedTarget = shoulder + direction * solvedDistance;

                // 余弦定理求肘部。pole 只决定肘部绕肩手轴的方向，不参与拉长骨骼。
                float a = (arm.UpperLength * arm.UpperLength - arm.ForeLength * arm.ForeLength
                    + solvedDistance * solvedDistance) / (2f * solvedDistance);
                float h = Mathf.Sqrt(Mathf.Max(0f, arm.UpperLength * arm.UpperLength - a * a));
                // 游戏当前肘部提供自然弯曲方向；不强制 V、向下或左右镜像。
                Vector3 pole = arm.DriverElbow.position - shoulder;
                Vector3 polePerpendicular = pole - direction * Vector3.Dot(pole, direction);
                if (polePerpendicular.sqrMagnitude < 1e-8f)
                {
                    polePerpendicular = arm.Fore.position - shoulder;
                    polePerpendicular -= direction * Vector3.Dot(polePerpendicular, direction);
                }
                if (polePerpendicular.sqrMagnitude < 1e-8f) polePerpendicular = Vector3.up;
                Vector3 elbow = shoulder + direction * a + polePerpendicular.normalized * h;

                Vector3 upperDirection = elbow - shoulder;
                Vector3 foreDirection = solvedTarget - elbow;
                Vector3 targetNormal = Vector3.Cross(upperDirection, foreDirection);
                if (targetNormal.sqrMagnitude < 1e-8f) targetNormal = Vector3.Cross(polePerpendicular, direction);
                targetNormal.Normalize();
                SetBoneRotation(arm.Upper, arm.LastUpperRotation, arm.UpperRestRotation,
                    arm.UpperRestBasisInverse, upperDirection, targetNormal);
                arm.LastUpperRotation = arm.Upper.rotation;
                SetBoneRotation(arm.Fore, arm.LastForeRotation, arm.ForeRestRotation,
                    arm.ForeRestBasisInverse, foreDirection, targetNormal);
                arm.LastForeRotation = arm.Fore.rotation;
            }

            float handError = Vector3.Distance(arm.Hand.TransformPoint(arm.PalmOffset), arm.DriverGrip.position);
            bool first = !arm.HasReachableState;
            bool changed = arm.HasReachableState && arm.LastReachable != reachable;
            bool problem = !reachable || handError > 0.005f;
            if (first || ((changed || problem) && Time.unscaledTime >= arm.NextLog))
            {
                arm.NextLog = Time.unscaledTime + 5f;
                Debug.Log($"[CharacterArmIk] {arm.Side} reachable={reachable} targetDistance={distance:0.###} "
                    + $"lengths={arm.UpperLength:0.###}+{arm.ForeLength:0.###} handError={handError:0.###}");
            }
            arm.HasReachableState = true;
            arm.LastReachable = reachable;
        }

        static bool TryFindPalmOffset(GltfLoader.Loaded skin, int handJoint, Transform root, Transform hand,
            out Vector3 palmOffset)
        {
            var vertices = skin.Mesh.vertices;
            Vector3 center = Vector3.zero;
            float sum = 0f;
            for (int i = 0; i < vertices.Length && i < skin.Weights.Length; i++)
            {
                var w = skin.Weights[i];
                float weight = 0f;
                if (w.boneIndex0 == handJoint) weight += w.weight0;
                if (w.boneIndex1 == handJoint) weight += w.weight1;
                if (w.boneIndex2 == handJoint) weight += w.weight2;
                if (w.boneIndex3 == handJoint) weight += w.weight3;
                if (weight < 0.5f) continue;                         // 排除手腕与前臂过渡圈
                center += root.TransformPoint(vertices[i]) * weight;
                sum += weight;
            }
            palmOffset = sum > 0.0001f ? hand.InverseTransformPoint(center / sum) : Vector3.zero;
            return sum > 0.0001f;
        }

        static void SetBoneRotation(Transform bone, Quaternion previousRotation, Quaternion restRotation,
            Quaternion restBasisInverse, Vector3 desiredDirection, Vector3 desiredNormal)
        {
            if (desiredDirection.sqrMagnitude < 1e-8f || desiredNormal.sqrMagnitude < 1e-8f) return;
            // 同时约束方向与弯曲平面。只做 FromToRotation 会留下绕骨轴的滚转自由度。
            Quaternion rotation = Quaternion.LookRotation(desiredDirection.normalized, desiredNormal)
                * restBasisInverse * restRotation;
            // 与上一帧最终姿势比较，并留 5° 余量，避免在候选分界线上来回翻面。
            Quaternion alternate = Quaternion.AngleAxis(180f, desiredDirection.normalized) * rotation;
            if (Quaternion.Angle(previousRotation, alternate) + 5f < Quaternion.Angle(previousRotation, rotation))
                rotation = alternate;
            bone.rotation = rotation;
        }

        static Vector3 FallbackNormal(Vector3 direction)
        {
            var normal = Vector3.Cross(direction, Vector3.up);
            if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(direction, Vector3.right);
            return normal.normalized;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_root != null)
            {
                try { UnityEngine.Object.Destroy(_root.gameObject); } catch { }
                _root = null;
            }
            _arms = new Arm[0];
            RendererBones = new Transform[0];
            RendererBindposes = new Matrix4x4[0];
            JointToRenderer = new int[0];
        }

        sealed class Driver : MonoBehaviour
        {
            public CharacterArmIk Target;
            bool _failed;
            void LateUpdate()
            {
                if (_failed || Target == null) return;
                try { Target.Apply(); }
                catch (Exception ex)
                {
                    _failed = true;
                    Debug.LogWarning("[CharacterArmIk] update failed: " + ex.Message);
                }
            }
        }

        static Transform FindBone(Transform[] bones, string name)
        {
            foreach (var bone in bones) if (bone != null && bone.name == name) return bone;
            return null;
        }

        static int Depth(GltfLoader.RigNode[] nodes, int index)
        {
            int depth = 0;
            var seen = new HashSet<int>();
            while (index >= 0 && index < nodes.Length && nodes[index].Parent >= 0)
            {
                if (!seen.Add(index)) throw new Exception("a cycle exists in the source node hierarchy");
                index = nodes[index].Parent;
                depth++;
            }
            return depth;
        }

        internal static Quaternion RotationOf(Matrix4x4 m)
        {
            Vector3 up = m.GetColumn(1);
            Vector3 forward = m.GetColumn(2);
            if (up.sqrMagnitude < 1e-10f || forward.sqrMagnitude < 1e-10f) return Quaternion.identity;
            return Quaternion.LookRotation(forward, up);
        }

        internal static void SetLocalMatrix(Transform target, Matrix4x4 matrix)
        {
            target.localPosition = matrix.GetColumn(3);
            target.localRotation = RotationOf(matrix);
            var scale = new Vector3(
                ((Vector3)matrix.GetColumn(0)).magnitude,
                ((Vector3)matrix.GetColumn(1)).magnitude,
                ((Vector3)matrix.GetColumn(2)).magnitude);
            if (matrix.determinant < 0f) scale.x = -scale.x;
            target.localScale = scale;
        }
    }
}
